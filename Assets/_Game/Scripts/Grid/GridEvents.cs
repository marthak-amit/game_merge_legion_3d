namespace MergeLegion.Grid
{
    public readonly struct UnitSpawnedEvent
    {
        public readonly int Index, Line, Level;
        public UnitSpawnedEvent(int index, int line, int level) { Index = index; Line = line; Level = level; }
    }

    public readonly struct UnitMovedEvent
    {
        public readonly int From, To;
        public UnitMovedEvent(int from, int to) { From = from; To = to; }
    }

    public readonly struct UnitSwappedEvent
    {
        public readonly int A, B;
        public UnitSwappedEvent(int a, int b) { A = a; B = b; }
    }

    public readonly struct UnitMergedEvent
    {
        public readonly int From, To, Line, NewLevel;
        public UnitMergedEvent(int from, int to, int line, int newLevel) { From = from; To = to; Line = line; NewLevel = newLevel; }
    }

    /// <summary>Whole grid replaced (load, revive, reset): views must rebuild.</summary>
    public readonly struct GridResetEvent { }
}
