using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>
    /// 敌人的空间哈希网格（格子边长 1），用于邻近查询，让碰撞从 O(n²) 降到 O(n)。
    /// 每帧整体重建；桶冲突只会多出候选，不影响正确性和确定性。
    /// </summary>
    public sealed class SpatialGrid
    {
        private const int BUCKET_COUNT = 4096;
        private const int BUCKET_MASK = BUCKET_COUNT - 1;

        private readonly int[] m_head = new int[BUCKET_COUNT];
        private readonly int[] m_next;

        public SpatialGrid(int capacity)
        {
            m_next = new int[capacity];
        }

        public void Build(EnemyBuffer enemies)
        {
            for (var b = 0; b < BUCKET_COUNT; b++)
            {
                m_head[b] = -1;
            }

            for (var i = 0; i < enemies.Count; i++)
            {
                var b = Bucket(Cell(enemies.Pos[i].x), Cell(enemies.Pos[i].y));
                m_next[i] = m_head[b];
                m_head[b] = i;
            }
        }

        /// <summary>某格子桶内第一个敌人下标，-1 表示空。配合 Next 遍历。</summary>
        public int First(int cellX, int cellY) => m_head[Bucket(cellX, cellY)];

        public int Next(int index) => m_next[index];

        /// <summary>Q32.32 定点数右移 32 位即向下取整，得到格子坐标。</summary>
        public static int Cell(FixedPoint64 v) => (int)(v.RawValue >> FixedPoint64.FRACTIONAL_PLACES);

        private static int Bucket(int cellX, int cellY) => unchecked((cellX * 73856093) ^ (cellY * 19349663)) & BUCKET_MASK;
    }
}
