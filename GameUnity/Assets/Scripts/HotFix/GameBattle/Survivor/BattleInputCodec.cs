using System.Collections.Generic;

namespace GameBattle
{
    /// <summary>
    /// 指令批量打包：每帧每个玩家 2 字节（MoveX、MoveY），按帧、按玩家槽位顺序排列。
    /// 客户端上传和服务端解包共用，网络上只传这个字节数组。
    /// </summary>
    public static class BattleInputCodec
    {
        public const int BYTES_PER_INPUT = 2;

        /// <summary>单批最多帧数（10 秒），防止一次塞入过大的数据。</summary>
        public const int MAX_FRAMES_PER_BATCH = BattleConfig.TICK_RATE * 10;

        public static byte[] Pack(IReadOnlyList<BattleInput[]> frames, int start, int count, int playerCount)
        {
            var data = new byte[count * playerCount * BYTES_PER_INPUT];
            var n = 0;
            for (var f = start; f < start + count; f++)
            {
                for (var i = 0; i < playerCount; i++)
                {
                    data[n++] = unchecked((byte)frames[f][i].MoveX);
                    data[n++] = unchecked((byte)frames[f][i].MoveY);
                }
            }

            return data;
        }

        /// <summary>解包。数据长度不合法或超过单批上限时返回 false。</summary>
        public static bool TryUnpack(byte[] data, int playerCount, out BattleInput[][] frames)
        {
            frames = null;
            var frameBytes = playerCount * BYTES_PER_INPUT;
            if (data == null || data.Length == 0 || frameBytes <= 0 || data.Length % frameBytes != 0)
            {
                return false;
            }

            var count = data.Length / frameBytes;
            if (count > MAX_FRAMES_PER_BATCH)
            {
                return false;
            }

            frames = new BattleInput[count][];
            var n = 0;
            for (var f = 0; f < count; f++)
            {
                frames[f] = new BattleInput[playerCount];
                for (var i = 0; i < playerCount; i++)
                {
                    // 构造函数会把越界值钳回合法范围。
                    frames[f][i] = new BattleInput(unchecked((sbyte)data[n]), unchecked((sbyte)data[n + 1]));
                    n += 2;
                }
            }

            return true;
        }
    }
}
