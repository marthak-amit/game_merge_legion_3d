using System;
using MergeLegion.Core;
using MergeLegion.Monetization;
using MergeLegion.Services;

namespace MergeLegion.Meta
{
    public static class PushIds
    {
        public const string CastleFull = "castle_full";
        public const string ChestReady = "chest_ready";
        public const string DailyReward = "daily_reward";
        public const string EventStart = "event_start";
    }

    /// <summary>
    /// Schedules the local notifications from section 6: offline coins full, free chest ready, daily reward, event start.
    /// Rescheduled whenever the app goes to the background; cancelled when it returns.
    /// </summary>
    public sealed class PushScheduler
    {
        private readonly IPushService _push;
        private readonly CastleService _castle;
        private readonly ChestService _chests;
        private readonly LoginService _login;
        private readonly WeekendEventService _event;
        private readonly ITimeService _time;
        private readonly Func<string, string> _text;
        private readonly int _dailyHourLocal;

        public PushScheduler(IPushService push, CastleService castle, ChestService chests, LoginService login,
            WeekendEventService weekendEvent, ITimeService time, Func<string, string> text, int dailyHourLocal = 10)
        {
            _push = push;
            _castle = castle;
            _chests = chests;
            _login = login;
            _event = weekendEvent;
            _time = time;
            _text = text;
            _dailyHourLocal = dailyHourLocal;
        }

        public void Reschedule()
        {
            _push.CancelAllLocal();
            var now = _time.UtcNow;
            string title = _text("push.title");

            if (!_castle.IsFull)
                _push.ScheduleLocal(PushIds.CastleFull, title, _text("push.castle_full"), now + _castle.TimeUntilFull());

            if (!_chests.WoodenReady)
                _push.ScheduleLocal(PushIds.ChestReady, title, _text("push.chest_ready"), now + _chests.WoodenRemaining());

            // tomorrow at the configured hour (or today if the reward is still unclaimed and it is before that hour)
            var local = _time.LocalNow;
            var next = local.Date.AddHours(_dailyHourLocal);
            if (next <= local || _login.CanClaim) next = local.Date.AddDays(1).AddHours(_dailyHourLocal);
            _push.ScheduleLocal(PushIds.DailyReward, title, _text("push.daily_reward"), now + (next - local));

            if (!_event.IsActive && _event.Unlocked)
                _push.ScheduleLocal(PushIds.EventStart, title, _text("push.event_start"), now + _event.TimeUntilStart);
        }

        public void CancelAll() => _push.CancelAllLocal();
    }
}
