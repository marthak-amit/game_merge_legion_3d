#if MERGELEGION_MAX
using System;
using MergeLegion.Core;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Sdk
{
    /// <summary>
    /// AppLovin MAX mediation (AdMob, Unity Ads, Mintegral, Meta AN, ironSource adapters are enabled in the MAX Integration Manager).
    /// Active only when MERGELEGION_MAX is defined AND Resources/Config/sdk_keys.json has a MAX SDK key.
    /// API usage follows the MAX Unity plugin (MaxSdk / MaxSdkCallbacks); re-check names after upgrading the plugin.
    /// </summary>
    public static class MaxRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (string.IsNullOrEmpty(SdkKeys.Instance.maxSdkKey)) return;
            PlatformServiceOverrides.Ads = () => new MaxAdsService();
            PlatformServiceOverrides.Consent = () => new MaxConsentService();
        }
    }

    public sealed class MaxAdsService : IAdsService
    {
        private readonly SdkKeys _keys = SdkKeys.Instance;
        private Action<AdResult> _rewardedDone, _interstitialDone;
        private bool _rewardEarned;
        private int _rewardedRetry, _interstitialRetry;
        private bool _bannerCreated;
        private string _rewardedPlacement = "";

        public bool IsInitialized { get; private set; }
        public bool ForcedAdsRemoved { get; set; }
        public event Action<AdRevenueInfo> RevenuePaid;

        public void Initialize(Action onInitialized = null)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent += config =>
            {
                IsInitialized = true;
                HookCallbacks();
                LoadRewarded();
                LoadInterstitial();
                onInitialized?.Invoke();
            };
            MaxSdk.SetSdkKey(_keys.maxSdkKey);
            if (ServiceLocator.TryGet<IAuthService>(out var auth) && auth.PlayerId != null) MaxSdk.SetUserId(auth.PlayerId);
            MaxSdk.InitializeSdk();
        }

        private void HookCallbacks()
        {
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (id, info) => _rewardedRetry = 0;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (id, error) => Retry(ref _rewardedRetry, LoadRewarded);
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (id, error, info) => { Finish(ref _rewardedDone, AdResult.Failed); LoadRewarded(); };
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (id, reward, info) => _rewardEarned = true;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (id, info) =>
            {
                Finish(ref _rewardedDone, _rewardEarned ? AdResult.Completed : AdResult.Skipped);
                _rewardEarned = false;
                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (id, info) => Revenue(info, "rewarded");

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (id, info) => _interstitialRetry = 0;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (id, error) => Retry(ref _interstitialRetry, LoadInterstitial);
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (id, error, info) => { Finish(ref _interstitialDone, AdResult.Failed); LoadInterstitial(); };
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (id, info) => { Finish(ref _interstitialDone, AdResult.Completed); LoadInterstitial(); };
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (id, info) => Revenue(info, "interstitial");

            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (id, info) => Revenue(info, "banner");
        }

        private void Revenue(MaxSdkBase.AdInfo info, string format)
        {
            RevenuePaid?.Invoke(new AdRevenueInfo(info.NetworkName, string.IsNullOrEmpty(info.Placement) ? _rewardedPlacement : info.Placement, format, info.Revenue));
        }

        private static void Finish(ref Action<AdResult> callback, AdResult result)
        {
            var cb = callback;
            callback = null;
            cb?.Invoke(result);
        }

        // MAX recommends exponential back-off between failed loads (max 64s)
        private static void Retry(ref int attempt, Action load)
        {
            attempt = Math.Min(attempt + 1, 6);
            float delay = Mathf.Pow(2f, attempt);
            Tween.Value(0.01f, _ => { }, Ease.Linear, load, delay, null, true);
        }

        private void LoadRewarded() { if (!string.IsNullOrEmpty(_keys.MaxRewarded)) MaxSdk.LoadRewardedAd(_keys.MaxRewarded); }
        private void LoadInterstitial() { if (!string.IsNullOrEmpty(_keys.MaxInterstitial)) MaxSdk.LoadInterstitial(_keys.MaxInterstitial); }

        public bool IsRewardedReady(string placement) => IsInitialized && !string.IsNullOrEmpty(_keys.MaxRewarded) && MaxSdk.IsRewardedAdReady(_keys.MaxRewarded);

        public void ShowRewarded(string placement, Action<AdResult> onComplete)
        {
            if (!IsRewardedReady(placement)) { onComplete?.Invoke(AdResult.NotReady); return; }
            _rewardedDone = onComplete;
            _rewardEarned = false;
            _rewardedPlacement = placement;
            MaxSdk.ShowRewardedAd(_keys.MaxRewarded, placement);
        }

        public bool IsInterstitialReady(string placement) =>
            IsInitialized && !ForcedAdsRemoved && !string.IsNullOrEmpty(_keys.MaxInterstitial) && MaxSdk.IsInterstitialReady(_keys.MaxInterstitial);

        public void ShowInterstitial(string placement, Action<AdResult> onComplete)
        {
            if (!IsInterstitialReady(placement)) { onComplete?.Invoke(AdResult.NotReady); return; }
            _interstitialDone = onComplete;
            MaxSdk.ShowInterstitial(_keys.MaxInterstitial, placement);
        }

        public void ShowBanner(string placement)
        {
            if (!IsInitialized || ForcedAdsRemoved || string.IsNullOrEmpty(_keys.MaxBanner)) return;
            if (!_bannerCreated)
            {
                MaxSdk.CreateBanner(_keys.MaxBanner, MaxSdkBase.BannerPosition.BottomCenter);
                MaxSdk.SetBannerBackgroundColor(_keys.MaxBanner, new Color(0.04f, 0.05f, 0.1f));
                _bannerCreated = true;
            }
            MaxSdk.ShowBanner(_keys.MaxBanner);
        }

        public void HideBanner()
        {
            if (_bannerCreated) MaxSdk.HideBanner(_keys.MaxBanner);
        }
    }

    /// <summary>MAX consent flow (GDPR / CCPA / UMP) and iOS ATT.</summary>
    public sealed class MaxConsentService : IConsentService
    {
        public ConsentStatus Status { get; private set; } = ConsentStatus.Unknown;

        public void RequestConsent(Action<ConsentStatus> onComplete)
        {
            // The consent flow runs during MaxSdk.InitializeSdk() when enabled in the Integration Manager
            // (Terms and Privacy Policy Flow). Re-opening it for existing users lets players change their choice.
            var geography = MaxSdk.GetSdkConfiguration().ConsentFlowUserGeography;
            if (geography == MaxSdkBase.ConsentFlowUserGeography.Unknown || geography == MaxSdkBase.ConsentFlowUserGeography.Other)
            {
                Status = ConsentStatus.NotRequired;
                onComplete?.Invoke(Status);
                return;
            }
            MaxSdk.CmpService.ShowCmpForExistingUser(error =>
            {
                Status = error == null ? ConsentStatus.Granted : ConsentStatus.Denied;
                onComplete?.Invoke(Status);
            });
        }

        public void RequestTracking(Action<bool> onComplete)
        {
#if UNITY_IOS && !UNITY_EDITOR && MERGELEGION_ATT
            if (Unity.Advertisement.IosSupport.ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                Unity.Advertisement.IosSupport.ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                Unity.Advertisement.IosSupport.ATTrackingStatusBinding.RequestAuthorizationTracking();
            }
#endif
            onComplete?.Invoke(true);
        }
    }
}
#endif
