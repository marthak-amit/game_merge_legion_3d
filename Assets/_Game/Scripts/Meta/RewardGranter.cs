using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Save;

namespace MergeLegion.Meta
{
    public readonly struct RewardsGrantedEvent
    {
        public readonly string Source;
        public RewardsGrantedEvent(string source) { Source = source; }
    }

    /// <summary>
    /// The one place that turns authored <see cref="Reward"/>s into currency / entitlements.
    /// Resolve (what will the player get) is separate from Apply (give it), so UIs can preview and chests can roll.
    /// </summary>
    public sealed class RewardGranter
    {
        private readonly SaveService _save;
        private readonly CurrencyService _currency;
        private readonly GameConfig _config;
        private readonly GameDatabase _db;
        private readonly ITimeService _time;
        private readonly DeterministicRng _rng;
        private readonly Func<int> _levelProvider;

        /// <summary>Set by ChestService: rolls and returns a chest's contents (not yet applied).</summary>
        public Func<string, List<GrantedItem>> ChestRoller;
        /// <summary>Raised after entitlements change (RemoveAds / VIP) so the ads layer can refresh.</summary>
        public Action EntitlementsChanged;

        public RewardGranter(SaveService save, CurrencyService currency, GameConfig config, GameDatabase db,
            ITimeService time, DeterministicRng rng, Func<int> levelProvider)
        {
            _save = save;
            _currency = currency;
            _config = config;
            _db = db;
            _time = time;
            _rng = rng;
            _levelProvider = levelProvider;
        }

        public string RandomCommander()
        {
            if (_db.commanders.Count == 0) return "";
            return _db.commanders[_rng.NextInt(_db.commanders.Count)].id;
        }

        public long CoinsForWins(float wins) =>
            Math.Max(1L, (long)Math.Round(RewardService.WinCoins(_config.rewards, Math.Max(1, _levelProvider()), 1) * wins));

        public List<GrantedItem> Resolve(Reward r)
        {
            var list = new List<GrantedItem>(1);
            switch (r.type)
            {
                case RewardType.CoinsWins:
                    list.Add(new GrantedItem(RewardType.Coins, CoinsForWins(r.amount)));
                    break;
                case RewardType.Shards:
                    string target = string.IsNullOrEmpty(r.target) || r.target == "random" ? RandomCommander() : r.target;
                    list.Add(new GrantedItem(RewardType.Shards, r.amount, target));
                    break;
                case RewardType.Chest:
                    if (ChestRoller != null) list.AddRange(ChestRoller(r.target));
                    break;
                default:
                    list.Add(new GrantedItem(r.type, r.amount, r.target));
                    break;
            }
            return list;
        }

        public void Apply(IEnumerable<GrantedItem> items, string source)
        {
            foreach (var item in items)
            {
                switch (item.Type)
                {
                    case RewardType.Coins: _currency.Add(CurrencyType.Coins, item.Amount, source); break;
                    case RewardType.Gems: _currency.Add(CurrencyType.Gems, item.Amount, source); break;
                    case RewardType.ChestKeys: _currency.Add(CurrencyType.ChestKeys, item.Amount, source); break;
                    case RewardType.BattlePassXp: _currency.Add(CurrencyType.BattlePassXp, item.Amount, source); break;
                    case RewardType.Shards: _currency.AddShards(item.Target, (int)item.Amount, source); break;
                    case RewardType.VipDays: ExtendVip((int)item.Amount); break;
                    case RewardType.RemoveAds:
                        _save.Data.noAds = true;
                        EntitlementsChanged?.Invoke();
                        break;
                }
            }
            _save.MarkDirty();
            EventBus.Publish(new RewardsGrantedEvent(source));
        }

        public List<GrantedItem> Grant(Reward r, string source)
        {
            var items = Resolve(r);
            Apply(items, source);
            return items;
        }

        public List<GrantedItem> Grant(IEnumerable<Reward> rewards, string source)
        {
            var all = new List<GrantedItem>();
            foreach (var r in rewards) all.AddRange(Resolve(r));
            Apply(all, source);
            return all;
        }

        private void ExtendVip(int days)
        {
            long now = _time.UtcNow.Ticks;
            long start = Math.Max(now, _save.Data.vipUntilUtcTicks);
            _save.Data.vipUntilUtcTicks = start + TimeSpan.FromDays(days).Ticks;
            EntitlementsChanged?.Invoke();
        }

        /// <summary>Merges items of the same type/target (for reward popups).</summary>
        public static List<GrantedItem> Aggregate(IEnumerable<GrantedItem> items)
        {
            var result = new List<GrantedItem>();
            foreach (var it in items)
            {
                int idx = result.FindIndex(x => x.Type == it.Type && x.Target == it.Target);
                if (idx < 0) result.Add(it);
                else result[idx] = new GrantedItem(it.Type, result[idx].Amount + it.Amount, it.Target);
            }
            return result;
        }
    }
}
