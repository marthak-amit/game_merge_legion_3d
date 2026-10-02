using System;
using UnityEngine;

namespace MergeLegion.Services.Mock
{
    /// <summary>Always-ready ads that complete immediately and report fake revenue so downstream wiring can be tested.</summary>
    public sealed class MockAdsService : IAdsService
    {
        public bool IsInitialized { get; private set; }
        public bool ForcedAdsRemoved { get; set; }
        public event Action<AdRevenueInfo> RevenuePaid;

        /// <summary>Test hook: next show returns this result.</summary>
        public AdResult NextResult = AdResult.Completed;
        public bool Ready = true;
        public bool BannerVisible { get; private set; }

        public void Initialize(Action onInitialized = null)
        {
            IsInitialized = true;
            onInitialized?.Invoke();
        }

        public bool IsRewardedReady(string placement) => IsInitialized && Ready;
        public bool IsInterstitialReady(string placement) => IsInitialized && Ready && !ForcedAdsRemoved;

        public void ShowRewarded(string placement, Action<AdResult> onComplete)
        {
            Show(placement, "rewarded", 0.02, onComplete);
        }

        public void ShowInterstitial(string placement, Action<AdResult> onComplete)
        {
            if (ForcedAdsRemoved) { onComplete?.Invoke(AdResult.NotReady); return; }
            Show(placement, "interstitial", 0.01, onComplete);
        }

        public void ShowBanner(string placement)
        {
            if (ForcedAdsRemoved) return;
            BannerVisible = true;
        }

        public void HideBanner() => BannerVisible = false;

        private void Show(string placement, string format, double revenue, Action<AdResult> onComplete)
        {
            if (!IsInitialized || !Ready) { onComplete?.Invoke(AdResult.NotReady); return; }
            Debug.Log($"[Ads] mock {format} shown: {placement} -> {NextResult}");
            var result = NextResult;
            if (result == AdResult.Completed) RevenuePaid?.Invoke(new AdRevenueInfo("mock", placement, format, revenue));
            onComplete?.Invoke(result);
        }
    }
}
