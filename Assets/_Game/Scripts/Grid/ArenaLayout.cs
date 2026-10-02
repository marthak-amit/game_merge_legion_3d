using MergeLegion.Battle;
using MergeLegion.Data;

namespace MergeLegion.Grid
{
    /// <summary>
    /// World placement of the arena. The player's grid sits on the near half (negative z), the enemy formation mirrors it.
    /// Row 0 of either side is the row closest to the centre line.
    /// </summary>
    public sealed class ArenaLayout
    {
        public readonly int Cols;
        public readonly int Rows;
        public readonly float CellSize;
        public readonly float FrontZ;

        public ArenaLayout(GridConfig cfg)
        {
            Cols = cfg.cols;
            Rows = cfg.rows;
            CellSize = cfg.cellSize;
            FrontZ = cfg.frontZ;
        }

        public float ColumnX(int col, int cols) => (col - (cols - 1) * 0.5f) * CellSize;

        public Vec2 PlayerCell(int col, int row) => new Vec2(ColumnX(col, Cols), FrontZ - row * CellSize);

        /// <summary>Enemy formations are authored on the same column count; extra rows extend away from the centre.</summary>
        public Vec2 EnemyCell(int col, int row, int cols) => new Vec2(ColumnX(col, cols), -FrontZ + row * CellSize);

        public float HalfWidth => Cols * CellSize * 0.5f + 1.2f;
        public float NearZ => FrontZ - Rows * CellSize - 0.8f;
        public float FarZ => -FrontZ + (Rows + 2) * CellSize;
    }
}
