using System;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace Hotfix;

/// <summary>单人战斗结算：结果以服务端重放为准，校验通过后本局结束。</summary>
public sealed class C2G_SoloBattleEndRequestHandler : MessageRPC<C2G_SoloBattleEndRequest, G2C_SoloBattleEndResponse>
{
    protected override async FTask Run(Session session, C2G_SoloBattleEndRequest request, G2C_SoloBattleEndResponse response, Action reply)
    {
        var battle = session.GetSoloBattle(request.BattleId);
        if (battle == null)
        {
            response.ErrorCode = ErrorCode.BATTLE_NOT_FOUND;
            return;
        }

        response.ErrorCode = battle.Finish(request.FinalFrame, request.FinalHash);
        if (response.ErrorCode == ErrorCode.SUCCESS)
        {
            // 发奖、排行榜等结算接在这里，数据只取服务端模拟的状态。
            var state = battle.Verifier.Simulation.State;
            response.Frame = state.Frame;
            response.Kills = state.TotalKills;
            response.PlayerDied = state.IsOver;
            battle.Dispose();
        }

        await FTask.CompletedTask;
    }
}
