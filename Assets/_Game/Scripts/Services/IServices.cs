using System;
using System.Collections.Generic;

namespace MergeLegion.Services
{
    public interface IAnalyticsService
    {
        void Initialize();
        /// <summary>Parameters may be null. Implementations must not retain the dictionary.</summary>
        void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null);
        void SetUserProperty(string key, string value);
        void SetUserId(string playerId);
    }

    public interface IAdsService
    {
        bool IsInitialized { get; }
        /// <summary>True when the player bought No Ads: interstitials and banners are suppressed.</summary>
        bool ForcedAdsRemoved { get; set; }
        /// <summary>Fires for every impression with revenue (MAX ILRD).</summary>
        event Action<AdRevenueInfo> RevenuePaid;
        void Initialize(Action onInitialized = null);
        bool IsRewardedReady(string placement);
        void ShowRewarded(string placement, Action<AdResult> onComplete);
        bool IsInterstitialReady(string placement);
        void ShowInterstitial(string placement, Action<AdResult> onComplete);
        void ShowBanner(string placement);
        void HideBanner();
    }

    public interface IIAPService
    {
        bool IsInitialized { get; }
        event Action<PurchaseResult> PurchaseCompleted;
        void Initialize(IReadOnlyList<string> skus, Action onInitialized = null);
        ProductInfo GetProduct(string sku);
        bool IsOwned(string sku);
        void Purchase(string sku, Action<PurchaseResult> onComplete);
        void RestorePurchases(Action<bool> onComplete);
    }

    /// <summary>Optional: lets the store adapter learn each SKU's product type from the shop config.</summary>
    public interface IKindAware
    {
        Func<string, ProductKind> KindLookup { get; set; }
    }

    /// <summary>Optional: stores that keep a purchase pending until the game confirms delivery (Unity IAP pending purchases).</summary>
    public interface IConfirmingIapService
    {
        void ConfirmPurchase(string sku);
    }

    public interface IRemoteConfigService
    {
        bool IsReady { get; }
        /// <summary>Loads defaults, then fetches remote overrides. Calls back once, even on failure.</summary>
        void Fetch(Action<bool> onComplete);
        int GetInt(string key, int fallback = 0);
        long GetLong(string key, long fallback = 0);
        float GetFloat(string key, float fallback = 0f);
        bool GetBool(string key, bool fallback = false);
        string GetString(string key, string fallback = "");
    }

    public interface IAuthService
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        string DisplayName { get; set; }
        void SignInAnonymously(Action<bool> onComplete);
        void DeleteAccount(Action<bool> onComplete);
    }

    public interface ICloudSaveService
    {
        void Save(string json, Action<bool> onComplete);
        void Load(Action<CloudLoadResult> onComplete);
        void Delete(Action<bool> onComplete);
    }

    public interface ILeaderboardService
    {
        void SubmitScore(string boardId, long score, Action<bool> onComplete);
        void GetTop(string boardId, int count, Action<IReadOnlyList<LeaderboardEntry>> onComplete);
        void GetPlayerEntry(string boardId, Action<LeaderboardEntry> onComplete);
    }

    public interface IPushService
    {
        void RequestPermission(Action<bool> onComplete);
        void ScheduleLocal(string id, string title, string body, DateTime fireAtUtc);
        void CancelLocal(string id);
        void CancelAllLocal();
        string GetPushToken();
    }

    public interface IAttributionService
    {
        void Initialize();
        void LogPurchase(string sku, decimal price, string isoCurrency);
        void LogAdRevenue(AdRevenueInfo info);
        void LogEvent(string name, IReadOnlyDictionary<string, string> parameters = null);
    }

    public interface IConsentService
    {
        ConsentStatus Status { get; }
        /// <summary>GDPR/CCPA flow (MAX consent flow on device). Mock grants immediately.</summary>
        void RequestConsent(Action<ConsentStatus> onComplete);
        /// <summary>iOS App Tracking Transparency; no-op elsewhere. Must be called after the tutorial.</summary>
        void RequestTracking(Action<bool> onComplete);
    }
}
