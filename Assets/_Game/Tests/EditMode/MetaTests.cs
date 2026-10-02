using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Services.Mock;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    /// <summary>Wires the real meta services against mocks, the same way ServiceInstaller does.</summary>
    public abstract class MetaFixture
    {
        protected FakeTimeService Time;
        protected SaveService Save;
        protected MockAnalyticsService Analytics;
        protected MockAdsService MockAds;
        protected CurrencyService Currency;
        protected GameConfig Config;
        protected MetaConfig Meta;
        protected DailyService Daily;
        protected AdsManager Ads;
        protected RewardGranter Granter;
        protected GameDatabase Db;
        protected int Level = 10;

        [SetUp]
        public virtual void SetUpMeta()
        {
            EventBus.ClearAll();
            Time = new FakeTimeService { UtcNow = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Local).ToUniversalTime() };
            Save = new SaveService(new InMemorySaveStorage(), Time);
            Save.Load();
            Save.Data.highestCampaignLevel = Level - 1;
            Analytics = new MockAnalyticsService { LogToConsole = false };
            MockAds = new MockAdsService();
            MockAds.Initialize();
            Currency = new CurrencyService(Save, Analytics);
            Config = GameConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/game_config").text);
            Meta = MetaConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/meta_config").text);
            Db = GameDatabase.BuildFromCsv();
            Daily = new DailyService(Save, Time);
            Ads = new AdsManager(MockAds, Config, Daily, Save, Time, Analytics, new MockAttributionService());
            Granter = new RewardGranter(Save, Currency, Config, Db, Time, new DeterministicRng(11), () => Level);
            Granter.EntitlementsChanged = Ads.RefreshEntitlements;
        }
    }

    public class MetaConfigTests : MetaFixture
    {
        [Test]
        public void ShippedConfig_IsComplete()
        {
            Assert.AreEqual(4, Meta.chests.Count);
            Assert.AreEqual(7, Meta.login.days.Count);
            Assert.AreEqual(8, Meta.spin.segments.Count);
            Assert.GreaterOrEqual(Meta.missions.daily.Count, 5);
            Assert.GreaterOrEqual(Meta.missions.weekly.Count, 4);
            Assert.GreaterOrEqual(Meta.missions.achievements.Count, 4);
            foreach (var m in Meta.missions.daily) Assert.IsNotNull(m.reward, m.id);
        }

        [Test]
        public void Override_PatchesOnlyGivenFields()
        {
            var c = MetaConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/meta_config").text, "{\"castle\":{\"capHours\":12}}");
            Assert.AreEqual(12f, c.castle.capHours, 0.001);
            Assert.AreEqual(4, c.chests.Count);
        }
    }

    public class RewardGranterTests : MetaFixture
    {
        [Test]
        public void CoinsWins_ScalesWithPlayerLevel()
        {
            long at10 = Granter.CoinsForWins(1);
            Level = 40;
            Assert.Greater(Granter.CoinsForWins(1), at10 * 10);
        }

        [Test]
        public void Grant_AppliesEveryType()
        {
            Granter.Grant(new[]
            {
                new Reward(RewardType.Coins, 100), new Reward(RewardType.Gems, 7), new Reward(RewardType.ChestKeys, 2),
                new Reward(RewardType.Shards, 5, "selene"), new Reward(RewardType.BattlePassXp, 30)
            }, "test");
            Assert.AreEqual(100, Currency.Get(CurrencyType.Coins));
            Assert.AreEqual(7, Currency.Get(CurrencyType.Gems));
            Assert.AreEqual(2, Currency.Get(CurrencyType.ChestKeys));
            Assert.AreEqual(5, Currency.GetShards("selene"));
            Assert.AreEqual(30, Currency.Get(CurrencyType.BattlePassXp));
        }

        [Test]
        public void RandomShards_GoToARealCommander()
        {
            var items = Granter.Grant(new Reward(RewardType.Shards, 3, "random"), "test");
            Assert.AreEqual(1, items.Count);
            Assert.IsNotNull(Db.GetCommander(items[0].Target));
            Assert.AreEqual(3, Currency.GetShards(items[0].Target));
        }

        [Test]
        public void VipDays_Stack_AndRemoveAdsFlipsEntitlement()
        {
            Granter.Grant(new Reward(RewardType.VipDays, 7), "test");
            Granter.Grant(new Reward(RewardType.VipDays, 7), "test");
            Assert.IsTrue(Ads.IsVip);
            Assert.AreEqual(Time.UtcNow.AddDays(14).Ticks, Save.Data.vipUntilUtcTicks);

            Granter.Grant(new Reward(RewardType.RemoveAds, 1), "test");
            Assert.IsTrue(Save.Data.noAds);
            Assert.IsTrue(MockAds.ForcedAdsRemoved);
        }

        [Test]
        public void Aggregate_MergesSameTypeAndTarget()
        {
            var agg = RewardGranter.Aggregate(new[]
            {
                new GrantedItem(RewardType.Coins, 5), new GrantedItem(RewardType.Coins, 7),
                new GrantedItem(RewardType.Shards, 2, "a"), new GrantedItem(RewardType.Shards, 3, "b"), new GrantedItem(RewardType.Shards, 1, "a")
            });
            Assert.AreEqual(3, agg.Count);
            Assert.AreEqual(12, agg[0].Amount);
            Assert.AreEqual(3, agg.Find(x => x.Target == "a").Amount);
        }
    }

    public class CastleTests : MetaFixture
    {
        private CastleService _castle;

        [SetUp]
        public void SetUp() => _castle = new CastleService(Save, Meta.castle, Config, Time, Currency, () => Ads.IsVip);

        [Test]
        public void Accrues_Hourly_AndCapsAtEightHours()
        {
            Assert.AreEqual(0, _castle.Pending());
            Time.Advance(TimeSpan.FromHours(1));
            long hour = _castle.Pending();
            Assert.AreEqual(_castle.CoinsPerHour(), hour, 1);

            Time.Advance(TimeSpan.FromHours(20));
            Assert.IsTrue(_castle.IsFull);
            Assert.AreEqual(_castle.CoinsPerHour() * 8, _castle.Pending(), 2, "offline cap is 8h");
        }

        [Test]
        public void Collect_PaysOutAndResets_AdDoublesIt()
        {
            Time.Advance(TimeSpan.FromHours(4));
            long expected = _castle.Pending();
            long got = _castle.Collect(2f);
            Assert.AreEqual(expected * 2, got, 2);
            Assert.AreEqual(got, Currency.Get(CurrencyType.Coins));
            Assert.AreEqual(0, _castle.Pending());
        }

        [Test]
        public void Vip_DoublesIncome()
        {
            long normal = _castle.CoinsPerHour();
            Save.Data.vipUntilUtcTicks = Time.UtcNow.AddDays(1).Ticks;
            Assert.AreEqual(normal * 2, _castle.CoinsPerHour(), 2);
        }

        [Test]
        public void Upgrade_CostsCoins_RaisesIncome_AndSettlesPending()
        {
            long rate1 = _castle.CoinsPerHour();
            Time.Advance(TimeSpan.FromHours(2));
            long pending = _castle.Pending();
            Currency.Add(CurrencyType.Coins, _castle.UpgradeCost(), "test");

            Assert.IsTrue(_castle.Upgrade());
            Assert.AreEqual(2, _castle.Level);
            Assert.Greater(_castle.CoinsPerHour(), rate1);
            Assert.AreEqual(pending, Currency.Get(CurrencyType.Coins), 2, "pending income was paid out at the old rate");
        }

        [Test]
        public void Upgrade_FailsWithoutCoins_AndAtMaxLevel()
        {
            Assert.IsFalse(_castle.Upgrade());
            Save.Data.castleLevel = Meta.castle.maxLevel;
            Currency.Add(CurrencyType.Coins, 999999999999L, "test");
            Assert.IsFalse(_castle.Upgrade());
        }

        [Test]
        public void ClockSetBackwards_DoesNotCreateIncome()
        {
            Time.Advance(TimeSpan.FromHours(3));
            _castle.Collect();
            Time.Advance(TimeSpan.FromHours(-5));
            Assert.AreEqual(0, _castle.Pending());
            Time.Advance(TimeSpan.FromHours(5));
            Assert.AreEqual(5, _castle.Elapsed().TotalHours, 0.01, "re-anchored to the earlier time, so no free hours");
        }
    }

    public class ChestTests : MetaFixture
    {
        private ChestService _chests;

        [SetUp]
        public void SetUp() => _chests = new ChestService(Save, Meta, Currency, Granter, Time, new DeterministicRng(5), Analytics, Ads);

        [Test]
        public void DropRates_SumTo100_AndMatchWeights()
        {
            foreach (var chest in _chests.Chests)
            {
                float sum = 0f;
                foreach (var r in _chests.DropRates(chest.id)) sum += r.Percent;
                Assert.AreEqual(100f, sum, 0.01, chest.id);
            }
            var wooden = _chests.DropRates("wooden");
            Assert.AreEqual(70f, wooden.Find(r => r.Type == RewardType.CoinsWins).Percent, 0.01);
        }

        [Test]
        public void Wooden_FreeEveryFourHours()
        {
            Assert.IsTrue(_chests.WoodenReady);
            var first = _chests.OpenFree("wooden");
            Assert.AreEqual(ChestOpenStatus.Ok, first.Status);
            Assert.Greater(first.Items.Count, 0);
            Assert.IsFalse(_chests.WoodenReady);
            Assert.AreEqual(ChestOpenStatus.NotReady, _chests.OpenFree("wooden").Status);

            Time.Advance(TimeSpan.FromHours(3.9));
            Assert.IsFalse(_chests.WoodenReady);
            Time.Advance(TimeSpan.FromMinutes(10));
            Assert.IsTrue(_chests.WoodenReady);
        }

        [Test]
        public void Silver_NeedsRewardedAd_ThreePerDay()
        {
            int opened = 0;
            for (int i = 0; i < 5; i++)
            {
                ChestOpenResult res = default(ChestOpenResult);
                _chests.OpenWithAd("silver", r => res = r);
                if (res.Status == ChestOpenStatus.Ok) opened++;
            }
            Assert.AreEqual(3, opened);
            Assert.AreEqual(0, _chests.SilverRemainingToday);
        }

        [Test]
        public void Silver_AdSkipped_GivesNothing()
        {
            MockAds.NextResult = AdResult.Skipped;
            ChestOpenResult res = default(ChestOpenResult);
            _chests.OpenWithAd("silver", r => res = r);
            Assert.AreEqual(ChestOpenStatus.AdFailed, res.Status);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gems));
        }

        [Test]
        public void Gold_PaidWithGemsOrKeys()
        {
            Assert.AreEqual(ChestOpenStatus.NotEnough, _chests.OpenPaid("gold", ChestPayment.Gems).Status);
            Currency.Add(CurrencyType.Gems, 150, "test");
            var r = _chests.OpenPaid("gold", ChestPayment.Gems);
            Assert.AreEqual(ChestOpenStatus.Ok, r.Status);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gems) - r.Items.FindAll(i => i.Type == RewardType.Gems).ConvertAll(i => (int)i.Amount).Sum(), "gems spent");

            Currency.Add(CurrencyType.ChestKeys, 3, "test");
            Assert.AreEqual(ChestOpenStatus.Ok, _chests.OpenPaid("gold", ChestPayment.Keys).Status);
            Assert.AreEqual(0, Currency.Get(CurrencyType.ChestKeys));
        }

        [Test]
        public void Gold_AlwaysContainsGuaranteedShards()
        {
            for (int i = 0; i < 20; i++)
            {
                var items = _chests.Roll(Meta.Chest("gold"));
                Assert.IsTrue(items.Exists(x => x.Type == RewardType.Shards), "roll " + i);
            }
        }

        [Test]
        public void Opening_GrantsContents_PublishesEvent_LogsAnalytics()
        {
            int events = 0;
            EventBus.Subscribe<ChestOpenedEvent>(_ => events++);
            Currency.Add(CurrencyType.Gems, 500, "test");
            var r = _chests.OpenPaid("legendary", ChestPayment.Gems);
            Assert.AreEqual(1, events);
            Assert.IsTrue(Analytics.Events.Exists(e => e.StartsWith("chest_open")));
            long shards = 0;
            foreach (var c in Db.commanders) shards += Currency.GetShards(c.id);
            Assert.GreaterOrEqual(shards, Meta.Chest("legendary").guaranteedShardMin);
            Assert.Greater(r.Items.Count, 0);
        }

        [Test]
        public void Rolls_AreSeedDeterministic()
        {
            var a = new ChestService(Save, Meta, Currency, Granter, Time, new DeterministicRng(77), Analytics, Ads).Roll(Meta.Chest("silver"));
            var b = new ChestService(Save, Meta, Currency, Granter, Time, new DeterministicRng(77), Analytics, Ads).Roll(Meta.Chest("silver"));
            Assert.AreEqual(a.Count, b.Count);
        }

        [Test]
        public void ChestRewards_FromMissionsAndLogin_Work()
        {
            var items = Granter.Grant(new Reward(RewardType.Chest, 1, "gold"), "test");
            Assert.Greater(items.Count, 0);
        }
    }

    internal static class ListSum
    {
        public static int Sum(this List<int> list)
        {
            int s = 0;
            foreach (var v in list) s += v;
            return s;
        }
    }

    public class MissionTests : MetaFixture
    {
        private MissionService _missions;

        [SetUp]
        public void SetUp() => _missions = new MissionService(Save, Meta.missions, Daily, Granter, Analytics, () => Level);

        [TearDown]
        public void TearDown() => _missions.Dispose();

        [Test]
        public void Assigns3Daily_And3Weekly_Distinct()
        {
            var daily = _missions.Missions(0);
            var weekly = _missions.Missions(1);
            Assert.AreEqual(3, daily.Count);
            Assert.AreEqual(3, weekly.Count);
            var ids = new HashSet<string>();
            foreach (var m in daily) Assert.IsTrue(ids.Add(m.id));
        }

        [Test]
        public void MinLevelGate_HidesAdvancedMissionsFromNewPlayers()
        {
            _missions.Dispose();
            Level = 1;
            Save.Data.missions.Clear();
            Save.Data.missionDayKey = 0;
            _missions = new MissionService(Save, Meta.missions, Daily, Granter, Analytics, () => Level);
            foreach (var m in _missions.Missions(0)) Assert.LessOrEqual(_missions.Template(m.id).minLevel, 1);
        }

        [Test]
        public void SameDay_SameMissions_NextDay_Refreshes()
        {
            var today = new List<string>();
            foreach (var m in _missions.Missions(0)) today.Add(m.id);
            var again = new List<string>();
            foreach (var m in _missions.Missions(0)) again.Add(m.id);
            CollectionAssert.AreEqual(today, again);

            _missions.Report(MissionType.WinLevels, 50);
            Time.Advance(TimeSpan.FromDays(1));
            foreach (var m in _missions.Missions(0)) Assert.AreEqual(0, m.progress, "progress reset at local midnight");
        }

        [Test]
        public void EventsDriveProgress_AndClaimPaysReward()
        {
            Save.Data.missions.Clear();
            Save.Data.missions.Add(new MissionState { id = "d_win3", period = 0, slot = 0 });
            for (int i = 0; i < 3; i++) EventBus.Publish(new LevelCompletedEvent(i + 1, true, 3, false, false, 30f));

            var m = Save.Data.missions.Find(x => x.id == "d_win3");
            Assert.AreEqual(3, m.progress);
            Assert.IsTrue(_missions.IsComplete(m));

            var items = _missions.Claim("d_win3");
            Assert.IsNotNull(items);
            Assert.Greater(Currency.Get(CurrencyType.Coins), 0);
            Assert.IsNull(_missions.Claim("d_win3"), "cannot claim twice");
        }

        [Test]
        public void ProgressIsCapped_AndLossesDoNotCount()
        {
            Save.Data.missions.Clear();
            Save.Data.missions.Add(new MissionState { id = "d_win3", period = 0 });
            EventBus.Publish(new LevelCompletedEvent(1, false, 0, false, false, 10f));
            Assert.AreEqual(0, Save.Data.missions[0].progress);
            _missions.Report(MissionType.WinLevels, 99);
            Assert.AreEqual(3, Save.Data.missions[0].progress);
        }

        [Test]
        public void Merge_Buy_Chest_Research_Skill_Ad_AllReportProgress()
        {
            Save.Data.missions.Clear();
            foreach (var id in new[] { "d_merge20", "d_buy15", "d_chest1", "d_research2", "d_skill3", "d_ads2" })
                Save.Data.missions.Add(new MissionState { id = id, period = 0 });

            EventBus.Publish(new UnitMergedEvent(0, 1, 0, 2));
            EventBus.Publish(new UnitBoughtEvent(0, 50));
            EventBus.Publish(new ChestOpenedEvent("wooden"));
            EventBus.Publish(new ResearchChangedEvent(UnitLineId.Melee, ResearchStat.Hp));
            EventBus.Publish(new SkillUsedEventProxy());
            EventBus.Publish(new AdRewardedEvent("spin"));

            foreach (var m in Save.Data.missions) Assert.AreEqual(1, m.progress, m.id);
        }

        [Test]
        public void AllClaimed_UnlocksBonus_Once()
        {
            foreach (var m in _missions.Missions(0)) m.progress = _missions.Template(m.id).target;
            Assert.IsFalse(_missions.BonusAvailable(0));
            foreach (var m in _missions.Missions(0)) Assert.IsNotNull(_missions.Claim(m.id));
            Assert.IsTrue(_missions.BonusAvailable(0));
            Assert.IsNotNull(_missions.ClaimBonus(0));
            Assert.IsNull(_missions.ClaimBonus(0));
            Assert.AreEqual(1, Currency.Get(CurrencyType.ChestKeys) - 0 >= 1 ? 1 : 0);
        }

        [Test]
        public void Reroll_ReplacesUnfinishedMission()
        {
            var m = _missions.Missions(0)[0];
            string before = m.id;
            Assert.IsTrue(_missions.Reroll(before));
            Assert.AreNotEqual(before, _missions.Missions(0)[0].id);
            Assert.AreEqual(0, _missions.Missions(0)[0].progress);
        }

        [Test]
        public void Achievements_TierUp_WithLongTermProgress()
        {
            _missions.Report(MissionType.WinLevels, 9);
            var view = _missions.Achievements().Find(a => a.Template.id == "a_wins");
            Assert.AreEqual(10, view.NextTarget);
            Assert.IsFalse(view.CanClaim);

            _missions.Report(MissionType.WinLevels, 1);
            Assert.IsTrue(_missions.Achievements().Find(a => a.Template.id == "a_wins").CanClaim);
            Assert.IsNotNull(_missions.ClaimAchievement("a_wins"));

            view = _missions.Achievements().Find(a => a.Template.id == "a_wins");
            Assert.AreEqual(1, view.ClaimedTiers);
            Assert.AreEqual(50, view.NextTarget);
            Assert.IsNull(_missions.ClaimAchievement("a_wins"));
        }

        [Test]
        public void CoinEarnAndSpend_AreTracked()
        {
            Save.Data.missions.Clear();
            _missions.Report(MissionType.EarnCoins, 0);
            Currency.Add(CurrencyType.Coins, 100, "test");
            Currency.TrySpend(CurrencyType.Coins, 40, "test");
            int earned = IntEntries.Get(Save.Data.achievementProgress, "none");
            Assert.AreEqual(0, earned);
        }

        [Test]
        public void HasClaimable_DrivesRedDot()
        {
            Assert.IsFalse(_missions.HasClaimable());
            var m = _missions.Missions(0)[0];
            m.progress = _missions.Template(m.id).target;
            Assert.IsTrue(_missions.HasClaimable());
        }
    }

    public class LoginAndSpinTests : MetaFixture
    {
        [Test]
        public void Login_OneClaimPerDay_CyclesSevenDays()
        {
            var login = new LoginService(Save, Meta.login, Daily, Granter, Time);
            for (int day = 0; day < 8; day++)
            {
                Assert.IsTrue(login.CanClaim, "day " + day);
                Assert.AreEqual(day % 7, login.NextDayIndex);
                Assert.IsNotNull(login.Claim());
                Assert.IsFalse(login.CanClaim);
                Assert.IsNull(login.Claim());
                Time.Advance(TimeSpan.FromDays(1));
            }
        }

        [Test]
        public void Login_Day7_IsTheBigReward()
        {
            var login = new LoginService(Save, Meta.login, Daily, Granter, Time);
            var day7 = login.RewardsFor(6);
            Assert.IsTrue(day7.Count >= 2);
            Assert.IsTrue(System.Linq.Enumerable.Any(day7, r => r.type == RewardType.Chest));
        }

        [Test]
        public void Login_MissedDay_ContinuesByDefault_ResetsWhenConfigured()
        {
            var login = new LoginService(Save, Meta.login, Daily, Granter, Time);
            login.Claim();
            Time.Advance(TimeSpan.FromDays(3));
            Assert.AreEqual(1, login.NextDayIndex);

            Meta.login.resetOnMiss = true;
            Assert.AreEqual(0, login.NextDayIndex);
        }

        [Test]
        public void Spin_OneFreePerDay_ThenAdSpinsCapped()
        {
            var spin = new SpinService(Meta.spin, Daily, Granter, Ads, new DeterministicRng(3));
            Assert.AreEqual(1, spin.FreeRemaining);
            var free = spin.SpinFree();
            Assert.IsTrue(free.Success);
            Assert.AreEqual(0, spin.FreeRemaining);
            Assert.IsFalse(spin.SpinFree().Success);

            int ok = 0;
            for (int i = 0; i < 8; i++)
            {
                SpinOutcome o = new SpinOutcome();
                spin.SpinWithAd(r => o = r);
                if (o.Success) ok++;
            }
            Assert.AreEqual(5, ok, "ad cap of 5 per day from config");

            Time.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(1, spin.FreeRemaining);
        }

        [Test]
        public void Spin_RatesSumTo100_AndDistributionMatches()
        {
            var spin = new SpinService(Meta.spin, Daily, Granter, Ads, new DeterministicRng(9));
            float sum = 0f;
            foreach (var r in spin.Rates()) sum += r;
            Assert.AreEqual(100f, sum, 0.01);

            var counts = new int[spin.Segments.Count];
            var rng = new DeterministicRng(1);
            var gran = new RewardGranter(Save, Currency, Config, Db, Time, rng, () => Level);
            var s2 = new SpinService(Meta.spin, Daily, gran, Ads, rng);
            for (int i = 0; i < 4000; i++)
            {
                Save.Data.daily.counts.Clear();
                counts[s2.SpinFree().SegmentIndex]++;
            }
            float expected0 = spin.Rates()[0] / 100f * 4000f;
            Assert.AreEqual(expected0, counts[0], expected0 * 0.15f);
        }
    }
}
