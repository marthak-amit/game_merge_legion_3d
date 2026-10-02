using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Monetization
{
    public enum AdBlockReason { None, Disabled, DailyCap, Cooldown, NotReady }

    /// <summary>
    /// Policy layer over IAdsService: per-placement daily caps and cooldowns, interstitial pacing rules,
    /// No-Ads / VIP handling and ad analytics. Every number comes from GameConfig (Remote Config overridable).
    /// </summary>
    public sealed class AdsManager
    {
        private readonly IAdsService _ads;
        private readonly GameConfig _cfg;
        private readonly DailyService _daily;
        private readonly SaveService _save;
        private readonly ITimeService _time;
        private readonly IAnalyticsService _analytics;
        private readonly IAttributionService _attribution;

        private long _lastInterstitialTicks = long.MinValue / 2;
        private long _lastRewardedTicks = long.MinValue / 2;

        public AdsManager(IAdsService ads, GameConfig cfg, DailyService daily, SaveService save, ITimeService time,
            IAnalyticsService analytics, IAttributionService attribution)
        {
            _ads = ads;
            _cfg = cfg;
            _daily = daily;
            _save = save;
            _time = time;
            _analytics = analytics;
            _attribution = attribution;
            _ads.RevenuePaid += OnRevenue;
        }

        public bool ForcedAdsRemoved => _save.Data.noAds || IsVip;
        public bool IsVip => _save.Data.vipUntilUtcTicks > _time.UtcNow.Ticks;

        private static string DailyKey(string placement) => "ad_" + placement;

        public int Remaining(string placement)
        {
            var p = _cfg.ads.Find(placement);
            if (p == null || p.dailyCap <= 0) return int.MaxValue;
            return Math.Max(0, p.dailyCap - _daily.Get(DailyKey(placement)));
        }

        public float CooldownRemaining(string placement)
        {
            var p = _cfg.ads.Find(placement);
            if (p == null || p.cooldownSeconds <= 0f) return 0f;
            long last = LongEntries.Get(_save.Data.lastAdTicks, placement, 0);
            if (last == 0) return 0f;
            double left = p.cooldownSeconds - (_time.UtcNow.Ticks - last) / (double)TimeSpan.TicksPerSecond;
            return (float)Math.Max(0.0, left);
        }

        public AdBlockReason CanShowRewarded(string placement)
        {
            var p = _cfg.ads.Find(placement);
            if (p != null && !p.enabled) return AdBlockReason.Disabled;
            if (Remaining(placement) <= 0) return AdBlockReason.DailyCap;
            if (CooldownRemaining(placement) > 0f) return AdBlockReason.Cooldown;
            if (!_ads.IsRewardedReady(placement)) return AdBlockReason.NotReady;
            return AdBlockReason.None;
        }

        /// <summary>Call when a rewarded button becomes visible (ad_offered).</summary>
        public void LogOffered(string placement)
        {
            _analytics.LogEvent(AnalyticsEvents.AdOffered, Params(placement));
        }

        /// <summary>Shows a rewarded ad; <paramref name="onResult"/> receives true only when the reward should be granted.</summary>
        public void ShowRewarded(string placement, Action<bool> onResult)
        {
            if (CanShowRewarded(placement) != AdBlockReason.None) { onResult?.Invoke(false); return; }

            _ads.ShowRewarded(placement, result =>
            {
                bool earned = result == AdResult.Completed;
                if (result != AdResult.NotReady && result != AdResult.Failed)
                    _analytics.LogEvent(AnalyticsEvents.AdShown, Params(placement));
                if (earned)
                {
                    _lastRewardedTicks = _time.UtcNow.Ticks;
                    _daily.Increment(DailyKey(placement));
                    LongEntries.Set(_save.Data.lastAdTicks, placement, _time.UtcNow.Ticks);
                    _save.MarkDirty();
                    _analytics.LogEvent(AnalyticsEvents.AdRewarded, Params(placement));
                }
                onResult?.Invoke(earned);
            });
        }

        /// <summary>
        /// Interstitial pacing: level &gt;= min level, min gap, never soon after a rewarded ad, never during the tutorial,
        /// never with No Ads / VIP.
        /// </summary>
        public bool CanShowInterstitial(int playerLevel)
        {
            if (ForcedAdsRemoved) return false;
            if (!_save.Data.HasFlag(SaveFlags.TutorialDone)) return false;
            if (playerLevel < _cfg.ads.interstitialMinLevel) return false;
            long now = _time.UtcNow.Ticks;
            if (Seconds(now - _lastInterstitialTicks) < _cfg.ads.interstitialGapSeconds) return false;
            if (Seconds(now - _lastRewardedTicks) < _cfg.ads.interstitialAfterRewardedBlockSeconds) return false;
            return _ads.IsInterstitialReady(AdPlacements.InterstitialLevelEnd);
        }

        public bool TryShowInterstitial(int playerLevel, Action onDone)
        {
            if (!CanShowInterstitial(playerLevel)) { onDone?.Invoke(); return false; }
            _lastInterstitialTicks = _time.UtcNow.Ticks;
            _analytics.LogEvent(AnalyticsEvents.AdOffered, Params(AdPlacements.InterstitialLevelEnd));
            _ads.ShowInterstitial(AdPlacements.InterstitialLevelEnd, r =>
            {
                if (r == AdResult.Completed) _analytics.LogEvent(AnalyticsEvents.AdShown, Params(AdPlacements.InterstitialLevelEnd));
                onDone?.Invoke();
            });
            return true;
        }

        /// <summary>Banners: menu screens only, never in battle, never with No Ads / VIP.</summary>
        public void SetBannerVisible(bool visible)
        {
            if (visible && !ForcedAdsRemoved) _ads.ShowBanner(AdPlacements.BannerMenu);
            else _ads.HideBanner();
        }

        public void RefreshEntitlements() => _ads.ForcedAdsRemoved = ForcedAdsRemoved;

        private void OnRevenue(AdRevenueInfo info) => _attribution.LogAdRevenue(info);

        private static float Seconds(long ticks) => (float)(ticks / (double)TimeSpan.TicksPerSecond);

        private static Dictionary<string, object> Params(string placement) =>
            new Dictionary<string, object> { { AnalyticsParams.Placement, placement } };
    }
}
