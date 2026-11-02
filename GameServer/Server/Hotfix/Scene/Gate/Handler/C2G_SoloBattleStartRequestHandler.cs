using System;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace Hotfix;

/// <summary>单人战斗开局：下发服务端决定的种子和难度参数。</summary>
public sealed class C2G_SoloBattleStartRequestHandler : MessageRPC<C2G_SoloBattleStartRequest, G2C_SoloBattleStartResponse>
{
    protected override async FTask Run(Session session, C2G_SoloBattleStartRequest request, G2C_SoloBattleStartResponse response, Action reply)
    {
        if (session.GetComponent<PlayerDataFlagComponent>()?.playerData == null)
        {
            response.ErrorCode = ErrorCode.ROLE_NOT_FOUND;
            return;
        }

        var battle = session.StartSoloBattle();
        var settings = battle.Verifier.Replay.Settings;
        response.BattleId = battle.BattleId;
        response.Seed = settings.Seed;
        response.EnemyCap = settings.EnemyCap;
        response.SpawnPerWave = settings.SpawnPerWave;
        response.SpawnIntervalFrames = settings.SpawnIntervalFrames;
        await FTask.CompletedTask;
    }
}
