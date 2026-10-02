using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Meta
{
    public readonly struct ChestOpenedEvent
    {
        public readonly string ChestId;
        public ChestOpenedEvent(string chestId) { ChestId = chestId; }
    }

    public enum ChestPayment { Free, Ad, Gems, Keys }

    public enum ChestOpenStatus { Ok, NotReady, NotEnough, AdFailed, Unknown }

    public struct ChestOpenResult
    {
        public ChestOpenStatus Status;
        public List<GrantedItem> Items;
    }

    public struct DropRate
    {
        public RewardType Type;
        public float Percent;
        public int Min, Max;
    }

    /// <summary>Chests (section 2.4). Drop rates are derived from the same weights used for rolling, so the disclosure is always exact.</summary>
    public sealed class ChestService
    {
        private readonly SaveService _save;
        private readonly MetaConfig _cfg;
        private readonly CurrencyService _currency;
        private readonly RewardGranter _granter;
        private readonly ITimeService _time;
        private readonly DeterministicRng _rng;
        private readonly IAnalyticsService _analytics;
        private readonly AdsManager _ads;

        public ChestService(SaveService save, MetaConfig cfg, CurrencyService currency, RewardGranter granter, ITimeService time,
            DeterministicRng rng, IAnalyticsService analytics, AdsManager ads)
        {
            _save = save;
            _cfg = cfg;
            _currency = currency;
            _granter = granter;
            _time = time;
            _rng = rng;
            _analytics = analytics;
            _ads = ads;
            _granter.ChestRoller = id => Roll(_cfg.Chest(id));
        }

        public IReadOnlyList<ChestConfig> Chests => _cfg.chests;

        // ---------------------------------------------------------------- disclosure

        public List<DropRate> DropRates(string chestId)
        {
            var chest = _cfg.Chest(chestId);
            var rates = new List<DropRate>();
            if (chest == null) return rates;
            float total = 0f;
            foreach (var d in chest.drops) total += d.weight;
            foreach (var d in chest.drops)
                rates.Add(new DropRate { Type = d.type, Percent = total > 0f ? d.weight / total * 100f : 0f, Min = d.min, Max = d.max });
            return rates;
        }

        // ---------------------------------------------------------------- wooden (free timer)

        public TimeSpan WoodenRemaining()
        {
            long ready = _save.Data.woodenReadyTicks;
            long now = _time.UtcNow.Ticks;
            return ready <= now ? TimeSpan.Zero : TimeSpan.FromTicks(ready - now);
        }

        public bool WoodenReady => WoodenRemaining() == TimeSpan.Zero;

        public int SilverRemainingToday => _ads.Remaining(AdPlacements.SilverChest);

        // ---------------------------------------------------------------- opening

        public ChestOpenResult OpenFree(string chestId)
        {
            var chest = _cfg.Chest(chestId);
            if (chest == null) return Fail(ChestOpenStatus.Unknown);
            if (chest.cooldownHours <= 0f || !WoodenReady) return Fail(ChestOpenStatus.NotReady);
            _save.Data.woodenReadyTicks = _time.UtcNow.Ticks + TimeSpan.FromHours(chest.cooldownHours).Ticks;
            return Deliver(chest, ChestPayment.Free);
        }

        public void OpenWithAd(string chestId, Action<ChestOpenResult> onDone)
        {
            var chest = _cfg.Chest(chestId);
            if (chest == null || string.IsNullOrEmpty(chest.adPlacement)) { onDone(Fail(ChestOpenStatus.Unknown)); return; }
            _ads.ShowRewarded(chest.adPlacement, ok =>
            {
                onDone(ok ? Deliver(chest, ChestPayment.Ad) : Fail(ChestOpenStatus.AdFailed));
            });
        }

        public ChestOpenResult OpenPaid(string chestId, ChestPayment payment)
        {
            var chest = _cfg.Chest(chestId);
            if (chest == null) return Fail(ChestOpenStatus.Unknown);
            if (payment == ChestPayment.Gems)
            {
                if (chest.costGems <= 0 || !_currency.TrySpend(CurrencyType.Gems, chest.costGems, "chest_" + chestId)) return Fail(ChestOpenStatus.NotEnough);
            }
            else if (payment == ChestPayment.Keys)
            {
                if (chest.costKeys <= 0 || !_currency.TrySpend(CurrencyType.ChestKeys, chest.costKeys, "chest_" + chestId)) return Fail(ChestOpenStatus.NotEnough);
            }
            else return Fail(ChestOpenStatus.Unknown);
            return Deliver(chest, payment);
        }

        private ChestOpenResult Deliver(ChestConfig chest, ChestPayment payment)
        {
            var items = RewardGranter.Aggregate(Roll(chest));
            _granter.Apply(items, "chest_" + chest.id);
            _analytics.LogEvent(AnalyticsEvents.ChestOpen, new Dictionary<string, object>
            {
                { "chest", chest.id },
                { "payment", payment.ToString() }
            });
            _save.MarkDirty();
            EventBus.Publish(new ChestOpenedEvent(chest.id));
            return new ChestOpenResult { Status = ChestOpenStatus.Ok, Items = items };
        }

        private static ChestOpenResult Fail(ChestOpenStatus status) => new ChestOpenResult { Status = status, Items = new List<GrantedItem>() };

        /// <summary>Rolls contents without applying them.</summary>
        public List<GrantedItem> Roll(ChestConfig chest)
        {
            var items = new List<GrantedItem>();
            if (chest == null) return items;

            float total = 0f;
            foreach (var d in chest.drops) total += d.weight;

            for (int r = 0; r < chest.rolls && total > 0f; r++)
            {
                float pick = _rng.NextFloat() * total;
                DropEntry chosen = chest.drops[chest.drops.Count - 1];
                float acc = 0f;
                foreach (var d in chest.drops)
                {
                    acc += d.weight;
                    if (pick < acc) { chosen = d; break; }
                }
                int amount = chosen.min >= chosen.max ? chosen.min : _rng.Range(chosen.min, chosen.max + 1);
                items.AddRange(_granter.Resolve(new Reward(chosen.type, amount, chosen.target)));
            }

            if (chest.guaranteedShardMax > 0)
            {
                int amount = _rng.Range(chest.guaranteedShardMin, chest.guaranteedShardMax + 1);
                items.Add(new GrantedItem(RewardType.Shards, amount, _granter.RandomCommander()));
            }
            return items;
        }
    }
}
