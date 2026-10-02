using System;
using System.Collections.Generic;
using MergeLegion.Economy;
using MergeLegion.Save;

namespace MergeLegion.Meta
{
    /// <summary>Daily login calendar (section 2.6): 7-day cycle, day 7 is the big reward. One claim per local day.</summary>
    public sealed class LoginService
    {
        private readonly SaveService _save;
        private readonly LoginConfig _cfg;
        private readonly DailyService _daily;
        private readonly RewardGranter _granter;
        private readonly Core.ITimeService _time;

        public LoginService(SaveService save, LoginConfig cfg, DailyService daily, RewardGranter granter, Core.ITimeService time)
        {
            _save = save;
            _cfg = cfg;
            _daily = daily;
            _granter = granter;
            _time = time;
        }

        public int DayCount => _cfg.days.Count;

        /// <summary>0-based index of the day that will be claimed next.</summary>
        public int NextDayIndex
        {
            get
            {
                if (_cfg.resetOnMiss && MissedADay()) return 0;
                return DayCount == 0 ? 0 : _save.Data.loginIndex % DayCount;
            }
        }

        public bool CanClaim => DayCount > 0 && _save.Data.loginLastDayKey != _daily.DayKey;

        public IReadOnlyList<Reward> RewardsFor(int dayIndex) => _cfg.days[dayIndex % DayCount].items;

        public List<GrantedItem> Claim()
        {
            if (!CanClaim) return null;
            if (_cfg.resetOnMiss && MissedADay()) _save.Data.loginIndex = 0;
            int idx = _save.Data.loginIndex % DayCount;
            var items = _granter.Grant(_cfg.days[idx].items, "login_day_" + (idx + 1));
            _save.Data.loginIndex = idx + 1;
            _save.Data.loginLastDayKey = _daily.DayKey;
            _save.MarkDirty();
            return items;
        }

        private bool MissedADay()
        {
            int last = _save.Data.loginLastDayKey;
            if (last == 0) return false;
            int yesterday = DailyService.KeyOf(_time.LocalNow.AddDays(-1));
            return last != yesterday && last != _daily.DayKey;
        }
    }
}
