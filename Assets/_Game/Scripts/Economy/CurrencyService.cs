using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Economy
{
    public enum CurrencyType { Coins, Gems, ChestKeys, BattlePassXp }

    public readonly struct CurrencyChangedEvent
    {
        public readonly CurrencyType Type;
        public readonly long Delta;
        public readonly long Balance;
        public readonly string Reason;

        public CurrencyChangedEvent(CurrencyType type, long delta, long balance, string reason)
        {
            Type = type; Delta = delta; Balance = balance; Reason = reason;
        }
    }

    /// <summary>The single gateway for changing currency balances. Reports every earn/spend to analytics.</summary>
    public sealed class CurrencyService
    {
        private readonly SaveService _save;
        private readonly IAnalyticsService _analytics;

        public CurrencyService(SaveService save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public long Get(CurrencyType type)
        {
            var d = _save.Data;
            switch (type)
            {
                case CurrencyType.Coins: return d.coins;
                case CurrencyType.Gems: return d.gems;
                case CurrencyType.ChestKeys: return d.chestKeys;
                default: return d.battlePassXp;
            }
        }

        public bool CanAfford(CurrencyType type, long amount) => Get(type) >= amount;

        public void Add(CurrencyType type, long amount, string source)
        {
            if (amount <= 0) return;
            long balance = Set(type, Get(type) + amount);
            Report(AnalyticsEvents.CurrencyEarn, type, amount, AnalyticsParams.Source, source);
            EventBus.Publish(new CurrencyChangedEvent(type, amount, balance, source));
            _save.MarkDirty();
        }

        public bool TrySpend(CurrencyType type, long amount, string sink)
        {
            if (amount < 0 || Get(type) < amount) return false;
            if (amount == 0) return true;
            long balance = Set(type, Get(type) - amount);
            Report(AnalyticsEvents.CurrencySpend, type, amount, AnalyticsParams.Sink, sink);
            EventBus.Publish(new CurrencyChangedEvent(type, -amount, balance, sink));
            _save.MarkDirty();
            return true;
        }

        public long GetShards(string commanderId) => IntEntries.Get(_save.Data.commanderShards, commanderId);

        public void AddShards(string commanderId, int amount, string source)
        {
            if (amount <= 0) return;
            IntEntries.Add(_save.Data.commanderShards, commanderId, amount);
            Report(AnalyticsEvents.CurrencyEarn, "shards_" + commanderId, amount, AnalyticsParams.Source, source);
            _save.MarkDirty();
        }

        public bool TrySpendShards(string commanderId, int amount, string sink)
        {
            if (GetShards(commanderId) < amount) return false;
            IntEntries.Add(_save.Data.commanderShards, commanderId, -amount);
            Report(AnalyticsEvents.CurrencySpend, "shards_" + commanderId, amount, AnalyticsParams.Sink, sink);
            _save.MarkDirty();
            return true;
        }

        private long Set(CurrencyType type, long value)
        {
            var d = _save.Data;
            switch (type)
            {
                case CurrencyType.Coins: d.coins = value; break;
                case CurrencyType.Gems: d.gems = value; break;
                case CurrencyType.ChestKeys: d.chestKeys = value; break;
                default: d.battlePassXp = value; break;
            }
            return value;
        }

        private void Report(string evt, CurrencyType type, long amount, string key, string reason)
        {
            Report(evt, type.ToString().ToLowerInvariant(), amount, key, reason);
        }

        private void Report(string evt, string currency, long amount, string key, string reason)
        {
            _analytics?.LogEvent(evt, new Dictionary<string, object>
            {
                { AnalyticsParams.Currency, currency },
                { AnalyticsParams.Amount, amount },
                { key, reason }
            });
        }
    }
}
