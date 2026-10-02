namespace MergeLegion.Core
{
    /// <summary>xorshift64* RNG. Identical sequences on every platform, so seeded battles replay exactly.</summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        public DeterministicRng(int seed)
        {
            _state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL;
            if (_state == 0) _state = 0x2545F4914F6CDD1DUL;
            for (int i = 0; i < 4; i++) NextULong();
        }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Returns [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public int Range(int minInclusive, int maxExclusive) => minInclusive + NextInt(maxExclusive - minInclusive);

        /// <summary>Returns [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) / (float)(1UL << 24);

        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
