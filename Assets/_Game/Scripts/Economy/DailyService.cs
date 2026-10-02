using System;
using MergeLegion.Core;
using MergeLegion.Save;

namespace MergeLegion.Economy
{
    public readonly struct DayChangedEvent
    {
        public readonly bool NewWeek;
        public DayChangedEvent(bool newWeek) { NewWeek = newWeek; }
    }

    /// <summary>Counters that reset at local midnight (daily) and Monday (weekly). Backs ad caps, missions, spin, chests.</summary>
    public sealed class DailyService
    {
        private readonly SaveService _save;
        private readonly ITimeService _time;

        public DailyService(SaveService save, ITimeService time)
        {
            _save = save;
            _time = time;
            Refresh();
        }

        public int DayKey => KeyOf(_time.LocalNow);

        public int WeekKey
        {
            get
            {
                var d = _time.LocalNow.Date;
                int back = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
                return KeyOf(d.AddDays(-back));
            }
        }

        public static int KeyOf(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;

        /// <summary>Time until the next local midnight.</summary>
        public TimeSpan UntilReset() => _time.LocalNow.Date.AddDays(1) - _time.LocalNow;

        /// <summary>Resets counters if the local day/week rolled over. Returns true when something reset.</summary>
        public bool Refresh()
        {
            var d = _save.Data.daily;
            int day = DayKey, week = WeekKey;
            bool newDay = d.dayKey != day;
            bool newWeek = d.weekKey != week;
            if (!newDay && !newWeek) return false;

            if (newDay) { d.counts.Clear(); d.dayKey = day; }
            if (newWeek) { d.weeklyCounts.Clear(); d.weekKey = week; }
            _save.MarkDirty();
            EventBus.Publish(new DayChangedEvent(newWeek));
            return true;
        }

        public int Get(string key)
        {
            Refresh();
            return IntEntries.Get(_save.Data.daily.counts, key);
        }

        public int Increment(string key, int by = 1)
        {
            Refresh();
            _save.MarkDirty();
            return IntEntries.Add(_save.Data.daily.counts, key, by);
        }

        public int GetWeekly(string key)
        {
            Refresh();
            return IntEntries.Get(_save.Data.daily.weeklyCounts, key);
        }

        public int IncrementWeekly(string key, int by = 1)
        {
            Refresh();
            _save.MarkDirty();
            return IntEntries.Add(_save.Data.daily.weeklyCounts, key, by);
        }
    }
}
