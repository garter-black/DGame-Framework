using DGame;
using Fantasy;
using Fantasy.Async;
using GameBattle;
using GameProto;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic
{
    /// <summary>
    /// 单人战斗（服务端校验）：向服务端要种子开局 → 本地跑模拟 → 每秒上传一批指令和检查点哈希 →
    /// 结束时结算，结果以服务端重放为准。数据中心模块，由生成器自动注册，OnUpdate 每帧驱动。
    /// 以后做联机时，把 LocalBattleDriver 换成"按服务端广播帧推进"的驱动，表现层和校验器不变。
    /// </summary>
    public sealed class SoloBattleMgr : DataCenterModule<SoloBattleMgr>
    {
        private const int UPLOAD_INTERVAL_FRAMES = BattleConfig.TICK_RATE;

        private long m_battleId;
        private LocalBattleDriver m_driver;
        private SurvivorBattleView m_view;
        private Camera m_camera;
        private int m_uploadedFrame;
        private bool m_requesting;
        private bool m_ending;

        public bool IsInBattle => m_driver != null;

        public async FTask StartAsync()
        {
            if (IsInBattle || m_requesting)
            {
                return;
            }

            m_requesting = true;
            var response = await GameClient.Instance.Call(new C2G_SoloBattleStartRequest());
            m_requesting = false;

            if (response is not G2C_SoloBattleStartResponse start)
            {
                return;
            }

            if (start.ErrorCode != 0)
            {
                GameModule.UIModule.ShowTipsUI(G.R("开始战斗失败（错误码 {0}）", start.ErrorCode));
                return;
            }

            m_battleId = start.BattleId;
            m_driver = new LocalBattleDriver(new BattleSettings
            {
                Seed = start.Seed,
                PlayerCount = 1,
                EnemyCap = start.EnemyCap,
                SpawnPerWave = start.SpawnPerWave,
                SpawnIntervalFrames = start.SpawnIntervalFrames,
            });
            m_view ??= new SurvivorBattleView();
            m_camera = SurvivorBattleView.CreateCamera();
            m_uploadedFrame = 0;
            m_ending = false;
            GameModule.UIModule.CloseWindow<GameMainUI>();
        }

        public override void OnUpdate()
        {
            if (m_driver == null)
            {
                return;
            }

            var state = m_driver.Simulation.State;
            if (!m_ending)
            {
                m_driver.Update(Time.deltaTime, SurvivorBattleView.ReadKeyboard());
                var quit = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                m_ending = state.IsOver || quit;
            }

            m_view.Render(state, m_driver.Alpha, m_camera);

            // 同一时间只有一个请求在途，保证批次按顺序到达。
            if (m_requesting)
            {
                return;
            }

            if (state.Frame - m_uploadedFrame >= (m_ending ? 1 : UPLOAD_INTERVAL_FRAMES))
            {
                UploadAsync().Coroutine();
            }
            else if (m_ending)
            {
                FinishAsync().Coroutine();
            }
        }

        public override void OnRoleLogout() => Clear();

        private async FTask UploadAsync()
        {
            // 打包和算哈希必须在发出前同步完成：此刻的状态正好是本批最后一帧。
            var start = m_uploadedFrame;
            var count = Mathf.Min(m_driver.Simulation.State.Frame - start, BattleInputCodec.MAX_FRAMES_PER_BATCH);
            var request = new C2G_SoloBattleInputsRequest
            {
                BattleId = m_battleId,
                StartFrame = start + 1,
                Inputs = BattleInputCodec.Pack(m_driver.Replay.Frames, start, count, 1),
                CheckpointHash = count == m_driver.Simulation.State.Frame - start ? m_driver.Simulation.ComputeHash() : 0,
            };

            m_requesting = true;
            var response = await GameClient.Instance.Call(request);
            m_requesting = false;

            if (response is not G2C_SoloBattleInputsResponse result || result.ErrorCode != 0)
            {
                Abort(response?.ErrorCode ?? 0);
                return;
            }

            m_uploadedFrame = result.VerifiedFrame;
        }

        private async FTask FinishAsync()
        {
            m_requesting = true;
            var response = await GameClient.Instance.Call(new C2G_SoloBattleEndRequest
            {
                BattleId = m_battleId,
                FinalFrame = m_driver.Simulation.State.Frame,
                FinalHash = m_driver.Simulation.ComputeHash(),
            });
            m_requesting = false;

            if (response is not G2C_SoloBattleEndResponse end || end.ErrorCode != 0)
            {
                Abort(response?.ErrorCode ?? 0);
                return;
            }

            GameModule.UIModule.ShowTipsUI(G.R("结算已通过服务端校验：存活 {0} 秒，击杀 {1}", end.Frame / BattleConfig.TICK_RATE, end.Kills));
            Clear();
        }

        private void Abort(uint errorCode)
        {
            DLogger.Error($"[SoloBattle] {m_battleId} rejected by server, errorCode={errorCode}");
            GameModule.UIModule.ShowTipsUI(G.R("战斗校验失败，本局作废（错误码 {0}）", errorCode));
            Clear();
        }

        private void Clear()
        {
            if (m_driver == null)
            {
                return;
            }

            m_driver = null;
            m_battleId = 0;
            Object.Destroy(m_camera.gameObject);
            m_camera = null;
            m_requesting = false;
            GameModule.UIModule.ShowWindowAsync<GameMainUI>();
        }
    }
}
