using System.Collections.Generic;
using MergeLegion.Core;

namespace MergeLegion.Grid
{
    public struct UnitSlot
    {
        public int line;   // -1 = empty
        public int level;

        public bool IsEmpty => line < 0;
        public static UnitSlot Empty => new UnitSlot { line = -1, level = 0 };
        public static UnitSlot Of(int line, int level) => new UnitSlot { line = line, level = level };
    }

    /// <summary>Pure state of the player's merge grid. Index = row * cols + col; row 0 is the front row.</summary>
    public sealed class GridModel
    {
        private readonly UnitSlot[] _cells;

        public int Cols { get; }
        public int Rows { get; }
        public int Count => _cells.Length;

        public GridModel(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
            _cells = new UnitSlot[cols * rows];
            Clear();
        }

        public int Index(int col, int row) => row * Cols + col;
        public int ColOf(int index) => index % Cols;
        public int RowOf(int index) => index / Cols;
        public bool InRange(int index) => index >= 0 && index < _cells.Length;

        public UnitSlot Get(int index) => _cells[index];
        public void Set(int index, UnitSlot slot) => _cells[index] = slot;
        public bool IsEmpty(int index) => _cells[index].IsEmpty;

        public void Clear()
        {
            for (int i = 0; i < _cells.Length; i++) _cells[i] = UnitSlot.Empty;
        }

        public int EmptyCount()
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++) if (_cells[i].IsEmpty) n++;
            return n;
        }

        public int UnitCount() => _cells.Length - EmptyCount();

        /// <summary>Picks a uniformly random empty cell, or -1 when full.</summary>
        public int PickRandomEmpty(DeterministicRng rng)
        {
            int empty = EmptyCount();
            if (empty == 0) return -1;
            int pick = rng.NextInt(empty);
            for (int i = 0; i < _cells.Length; i++)
            {
                if (!_cells[i].IsEmpty) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        public int HighestLevel()
        {
            int best = 0;
            for (int i = 0; i < _cells.Length; i++)
                if (!_cells[i].IsEmpty && _cells[i].level > best) best = _cells[i].level;
            return best;
        }

        public void Snapshot(List<UnitSlot> into)
        {
            into.Clear();
            for (int i = 0; i < _cells.Length; i++) into.Add(_cells[i]);
        }
    }
}
