using System.IO;
using GameBattle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic
{
    /// <summary>
    /// 战斗沙盒：不走登录和热更流程，直接在场景里跑单机幸存者战斗，用来调玩法和压测。
    /// 场景：Assets/Scenes/BattleSandbox/BattleSandbox.unity。
    /// 操作：WASD/方向键移动；F5 保存录像；F6 重放校验；F1 开关面板。
    /// </summary>
    public sealed class SurvivorSandbox : MonoBehaviour
    {
        [SerializeField] private int m_seed = 1;
        [SerializeField] private int m_playerCount = 1;
        [SerializeField] private int m_enemyCap = 300;
        [SerializeField] private int m_spawnPerWave = 8;
        [SerializeField] private int m_spawnIntervalFrames = BattleConfig.TICK_RATE;

        [Tooltip("勾选后玩家不会死，便于长时间压测")]
        [SerializeField] private bool m_invincible;

        private LocalBattleDriver m_driver;
        private SurvivorBattleView m_view;
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
            m_view = new SurvivorBattleView();
            m_camera = Camera.main;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame) m_showPanel = !m_showPanel;
                if (keyboard.f5Key.wasPressedThisFrame) SaveReplay();
                if (keyboard.f6Key.wasPressedThisFrame) VerifyReplay();
            }

            m_driver.Update(Time.deltaTime, SurvivorBattleView.ReadKeyboard());
            m_fps = Mathf.Lerp(m_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
            m_view.Render(m_driver.Simulation.State, m_driver.Alpha, m_camera);
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
