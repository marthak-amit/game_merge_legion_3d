using System;
using System.Collections.Generic;
using UnityEngine;
using BlockBloom.Core;
using BlockBloom.Services;

namespace BlockBloom
{
    /// <summary>Ads / IAP / analytics policy in one place. Rewarded ads are always optional and always paid out; interstitials are rationed.</summary>
    public static class Monet
    {
        private static float _lastInterstitial = -999f;
        private static int _ends;

        public static IAdsService Ads { get { IAdsService a; ServiceLocator.TryGet(out a); return a; } }
        public static IIAPService Iap { get { IIAPService a; ServiceLocator.TryGet(out a); return a; } }
        public static IAnalyticsService Analytics { get { IAnalyticsService a; ServiceLocator.TryGet(out a); return a; } }

        public static void Log(string ev, params object[] kv)
        {
            var a = Analytics; if (a == null) return;
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i].ToString()] = kv[i + 1];
            a.LogEvent(ev, d);
        }

        /// <summary>Localized store price when available, otherwise the USD price point.</summary>
        public static string PriceText(Economy.Product p)
        {
            var iap = Iap;
            var info = iap != null ? iap.GetProduct(p.Sku) : null;
            if (info != null && !string.IsNullOrEmpty(info.LocalizedPrice)) return info.LocalizedPrice;
            return "$" + p.PriceUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool RewardedReady(string placement)
        {
            var a = Ads; return a != null && a.IsRewardedReady(placement);
        }

        /// <summary>Shows a rewarded video; grants the reward only on completion.</summary>
        public static void Rewarded(string placement, Action onReward)
        {
            var a = Ads;
            if (a == null || !a.IsRewardedReady(placement)) { App.I.Toast("No video available right now"); return; }
            Log("ad_rewarded_request", "placement", placement);
            a.ShowRewarded(placement, res =>
            {
                Log("ad_rewarded_result", "placement", placement, "result", res.ToString());
                if (res == AdResult.Completed) { if (onReward != null) onReward(); }
                else App.I.Toast("Watch the full video to get the reward");
            });
        }

        /// <summary>Called when a level/game ends. Shows an interstitial only if the player has settled in and enough time has passed.</summary>
        public static void MaybeInterstitial(Action next)
        {
            var d = Save.Data; var a = Ads;
            _ends++;
            bool allowed = a != null && !d.adsRemoved && !d.isUnder13 && d.unlockedLevel > Economy.FreeLevelsBeforeAds
                           && _ends % Economy.InterstitialEveryNthEnd == 0
                           && Time.realtimeSinceStartup - _lastInterstitial > Economy.InterstitialMinSeconds
                           && a.IsInterstitialReady("level_end");
            if (!allowed) { if (next != null) next(); return; }
            _lastInterstitial = Time.realtimeSinceStartup;
            a.ShowInterstitial("level_end", r => { Log("ad_interstitial", "result", r.ToString()); if (next != null) next(); });
        }

        public static void Buy(Economy.Product p, Action onDone)
        {
            var iap = Iap;
            if (iap == null) return;
            if (Save.Data.isUnder13) { App.I.Toast("Ask a parent to make purchases"); return; }
            Log("iap_start", "sku", p.Sku);
            iap.Purchase(p.Sku, res =>
            {
                if (!res.Success) { App.I.Toast("Purchase cancelled"); return; }
                Grant(p);
                Log("iap_success", "sku", p.Sku, "price_usd", (double)p.PriceUsd);
                if (onDone != null) onDone();
            });
        }

        private static void Grant(Economy.Product p)
        {
            var d = Save.Data;
            switch (p.Kind)
            {
                case Economy.ProductKindEx.Coins: Economy.AddCoins(p.Coins); break;
                case Economy.ProductKindEx.RemoveAds:
                    d.adsRemoved = true; var a = Ads; if (a != null) { a.ForcedAdsRemoved = true; a.HideBanner(); }
                    Save.Commit(); break;
                case Economy.ProductKindEx.Starter:
                    if (d.starterBought) return;
                    d.starterBought = true; d.coins += p.Coins; d.boosterBomb += 3; d.boosterUndo += 3; d.boosterShuffle += 3; d.hearts = Economy.MaxHearts;
                    Save.Commit(); break;
            }
            Sfx.Coin();
        }
    }
}
