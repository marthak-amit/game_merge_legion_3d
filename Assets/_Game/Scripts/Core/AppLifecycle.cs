using System.Collections.Generic;
using MergeLegion.Save;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Core
{
    /// <summary>Translates Unity app callbacks into EventBus events and tracks session length.</summary>
    public sealed class AppLifecycle : MonoBehaviour
    {
        private float _sessionStart;
        private bool _sessionOpen;

        public void BeginSession()
        {
            _sessionStart = Time.realtimeSinceStartup;
            _sessionOpen = true;
            var save = ServiceLocator.Get<SaveService>();
            save.Data.sessionCount++;
            save.MarkDirty();
            if (ServiceLocator.TryGet<Monetization.OfferService>(out var offers))
                offers.OnSessionStart(ServiceLocator.Get<Economy.CurrencyService>().Get(Economy.CurrencyType.Gems));
            ServiceLocator.Get<IAnalyticsService>().LogEvent(AnalyticsEvents.SessionStart,
                new Dictionary<string, object> { { "session_index", save.Data.sessionCount } });
        }

        private void EndSession()
        {
            if (!_sessionOpen) return;
            _sessionOpen = false;
            int seconds = Mathf.RoundToInt(Time.realtimeSinceStartup - _sessionStart);
            var save = ServiceLocator.Get<SaveService>();
            save.Data.totalPlaytimeSeconds += seconds;
            if (ServiceLocator.TryGet<Monetization.OfferService>(out var offers)) offers.TouchLastSeen();
            save.MarkDirty();
            ServiceLocator.Get<IAnalyticsService>().LogEvent(AnalyticsEvents.SessionEnd,
                new Dictionary<string, object> { { AnalyticsParams.DurationSec, seconds } });
        }

        private void OnApplicationPause(bool paused)
        {
            if (!ServiceLocator.Has<SaveService>()) return;
            if (paused) EndSession();
            else if (!_sessionOpen) BeginSession();
            if (ServiceLocator.TryGet<Meta.PushScheduler>(out var push))
            {
                if (paused) push.Reschedule(); else push.CancelAll();
            }
            EventBus.Publish(new AppPauseEvent(paused));
        }

        private void OnApplicationQuit()
        {
            if (!ServiceLocator.Has<SaveService>()) return;
            EndSession();
            ServiceLocator.Get<SaveService>().Flush();
        }

        private void OnEnable() => Application.lowMemory += OnLowMemory;
        private void OnDisable() => Application.lowMemory -= OnLowMemory;

        private static void OnLowMemory()
        {
            EventBus.Publish(new LowMemoryEvent());
            Resources.UnloadUnusedAssets();
        }
    }
}
