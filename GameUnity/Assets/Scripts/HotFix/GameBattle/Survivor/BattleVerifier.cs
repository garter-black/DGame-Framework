namespace GameBattle
{
    public enum VerifyResult
    {
        Ok,
        /// <summary>批次起始帧不是"已校验帧 + 1"（丢包、重发或乱序）。</summary>
        NotSequential,
        /// <summary>指令数据长度不合法、超出单批上限，或检查点帧号对不上。</summary>
        InvalidInput,
        /// <summary>同一帧双方状态哈希不同：不同步或作弊。</summary>
        HashMismatch,
    }

    /// <summary>
    /// 服务端校验器：按顺序吃进指令批次，用同一份模拟重放，并核对客户端上报的检查点哈希。
    /// 单人校验和以后的联机房间都用它；它不碰时间和网络，可以离线测试。
    /// </summary>
    public sealed class BattleVerifier
    {
        public BattleSimulation Simulation { get; }

        public BattleReplay Replay { get; }

        /// <summary>已校验（已重放）到的帧号。</summary>
        public int Frame => Simulation.State.Frame;

        public BattleVerifier(in BattleSettings settings)
        {
            Simulation = new BattleSimulation(settings);
            Replay = new BattleReplay(settings);
        }

        /// <param name="startFrame">本批第一帧的帧号（从 1 开始）。</param>
        /// <param name="packedInputs">BattleInputCodec.Pack 的结果。</param>
        /// <param name="checkpointHash">
        /// 客户端在本批最后一帧算出的状态哈希；0 表示本批不带检查点（积压过多被拆批时）。
        /// 跳过检查点不能作弊：后续检查点和结算哈希仍会覆盖这些帧。
        /// </param>
        public VerifyResult ApplyBatch(int startFrame, byte[] packedInputs, ulong checkpointHash)
        {
            if (startFrame != Frame + 1)
            {
                return VerifyResult.NotSequential;
            }

            if (!BattleInputCodec.TryUnpack(packedInputs, Replay.Settings.PlayerCount, out var frames))
            {
                return VerifyResult.InvalidInput;
            }

            foreach (var inputs in frames)
            {
                Replay.Record(inputs);
                Simulation.Tick(inputs);
            }

            return checkpointHash == 0 || Simulation.ComputeHash() == checkpointHash ? VerifyResult.Ok : VerifyResult.HashMismatch;
        }

        /// <summary>结算：客户端声称的最终帧和哈希必须与服务端一致。</summary>
        public VerifyResult Finish(int finalFrame, ulong finalHash)
        {
            if (finalFrame != Frame)
            {
                return VerifyResult.InvalidInput;
            }

            return Simulation.ComputeHash() == finalHash ? VerifyResult.Ok : VerifyResult.HashMismatch;
        }
    }
}
