namespace MergeLegion.Grid
{
    public enum DropKind { Invalid, Move, Merge, Swap }

    public struct DropResult
    {
        public DropKind Kind;
        public int From;
        public int To;
        public int Line;
        public int Level;   // Merge: the new level now at 'To'. Otherwise the level of the unit that was dragged.
    }

    /// <summary>
    /// Merge rules (section 1.1): same line + same level merges to level+1 (max configurable);
    /// empty cell = move; different unit = swap.
    /// </summary>
    public static class MergeService
    {
        public static bool CanMerge(UnitSlot a, UnitSlot b, int maxLevel)
        {
            return !a.IsEmpty && !b.IsEmpty && a.line == b.line && a.level == b.level && a.level < maxLevel;
        }

        public static DropResult Drop(GridModel grid, int from, int to, int maxLevel)
        {
            var invalid = new DropResult { Kind = DropKind.Invalid, From = from, To = to };
            if (from == to || !grid.InRange(from) || !grid.InRange(to) || grid.IsEmpty(from)) return invalid;

            var a = grid.Get(from);
            var b = grid.Get(to);

            if (b.IsEmpty)
            {
                grid.Set(to, a);
                grid.Set(from, UnitSlot.Empty);
                return new DropResult { Kind = DropKind.Move, From = from, To = to, Line = a.line, Level = a.level };
            }

            if (CanMerge(a, b, maxLevel))
            {
                grid.Set(to, UnitSlot.Of(a.line, a.level + 1));
                grid.Set(from, UnitSlot.Empty);
                return new DropResult { Kind = DropKind.Merge, From = from, To = to, Line = a.line, Level = a.level + 1 };
            }

            // Two identical max-level units: swapping changes nothing, so reject and let the view snap back.
            if (a.line == b.line && a.level == b.level) return invalid;

            grid.Set(to, a);
            grid.Set(from, b);
            return new DropResult { Kind = DropKind.Swap, From = from, To = to, Line = a.line, Level = a.level };
        }
    }
}
