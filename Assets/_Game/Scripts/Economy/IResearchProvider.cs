using MergeLegion.Data;

namespace MergeLegion.Economy
{
    /// <summary>Permanent per-line buffs from the Research Lab (section 2.2).</summary>
    public interface IResearchProvider
    {
        float BuyDiscount(UnitLineId line);   // 0..1
        float HpBonus(UnitLineId line);       // +0.10 = +10%
        float DamageBonus(UnitLineId line);
    }

    public sealed class NullResearch : IResearchProvider
    {
        public static readonly NullResearch Instance = new NullResearch();
        public float BuyDiscount(UnitLineId line) => 0f;
        public float HpBonus(UnitLineId line) => 0f;
        public float DamageBonus(UnitLineId line) => 0f;
    }
}
