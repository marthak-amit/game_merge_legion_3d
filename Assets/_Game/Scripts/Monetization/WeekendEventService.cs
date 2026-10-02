using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Monetization
{
    public readonly struct EventChangedEvent { }

    /// <summary>
    /// Weekend event (section 2.9): its own level set, token currency and reward track. Definition = MonetizationConfig.weekendEvent,
    /// replaceable at any time with the Remote Config JSON "weekend_event_json" (no app update).
    /// </summary>
    public sealed class WeekendEventService
    {
        private readonly SaveService _save;
        private readonly ITimeService _time;
        private readonly RewardGranter _granter;
        private readonly GameDatabase _db;
        private readonly IAnalyticsService _analytics;
        private readonly Func<int> _playerLevel;
        private EventConfig _cfg;

        public WeekendEventService(SaveService save, EventConfig fallback, IRemoteConfigService remote, ITimeService time,
            RewardGranter granter, GameDatabase db, IAnalyticsService analytics, Func<int> playerLevel)
        {
            _save = save;
            _time = time;
            _granter = granter;
            _db = db;
            _analytics = analytics;
            _playerLevel = playerLevel;
            _cfg = fallback;

            string json = remote != null ? remote.GetString(RemoteKeys.EventConfig, "") : "";
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var remoteCfg = new EventConfig();
                    UnityEngine.JsonUtility.FromJsonOverwrite(json, remoteCfg);
                    if (!string.IsNullOrEmpty(remoteCfg.id)) _cfg = remoteCfg;
                }
                catch (Exception) { /* keep the bundled definition */ }
            }
            EnsureCurrent();
        }

        public EventConfig Config => _cfg;

        // ---------------------------------------------------------------- schedule

        public void GetWindow(out DateTime startUtc, out DateTime endUtc)
        {
            if (DateTime.TryParse(_cfg.startUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var s) &&
                DateTime.TryParse(_cfg.endUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var e))
            {
                startUtc = s.ToUniversalTime();
                endUtc = e.ToUniversalTime();
                return;
            }

            // recurring weekend: Saturday 00:00 local to Monday 00:00 local of the current week
            var local = _time.LocalNow.Date;
            int sinceMonday = ((int)local.DayOfWeek + 6) % 7;      // Mon=0 .. Sun=6
            var saturday = local.AddDays(5 - sinceMonday);
            startUtc = DateTime.SpecifyKind(saturday, DateTimeKind.Local).ToUniversalTime();
            endUtc = DateTime.SpecifyKind(saturday.AddDays(2), DateTimeKind.Local).ToUniversalTime();
        }

        public bool IsActive
        {
            get
            {
                GetWindow(out var s, out var e);
                var now = _time.UtcNow;
                return now >= s && now < e;
            }
        }

        public TimeSpan TimeLeft
        {
            get
            {
                GetWindow(out _, out var e);
                var left = e - _time.UtcNow;
                return left < TimeSpan.Zero ? TimeSpan.Zero : left;
            }
        }

        public TimeSpan TimeUntilStart
        {
            get
            {
                GetWindow(out var s, out _);
                var d = s - _time.UtcNow;
                return d < TimeSpan.Zero ? TimeSpan.Zero : d;
            }
        }

        public bool Unlocked => _save.Data.highestCampaignLevel + 1 >= _cfg.minPlayerLevel;

        public bool Available => IsActive && Unlocked;

        private string CurrentEventKey
        {
            get
            {
                GetWindow(out var s, out _);
                return _cfg.id + ":" + s.Ticks;
            }
        }

        public void EnsureCurrent()
        {
            string key = CurrentEventKey;
            if (_save.Data.eventId == key) return;
            _save.Data.eventId = key;
            _save.Data.eventTokens = 0;
            _save.Data.eventLevelIndex = 0;
            _save.Data.eventClaimedMask = 0;
            _save.MarkDirty();
            EventBus.Publish(new EventChangedEvent());
        }

        // ---------------------------------------------------------------- play

        public long Tokens
        {
            get { EnsureCurrent(); return _save.Data.eventTokens; }
        }

        public int LevelIndex => _save.Data.eventLevelIndex;

        public LevelDefinition NextLevel()
        {
            EnsureCurrent();
            int index = _save.Data.eventLevelIndex;
            int player = Math.Max(1, _playerLevel());
            int seed = (int)(CurrentEventKey.GetHashCode() & 0x7fffffff) + index * 131;
            var rng = new DeterministicRng(seed);

            double curve = 70.0 * Math.Pow(1.03, player - 1);
            double budget = curve * _cfg.difficulty * (1.0 + 0.03 * index);

            var level = new LevelDefinition
            {
                id = player,
                chapter = 0,
                index = index + 1,
                theme = ProceduralLevelGenerator.Themes[rng.NextInt(ProceduralLevelGenerator.Themes.Length)],
                enemyCols = 5,
                power = (float)budget
            };

            if ((index + 1) % 10 == 0)
            {
                level.isBoss = true;
                level.hasBoss = true;
                level.boss = ProceduralLevelGenerator.BossForStrength((float)(budget * 0.5), _db, rng);
                budget *= 0.5;
            }
            int lines = player >= 20 ? 4 : player >= 8 ? 3 : 2;
            ProceduralLevelGenerator.FillFormation(level, _db, budget, rng, lines, 40);
            return level;
        }

        /// <summary>Records an event-level win: tokens and progress.</summary>
        public long OnWin(int stars)
        {
            EnsureCurrent();
            long tokens = 1 + (long)_cfg.tokensPerStar * Math.Max(1, stars);
            _save.Data.eventTokens += tokens;
            _save.Data.eventLevelIndex = Math.Min(_save.Data.eventLevelIndex + 1, Math.Max(0, _cfg.levelCount - 1));
            _save.MarkDirty();
            EventBus.Publish(new EventChangedEvent());
            return tokens;
        }

        // ---------------------------------------------------------------- reward track

        public bool IsClaimed(int index) => (_save.Data.eventClaimedMask & (1L << index)) != 0;

        public bool CanClaim(int index) => index >= 0 && index < _cfg.track.Count && !IsClaimed(index) && Tokens >= _cfg.track[index].tokens;

        public List<GrantedItem> Claim(int index)
        {
            if (!CanClaim(index)) return null;
            _save.Data.eventClaimedMask |= 1L << index;
            _save.MarkDirty();
            var items = _granter.Grant(_cfg.track[index].reward, "event_" + _cfg.id);
            EventBus.Publish(new EventChangedEvent());
            return items;
        }

        public bool HasClaimable()
        {
            for (int i = 0; i < _cfg.track.Count; i++) if (CanClaim(i)) return true;
            return false;
        }
    }

    /// <summary>What kind of battle the Battle scene should run (set by the screen that launches it).</summary>
    public enum GameMode { Campaign, Event, Arena }

    public static class GameSession
    {
        public static GameMode Mode = GameMode.Campaign;
        public static void Reset() => Mode = GameMode.Campaign;

        /// <summary>Starts a battle in the given mode (the caller then loads the Battle scene).</summary>
        public static void Begin(GameMode mode) => Mode = mode;
    }
}
