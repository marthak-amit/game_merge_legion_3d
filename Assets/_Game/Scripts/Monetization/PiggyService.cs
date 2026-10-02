using System;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Levels;
using MergeLegion.Save;

namespace MergeLegion.Monetization
{
    public readonly struct PiggyChangedEvent { }

    /// <summary>Piggy Bank: gems accumulate from play; breaking it (IAP) pays them out.</summary>
    public sealed class PiggyService : IDisposable
    {
        private readonly SaveService _save;
        private readonly PiggyConfig _cfg;
        private readonly CurrencyService _currency;

        public PiggyService(SaveService save, PiggyConfig cfg, CurrencyService currency)
        {
            _save = save;
            _cfg = cfg;
            _currency = currency;
            EventBus.Subscribe<LevelCompletedEvent>(OnLevel);
        }

        public void Dispose() => EventBus.Unsubscribe<LevelCompletedEvent>(OnLevel);

        public long Accumulated => _save.Data.piggyGems;
        public long Capacity => _cfg.capGems;
        public bool IsFull => Accumulated >= _cfg.capGems;
        public bool CanBreak => Accumulated >= _cfg.minBreakGems;
        public string Sku => _cfg.sku;

        private void OnLevel(LevelCompletedEvent e)
        {
            long add = e.Won ? _cfg.gemsPerWin : _cfg.gemsPerLoss;
            _save.Data.piggyGems = Math.Min(_cfg.capGems, _save.Data.piggyGems + add);
            _save.MarkDirty();
            EventBus.Publish(new PiggyChangedEvent());
        }

        /// <summary>Pays out the contents. Call only after the piggy_bank purchase succeeded.</summary>
        public long Break()
        {
            long gems = _save.Data.piggyGems;
            if (gems <= 0) return 0;
            _currency.Add(CurrencyType.Gems, gems, "piggy_bank");
            _save.Data.piggyGems = 0;
            _save.MarkDirty();
            EventBus.Publish(new PiggyChangedEvent());
            return gems;
        }
    }
}
