using System;
using DGame.FixedPoint;
using GameBattle;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 幸存者战斗的表现层：只读 BattleState，按上一帧/当前帧插值，用 GPU Instancing 批量画，
    /// 不为每个敌人创建 GameObject。沙盒和联网战斗共用。
    /// shortcut: 方块 + Standard 材质占位，换美术资源时替换 Mesh/材质即可，渲染方式不变。
    /// </summary>
    public sealed class SurvivorBattleView
    {
        // RenderMeshInstanced 单批上限。
        private const int BATCH = 1023;

        private readonly Mesh m_mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        private readonly RenderParams m_playerParams = CreateParams(new Color(0.2f, 0.6f, 1f));
        private readonly RenderParams m_enemyParams = CreateParams(new Color(0.9f, 0.25f, 0.2f));
        private readonly RenderParams m_projectileParams = CreateParams(new Color(1f, 0.9f, 0.3f));
        private readonly RenderParams m_groundParams = CreateParams(new Color(0.3f, 0.3f, 0.3f));
        private readonly Matrix4x4[] m_matrices = new Matrix4x4[BattleConfig.MAX_ENEMIES];

        /// <param name="alpha">LocalBattleDriver.Alpha，0~1。</param>
        /// <param name="camera">跟随本机玩家的相机，可为 null。</param>
        public void Render(BattleState state, float alpha, Camera camera)
        {
            var focus = Lerp(state.Players.PrevPos[0], state.Players.Pos[0], alpha);
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(focus + new Vector3(0, 22, -10), Quaternion.Euler(65, 0, 0));
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
            // 自发光：场景里没有灯光（如 GameStart）也能看见。
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.6f);
            return new RenderParams(material);
        }

        /// <summary>创建战斗专用透视相机，夹在场景主相机和 UI 相机之间。战斗结束时由调用方销毁。</summary>
        public static Camera CreateCamera()
        {
            var camera = new GameObject("SurvivorBattleCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            camera.fieldOfView = 50;
            camera.depth = 0;
            return camera;
        }

        /// <summary>WASD/方向键 → 指令。</summary>
        public static BattleInput ReadKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return default;
            }

            var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            var y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            return new BattleInput(x * BattleConfig.INPUT_SCALE, y * BattleConfig.INPUT_SCALE);
        }
    }
}
