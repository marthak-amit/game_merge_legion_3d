using System;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Save;

namespace MergeLegion.Meta
{
    public enum ResearchStat { Hp, Damage, Discount }

    public enum ResearchResult { Ok, MaxLevel, NoCoins }

    public readonly struct ResearchChangedEvent
    {
        public readonly UnitLineId Line;
        public readonly ResearchStat Stat;
        public ResearchChangedEvent(UnitLineId line, ResearchStat stat) { Line = line; Stat = stat; }
    }

    /// <summary>Research Lab (section 2.2): permanent per-line buffs bought with coins.</summary>
    public sealed class ResearchService : IResearchProvider
    {
        private readonly SaveService _save;
        private readonly CurrencyService _currency;
        private readonly ResearchConfig _cfg;

        public ResearchService(SaveService save, CurrencyService currency, ResearchConfig cfg)
        {
            _save = save;
            _currency = currency;
            _cfg = cfg;
        }

        private static string Key(UnitLineId line, ResearchStat stat) => (int)line + "_" + (int)stat;

        public int GetLevel(UnitLineId line, ResearchStat stat) => IntEntries.Get(_save.Data.research, Key(line, stat));

        public int MaxLevel(ResearchStat stat)
        {
            switch (stat)
            {
                case ResearchStat.Hp: return _cfg.maxHpLevel;
                case ResearchStat.Damage: return _cfg.maxDamageLevel;
                default: return _cfg.maxDiscountLevel;
            }
        }

        public long Cost(UnitLineId line, ResearchStat stat)
        {
            int lv = GetLevel(line, stat);
            double baseCost = stat == ResearchStat.Discount ? _cfg.discountBaseCost : _cfg.baseCost;
            double growth = stat == ResearchStat.Discount ? _cfg.discountCostGrowth : _cfg.costGrowth;
            return (long)Math.Ceiling(baseCost * Math.Pow(growth, lv));
        }

        public ResearchResult Upgrade(UnitLineId line, ResearchStat stat)
        {
            if (GetLevel(line, stat) >= MaxLevel(stat)) return ResearchResult.MaxLevel;
            if (!_currency.TrySpend(CurrencyType.Coins, Cost(line, stat), "research")) return ResearchResult.NoCoins;
            IntEntries.Add(_save.Data.research, Key(line, stat), 1);
            _save.MarkDirty();
            EventBus.Publish(new ResearchChangedEvent(line, stat));
            return ResearchResult.Ok;
        }

        public float BuyDiscount(UnitLineId line) => GetLevel(line, ResearchStat.Discount) * _cfg.discountPerLevel;
        public float HpBonus(UnitLineId line) => GetLevel(line, ResearchStat.Hp) * _cfg.hpPerLevel;
        public float DamageBonus(UnitLineId line) => GetLevel(line, ResearchStat.Damage) * _cfg.damagePerLevel;
    }
}
