#if BLOCKBLOOM_APPSFLYER
using System;
using System.Collections.Generic;
using System.Globalization;
using AppsFlyerSDK;
using BlockBloom.Services;
using UnityEngine;

namespace BlockBloom.Sdk
{
    /// <summary>
    /// AppsFlyer attribution: install attribution, purchase events and ad revenue (ILRD from MAX).
    /// Dev key / iOS app id come from Resources/Config/sdk_keys.json. Written against AppsFlyer Unity plugin 6.x.
    /// </summary>
    public static class AppsFlyerRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (string.IsNullOrEmpty(SdkKeys.Instance.appsFlyerDevKey)) return;
            PlatformServiceOverrides.Attribution = () => new AppsFlyerAttribution();
        }
    }

    public sealed class AppsFlyerAttribution : IAttributionService
    {
        private bool _started;

        public void Initialize()
        {
            var keys = SdkKeys.Instance;
            AppsFlyer.setIsDebug(Debug.isDebugBuild);
            AppsFlyer.initSDK(keys.appsFlyerDevKey, keys.appsFlyerAppIdIos);
#if UNITY_IOS && !UNITY_EDITOR
            // wait up to 60s for the ATT answer so the install is attributed with the user's choice
            AppsFlyeriOS.waitForATTUserAuthorizationWithTimeoutInterval(60);
#endif
            AppsFlyer.startSDK();
            _started = true;
        }

        public void LogPurchase(string sku, decimal price, string isoCurrency)
        {
            if (!_started) return;
            AppsFlyer.sendEvent(AFInAppEvents.PURCHASE, new Dictionary<string, string>
            {
                { AFInAppEvents.REVENUE, price.ToString(CultureInfo.InvariantCulture) },
                { AFInAppEvents.CURRENCY, isoCurrency },
                { AFInAppEvents.CONTENT_ID, sku },
                { AFInAppEvents.QUANTITY, "1" }
            });
        }

        public void LogAdRevenue(AdRevenueInfo info)
        {
            if (!_started) return;
#if BLOCKBLOOM_APPSFLYER_ADREVENUE
            var data = new AFAdRevenueData(info.Network, AppsFlyerAdRevenueMediationNetworkType.AppsFlyerAdRevenueMediationNetworkTypeApplovinMax, "USD", info.RevenueUsd);
            AppsFlyerAdRevenue.logAdRevenue(data, new Dictionary<string, string> { { "ad_format", info.Format }, { "placement", info.Placement } });
#else
            AppsFlyer.sendEvent("af_ad_revenue", new Dictionary<string, string>
            {
                { "af_revenue", info.RevenueUsd.ToString("0.000000", CultureInfo.InvariantCulture) },
                { "af_currency", "USD" },
                { "af_ad_network", info.Network ?? "" },
                { "af_ad_format", info.Format ?? "" }
            });
#endif
        }

        public void LogEvent(string name, IReadOnlyDictionary<string, string> parameters = null)
        {
            if (!_started) return;
            var dict = new Dictionary<string, string>();
            if (parameters != null) foreach (var kv in parameters) dict[kv.Key] = kv.Value;
            AppsFlyer.sendEvent(name, dict);
        }
    }
}
#endif
