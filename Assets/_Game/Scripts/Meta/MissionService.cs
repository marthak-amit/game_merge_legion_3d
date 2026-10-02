using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Meta
{
    public readonly struct MissionProgressEvent
    {
        public readonly string Id;
        public MissionProgressEvent(string id) { Id = id; }
    }

    public struct AchievementView
    {
        public AchievementTemplate Template;
        public int Progress;
        public int ClaimedTiers;
        public int NextTarget;      // 0 when all tiers claimed
        public bool CanClaim;
    }

    /// <summary>
    /// Daily (3/day, local-midnight refresh), weekly and achievement tracking (section 2.5). Progress is driven entirely by
    /// EventBus events, so gameplay code never talks to missions directly.
    /// </summary>
    public sealed class MissionService : IDisposable
    {
        private readonly SaveService _save;
        private readonly MissionConfig _cfg;
        private readonly DailyService _daily;
        private readonly RewardGranter _granter;
        private readonly IAnalyticsService _analytics;
        private readonly Func<int> _level;
        private readonly int _playerSeed;

        public MissionService(SaveService save, MissionConfig cfg, DailyService daily, RewardGranter granter,
            IAnalyticsService analytics, Func<int> playerLevel)
        {
            _save = save;
            _cfg = cfg;
            _daily = daily;
            _granter = granter;
            _analytics = analytics;
            _level = playerLevel;
            _playerSeed = save.Data.playerId.GetHashCode() & 0x7fffffff;

            Refresh();
            EventBus.Subscribe<LevelCompletedEvent>(OnLevel);
            EventBus.Subscribe<UnitMergedEvent>(OnMerge);
            EventBus.Subscribe<UnitBoughtEvent>(OnBought);
            EventBus.Subscribe<SkillUsedEventProxy>(OnSkillProxy);
            EventBus.Subscribe<AdRewardedEvent>(OnAd);
            EventBus.Subscribe<ChestOpenedEvent>(OnChest);
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
            EventBus.Subscribe<ResearchChangedEvent>(OnResearch);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<LevelCompletedEvent>(OnLevel);
            EventBus.Unsubscribe<UnitMergedEvent>(OnMerge);
            EventBus.Unsubscribe<UnitBoughtEvent>(OnBought);
            EventBus.Unsubscribe<SkillUsedEventProxy>(OnSkillProxy);
            EventBus.Unsubscribe<AdRewardedEvent>(OnAd);
            EventBus.Unsubscribe<ChestOpenedEvent>(OnChest);
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);
            EventBus.Unsubscribe<ResearchChangedEvent>(OnResearch);
        }

        // ---------------------------------------------------------------- assignment

        public void Refresh()
        {
            var d = _save.Data;
            _daily.Refresh();
            bool changed = false;
            int day = _daily.DayKey, week = _daily.WeekKey;
            if (d.missionDayKey != day)
            {
                d.missions.RemoveAll(m => m.period == 0);
                Assign(0, _cfg.daily, _cfg.dailyCount, day);
                d.missionDayKey = day;
                d.dailyBonusClaimed = false;
                changed = true;
            }
            if (d.missionWeekKey != week)
            {
                d.missions.RemoveAll(m => m.period == 1);
                Assign(1, _cfg.weekly, _cfg.weeklyCount, week);
                d.missionWeekKey = week;
                d.weeklyBonusClaimed = false;
                changed = true;
            }
            if (changed) _save.MarkDirty();
        }

        private void Assign(int period, List<MissionTemplate> pool, int count, int key)
        {
            var rng = new DeterministicRng(_playerSeed ^ (key * 2654435));
            var candidates = new List<MissionTemplate>();
            int level = _level();
            foreach (var t in pool) if (t.minLevel <= level) candidates.Add(t);
            if (candidates.Count == 0) candidates.AddRange(pool);

            for (int slot = 0; slot < count && candidates.Count > 0; slot++)
            {
                int i = rng.NextInt(candidates.Count);
                _save.Data.missions.Add(new MissionState { id = candidates[i].id, period = period, slot = slot });
                candidates.RemoveAt(i);
            }
        }

        public MissionTemplate Template(string id)
        {
            foreach (var t in _cfg.daily) if (t.id == id) return t;
            foreach (var t in _cfg.weekly) if (t.id == id) return t;
            return null;
        }

        public List<MissionState> Missions(int period)
        {
            Refresh();
            return _save.Data.missions.FindAll(m => m.period == period);
        }

        public bool IsComplete(MissionState m)
        {
            var t = Template(m.id);
            return t != null && m.progress >= t.target;
        }

        // ---------------------------------------------------------------- claiming

        public List<GrantedItem> Claim(string missionId)
        {
            var m = _save.Data.missions.Find(x => x.id == missionId);
            var t = Template(missionId);
            if (m == null || t == null || m.claimed || !IsComplete(m)) return null;

            m.claimed = true;
            var items = _granter.Grant(t.reward, "mission_" + missionId);
            _analytics.LogEvent(AnalyticsEvents.MissionComplete, new Dictionary<string, object> { { "mission", missionId }, { "period", m.period } });
            _save.MarkDirty();
            return items;
        }

        public bool AllClaimed(int period)
        {
            var list = Missions(period);
            return list.Count > 0 && list.TrueForAll(m => m.claimed);
        }

        public bool BonusAvailable(int period) =>
            AllClaimed(period) && !(period == 0 ? _save.Data.dailyBonusClaimed : _save.Data.weeklyBonusClaimed);

        public List<GrantedItem> ClaimBonus(int period)
        {
            if (!BonusAvailable(period)) return null;
            if (period == 0) _save.Data.dailyBonusClaimed = true; else _save.Data.weeklyBonusClaimed = true;
            var reward = period == 0 ? _cfg.dailyAllBonus : _cfg.weeklyAllBonus;
            _save.MarkDirty();
            return reward == null ? new List<GrantedItem>() : _granter.Grant(reward, "mission_bonus_" + period);
        }

        /// <summary>Swaps one unfinished mission for a different one (rewarded-ad reroll).</summary>
        public bool Reroll(string missionId)
        {
            var m = _save.Data.missions.Find(x => x.id == missionId);
            if (m == null || m.claimed || IsComplete(m)) return false;
            var pool = m.period == 0 ? _cfg.daily : _cfg.weekly;
            var taken = new HashSet<string>();
            foreach (var x in _save.Data.missions) taken.Add(x.id);
            var options = pool.FindAll(t => !taken.Contains(t.id) && t.minLevel <= _level());
            if (options.Count == 0) return false;
            var rng = new DeterministicRng(_playerSeed ^ (m.slot * 977) ^ _daily.DayKey ^ m.progress);
            m.id = options[rng.NextInt(options.Count)].id;
            m.progress = 0;
            _save.MarkDirty();
            return true;
        }

        // ---------------------------------------------------------------- achievements

        public List<AchievementView> Achievements()
        {
            var views = new List<AchievementView>();
            foreach (var a in _cfg.achievements)
            {
                int progress = IntEntries.Get(_save.Data.achievementProgress, a.id);
                int claimed = IntEntries.Get(_save.Data.achievementClaimed, a.id);
                int next = claimed < a.tiers.Count ? a.tiers[claimed].target : 0;
                views.Add(new AchievementView
                {
                    Template = a, Progress = progress, ClaimedTiers = claimed, NextTarget = next,
                    CanClaim = next > 0 && progress >= next
                });
            }
            return views;
        }

        public List<GrantedItem> ClaimAchievement(string id)
        {
            var a = _cfg.achievements.Find(x => x.id == id);
            if (a == null) return null;
            int progress = IntEntries.Get(_save.Data.achievementProgress, id);
            int claimed = IntEntries.Get(_save.Data.achievementClaimed, id);
            if (claimed >= a.tiers.Count || progress < a.tiers[claimed].target) return null;

            var items = _granter.Grant(a.tiers[claimed].reward, "achievement_" + id);
            IntEntries.Set(_save.Data.achievementClaimed, id, claimed + 1);
            _analytics.LogEvent(AnalyticsEvents.MissionComplete, new Dictionary<string, object> { { "mission", id }, { "tier", claimed + 1 } });
            _save.MarkDirty();
            return items;
        }

        /// <summary>True if anything can be claimed (drives the red dot on the Missions button).</summary>
        public bool HasClaimable()
        {
            foreach (var m in _save.Data.missions) if (!m.claimed && IsComplete(m)) return true;
            foreach (var a in Achievements()) if (a.CanClaim) return true;
            return BonusAvailable(0) || BonusAvailable(1);
        }

        // ---------------------------------------------------------------- progress

        public void Report(MissionType type, int amount = 1)
        {
            if (amount <= 0) return;
            Refresh();
            foreach (var m in _save.Data.missions)
            {
                if (m.claimed) continue;
                var t = Template(m.id);
                if (t == null || t.type != type) continue;
                int before = m.progress;
                m.progress = Math.Min(t.target, m.progress + amount);
                if (m.progress != before) EventBus.Publish(new MissionProgressEvent(m.id));
            }
            foreach (var a in _cfg.achievements)
                if (a.type == type) IntEntries.Add(_save.Data.achievementProgress, a.id, amount);
            _save.MarkDirty();
        }

        private void OnLevel(LevelCompletedEvent e)
        {
            if (!e.Won) return;
            Report(MissionType.WinLevels);
            if (e.IsBoss) Report(MissionType.WinBoss);
            if (e.Stars >= 3) Report(MissionType.ThreeStarWins);
        }

        private void OnMerge(UnitMergedEvent e) => Report(MissionType.MergeUnits);
        private void OnBought(UnitBoughtEvent e) => Report(MissionType.BuyUnits);
        private void OnSkillProxy(SkillUsedEventProxy e) => Report(MissionType.UseSkill);
        private void OnAd(AdRewardedEvent e) => Report(MissionType.WatchAds);
        private void OnChest(ChestOpenedEvent e) => Report(MissionType.OpenChests);
        private void OnResearch(ResearchChangedEvent e) => Report(MissionType.UpgradeResearch);

        private void OnCurrency(CurrencyChangedEvent e)
        {
            if (e.Type != CurrencyType.Coins) return;
            if (e.Delta > 0) Report(MissionType.EarnCoins, (int)Math.Min(int.MaxValue, e.Delta));
            else Report(MissionType.SpendCoins, (int)Math.Min(int.MaxValue, -e.Delta));
        }
    }

    /// <summary>Bridge so Meta does not depend on the Battle namespace (the director publishes the real event too).</summary>
    public readonly struct SkillUsedEventProxy { }
}
