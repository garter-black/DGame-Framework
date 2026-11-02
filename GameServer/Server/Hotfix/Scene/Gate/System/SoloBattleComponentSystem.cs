using Fantasy;
using Fantasy.Entitas.Interface;
using Fantasy.Helper;
using Fantasy.Network;
using BattleConfig = GameBattle.BattleConfig;
using BattleInputCodec = GameBattle.BattleInputCodec;
using BattleSettings = GameBattle.BattleSettings;
using BattleVerifier = GameBattle.BattleVerifier;
using VerifyResult = GameBattle.VerifyResult;

namespace Hotfix;

public sealed class SoloBattleComponentDestroySystem : DestroySystem<SoloBattleComponent>
{
    protected override void Destroy(SoloBattleComponent self)
    {
        self.BattleId = 0;
        self.Verifier = null;
        self.StartTimeMs = 0;
    }
}

/// <summary>
/// 单人战斗校验：开局下发种子，按批重放客户端指令并核对哈希，结算以服务端结果为准。
/// 任何校验失败（帧号不连续除外，客户端可能在重发）都会作废本局。
/// </summary>
public static class SoloBattleComponentSystem
{
    // 允许客户端帧数领先服务端真实时间的余量：网络延迟 + 批量上传间隔。
    private const int SPEED_TOLERANCE_FRAMES = BattleConfig.TICK_RATE * 3;

    public static SoloBattleComponent StartSoloBattle(this Session session)
    {
        // 新开一局即放弃旧局。
        session.RemoveComponent<SoloBattleComponent>();

        var battle = session.AddComponent<SoloBattleComponent>();
        // 组件的 Id 与父实体（Session）相同，不能区分前后两局；RuntimeId 每次创建都不同。
        battle.BattleId = battle.RuntimeId;
        battle.Verifier = new BattleVerifier(BattleSettings.Default(Random.Shared.Next(), 1));
        battle.StartTimeMs = TimeHelper.Now;
        return battle;
    }

    /// <summary>取当前会话正在进行的单人战斗，ID 不符返回 null。</summary>
    public static SoloBattleComponent GetSoloBattle(this Session session, long battleId)
    {
        var battle = session.GetComponent<SoloBattleComponent>();
        return battle != null && battle.BattleId == battleId ? battle : null;
    }

    public static uint ApplyInputs(this SoloBattleComponent self, int startFrame, byte[] inputs, ulong checkpointHash)
    {
        var playerCount = self.Verifier.Replay.Settings.PlayerCount;
        var frames = (inputs?.Length ?? 0) / (BattleInputCodec.BYTES_PER_INPUT * playerCount);
        var allowedFrames = (TimeHelper.Now - self.StartTimeMs) * BattleConfig.TICK_RATE / 1000 + SPEED_TOLERANCE_FRAMES;
        if (self.Verifier.Frame + frames > allowedFrames)
        {
            return self.Reject(ErrorCode.BATTLE_TOO_FAST, $"frame {self.Verifier.Frame + frames} > allowed {allowedFrames}");
        }

        var result = self.Verifier.ApplyBatch(startFrame, inputs, checkpointHash);
        return result switch
        {
            VerifyResult.Ok => ErrorCode.SUCCESS,
            VerifyResult.NotSequential => ErrorCode.BATTLE_FRAME_NOT_SEQUENTIAL,
            _ => self.Reject(ToErrorCode(result), $"{result} at frame {self.Verifier.Frame}"),
        };
    }

    public static uint Finish(this SoloBattleComponent self, int finalFrame, ulong finalHash)
    {
        var result = self.Verifier.Finish(finalFrame, finalHash);
        if (result != VerifyResult.Ok)
        {
            return self.Reject(ToErrorCode(result), $"finish {result} client frame {finalFrame} server frame {self.Verifier.Frame}");
        }

        var state = self.Verifier.Simulation.State;
        Log.Info($"[SoloBattle] {self.BattleId} verified: frame={state.Frame} kills={state.TotalKills} died={state.IsOver}");
        return ErrorCode.SUCCESS;
    }

    private static uint ToErrorCode(VerifyResult result) => result switch
    {
        VerifyResult.HashMismatch => ErrorCode.BATTLE_HASH_MISMATCH,
        VerifyResult.NotSequential => ErrorCode.BATTLE_FRAME_NOT_SEQUENTIAL,
        _ => ErrorCode.BATTLE_INPUT_INVALID,
    };

    private static uint Reject(this SoloBattleComponent self, uint errorCode, string reason)
    {
        Log.Warning($"[SoloBattle] {self.BattleId} rejected ({errorCode}): {reason}");
        self.Dispose();
        return errorCode;
    }
}
