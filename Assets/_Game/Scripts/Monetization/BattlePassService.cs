using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Meta;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Monetization
{
    public readonly struct BattlePassChangedEvent
    {
        public readonly int Tier;
        public BattlePassChangedEvent(int tier) { Tier = tier; }
    }

    /// <summary>
    /// 30-day season, 30 tiers, free + premium tracks (section 2.8). Season XP lives in the BattlePassXp currency and
    /// resets, together with claims and the premium unlock, when a new season starts.
    /// </summary>
    public sealed class BattlePassService : IDisposable
    {
        public static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly SaveService _save;
        private readonly BattlePassConfig _cfg;
        private readonly ITimeService _time;
        private readonly RewardGranter _granter;
        private readonly IAnalyticsService _analytics;
        private int _lastTier;

        public BattlePassService(SaveService save, BattlePassConfig cfg, ITimeService time, RewardGranter granter, IAnalyticsService analytics)
        {
            _save = save;
            _cfg = cfg;
            _time = time;
            _granter = granter;
            _analytics = analytics;
            EnsureSeason();
            _lastTier = Tier;
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
        }

        public void Dispose() => EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);

        public int TierCount => _cfg.tiers.Count;
        public BattlePassTierDef TierDef(int tier) => _cfg.tiers[tier - 1];
        public string PremiumSku => _cfg.premiumSku;

        public int SeasonId => (int)Math.Floor((_time.UtcNow - Epoch).TotalDays / Math.Max(1, _cfg.seasonDays));

        public TimeSpan TimeLeft
        {
            get
            {
                var end = Epoch.AddDays((SeasonId + 1) * (double)_cfg.seasonDays);
                return end - _time.UtcNow;
            }
        }

        public long Xp
        {
            get { EnsureSeason(); return _save.Data.battlePassXp; }
        }

        public bool Premium
        {
            get { EnsureSeason(); return _save.Data.bpPremium; }
        }

        /// <summary>Cumulative XP needed to reach <paramref name="tier"/>.</summary>
        public long XpToReach(int tier)
        {
            long sum = 0;
            for (int i = 1; i <= tier; i++) sum += _cfg.baseXp + (long)_cfg.xpStep * (i - 1);
            return sum;
        }

        public int Tier
        {
            get
            {
                long xp = Xp;
                int tier = 0;
                while (tier < TierCount && xp >= XpToReach(tier + 1)) tier++;
                return tier;
            }
        }

        /// <summary>Progress inside the current tier as (have, need). At max tier both are 0.</summary>
        public void TierProgress(out long have, out long need)
        {
            int tier = Tier;
            if (tier >= TierCount) { have = need = 0; return; }
            have = Xp - XpToReach(tier);
            need = XpToReach(tier + 1) - XpToReach(tier);
        }

        public void EnsureSeason()
        {
            int season = SeasonId;
            if (_save.Data.bpSeasonId == season) return;
            _save.Data.bpSeasonId = season;
            _save.Data.battlePassXp = 0;
            _save.Data.bpPremium = false;
            _save.Data.bpClaimedFree = 0;
            _save.Data.bpClaimedPremium = 0;
            _lastTier = 0;
            _save.MarkDirty();
            EventBus.Publish(new BattlePassChangedEvent(0));
        }

        public void UnlockPremium()
        {
            EnsureSeason();
            _save.Data.bpPremium = true;
            _save.MarkDirty();
            EventBus.Publish(new BattlePassChangedEvent(Tier));
        }

        public bool CanClaimFree(int tier) => tier >= 1 && tier <= Tier && !IsSet(_save.Data.bpClaimedFree, tier);
        public bool CanClaimPremium(int tier) => Premium && tier >= 1 && tier <= Tier && !IsSet(_save.Data.bpClaimedPremium, tier);
        public bool IsFreeClaimed(int tier) => IsSet(_save.Data.bpClaimedFree, tier);
        public bool IsPremiumClaimed(int tier) => IsSet(_save.Data.bpClaimedPremium, tier);

        public List<GrantedItem> ClaimFree(int tier)
        {
            if (!CanClaimFree(tier)) return null;
            _save.Data.bpClaimedFree |= 1L << (tier - 1);
            return Granted(TierDef(tier).free, tier);
        }

        public List<GrantedItem> ClaimPremium(int tier)
        {
            if (!CanClaimPremium(tier)) return null;
            _save.Data.bpClaimedPremium |= 1L << (tier - 1);
            return Granted(TierDef(tier).premium, tier);
        }

        public List<GrantedItem> ClaimAll()
        {
            var all = new List<GrantedItem>();
            for (int t = 1; t <= Tier; t++)
            {
                var f = ClaimFree(t);
                if (f != null) all.AddRange(f);
                var p = ClaimPremium(t);
                if (p != null) all.AddRange(p);
            }
            return RewardGranter.Aggregate(all);
        }

        public bool HasClaimable()
        {
            for (int t = 1; t <= Tier; t++)
                if (CanClaimFree(t) || CanClaimPremium(t)) return true;
            return false;
        }

        private List<GrantedItem> Granted(Reward r, int tier)
        {
            _save.MarkDirty();
            var items = r == null ? new List<GrantedItem>() : _granter.Grant(r, "battle_pass_" + tier);
            EventBus.Publish(new BattlePassChangedEvent(Tier));
            return items;
        }

        private static bool IsSet(long mask, int tier) => (mask & (1L << (tier - 1))) != 0;

        private void OnCurrency(CurrencyChangedEvent e)
        {
            if (e.Type != CurrencyType.BattlePassXp || e.Delta <= 0) return;
            EnsureSeason();
            int tier = Tier;
            if (tier == _lastTier) return;
            _lastTier = tier;
            _analytics.LogEvent(AnalyticsEvents.BattlePassTier, new Dictionary<string, object> { { "tier", tier }, { "premium", _save.Data.bpPremium } });
            EventBus.Publish(new BattlePassChangedEvent(tier));
        }
    }
}
