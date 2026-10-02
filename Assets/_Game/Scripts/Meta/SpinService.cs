using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Monetization;
using MergeLegion.Save;

namespace MergeLegion.Meta
{
    public struct SpinOutcome
    {
        public bool Success;
        public int SegmentIndex;
        public List<GrantedItem> Items;
    }

    /// <summary>Lucky Spin (section 2.7): free spins per day plus rewarded-ad spins (cap from the ads config).</summary>
    public sealed class SpinService
    {
        private const string FreeKey = "spin_free";

        private readonly SpinConfig _cfg;
        private readonly DailyService _daily;
        private readonly RewardGranter _granter;
        private readonly AdsManager _ads;
        private readonly DeterministicRng _rng;

        public SpinService(SpinConfig cfg, DailyService daily, RewardGranter granter, AdsManager ads, DeterministicRng rng)
        {
            _cfg = cfg;
            _daily = daily;
            _granter = granter;
            _ads = ads;
            _rng = rng;
        }

        public IReadOnlyList<SpinSegment> Segments => _cfg.segments;
        public int FreeRemaining => Math.Max(0, _cfg.freePerDay - _daily.Get(FreeKey));
        public int AdSpinsRemaining => _ads.Remaining(AdPlacements.Spin);

        public List<float> Rates()
        {
            float total = 0f;
            foreach (var s in _cfg.segments) total += s.weight;
            var list = new List<float>();
            foreach (var s in _cfg.segments) list.Add(total > 0f ? s.weight / total * 100f : 0f);
            return list;
        }

        public SpinOutcome SpinFree()
        {
            if (FreeRemaining <= 0) return new SpinOutcome { Success = false, Items = new List<GrantedItem>() };
            _daily.Increment(FreeKey);
            return Resolve();
        }

        public void SpinWithAd(Action<SpinOutcome> onDone)
        {
            _ads.ShowRewarded(AdPlacements.Spin, ok =>
                onDone(ok ? Resolve() : new SpinOutcome { Success = false, Items = new List<GrantedItem>() }));
        }

        private SpinOutcome Resolve()
        {
            float total = 0f;
            foreach (var s in _cfg.segments) total += s.weight;
            float pick = _rng.NextFloat() * total, acc = 0f;
            int index = _cfg.segments.Count - 1;
            for (int i = 0; i < _cfg.segments.Count; i++)
            {
                acc += _cfg.segments[i].weight;
                if (pick < acc) { index = i; break; }
            }
            var items = _granter.Grant(_cfg.segments[index].reward, "spin");
            return new SpinOutcome { Success = true, SegmentIndex = index, Items = items };
        }
    }
}
