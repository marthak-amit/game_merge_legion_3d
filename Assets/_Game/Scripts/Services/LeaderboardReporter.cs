using System;
using MergeLegion.Core;
using MergeLegion.Levels;
using MergeLegion.Meta.Arena;
using MergeLegion.Save;

namespace MergeLegion.Services
{
    /// <summary>Submits campaign / endless progress to the leaderboards whenever a level is won.</summary>
    public sealed class LeaderboardReporter : IDisposable
    {
        private readonly SaveService _save;
        private readonly ILeaderboardService _boards;

        public LeaderboardReporter(SaveService save, ILeaderboardService boards)
        {
            _save = save;
            _boards = boards;
            EventBus.Subscribe<LevelCompletedEvent>(OnLevel);
        }

        public void Dispose() => EventBus.Unsubscribe<LevelCompletedEvent>(OnLevel);

        private void OnLevel(LevelCompletedEvent e)
        {
            if (!e.Won || _save.Data.tamperDetected) return;
            if (e.Endless) _boards.SubmitScore(LeaderboardIds.EndlessWave, _save.Data.endlessBestWave, null);
            else _boards.SubmitScore(LeaderboardIds.CampaignLevel, _save.Data.highestCampaignLevel, null);
        }
    }
}
