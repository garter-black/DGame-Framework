using System;
using System.IO;
using DGame.FixedPoint;
using GameBattle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic
{
    /// <summary>
    /// 战斗沙盒：不走登录和热更流程，直接在场景里跑单机幸存者战斗，用来调玩法和压测。
    /// 场景：Assets/Scenes/BattleSandbox/BattleSandbox.unity。
    /// 操作：WASD/方向键移动；F5 保存录像；F6 重放校验；F1 开关面板。
    /// 渲染用 GPU Instancing 批量画，不为每个敌人创建 GameObject。
    /// </summary>
    public sealed class SurvivorSandbox : MonoBehaviour
    {
        // RenderMeshInstanced 单批上限。
        private const int BATCH = 1023;

        [SerializeField] private int m_seed = 1;
        [SerializeField] private int m_playerCount = 1;
        [SerializeField] private int m_enemyCap = 300;
        [SerializeField] private int m_spawnPerWave = 8;
        [SerializeField] private int m_spawnIntervalFrames = BattleConfig.TICK_RATE;

        [Tooltip("勾选后玩家不会死，便于长时间压测")]
        [SerializeField] private bool m_invincible;

        private LocalBattleDriver m_driver;
        private Mesh m_mesh;
        private RenderParams m_playerParams;
        private RenderParams m_enemyParams;
        private RenderParams m_projectileParams;
        private RenderParams m_groundParams;
        private Matrix4x4[] m_matrices;
        private Camera m_camera;
        private bool m_showPanel = true;
        private string m_status = string.Empty;
        private float m_fps;

        private void Start()
        {
            m_driver = new LocalBattleDriver(new BattleSettings
            {
                Seed = m_seed,
                PlayerCount = m_playerCount,
                EnemyCap = m_enemyCap,
                SpawnPerWave = m_spawnPerWave,
                SpawnIntervalFrames = m_spawnIntervalFrames,
                Invincible = m_invincible,
            });

            m_mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            m_playerParams = CreateParams(new Color(0.2f, 0.6f, 1f));
            m_enemyParams = CreateParams(new Color(0.9f, 0.25f, 0.2f));
            m_projectileParams = CreateParams(new Color(1f, 0.9f, 0.3f));
            m_groundParams = CreateParams(new Color(0.3f, 0.3f, 0.3f));
            m_matrices = new Matrix4x4[BattleConfig.MAX_ENEMIES];
            m_camera = Camera.main;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var input = default(BattleInput);
            if (keyboard != null)
            {
                var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                var y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                input = new BattleInput(x * BattleConfig.INPUT_SCALE, y * BattleConfig.INPUT_SCALE);

                if (keyboard.f1Key.wasPressedThisFrame) m_showPanel = !m_showPanel;
                if (keyboard.f5Key.wasPressedThisFrame) SaveReplay();
                if (keyboard.f6Key.wasPressedThisFrame) VerifyReplay();
            }

            m_driver.Update(Time.deltaTime, input);
            m_fps = Mathf.Lerp(m_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
            Render();
        }

        private void Render()
        {
            var state = m_driver.Simulation.State;
            var alpha = m_driver.Alpha;

            var focus = Lerp(state.Players.PrevPos[0], state.Players.Pos[0], alpha);
            if (m_camera != null)
            {
                m_camera.transform.SetPositionAndRotation(focus + new Vector3(0, 22, -10), Quaternion.Euler(65, 0, 0));
            }

            DrawGround(focus);
            Draw(m_playerParams, state.Players.PrevPos, state.Players.Pos, state.Players.Count, alpha, 1f);
            Draw(m_enemyParams, state.Enemies.PrevPos, state.Enemies.Pos, state.Enemies.Count, alpha, 0.8f);
            Draw(m_projectileParams, state.Projectiles.PrevPos, state.Projectiles.Pos, state.Projectiles.Count, alpha, 0.3f);
        }

        private void Draw(in RenderParams rp, FixedPointVector2[] prev, FixedPointVector2[] pos, int count, float alpha, float size)
        {
            var scale = Vector3.one * size;
            for (var i = 0; i < count; i++)
            {
                m_matrices[i] = Matrix4x4.TRS(Lerp(prev[i], pos[i], alpha), Quaternion.identity, scale);
            }

            for (var start = 0; start < count; start += BATCH)
            {
                Graphics.RenderMeshInstanced(rp, m_mesh, 0, m_matrices, Math.Min(BATCH, count - start), start);
            }
        }

        // 跟随镜头的网格线，让移动有参照物。
        private void DrawGround(Vector3 focus)
        {
            const int half = 15;
            var n = 0;
            var cx = Mathf.Round(focus.x / 2) * 2;
            var cz = Mathf.Round(focus.z / 2) * 2;
            for (var k = -half; k <= half; k += 2)
            {
                m_matrices[n++] = Matrix4x4.TRS(new Vector3(cx + k, -0.5f, cz), Quaternion.identity, new Vector3(0.05f, 0.05f, half * 2));
                m_matrices[n++] = Matrix4x4.TRS(new Vector3(cx, -0.5f, cz + k), Quaternion.identity, new Vector3(half * 2, 0.05f, 0.05f));
            }

            Graphics.RenderMeshInstanced(m_groundParams, m_mesh, 0, m_matrices, n);
        }

        private static Vector3 Lerp(FixedPointVector2 prev, FixedPointVector2 pos, float t) =>
            new Vector3(
                Mathf.LerpUnclamped(prev.x.AsFloat(), pos.x.AsFloat(), t),
                0,
                Mathf.LerpUnclamped(prev.y.AsFloat(), pos.y.AsFloat(), t));

        private static RenderParams CreateParams(Color color)
        {
            var material = new Material(Shader.Find("Standard")) { enableInstancing = true, color = color };
            return new RenderParams(material);
        }

        private void SaveReplay()
        {
            var dir = Path.Combine(Application.persistentDataPath, "Replays");
            Directory.CreateDirectory(dir);
            var hash = m_driver.Simulation.ComputeHash();
            var path = Path.Combine(dir, $"survivor_f{m_driver.Simulation.State.Frame}_{hash:X16}.replay");
            File.WriteAllBytes(path, m_driver.Replay.ToBytes());
            m_status = $"录像已保存：{path}";
            Debug.Log(m_status);
        }

        private void VerifyReplay()
        {
            var expected = m_driver.Simulation.ComputeHash();
            var replayed = BattleReplay.FromBytes(m_driver.Replay.ToBytes()).Play().ComputeHash();
            m_status = expected == replayed
                ? $"重放一致 {replayed:X16}（{m_driver.Replay.Frames.Count} 帧）"
                : $"重放不一致！当前 {expected:X16} 重放 {replayed:X16}";
            if (expected == replayed) Debug.Log(m_status);
            else Debug.LogError(m_status);
        }

        private void OnGUI()
        {
            if (!m_showPanel)
            {
                return;
            }

            var state = m_driver.Simulation.State;
            GUILayout.BeginArea(new Rect(10, 10, 460, 260), GUI.skin.box);
            GUILayout.Label($"FPS {m_fps:F0}   逻辑帧 {state.Frame}   敌人 {state.Enemies.Count}   子弹 {state.Projectiles.Count}");
            GUILayout.Label($"Tick {m_driver.LastTickMs:F2}ms   最大 {m_driver.MaxTickMs:F2}ms   预算 {1000f / BattleConfig.TICK_RATE:F0}ms");
            GUILayout.Label($"HP {state.Players.Hp[0]}   击杀 {state.TotalKills}   {(state.IsOver ? "已结束" : "进行中")}");
            GUILayout.Label($"哈希 {m_driver.Simulation.ComputeHash():X16}");
            GUILayout.Label("WASD 移动  F5 保存录像  F6 重放校验  F1 隐藏面板");
            if (!string.IsNullOrEmpty(m_status)) GUILayout.Label(m_status);
            GUILayout.EndArea();
        }
    }
}
