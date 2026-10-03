namespace BlockBloom.Logic
{
    public enum GoalType { Score, Lines, Gems }

    public sealed class LevelDef
    {
        public int Index;            // 1-based
        public GoalType Goal;
        public int Target;
        public ulong Prefill;        // cells that start occupied
        public ulong Gems;           // subset of Prefill that holds collectible gems
        public int Seed;
        public int RewardCoins;
        public int Chapter { get { return (Index - 1) / 10 + 1; } }
    }

    /// <summary>Procedural adventure levels: deterministic from the index, so they can be tuned without shipping data.</summary>
    public static class Adventure
    {
        public const int LevelCount = 300;

        public static LevelDef Get(int index)
        {
            if (index < 1) index = 1;
            var rng = new BbRng(index * 7919 + 13);
            var def = new LevelDef { Index = index, Seed = index * 104729 + 7 };

            int type = index <= 3 ? 0 : (index % 3);
            def.Goal = type == 0 ? GoalType.Score : (type == 1 ? GoalType.Lines : GoalType.Gems);

            int fillCells = index <= 2 ? 0 : System.Math.Min(30, 4 + index * 2 / 3);
            def.Prefill = RandomPrefill(rng, fillCells);

            switch (def.Goal)
            {
                case GoalType.Score: def.Target = 140 + index * 38; break;
                case GoalType.Lines: def.Target = System.Math.Min(36, 4 + index / 2); break;
                default:
                    int gems = System.Math.Min(14, 3 + index / 4);
                    def.Gems = PickGems(rng, def.Prefill, gems, ref def.Prefill);
                    def.Target = Bits.PopCount(def.Gems);
                    break;
            }
            def.RewardCoins = 40 + System.Math.Min(index, 120) * 2 + (index % 10 == 0 ? 150 : 0);
            return def;
        }

        private static ulong RandomPrefill(BbRng rng, int cells)
        {
            ulong occ = 0; int guard = 0;
            while (Bits.PopCount(occ) < cells && guard++ < 400)
            {
                int r = rng.Range(0, Bits.N), c = rng.Range(0, Bits.N);
                ulong next = occ | Bits.Bit(r, c);
                // never pre-complete a line
                if (Bits.ClearedMask(next) != 0) continue;
                occ = next;
            }
            return occ;
        }

        private static ulong PickGems(BbRng rng, ulong prefill, int count, ref ulong prefillOut)
        {
            ulong gems = 0; int guard = 0;
            while (Bits.PopCount(gems) < count && guard++ < 600)
            {
                int r = rng.Range(0, Bits.N), c = rng.Range(0, Bits.N);
                ulong b = Bits.Bit(r, c);
                if ((gems & b) != 0) continue;
                ulong nextOcc = prefill | gems | b;
                if (Bits.ClearedMask(nextOcc) != 0) continue;
                gems |= b;
            }
            prefillOut = prefill | gems;
            return gems;
        }

        public static string GoalText(LevelDef d)
        {
            switch (d.Goal)
            {
                case GoalType.Score: return "Score " + d.Target;
                case GoalType.Lines: return "Clear " + d.Target + " lines";
                default: return "Collect " + d.Target + " gems";
            }
        }
    }
}
