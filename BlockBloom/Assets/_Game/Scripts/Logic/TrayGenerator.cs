using System.Collections.Generic;

namespace BlockBloom.Logic
{
    /// <summary>Tunable (remote config) knobs for the piece generator.</summary>
    public sealed class GenParams
    {
        public float AssistChance = 0.55f;      // when the board is crowded, chance that a tray contains a line-completing piece
        public float CrowdStart = 0.34f;        // fill ratio where the generator begins to favour small pieces
        public float HardScore = 6000f;         // score where big awkward pieces reach full weight
        public int Attempts = 40;
        public int NodeBudget = 25000;
    }

    public static class Solver
    {
        /// <summary>True when all pieces of the tray can be placed one after the other (line clears between placements included).</summary>
        public static bool TraySolvable(ulong occ, Shape[] tray, int nodeBudget)
        {
            int nodes = 0;
            return Dfs(occ, tray, 0, ref nodes, nodeBudget);
        }

        private static bool Dfs(ulong occ, Shape[] tray, int used, ref int nodes, int budget)
        {
            if (used == (1 << tray.Length) - 1) return true;
            for (int i = 0; i < tray.Length; i++)
            {
                if ((used & (1 << i)) != 0) continue;
                // identical pieces: only try the first copy
                bool dup = false;
                for (int j = 0; j < i; j++) if ((used & (1 << j)) == 0 && tray[j] == tray[i]) { dup = true; break; }
                if (dup) continue;
                Shape s = tray[i];
                for (int r = 0; r + s.Height <= Bits.N; r++)
                    for (int c = 0; c + s.Width <= Bits.N; c++)
                    {
                        ulong m = s.MaskAt(r, c);
                        if ((occ & m) != 0) continue;
                        if (++nodes > budget) return true;
                        ulong n = occ | m;
                        n &= ~Bits.ClearedMask(n);
                        if (Dfs(n, tray, used | (1 << i), ref nodes, budget)) return true;
                    }
            }
            return false;
        }

        /// <summary>Can this piece complete at least one line somewhere on the board?</summary>
        public static bool CanClearLine(ulong occ, Shape s)
        {
            for (int r = 0; r + s.Height <= Bits.N; r++)
                for (int c = 0; c + s.Width <= Bits.N; c++)
                {
                    ulong m = s.MaskAt(r, c);
                    if ((occ & m) != 0) continue;
                    if (Bits.ClearedMask(occ | m) != 0) return true;
                }
            return false;
        }

        public static bool AnyPlacement(ulong occ, Shape s)
        {
            for (int r = 0; r + s.Height <= Bits.N; r++)
                for (int c = 0; c + s.Width <= Bits.N; c++)
                    if ((occ & s.MaskAt(r, c)) == 0) return true;
            return false;
        }

        public static bool GameOver(ulong occ, IList<Shape> tray)
        {
            for (int i = 0; i < tray.Count; i++) if (tray[i] != null && AnyPlacement(occ, tray[i])) return false;
            return true;
        }

        /// <summary>Best greedy placement for a piece (most lines, then most compact). Used by revive helper, auto-play tests and the tutorial hint.</summary>
        public static bool BestPlacement(ulong occ, Shape s, out int bestR, out int bestC)
        {
            bestR = -1; bestC = -1; int bestScore = int.MinValue;
            for (int r = 0; r + s.Height <= Bits.N; r++)
                for (int c = 0; c + s.Width <= Bits.N; c++)
                {
                    ulong m = s.MaskAt(r, c);
                    if ((occ & m) != 0) continue;
                    ulong after = occ | m;
                    ulong cleared = Bits.ClearedMask(after);
                    after &= ~cleared;
                    int score = Bits.PopCount(cleared) * 20 - Bits.PopCount(after) * 2 - Holes(after);
                    if (score > bestScore) { bestScore = score; bestR = r; bestC = c; }
                }
            return bestR >= 0;
        }

