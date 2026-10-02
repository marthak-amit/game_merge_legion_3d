using System;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Save;

namespace MergeLegion.Meta
{
    public readonly struct CastleChangedEvent { }

    /// <summary>Idle castle (section 2.3): generates coins while away, capped at N hours; upgrade with coins.</summary>
    public sealed class CastleService
    {
        private readonly SaveService _save;
        private readonly CastleConfig _cfg;
        private readonly GameConfig _game;
        private readonly ITimeService _time;
        private readonly CurrencyService _currency;
        private readonly Func<bool> _isVip;

        public CastleService(SaveService save, CastleConfig cfg, GameConfig game, ITimeService time, CurrencyService currency, Func<bool> isVip)
        {
            _save = save;
            _cfg = cfg;
            _game = game;
            _time = time;
            _currency = currency;
            _isVip = isVip;
            if (_save.Data.castleLastCollectTicks == 0) _save.Data.castleLastCollectTicks = _time.UtcNow.Ticks;
            if (_save.Data.castleLevel < 1) _save.Data.castleLevel = 1;
        }

        public int Level => _save.Data.castleLevel;
        public int MaxLevel => _cfg.maxLevel;
        public float CapHours => _cfg.capHours;

        public long CoinsPerHour()
        {
            int level = Math.Max(1, _save.Data.highestCampaignLevel + 1);
            double perWin = RewardService.WinCoins(_game.rewards, level, 1);
            double mult = 1.0 + _cfg.levelStep * (Level - 1);
            double vip = _isVip() ? _cfg.vipMultiplier : 1.0;
            return Math.Max(1L, (long)Math.Round(perWin * _cfg.winsPerHour * mult * vip));
        }

        public TimeSpan Elapsed()
        {
            long now = _time.UtcNow.Ticks;
            if (now < _save.Data.castleLastCollectTicks)
            {
                // clock moved backwards: re-anchor so time cannot be farmed by toggling the device clock
                _save.Data.castleLastCollectTicks = now;
                return TimeSpan.Zero;
            }
            var elapsed = TimeSpan.FromTicks(now - _save.Data.castleLastCollectTicks);
            var cap = TimeSpan.FromHours(_cfg.capHours);
            return elapsed > cap ? cap : elapsed;
        }

        public bool IsFull => Elapsed() >= TimeSpan.FromHours(_cfg.capHours);

        public TimeSpan TimeUntilFull() => TimeSpan.FromHours(_cfg.capHours) - Elapsed();

        public long Pending() => (long)Math.Floor(Elapsed().TotalHours * CoinsPerHour());

        /// <summary>Collects pending coins. multiplier 2 = the rewarded-ad double.</summary>
        public long Collect(float multiplier = 1f)
        {
            long amount = (long)Math.Floor(Pending() * Math.Max(1f, multiplier));
            _save.Data.castleLastCollectTicks = _time.UtcNow.Ticks;
            if (amount > 0) _currency.Add(CurrencyType.Coins, amount, "castle_collect");
            _save.MarkDirty();
            EventBus.Publish(new CastleChangedEvent());
            return amount;
        }

        public long UpgradeCost() => (long)Math.Ceiling(_cfg.upgradeBaseCost * Math.Pow(_cfg.upgradeGrowth, Level - 1));

        public bool Upgrade()
        {
            if (Level >= _cfg.maxLevel) return false;
            // settle accrued income at the old rate first so an upgrade never retroactively changes it
            long pending = Pending();
            if (!_currency.TrySpend(CurrencyType.Coins, UpgradeCost(), "castle_upgrade")) return false;
            if (pending > 0) _currency.Add(CurrencyType.Coins, pending, "castle_collect");
            _save.Data.castleLastCollectTicks = _time.UtcNow.Ticks;
            _save.Data.castleLevel++;
            _save.MarkDirty();
            EventBus.Publish(new CastleChangedEvent());
            return true;
        }
    }
}
