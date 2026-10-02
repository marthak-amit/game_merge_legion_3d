using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Services.Mock;
using MergeLegion.UI;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class MockServicesTests
    {
        [Test]
        public void RemoteConfig_ParsesDefaultsAndFallsBack()
        {
            const string json = "{\"entries\":[{\"key\":\"a\",\"value\":\"12\"},{\"key\":\"b\",\"value\":\"1.5\"},{\"key\":\"c\",\"value\":\"true\"},{\"key\":\"d\",\"value\":\"hi\"}]}";
            var rc = new MockRemoteConfigService(json);
            Assert.AreEqual(12, rc.GetInt("a"));
            Assert.AreEqual(1.5f, rc.GetFloat("b"), 0.0001f);
            Assert.IsTrue(rc.GetBool("c"));
            Assert.AreEqual("hi", rc.GetString("d"));
            Assert.AreEqual(7, rc.GetInt("missing", 7));
            Assert.AreEqual(3, rc.GetInt("d", 3), "unparseable values fall back");
        }

        [Test]
        public void RemoteConfig_OverrideWins()
        {
            var rc = new MockRemoteConfigService("{\"entries\":[{\"key\":\"a\",\"value\":\"1\"}]}");
            rc.SetOverride("a", "9");
            Assert.AreEqual(9, rc.GetInt("a"));
        }

        [Test]
        public void RemoteConfig_Fetch_MarksReadyAndCallsBack()
        {
            var rc = new MockRemoteConfigService();
            bool ok = false;
            rc.Fetch(r => ok = r);
            Assert.IsTrue(rc.IsReady);
            Assert.IsTrue(ok);
        }

        [Test]
        public void ShippedRemoteDefaults_AreValidJson()
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ServiceInstaller.DefaultsResource);
            Assert.IsNotNull(asset);
            var rc = new MockRemoteConfigService(asset.text);
            Assert.Greater(rc.GetFloat(RemoteKeys.SaveAutosaveSeconds), 0f);
            Assert.Greater(rc.GetFloat(RemoteKeys.BootTimeoutSeconds), 0f);
        }

        [Test]
        public void Ads_RewardedCompletes_AndReportsRevenue()
        {
            var ads = new MockAdsService();
            ads.Initialize();
            int revenue = 0;
            ads.RevenuePaid += _ => revenue++;
            AdResult got = AdResult.Failed;

            ads.ShowRewarded("win_3x", r => got = r);

            Assert.AreEqual(AdResult.Completed, got);
            Assert.AreEqual(1, revenue);
        }

        [Test]
        public void Ads_NoAdsPurchase_BlocksInterstitialAndBannerButNotRewarded()
        {
            var ads = new MockAdsService();
            ads.Initialize();
            ads.ForcedAdsRemoved = true;

            AdResult inter = AdResult.Completed;
            ads.ShowInterstitial("level_end", r => inter = r);
            ads.ShowBanner("home");
            AdResult rewarded = AdResult.Failed;
            ads.ShowRewarded("revive", r => rewarded = r);

            Assert.AreEqual(AdResult.NotReady, inter);
            Assert.IsFalse(ads.BannerVisible);
            Assert.AreEqual(AdResult.Completed, rewarded);
        }

        [Test]
        public void Ads_BeforeInitialize_NotReady()
        {
            var ads = new MockAdsService();
            AdResult got = AdResult.Completed;
            ads.ShowRewarded("x", r => got = r);
            Assert.AreEqual(AdResult.NotReady, got);
        }

        [Test]
        public void IAP_PurchaseKnownSku_SucceedsAndIsOwned()
        {
            var iap = new MockIAPService();
            iap.Initialize(new[] { "no_ads" });
            PurchaseResult res = default;
            iap.Purchase("no_ads", r => res = r);
            Assert.IsTrue(res.Success);
            Assert.IsTrue(iap.IsOwned("no_ads"));
        }

        [Test]
        public void IAP_UnknownSku_Fails()
        {
            var iap = new MockIAPService();
            iap.Initialize(new[] { "no_ads" });
            PurchaseResult res = default;
            iap.Purchase("nope", r => res = r);
            Assert.AreEqual(PurchaseStatus.Failed, res.Status);
        }

        [Test]
        public void Auth_SignIn_ExposesPlayerId()
        {
            var auth = new MockAuthService("p1");
            Assert.IsNull(auth.PlayerId);
            auth.SignInAnonymously(_ => { });
            Assert.AreEqual("p1", auth.PlayerId);
        }

        [Test]
        public void Leaderboard_SubmitScore_RanksPlayerAndKeepsBest()
        {
            var auth = new MockAuthService("me");
            auth.SignInAnonymously(_ => { });
            var lb = new MockLeaderboardService(auth);

            lb.SubmitScore("campaign", 1000, _ => { });
            lb.SubmitScore("campaign", 5, _ => { });

            LeaderboardEntry me = null;
            lb.GetPlayerEntry("campaign", e => me = e);
            Assert.AreEqual(1000, me.Score);
            Assert.AreEqual(1, me.Rank);

            IReadOnlyList<LeaderboardEntry> top = null;
            lb.GetTop("campaign", 3, t => top = t);
            Assert.AreEqual(3, top.Count);
            Assert.AreEqual("me", top[0].PlayerId);
        }

        [Test]
        public void CloudSave_RoundTripsThroughStorage()
        {
            var cloud = new MockCloudSaveService(new InMemorySaveStorage());
            CloudLoadResult empty = default;
            cloud.Load(r => empty = r);
            Assert.AreEqual(CloudLoadStatus.NotFound, empty.Status);

            cloud.Save("{\"x\":1}", _ => { });
            CloudLoadResult got = default;
            cloud.Load(r => got = r);
            Assert.AreEqual(CloudLoadStatus.Ok, got.Status);
            Assert.AreEqual("{\"x\":1}", got.Json);
        }

        [Test]
        public void Push_ScheduleAndCancel()
        {
            var push = new MockPushService();
            push.ScheduleLocal("chest", "t", "b", new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
            Assert.AreEqual(1, push.Scheduled.Count);
            push.CancelLocal("chest");
            Assert.AreEqual(0, push.Scheduled.Count);
        }

        [Test]
        public void InstallCore_RegistersTimeSaveAndRemoteConfig()
        {
            ServiceLocator.Clear();
            var save = ServiceInstaller.InstallCore(new InMemorySaveStorage(), new FakeTimeService(), "{\"entries\":[]}");
            ServiceInstaller.InstallPlatformServices(save);

            Assert.IsTrue(ServiceLocator.Has<ITimeService>());
            Assert.IsTrue(ServiceLocator.Has<SaveService>());
            Assert.IsTrue(ServiceLocator.Has<IRemoteConfigService>());
            Assert.IsTrue(ServiceLocator.Has<IAnalyticsService>());
            Assert.IsTrue(ServiceLocator.Has<IAdsService>());
            Assert.IsTrue(ServiceLocator.Has<IIAPService>());
            Assert.IsTrue(ServiceLocator.Has<IAuthService>());
            Assert.IsTrue(ServiceLocator.Has<ICloudSaveService>());
            Assert.IsTrue(ServiceLocator.Has<ILeaderboardService>());
            Assert.IsTrue(ServiceLocator.Has<IPushService>());
            Assert.IsTrue(ServiceLocator.Has<IAttributionService>());
            Assert.IsTrue(ServiceLocator.Has<IConsentService>());
            ServiceLocator.Clear();
        }

        [Test]
        public void Loc_ReturnsValueOrMarkedKey()
        {
            Loc.Reset();
            Loc.Parse("{\"entries\":[{\"key\":\"k\",\"value\":\"Hello {0}\"}]}");
            Assert.AreEqual("Hello Bob", Loc.Format("k", "Bob"));
            Assert.AreEqual("#missing", Loc.Get("missing"));
            Loc.Reset();
        }

        [Test]
        public void ShippedEnglishTable_HasHomeKeys()
        {
            Loc.Reset();
            Loc.Load("en");
            Assert.AreNotEqual("#home.battle", Loc.Get("home.battle"));
            Assert.AreNotEqual("#game.title", Loc.Get("game.title"));
            Loc.Reset();
        }
    }
}
