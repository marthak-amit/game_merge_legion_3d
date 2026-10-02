using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Levels
{
    public readonly struct LevelCompletedEvent
    {
        public readonly int Level;
        public readonly bool Won;
        public readonly int Stars;
        public readonly bool IsBoss;
        public readonly bool Endless;
        public readonly float Duration;

        public LevelCompletedEvent(int level, bool won, int stars, bool isBoss, bool endless, float duration)
        {
            Level = level; Won = won; Stars = stars; IsBoss = isBoss; Endless = endless; Duration = duration;
        }
    }

    public readonly struct LevelStartedEvent
    {
        public readonly int Level;
        public LevelStartedEvent(int level) { Level = level; }
    }

    /// <summary>Campaign + endless progress, streaks, stars and level analytics.</summary>
    public sealed class CampaignService
    {
        private readonly SaveService _save;
        private readonly IAnalyticsService _analytics;

        public CampaignService(SaveService save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public int CurrentLevel => _save.Data.highestCampaignLevel + 1;

        public int BestStars(int level) => IntEntries.Get(_save.Data.levelStars, level.ToString());

        public int TotalStars()
        {
            int sum = 0;
            foreach (var e in _save.Data.levelStars) sum += e.value;
            return sum;
        }

        public void OnStart(int level, float armyPower)
        {
            _analytics.LogEvent(AnalyticsEvents.LevelStart, new Dictionary<string, object>
            {
                { AnalyticsParams.Level, level },
                { AnalyticsParams.ArmyPower, (int)armyPower }
            });
            EventBus.Publish(new LevelStartedEvent(level));
        }

        public void OnWin(int level, int stars, bool isBoss, float duration, float armyPower)
        {
            var d = _save.Data;
            bool endless = level > LevelRepository.CampaignLevels;
            if (level > d.highestCampaignLevel) d.highestCampaignLevel = level;
            if (endless) d.endlessBestWave = System.Math.Max(d.endlessBestWave, level - LevelRepository.CampaignLevels);
            if (stars > BestStars(level)) IntEntries.Set(d.levelStars, level.ToString(), stars);
            d.winStreak++;
            d.loseStreak = 0;
            d.totalWins++;
            _save.MarkDirty();

            _analytics.LogEvent(AnalyticsEvents.LevelComplete, new Dictionary<string, object>
            {
                { AnalyticsParams.Level, level },
                { AnalyticsParams.Stars, stars },
                { AnalyticsParams.DurationSec, (int)duration },
                { AnalyticsParams.ArmyPower, (int)armyPower }
            });
            EventBus.Publish(new LevelCompletedEvent(level, true, stars, isBoss, endless, duration));
        }

        public void OnLoss(int level, bool isBoss, float duration)
        {
            var d = _save.Data;
            d.loseStreak++;
            d.winStreak = 0;
            d.totalLosses++;
            _save.MarkDirty();
            _analytics.LogEvent(AnalyticsEvents.LevelFail, new Dictionary<string, object>
            {
                { AnalyticsParams.Level, level },
                { AnalyticsParams.DurationSec, (int)duration }
            });
            EventBus.Publish(new LevelCompletedEvent(level, false, 0, isBoss, level > LevelRepository.CampaignLevels, duration));
        }
    }
}
