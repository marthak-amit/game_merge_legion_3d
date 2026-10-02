using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Levels;
using MergeLegion.Save;

namespace MergeLegion.Services
{
    public readonly struct SaveReplacedEvent { }

    public enum SyncOutcome { UploadedLocal, AdoptedCloud, NothingToDo, Offline, Failed }

    /// <summary>
    /// Local &lt;-&gt; cloud save sync: higher progress wins, and when both sides have progress the player is asked
    /// (via <see cref="ConflictPrompt"/>). Never uploads a save that failed the tamper check.
    /// </summary>
    public sealed class CloudSyncService : IDisposable
    {
        private readonly SaveService _save;
        private readonly ICloudSaveService _cloud;
        private readonly IAuthService _auth;
        private readonly ITimeService _time;
        private readonly IAnalyticsService _analytics;
        private readonly float _minUploadSeconds;
        private long _lastUploadTicks;
        private bool _busy;

        /// <summary>UI hook: show "cloud vs this device" and call back with the player's pick. Null = take the higher-progress save.</summary>
        public Action<SaveData, SaveData, Action<MergeChoice>> ConflictPrompt;

        /// <summary>Registered by the UI layer at startup so Services does not depend on UI.</summary>
        public static Action<SaveData, SaveData, Action<MergeChoice>> DefaultConflictPrompt;

        public CloudSyncService(SaveService save, ICloudSaveService cloud, IAuthService auth, ITimeService time,
            IAnalyticsService analytics, float minUploadSeconds = 60f)
        {
            _save = save;
            _cloud = cloud;
            _auth = auth;
            _time = time;
            _analytics = analytics;
            _minUploadSeconds = minUploadSeconds;
            ConflictPrompt = DefaultConflictPrompt;
            EventBus.Subscribe<LevelCompletedEvent>(OnLevel);
            EventBus.Subscribe<AppPauseEvent>(OnPause);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<LevelCompletedEvent>(OnLevel);
            EventBus.Unsubscribe<AppPauseEvent>(OnPause);
        }

        private void OnLevel(LevelCompletedEvent e)
        {
            if (e.Won) Upload(false, null);
        }

        private void OnPause(AppPauseEvent e)
        {
            if (e.Paused) Upload(true, null);
        }

        public void SyncOnBoot(Action<SyncOutcome> done)
        {
            if (!_auth.IsSignedIn) { done?.Invoke(SyncOutcome.Offline); return; }
            _cloud.Load(result =>
            {
                if (result.Status == CloudLoadStatus.Failed) { done?.Invoke(SyncOutcome.Offline); return; }
                if (result.Status == CloudLoadStatus.NotFound) { UploadNow(o => done?.Invoke(o ? SyncOutcome.UploadedLocal : SyncOutcome.Failed)); return; }

                var decoded = _save.Import(result.Json);
                if (!decoded.HasData) { UploadNow(o => done?.Invoke(o ? SyncOutcome.UploadedLocal : SyncOutcome.Failed)); return; }
                if (decoded.Status == DecodeStatus.Tampered) { done?.Invoke(SyncOutcome.NothingToDo); return; }

                var decision = SaveMerger.Resolve(_save.Data, decoded.Data);
                if (decision.PromptRequired && ConflictPrompt != null)
                    ConflictPrompt(_save.Data, decoded.Data, choice => Apply(choice, decoded.Data, done));
                else Apply(decision.Choice, decoded.Data, done);
            });
        }

        private void Apply(MergeChoice choice, SaveData cloudData, Action<SyncOutcome> done)
        {
            if (choice == MergeChoice.UseCloud)
            {
                _save.Replace(cloudData);
                EventBus.Publish(new SaveReplacedEvent());
                _analytics.LogEvent("cloud_adopted");
                done?.Invoke(SyncOutcome.AdoptedCloud);
            }
            else UploadNow(ok => done?.Invoke(ok ? SyncOutcome.UploadedLocal : SyncOutcome.Failed));
        }

        public void Upload(bool force, Action<bool> done)
        {
            if (!_auth.IsSignedIn || _busy || _save.Data.tamperDetected) { done?.Invoke(false); return; }
            long now = _time.UtcNow.Ticks;
            if (!force && (now - _lastUploadTicks) / (double)TimeSpan.TicksPerSecond < _minUploadSeconds) { done?.Invoke(false); return; }
            UploadNow(done);
        }

        private void UploadNow(Action<bool> done)
        {
            if (_save.Data.tamperDetected) { done?.Invoke(false); return; }
            _busy = true;
            _save.Flush();
            _cloud.Save(_save.ExportJson(), ok =>
            {
                _busy = false;
                if (ok) _lastUploadTicks = _time.UtcNow.Ticks;
                done?.Invoke(ok);
            });
        }

        /// <summary>Settings > Delete account &amp; data: removes the cloud copy, the account and local progress.</summary>
        public void DeleteEverything(Action<bool> done)
        {
            _cloud.Delete(cloudOk =>
                _auth.DeleteAccount(authOk =>
                {
                    _save.DeleteAll();
                    EventBus.Publish(new SaveReplacedEvent());
                    done?.Invoke(cloudOk && authOk);
                }));
        }
    }
}
