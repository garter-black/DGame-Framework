using System;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace Hotfix;

/// <summary>单人战斗指令批次：服务端重放并核对检查点哈希。</summary>
public sealed class C2G_SoloBattleInputsRequestHandler : MessageRPC<C2G_SoloBattleInputsRequest, G2C_SoloBattleInputsResponse>
{
    protected override async FTask Run(Session session, C2G_SoloBattleInputsRequest request, G2C_SoloBattleInputsResponse response, Action reply)
    {
        var battle = session.GetSoloBattle(request.BattleId);
        if (battle == null)
        {
            response.ErrorCode = ErrorCode.BATTLE_NOT_FOUND;
            return;
        }

        response.ErrorCode = battle.ApplyInputs(request.StartFrame, request.Inputs, request.CheckpointHash);
        response.VerifiedFrame = battle.IsDisposed ? 0 : battle.Verifier.Frame;
        await FTask.CompletedTask;
    }
}
