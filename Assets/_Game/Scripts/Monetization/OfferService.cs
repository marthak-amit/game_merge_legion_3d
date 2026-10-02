using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Levels;
using MergeLegion.Save;

namespace MergeLegion.Monetization
{
    public struct ActiveOffer
    {
        public OfferDef Def;
        public ProductDef Product;
        public DateTime ExpiresUtc;
        public TimeSpan Remaining;
    }

    public readonly struct OfferActivatedEvent
    {
        public readonly string OfferId;
        public OfferActivatedEvent(string offerId) { OfferId = offerId; }
    }

    /// <summary>
    /// Dynamic offers (section 3.2): triggered by lose streaks, chapter clears, progression milestones and returning players.
    /// Everything (triggers, timers, cooldowns, caps) comes from the monetization config / Remote Config.
    /// </summary>
    public sealed class OfferService : IDisposable
    {
        private readonly SaveService _save;
        private readonly MonetizationConfig _cfg;
        private readonly ITimeService _time;

        public OfferService(SaveService save, MonetizationConfig cfg, ITimeService time)
        {
            _save = save;
            _cfg = cfg;
            _time = time;
            EventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
            EventBus.Subscribe<PurchaseFulfilledEvent>(OnPurchase);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<LevelCompletedEvent>(OnLevelCompleted);
            EventBus.Unsubscribe<PurchaseFulfilledEvent>(OnPurchase);
        }

        private void OnPurchase(PurchaseFulfilledEvent e) => MarkPurchased(e.Sku);

        // ---------------------------------------------------------------- triggers

        public void OnLevelCompleted(LevelCompletedEvent e)
        {
            foreach (var def in _cfg.offers)
            {
                switch (def.trigger)
                {
                    case OfferTrigger.LevelReached:
                        if (e.Won && e.Level + 1 >= def.triggerValue) TryActivate(def);
                        break;
                    case OfferTrigger.LoseStreak:
                        if (!e.Won && _save.Data.loseStreak >= def.triggerValue) TryActivate(def);
                        break;
                    case OfferTrigger.ChapterClear:
                        if (e.Won && !e.Endless && def.triggerValue > 0 && e.Level % def.triggerValue == 0) TryActivate(def);
                        break;
                }
            }
        }

        /// <summary>Call once per app session start: evaluates returning-player offers and records the visit.</summary>
        public void OnSessionStart(long currentGems)
        {
            long now = _time.UtcNow.Ticks;
            long lastSeen = _save.Data.lastSeenUtcTicks;
            foreach (var def in _cfg.offers)
            {
                if (def.trigger == OfferTrigger.ReturningPlayer && lastSeen > 0 &&
                    TimeSpan.FromTicks(now - lastSeen).TotalHours >= def.triggerValue) TryActivate(def);
                if (def.trigger == OfferTrigger.GemsLow && currentGems < def.triggerValue && _save.Data.highestCampaignLevel >= 5) TryActivate(def);
            }
            _save.Data.lastSeenUtcTicks = now;
            _save.MarkDirty();
        }

        public void TouchLastSeen()
        {
            _save.Data.lastSeenUtcTicks = _time.UtcNow.Ticks;
            _save.MarkDirty();
        }

        private bool TryActivate(OfferDef def)
        {
            var product = _cfg.Product(def.sku);
            if (product == null) return false;
            if (product.oneTime && _save.Data.purchasedSkus.Contains(def.sku)) return false;

            var st = State(def.id);
            long now = _time.UtcNow.Ticks;
            bool running = st.expiresUtcTicks > now && !st.purchased;
            if (running) return false;
            if (def.maxActivations > 0 && st.activations >= def.maxActivations) return false;
            if (st.lastActivatedUtcTicks > 0 && TimeSpan.FromTicks(now - st.lastActivatedUtcTicks).TotalHours < def.cooldownHours) return false;

            st.expiresUtcTicks = now + TimeSpan.FromHours(def.durationHours).Ticks;
            st.lastActivatedUtcTicks = now;
            st.activations++;
            st.presented = false;
            st.purchased = false;
            _save.MarkDirty();
            EventBus.Publish(new OfferActivatedEvent(def.id));
            return true;
        }

        private OfferState State(string id)
        {
            var st = _save.Data.offers.Find(o => o.id == id);
            if (st == null)
            {
                st = new OfferState { id = id };
                _save.Data.offers.Add(st);
            }
            return st;
        }

        // ---------------------------------------------------------------- queries

        public List<ActiveOffer> Active()
        {
            var list = new List<ActiveOffer>();
            long now = _time.UtcNow.Ticks;
            foreach (var def in _cfg.offers)
            {
                var st = _save.Data.offers.Find(o => o.id == def.id);
                if (st == null || st.purchased || st.expiresUtcTicks <= now) continue;
                list.Add(new ActiveOffer
                {
                    Def = def,
                    Product = _cfg.Product(def.sku),
                    ExpiresUtc = new DateTime(st.expiresUtcTicks, DateTimeKind.Utc),
                    Remaining = TimeSpan.FromTicks(st.expiresUtcTicks - now)
                });
            }
            return list;
        }

        public bool IsActive(string offerId) => Active().Exists(o => o.Def.id == offerId);

        /// <summary>Returns a newly triggered offer whose popup has not been shown yet, and marks it shown.</summary>
        public bool TryTakePresentable(out ActiveOffer offer)
        {
            foreach (var a in Active())
            {
                var st = State(a.Def.id);
                if (st.presented) continue;
                st.presented = true;
                _save.MarkDirty();
                offer = a;
                return true;
            }
            offer = default(ActiveOffer);
            return false;
        }

        public void MarkPurchased(string sku)
        {
            foreach (var def in _cfg.offers)
            {
                if (def.sku != sku) continue;
                var st = _save.Data.offers.Find(o => o.id == def.id);
                if (st != null && st.expiresUtcTicks > _time.UtcNow.Ticks) st.purchased = true;
            }
            _save.MarkDirty();
        }
    }
}
