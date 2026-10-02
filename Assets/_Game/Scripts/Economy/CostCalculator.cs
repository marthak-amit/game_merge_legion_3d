using System;

namespace MergeLegion.Economy
{
    public static class CostCalculator
    {
        /// <summary>cost = base * growth^buyCount, reduced by a research discount (0..1), at least 1.</summary>
        public static long UnitCost(int baseCost, float growth, int buyCount, float discount = 0f)
        {
            double raw = baseCost * Math.Pow(growth, Math.Max(0, buyCount));
            raw *= 1.0 - Math.Min(0.9, Math.Max(0.0, discount));
            return Math.Max(1L, (long)Math.Ceiling(raw - 1e-9));
        }
    }
}