        // counts empty cells with no empty neighbours (isolated holes) - lower is better
        private static int Holes(ulong occ)
        {
            int holes = 0;
            for (int r = 0; r < Bits.N; r++)
                for (int c = 0; c < Bits.N; c++)
                {
                    if ((occ & Bits.Bit(r, c)) != 0) continue;
                    bool open = (r > 0 && (occ & Bits.Bit(r - 1, c)) == 0) || (r < Bits.N - 1 && (occ & Bits.Bit(r + 1, c)) == 0)
                             || (c > 0 && (occ & Bits.Bit(r, c - 1)) == 0) || (c < Bits.N - 1 && (occ & Bits.Bit(r, c + 1)) == 0);
                    if (!open) holes += 3;
                }
            return holes;
        }
    }

    /// <summary>
    /// Produces the three pieces of each tray. It is deliberately not uniform random:
    /// every tray is guaranteed solvable, a crowded board gets smaller pieces and (often) a line-completing piece, and a rising score
    /// gradually brings in the awkward big shapes - so the game stays tense without ever feeling unfair.
    /// </summary>
    public sealed class TrayGenerator
    {
        private readonly BbRng _rng;
        public readonly GenParams P;

        public TrayGenerator(int seed, GenParams p = null) { _rng = new BbRng(seed); P = p ?? new GenParams(); }

        public Shape[] Next(ulong occ, int score)
        {
            float fill = Bits.PopCount(occ) / 64f;
            float crowd = Clamp01((fill - P.CrowdStart) / 0.45f);
            float hard = Clamp01(score / P.HardScore);
            bool wantAssist = crowd > 0.15f && _rng.Chance(P.AssistChance);

            var weights = BuildWeights(crowd, hard);
            var tray = new Shape[3];

            for (int pass = 0; pass < 2; pass++)
            {
                bool needAssist = wantAssist && pass == 0;
                for (int i = 0; i < P.Attempts; i++)
                {
                    for (int k = 0; k < 3; k++) tray[k] = Pick(weights);
                    if (!Solver.TraySolvable(occ, tray, P.NodeBudget)) continue;
                    if (needAssist)
                    {
                        bool any = false;
                        for (int k = 0; k < 3 && !any; k++) any = Solver.CanClearLine(occ, tray[k]);
                        if (!any) continue;
                    }
                    // avoid three huge pieces on a crowded board even when technically solvable
                    if (crowd > 0.5f && tray[0].Count >= 5 && tray[1].Count >= 5 && tray[2].Count >= 5) continue;
                    return (Shape[])tray.Clone();
                }
            }

            // mercy: nothing fully solvable - hand out the smallest pieces that still fit
            var fits = new List<Shape>();
            foreach (var s in ShapeCatalog.All) if (Solver.AnyPlacement(occ, s)) fits.Add(s);
            fits.Sort((a, b) => a.Count.CompareTo(b.Count));
            var mercy = new Shape[3];
            for (int k = 0; k < 3; k++)
            {
                if (fits.Count == 0) mercy[k] = ShapeCatalog.All[0];
                else mercy[k] = fits[_rng.Range(0, System.Math.Min(fits.Count, 6))];
            }
            return mercy;
        }

        /// <summary>Plain weighted tray (daily challenge: the same sequence for every player, no board feedback).</summary>
        public Shape[] NextBlind()
        {
            var w = BuildWeights(0f, 0.3f);
            return new[] { Pick(w), Pick(w), Pick(w) };
        }

        private float[] BuildWeights(float crowd, float hard)
        {
            var all = ShapeCatalog.All;
            var w = new float[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                int n = all[i].Count;
                float small = n <= 3 ? 2.4f : (n == 4 ? 1.15f : 0.4f);
                float big = n >= 5 ? 1.6f : 1f;
                w[i] = all[i].Weight * Lerp(1f, small, crowd) * Lerp(1f, big, hard);
            }
            return w;
        }

        private Shape Pick(float[] w)
        {
            float total = 0f;
            for (int i = 0; i < w.Length; i++) total += w[i];
            float t = _rng.Value() * total;
            for (int i = 0; i < w.Length; i++)
            {
                t -= w[i];
                if (t <= 0f) return ShapeCatalog.All[i];
            }
            return ShapeCatalog.All[w.Length - 1];
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    }
}
