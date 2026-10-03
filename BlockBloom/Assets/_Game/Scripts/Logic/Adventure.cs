using System.Collections.Generic;

namespace BlockBloom.Logic
{
    public enum GoalType { Score, Lines, Gems, Combo, Colour }

    public struct Goal
    {
        public GoalType Type;
        public int Target;
        public int Colour;      // only for GoalType.Colour (palette index)
    }

    public sealed class LevelDef
    {
        public int Index;            // 1-based
        public Goal[] Goals;
        public int MaxMoves;         // 0 = unlimited
        public int Star2Score, Star3Score;
        public ulong Prefill;        // cells that start occupied
        public ulong Gems;           // subset of Prefill that holds collectible gems
        public int Seed;
        public int RewardCoins;
        public int Chapter { get { return (Index - 1) / 10 + 1; } }
        public bool IsBoss { get { return Index % 10 == 0; } }
    }

    /// <summary>Procedural adventure levels: deterministic from the index so they can be tuned without shipping data.</summary>
    public static class Adventure
    {
        public const int LevelCount = 300;
        public const int PaletteSize = 7;

        public static LevelDef Get(int index)
        {
            if (index < 1) index = 1;
            if (index > LevelCount) index = LevelCount;
            var rng = new BbRng(index * 7919 + 13);
            var def = new LevelDef { Index = index, Seed = index * 104729 + 7 };

            int fill = index <= 2 ? 0 : System.Math.Min(18, 2 + index / 2);
            ulong prefill = RandomPrefill(rng, fill);
            var goals = new List<Goal>();

            // level flavour rotates so no two neighbours feel the same
            int flavour = index <= 3 ? 0 : (index % 6);
            int diff = System.Math.Min(index, 120);
            switch (flavour)
            {
                case 0: goals.Add(new Goal { Type = GoalType.Score, Target = 120 + diff * 7 }); break;
                case 1: goals.Add(new Goal { Type = GoalType.Lines, Target = System.Math.Min(20, 4 + diff / 4) }); break;
                case 2:
                    {
                        int n = System.Math.Min(9, 2 + diff / 8);
                        def.Gems = PickGems(rng, prefill, n, out prefill);
                        goals.Add(new Goal { Type = GoalType.Gems, Target = n });
                        break;
                    }
                case 3:
                    goals.Add(new Goal { Type = GoalType.Combo, Target = System.Math.Min(7, 2 + diff / 18) });
                    goals.Add(new Goal { Type = GoalType.Score, Target = 100 + diff * 5 });
                    break;
                case 4:
                    goals.Add(new Goal { Type = GoalType.Colour, Colour = rng.Range(0, PaletteSize), Target = System.Math.Min(30, 8 + diff / 4) });
                    break;
                default:
                    {
                        int n = System.Math.Min(7, 2 + diff / 12);
                        def.Gems = PickGems(rng, prefill, n, out prefill);
                        goals.Add(new Goal { Type = GoalType.Gems, Target = n });
                        goals.Add(new Goal { Type = GoalType.Lines, Target = System.Math.Min(16, 3 + diff / 6) });
                        break;
                    }
            }
            def.Prefill = prefill;
            def.Goals = goals.ToArray();

            // move budget: generous early, tighter later; boss levels are tighter again
            int need = EstimateMoves(def);
            float slack = index <= 5 ? 2.4f : (index <= 30 ? 1.8f : (index <= 100 ? 1.5f : 1.35f));
            if (def.IsBoss) slack *= 0.9f;
            def.MaxMoves = System.Math.Min(48, System.Math.Max(10, (int)(need * slack + 0.5f)));

            int baseScore = ScoreEstimate(def);
            def.Star2Score = (int)(baseScore * 1.35f);
            def.Star3Score = (int)(baseScore * 1.9f);
            def.RewardCoins = 40 + System.Math.Min(index, 150) + (def.IsBoss ? 150 : 0);
            return def;
        }

        /// <summary>Rough number of moves a decent player needs; the move limit is derived from it.</summary>
        private static int EstimateMoves(LevelDef d)
        {
            int moves = 6;
            foreach (var g in d.Goals)
            {
                switch (g.Type)
                {
                    case GoalType.Score: moves = System.Math.Max(moves, g.Target / 11); break;
                    case GoalType.Lines: moves = System.Math.Max(moves, (int)(g.Target * 1.8f) + 2); break;
                    case GoalType.Gems: moves = System.Math.Max(moves, g.Target * 3 + 4); break;
                    case GoalType.Combo: moves = System.Math.Max(moves, g.Target * 4 + 6); break;
                    case GoalType.Colour: moves = System.Math.Max(moves, g.Target / 2 + 8); break;
                }
            }
            return moves;
        }

        private static int ScoreEstimate(LevelDef d)
        {
            int s = d.MaxMoves * 5;
            foreach (var g in d.Goals) if (g.Type == GoalType.Score && g.Target > s) s = g.Target;
            return s;
        }

        private static ulong RandomPrefill(BbRng rng, int cells)
        {
            ulong occ = 0; int guard = 0;
            while (Bits.PopCount(occ) < cells && guard++ < 400)
            {
                int r = rng.Range(0, Bits.N), c = rng.Range(0, Bits.N);
                ulong next = occ | Bits.Bit(r, c);
                if (Bits.ClearedMask(next) != 0) continue;   // never pre-complete a line
                occ = next;
            }
            return occ;
        }

        private static ulong PickGems(BbRng rng, ulong prefill, int count, out ulong prefillOut)
        {
            ulong gems = 0; int guard = 0;
            while (Bits.PopCount(gems) < count && guard++ < 600)
            {
                int r = rng.Range(0, Bits.N), c = rng.Range(0, Bits.N);
                ulong b = Bits.Bit(r, c);
                if ((gems & b) != 0) continue;
                if (Bits.ClearedMask(prefill | gems | b) != 0) continue;
                gems |= b;
            }
            prefillOut = prefill | gems;
            return gems;
        }

        public static string GoalText(Goal g)
        {
            switch (g.Type)
            {
                case GoalType.Score: return "Score " + g.Target;
                case GoalType.Lines: return "Clear " + g.Target + " lines";
                case GoalType.Gems: return "Collect " + g.Target + " gems";
                case GoalType.Combo: return "Reach a x" + g.Target + " combo";
                default: return "Clear " + g.Target + " blocks";
            }
        }

        public static string ShortGoal(Goal g)
        {
            switch (g.Type)
            {
                case GoalType.Score: return "SCORE";
                case GoalType.Lines: return "LINES";
                case GoalType.Gems: return "GEMS";
                case GoalType.Combo: return "COMBO";
                default: return "BLOCKS";
            }
        }
    }
}
