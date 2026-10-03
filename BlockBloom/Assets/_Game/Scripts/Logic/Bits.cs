namespace BlockBloom.Logic
{
    /// <summary>The 8x8 board lives in one ulong: bit (r * 8 + c). Everything in the puzzle core is bit math, so solving and generating are cheap.</summary>
    public static class Bits
    {
        public const int N = 8;
        public const int Cells = 64;
        private const ulong RowBase = 0xFFUL;
        private const ulong ColBase = 0x0101010101010101UL;

        public static readonly ulong[] RowMasks = BuildRows();
        public static readonly ulong[] ColMasks = BuildCols();

        private static ulong[] BuildRows() { var a = new ulong[N]; for (int r = 0; r < N; r++) a[r] = RowBase << (r * N); return a; }
        private static ulong[] BuildCols() { var a = new ulong[N]; for (int c = 0; c < N; c++) a[c] = ColBase << c; return a; }

        public static ulong Bit(int r, int c) { return 1UL << (r * N + c); }

        public static int PopCount(ulong v)
        {
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }

        /// <summary>Complete rows / columns in an occupancy mask.</summary>
        public static ulong FullLines(ulong occ, out int rows, out int cols, out int rowMask, out int colMask)
        {
            ulong cleared = 0; rows = 0; cols = 0; rowMask = 0; colMask = 0;
            for (int i = 0; i < N; i++)
            {
                if ((occ & RowMasks[i]) == RowMasks[i]) { rows++; rowMask |= 1 << i; cleared |= RowMasks[i]; }
                if ((occ & ColMasks[i]) == ColMasks[i]) { cols++; colMask |= 1 << i; cleared |= ColMasks[i]; }
            }
            return cleared;
        }

        public static ulong ClearedMask(ulong occ)
        {
            int a, b, c, d;
            return FullLines(occ, out a, out b, out c, out d);
        }

        /// <summary>3x3 block centred on (r,c), clipped to the board. Used by the bomb power-up.</summary>
        public static ulong Area3x3(int r, int c)
        {
            ulong m = 0;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int rr = r + dr, cc = c + dc;
                    if (rr >= 0 && rr < N && cc >= 0 && cc < N) m |= Bit(rr, cc);
                }
            return m;
        }
    }

    /// <summary>Tiny xorshift RNG: deterministic across platforms (daily challenge and adventure levels depend on it).</summary>
    public sealed class BbRng
    {
        private uint _s;
        public BbRng(int seed) { _s = (uint)(seed * 747796405 + 2891336453u); if (_s == 0) _s = 0x9E3779B9; Next(); Next(); }
        public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public int Range(int minInclusive, int maxExclusive) { return minInclusive + (int)(Next() % (uint)(maxExclusive - minInclusive)); }
        public float Value() { return (Next() & 0xFFFFFF) / 16777216f; }
        public bool Chance(float p) { return Value() < p; }
    }
}
