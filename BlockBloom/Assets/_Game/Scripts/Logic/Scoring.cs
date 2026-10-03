namespace BlockBloom.Logic
{
    public struct MoveScore
    {
        public int PlacePoints, LinePoints, PerfectBonus;
        public int Combo;            // streak after this move (0 = none)
        public float Multiplier;
        public int Total { get { return PlacePoints + LinePoints + PerfectBonus; } }
    }

    /// <summary>
    /// Score rules: 1 point per cell placed; line clears are worth more the more lines at once; consecutive clearing moves build a combo
    /// multiplier. The combo survives <see cref="ComboGraceMoves"/> moves without a clear (generous = more satisfying streaks).
    /// </summary>
    public sealed class ScoreKeeper
    {
        public const int ComboGraceMoves = 3;
        public const int PerfectClearBonus = 250;

        public int Score;
        public int Combo;
        public int MovesSinceClear;
        public int TotalLines;
        public int BestCombo;

        public static int BaseLinePoints(int lines)
        {
            if (lines <= 0) return 0;
            return lines * 10 + (lines - 1) * lines * 5;   // 1:10  2:30  3:60  4:100  5:150  6:210 ...
        }

        public static float MultiplierFor(int combo)
        {
            if (combo <= 1) return 1f;
            float m = 1f + 0.5f * (combo - 1);
            return m > 6f ? 6f : m;
        }

        public MoveScore Apply(int cellsPlaced, int lines, bool perfectClear)
        {
            var ms = new MoveScore { PlacePoints = cellsPlaced };
            if (lines > 0)
            {
                Combo++;
                MovesSinceClear = 0;
                TotalLines += lines;
                if (Combo > BestCombo) BestCombo = Combo;
                ms.Multiplier = MultiplierFor(Combo);
                ms.LinePoints = (int)(BaseLinePoints(lines) * ms.Multiplier + 0.5f);
                if (perfectClear) ms.PerfectBonus = PerfectClearBonus;
            }
            else
            {
                ms.Multiplier = MultiplierFor(Combo);
                MovesSinceClear++;
                if (MovesSinceClear >= ComboGraceMoves) Combo = 0;
            }
            ms.Combo = Combo;
            Score += ms.Total;
            return ms;
        }

        public ScoreKeeper Clone()
        {
            return new ScoreKeeper { Score = Score, Combo = Combo, MovesSinceClear = MovesSinceClear, TotalLines = TotalLines, BestCombo = BestCombo };
        }

        public void CopyFrom(ScoreKeeper o)
        {
            Score = o.Score; Combo = o.Combo; MovesSinceClear = o.MovesSinceClear; TotalLines = o.TotalLines; BestCombo = o.BestCombo;
        }
    }
}
