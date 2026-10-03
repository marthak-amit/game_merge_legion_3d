#if BLOCKBLOOM_FIREBASE
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.Messaging;
using Firebase.RemoteConfig;
using BlockBloom.Core;
using BlockBloom.Services;
using BlockBloom.Services.Mock;
using UnityEngine;

namespace BlockBloom.Sdk
{
    /// <summary>
    /// Firebase: Analytics (sink), Remote Config, Crashlytics and Messaging (push token).
    /// Needs google-services.json / GoogleService-Info.plist in Assets/. Written against Firebase Unity SDK 12.x;
    /// re-check names after upgrading.
    /// </summary>
    public static class FirebaseRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformServiceOverrides.RemoteConfig = defaults => new FirebaseRemoteConfigService(defaults);
            PlatformServiceOverrides.AnalyticsSinks.Add(() => new FirebaseAnalyticsSink());
            PlatformServiceOverrides.PostInstall.Add(FirebaseCore.StartCrashlyticsAndMessaging);
        }
    }

    internal static class FirebaseCore
    {
        public static DependencyStatus Status = DependencyStatus.UnavailableOther;
        public static bool Ready => Status == DependencyStatus.Available;
        private static bool _checking;
        private static readonly List<Action> Waiting = new List<Action>();

        public static void WhenReady(Action action)
        {
            if (Ready) { action(); return; }
            Waiting.Add(action);
            if (_checking) return;
            _checking = true;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                Status = task.Result;
                if (Ready)
                {
                    foreach (var a in Waiting) a();
                }
                else Debug.LogWarning("[Firebase] dependencies unavailable: " + Status);
                Waiting.Clear();
            });
        }

        public static void StartCrashlyticsAndMessaging()
        {
            WhenReady(() =>
            {
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                if (ServiceLocator.TryGet<IAuthService>(out var auth) && auth.PlayerId != null) Crashlytics.SetUserId(auth.PlayerId);
                FirebaseMessaging.TokenReceived += (s, e) => PushTokenHolder.Token = e.Token;
                FirebaseMessaging.MessageReceived += (s, e) => Debug.Log("[Firebase] message received");
            });
        }
    }

    public static class PushTokenHolder
    {
        public static string Token = "";
    }

    public sealed class FirebaseAnalyticsSink : IAnalyticsService
    {
        public void Initialize() => FirebaseCore.WhenReady(() => FirebaseAnalytics.SetAnalyticsCollectionEnabled(true));

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!FirebaseCore.Ready) return;
            if (parameters == null || parameters.Count == 0) { FirebaseAnalytics.LogEvent(name); return; }
            var list = new List<Parameter>(parameters.Count);
            foreach (var kv in parameters)
            {
                switch (kv.Value)
                {
                    case int i: list.Add(new Parameter(kv.Key, i)); break;
                    case long l: list.Add(new Parameter(kv.Key, l)); break;
                    case float f: list.Add(new Parameter(kv.Key, f)); break;
                    case double d: list.Add(new Parameter(kv.Key, d)); break;
                    case decimal m: list.Add(new Parameter(kv.Key, (double)m)); break;
                    case bool b: list.Add(new Parameter(kv.Key, b ? 1 : 0)); break;
                    default: list.Add(new Parameter(kv.Key, Convert.ToString(kv.Value))); break;
                }
            }
            FirebaseAnalytics.LogEvent(name, list.ToArray());
        }

        public void SetUserProperty(string key, string value)
        {
            if (FirebaseCore.Ready) FirebaseAnalytics.SetUserProperty(key, value);
        }

        public void SetUserId(string playerId)
        {
            if (FirebaseCore.Ready) FirebaseAnalytics.SetUserId(playerId);
        }
    }

    /// <summary>Remote Config: bundled defaults first (so the game works offline), then fetch + activate.</summary>
    public sealed class FirebaseRemoteConfigService : IRemoteConfigService
    {
        private readonly MockRemoteConfigService _defaults;

        public FirebaseRemoteConfigService(string defaultsJson) { _defaults = new MockRemoteConfigService(defaultsJson); }

        public bool IsReady { get; private set; }

        public void Fetch(Action<bool> onComplete)
        {
            _defaults.Fetch(null);
            FirebaseCore.WhenReady(() =>
            {
                var rc = FirebaseRemoteConfig.DefaultInstance;
                rc.FetchAsync(TimeSpan.FromHours(1)).ContinueWithOnMainThread(fetch =>
                {
                    if (fetch.IsFaulted) { IsReady = true; onComplete?.Invoke(false); return; }
                    rc.ActivateAsync().ContinueWithOnMainThread(_ => { IsReady = true; onComplete?.Invoke(true); });
                });
            });
            // never hold up the boot if Firebase cannot initialise
            Tween.Value(3f, _ => { }, Ease.Linear, () => { if (!IsReady) { IsReady = true; onComplete?.Invoke(false); } }, 0f, null, true);
        }

        private bool TryRemote(string key, out string value)
        {
            value = null;
            if (!FirebaseCore.Ready) return false;
            var v = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            if (v.Source == ValueSource.StaticValue) return false;
            value = v.StringValue;
            return true;
        }

        public int GetInt(string key, int fallback = 0) => TryRemote(key, out var s) && int.TryParse(s, out var v) ? v : _defaults.GetInt(key, fallback);
        public long GetLong(string key, long fallback = 0) => TryRemote(key, out var s) && long.TryParse(s, out var v) ? v : _defaults.GetLong(key, fallback);
        public float GetFloat(string key, float fallback = 0f) =>
            TryRemote(key, out var s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : _defaults.GetFloat(key, fallback);
        public bool GetBool(string key, bool fallback = false) => TryRemote(key, out var s) && bool.TryParse(s, out var v) ? v : _defaults.GetBool(key, fallback);
        public string GetString(string key, string fallback = "") => TryRemote(key, out var s) ? s : _defaults.GetString(key, fallback);
    }
}
#endif
