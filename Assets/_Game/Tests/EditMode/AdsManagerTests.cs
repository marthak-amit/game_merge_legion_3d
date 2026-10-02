using System;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Services.Mock;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class AdsManagerTests
    {
        private FakeTimeService _time;
        private SaveService _save;
        private MockAdsService _mock;
        private MockAnalyticsService _analytics;
        private GameConfig _cfg;
        private AdsManager _ads;

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
            _time = new FakeTimeService();
            _save = new SaveService(new InMemorySaveStorage(), _time);
            _save.Load();
            _analytics = new MockAnalyticsService { LogToConsole = false };
            _mock = new MockAdsService();
            _mock.Initialize();
            _cfg = GameConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/game_config").text);
            _ads = new AdsManager(_mock, _cfg, new DailyService(_save, _time), _save, _time, _analytics, new MockAttributionService());
        }

        private bool Show(string placement)
        {
            bool got = false;
            _ads.ShowRewarded(placement, r => got = r);
            return got;
        }

        [Test]
        public void Rewarded_GrantsOnCompletion_AndLogsFunnel()
        {
            Assert.IsTrue(Show(AdPlacements.Win3x));
            Assert.IsTrue(_analytics.Events.Exists(e => e.StartsWith("ad_shown")));
            Assert.IsTrue(_analytics.Events.Exists(e => e.StartsWith("ad_rewarded")));
        }

        [Test]
        public void Rewarded_Skipped_NoReward()
        {
            _mock.NextResult = AdResult.Skipped;
            Assert.IsFalse(Show(AdPlacements.Win3x));
            Assert.IsFalse(_analytics.Events.Exists(e => e.StartsWith("ad_rewarded")));
        }

        [Test]
        public void DailyCap_BlocksAfterLimit_ThenResetsAtLocalMidnight()
        {
            for (int i = 0; i < 3; i++) Assert.IsTrue(Show(AdPlacements.SilverChest));
            Assert.AreEqual(0, _ads.Remaining(AdPlacements.SilverChest));
            Assert.AreEqual(AdBlockReason.DailyCap, _ads.CanShowRewarded(AdPlacements.SilverChest));
            Assert.IsFalse(Show(AdPlacements.SilverChest));

            _time.Advance(TimeSpan.FromHours(30));
            Assert.AreEqual(3, _ads.Remaining(AdPlacements.SilverChest));
            Assert.IsTrue(Show(AdPlacements.SilverChest));
        }

        [Test]
        public void Cooldown_BlocksFreeUnitUntilElapsed()
        {
            Assert.IsTrue(Show(AdPlacements.FreeUnit));
            Assert.AreEqual(AdBlockReason.Cooldown, _ads.CanShowRewarded(AdPlacements.FreeUnit));
            Assert.Greater(_ads.CooldownRemaining(AdPlacements.FreeUnit), 100f);
            _time.Advance(TimeSpan.FromMinutes(3.1));
            Assert.AreEqual(AdBlockReason.None, _ads.CanShowRewarded(AdPlacements.FreeUnit));
        }

        [Test]
        public void NotReady_ReportsReason()
        {
            _mock.Ready = false;
            Assert.AreEqual(AdBlockReason.NotReady, _ads.CanShowRewarded(AdPlacements.Win3x));
        }

        [Test]
        public void Interstitial_Rules()
        {
            // never during tutorial
            Assert.IsFalse(_ads.CanShowInterstitial(10));
            _save.Data.SetFlag(SaveFlags.TutorialDone);

            // level gate
            Assert.IsFalse(_ads.CanShowInterstitial(4));
            Assert.IsTrue(_ads.CanShowInterstitial(5));

            // min gap
            Assert.IsTrue(_ads.TryShowInterstitial(6, null));
            Assert.IsFalse(_ads.CanShowInterstitial(6));
            _time.Advance(TimeSpan.FromSeconds(61));
            Assert.IsTrue(_ads.CanShowInterstitial(6));
        }

        [Test]
        public void Interstitial_NeverRightAfterRewarded()
        {
            _save.Data.SetFlag(SaveFlags.TutorialDone);
            Assert.IsTrue(Show(AdPlacements.Win3x));
            Assert.IsFalse(_ads.CanShowInterstitial(10));
            _time.Advance(TimeSpan.FromSeconds(91));
            Assert.IsTrue(_ads.CanShowInterstitial(10));
        }

        [Test]
        public void NoAds_RemovesInterstitialsButKeepsRewarded()
        {
            _save.Data.SetFlag(SaveFlags.TutorialDone);
            _save.Data.noAds = true;
            Assert.IsFalse(_ads.CanShowInterstitial(50));
            Assert.IsTrue(Show(AdPlacements.Win3x));
        }

        [Test]
        public void Vip_RemovesInterstitialsWhileActive()
        {
            _save.Data.SetFlag(SaveFlags.TutorialDone);
            _save.Data.vipUntilUtcTicks = _time.UtcNow.AddDays(1).Ticks;
            Assert.IsTrue(_ads.IsVip);
            Assert.IsFalse(_ads.CanShowInterstitial(50));
            _time.Advance(TimeSpan.FromDays(2));
            Assert.IsFalse(_ads.IsVip);
            Assert.IsTrue(_ads.CanShowInterstitial(50));
        }

        [Test]
        public void Banner_HiddenWithNoAds()
        {
            _ads.SetBannerVisible(true);
            Assert.IsTrue(_mock.BannerVisible);
            _save.Data.noAds = true;
            _ads.SetBannerVisible(true);
            Assert.IsFalse(_mock.BannerVisible);
        }
    }

    public class DailyServiceTests
    {
        [Test]
        public void Counters_ResetAtLocalMidnight_WeeklyAtMonday()
        {
            var time = new FakeTimeService { UtcNow = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Local).ToUniversalTime() }; // a Wednesday
            var save = new SaveService(new InMemorySaveStorage(), time);
            save.Load();
            var daily = new DailyService(save, time);
            daily.Increment("x", 2);
            daily.IncrementWeekly("w", 5);
            Assert.AreEqual(2, daily.Get("x"));

            time.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(0, daily.Get("x"));
            Assert.AreEqual(5, daily.GetWeekly("w"), "same week");

            time.Advance(TimeSpan.FromDays(5)); // next Monday
            Assert.AreEqual(0, daily.GetWeekly("w"));
        }
    }
}
