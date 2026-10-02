namespace MergeLegion.Data
{
    public enum UnitLineId
    {
        Melee = 0,
        Ranged = 1,
        Tank = 2,
        Flying = 3
    }

    public static class UnitLines
    {
        public const int Count = 4;
        public static UnitLineId FromIndex(int i) => (UnitLineId)i;
    }
}
