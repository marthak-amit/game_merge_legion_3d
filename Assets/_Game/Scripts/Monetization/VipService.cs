using System;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Meta;
using MergeLegion.Save;

namespace MergeLegion.Monetization
{
    /// <summary>VIP weekly subscription perks: daily gems, 2x offline (CastleService), no forced ads (AdsManager), badge.</summary>
    public sealed class VipService
    {
        private readonly SaveService _save;
        private readonly VipConfig _cfg;
        private readonly ITimeService _time;
        private readonly DailyService _daily;
        private readonly RewardGranter _granter;

        public VipService(SaveService save, VipConfig cfg, ITimeService time, DailyService daily, RewardGranter granter)
        {
            _save = save;
            _cfg = cfg;
            _time = time;
            _daily = daily;
            _granter = granter;
        }

        public bool IsActive => _save.Data.vipUntilUtcTicks > _time.UtcNow.Ticks;

        public TimeSpan Remaining => IsActive ? TimeSpan.FromTicks(_save.Data.vipUntilUtcTicks - _time.UtcNow.Ticks) : TimeSpan.Zero;

        public bool DailyGemsAvailable => IsActive && _save.Data.vipClaimDayKey != _daily.DayKey;

        public int DailyGems => _cfg.dailyGems;

        /// <summary>Starts or extends the subscription (called after the vip_weekly purchase or a renewal).</summary>
        public void Activate() => _granter.Grant(new Reward(RewardType.VipDays, _cfg.days), "vip");

        public bool ClaimDaily()
        {
            if (!DailyGemsAvailable) return false;
            _save.Data.vipClaimDayKey = _daily.DayKey;
            _granter.Grant(new Reward(RewardType.Gems, _cfg.dailyGems), "vip_daily");
            return true;
        }
    }
}
