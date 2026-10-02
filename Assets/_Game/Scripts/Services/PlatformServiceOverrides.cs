using System;
using System.Collections.Generic;
using MergeLegion.Data;
using MergeLegion.Meta.Arena;
using MergeLegion.Save;

namespace MergeLegion.Services
{
    /// <summary>
    /// Extension points for real SDK implementations. Each SDK lives in its own assembly (Scripts/Sdk/*), compiled only
    /// when its scripting define is set, and fills these factories in a BeforeSceneLoad hook. When nothing is registered
    /// the Mock implementations are used, so the game always runs in the Editor without keys.
    /// </summary>
    public static class PlatformServiceOverrides
    {
        public static Func<string, IRemoteConfigService> RemoteConfig;          // arg = bundled defaults JSON
        public static Func<SaveData, IAuthService> Auth;
        public static Func<ICloudSaveService> CloudSave;
        public static Func<IAuthService, ILeaderboardService> Leaderboards;
        /// <summary>Wrap the leaderboard service (e.g. mirror scores to Google Play Games / Game Center).</summary>
        public static readonly List<Func<ILeaderboardService, ILeaderboardService>> LeaderboardDecorators = new List<Func<ILeaderboardService, ILeaderboardService>>();
        /// <summary>Extra SDK startup work (Crashlytics, attribution start...) executed after services are installed.</summary>
        public static readonly List<Action> PostInstall = new List<Action>();
        public static Func<GameDatabase, ArenaConfig, IArenaBackend> ArenaBackend;
        public static Func<IAdsService> Ads;
        public static Func<IIAPService> Iap;
        public static Func<IReceiptValidator> Receipts;
        public static Func<IPushService> Push;
        public static Func<IAttributionService> Attribution;
        public static Func<IConsentService> Consent;

        /// <summary>Analytics fan-out: Firebase, GameAnalytics, AppsFlyer ... each registers a sink.</summary>
        public static readonly List<Func<IAnalyticsService>> AnalyticsSinks = new List<Func<IAnalyticsService>>();

        public static void ResetForTests()
        {
            RemoteConfig = null; Auth = null; CloudSave = null; Leaderboards = null; ArenaBackend = null; Ads = null; Iap = null;
            Receipts = null; Push = null; Attribution = null; Consent = null;
            AnalyticsSinks.Clear();
            LeaderboardDecorators.Clear();
            PostInstall.Clear();
        }
    }

    /// <summary>Forwards every analytics call to all registered sinks (a failing SDK never breaks the others).</summary>
    public sealed class CompositeAnalyticsService : IAnalyticsService
    {
        private readonly List<IAnalyticsService> _sinks;

        public CompositeAnalyticsService(List<IAnalyticsService> sinks) { _sinks = sinks; }

        public int SinkCount => _sinks.Count;

        public void Initialize() { foreach (var s in _sinks) Safe(() => s.Initialize()); }

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
            foreach (var s in _sinks) Safe(() => s.LogEvent(name, parameters));
        }

        public void SetUserProperty(string key, string value) { foreach (var s in _sinks) Safe(() => s.SetUserProperty(key, value)); }

        public void SetUserId(string playerId) { foreach (var s in _sinks) Safe(() => s.SetUserId(playerId)); }

        private static void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }
    }
}
