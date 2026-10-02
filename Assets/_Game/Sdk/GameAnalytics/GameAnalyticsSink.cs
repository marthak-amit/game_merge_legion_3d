#if MERGELEGION_GA
using System;
using System.Collections.Generic;
using GameAnalyticsSDK;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Sdk
{
    /// <summary>
    /// GameAnalytics sink: maps the shared event names onto GA progression, resource and design events.
    /// Game / secret keys live in the GameAnalytics settings asset (Window > GameAnalytics > Select Settings).
    /// </summary>
    public static class GameAnalyticsRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => PlatformServiceOverrides.AnalyticsSinks.Add(() => new GameAnalyticsSink());
    }

    public sealed class GameAnalyticsSink : IAnalyticsService
    {
        private static readonly string[] Currencies = { "coins", "gems", "chestkeys", "battlepassxp" };

        public void Initialize()
        {
            GameAnalytics.SetEnabledInfoLog(false);
            GameAnalytics.Initialize();
            foreach (var c in Currencies) { /* resource currencies/item types must be declared in the GA settings asset */ }
        }

        public void SetUserProperty(string key, string value) { }
        public void SetUserId(string playerId) => GameAnalytics.SetCustomId(playerId);

        public void LogEvent(string name, IReadOnlyDictionary<string, object> p = null)
        {
            switch (name)
            {
                case AnalyticsEvents.LevelStart: Progression(GAProgressionStatus.Start, p, null); return;
                case AnalyticsEvents.LevelComplete: Progression(GAProgressionStatus.Complete, p, Score(p)); return;
                case AnalyticsEvents.LevelFail: Progression(GAProgressionStatus.Fail, p, null); return;
                case AnalyticsEvents.CurrencyEarn: Resource(GAResourceFlowType.Source, p, AnalyticsParams.Source); return;
                case AnalyticsEvents.CurrencySpend: Resource(GAResourceFlowType.Sink, p, AnalyticsParams.Sink); return;
                case AnalyticsEvents.IapPurchase: return; // business events are sent with the receipt by the attribution/IAP layer
            }
            GameAnalytics.NewDesignEvent(Design(name, p));
        }

        private static int? Score(IReadOnlyDictionary<string, object> p)
        {
            return p != null && p.TryGetValue(AnalyticsParams.Stars, out var s) ? Convert.ToInt32(s) : (int?)null;
        }

        private static void Progression(GAProgressionStatus status, IReadOnlyDictionary<string, object> p, int? score)
        {
            int level = p != null && p.TryGetValue(AnalyticsParams.Level, out var l) ? Convert.ToInt32(l) : 0;
            string world = "world" + (level > 200 ? 11 : (Mathf.Max(1, level) - 1) / 20 + 1).ToString("00");
            string lv = "level" + level.ToString("000");
            if (score.HasValue) GameAnalytics.NewProgressionEvent(status, world, lv, score.Value);
            else GameAnalytics.NewProgressionEvent(status, world, lv);
        }

        private static void Resource(GAResourceFlowType flow, IReadOnlyDictionary<string, object> p, string reasonKey)
        {
            if (p == null) return;
            string currency = Convert.ToString(p[AnalyticsParams.Currency]);
            float amount = Convert.ToSingle(p[AnalyticsParams.Amount]);
            string reason = p.TryGetValue(reasonKey, out var r) ? Convert.ToString(r) : "other";
            GameAnalytics.NewResourceEvent(flow, currency, amount, "economy", reason);
        }

        private static string Design(string name, IReadOnlyDictionary<string, object> p)
        {
            if (p == null) return name;
            foreach (var key in new[] { AnalyticsParams.Placement, AnalyticsParams.Line, AnalyticsParams.Sku, AnalyticsParams.Step, "chest", "mission" })
                if (p.TryGetValue(key, out var v)) return name + ":" + Convert.ToString(v);
            return name;
        }
    }
}
#endif
