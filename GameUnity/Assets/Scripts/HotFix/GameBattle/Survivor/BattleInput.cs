namespace GameBattle
{
    /// <summary>
    /// 一个玩家在一个逻辑帧的指令。帧同步只同步它，网络上一帧一个玩家 2 字节。
    /// </summary>
    public struct BattleInput
    {
        /// <summary>摇杆 X，[-BattleConfig.INPUT_SCALE, INPUT_SCALE]。</summary>
        public sbyte MoveX;

        /// <summary>摇杆 Y，[-BattleConfig.INPUT_SCALE, INPUT_SCALE]。</summary>
        public sbyte MoveY;

        public BattleInput(int moveX, int moveY)
        {
            MoveX = Clamp(moveX);
            MoveY = Clamp(moveY);
        }

        // 输入来自网络，进入模拟前钳到合法范围，防止恶意值影响移动速度。
        private static sbyte Clamp(int v) =>
            (sbyte)(v < -BattleConfig.INPUT_SCALE ? -BattleConfig.INPUT_SCALE : v > BattleConfig.INPUT_SCALE ? BattleConfig.INPUT_SCALE : v);
    }
}
