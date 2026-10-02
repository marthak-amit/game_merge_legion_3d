using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Meta.Arena;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Services.Mock;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class ArenaMathTests
    {
        private readonly ArenaConfig _cfg = new ArenaConfig();

        [Test]
        public void EqualTrophies_SplitEvenly()
        {
            Assert.AreEqual(0.5f, ArenaMath.ExpectedWin(1000, 1000, 400f), 0.001);
            Assert.AreEqual(18, ArenaMath.WinGain(_cfg, 1000, 1000));
            Assert.AreEqual(18, ArenaMath.LossCost(_cfg, 1000, 1000));
        }

        [Test]
        public void BeatingStrongerOpponentsPaysMore_LosingToWeakerCostsMore()
        {
            Assert.Greater(ArenaMath.WinGain(_cfg, 1000, 1400), ArenaMath.WinGain(_cfg, 1000, 1000));
            Assert.Greater(ArenaMath.LossCost(_cfg, 1000, 600), ArenaMath.LossCost(_cfg, 1000, 1000));
            Assert.GreaterOrEqual(ArenaMath.WinGain(_cfg, 3000, 0), _cfg.minGain);
            Assert.GreaterOrEqual(ArenaMath.LossCost(_cfg, 0, 3000), _cfg.minLoss);
        }
    }

    public class ArenaServiceTests : MetaFixture
    {
        private MockArenaBackend _backend;
        private ArenaService _arena;
        private ArmyService _army;
        private MockAuthService _auth;
        private MockLeaderboardService _boards;
        private ArenaConfig _cfg;
        private CommanderService _commanders;

        [SetUp]
        public void SetUp()
        {
            _cfg = ArenaConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/arena_config").text);
            _backend = new MockArenaBackend(Db, _cfg);
            _auth = new MockAuthService("me");
            _auth.SignInAnonymously(null);
            _boards = new MockLeaderboardService(_auth);
            _army = new ArmyService(new GridModel(5, 3), Save, Currency, Config, Db, new DeterministicRng(2), Analytics);
            _commanders = new CommanderService(Save, Currency, Db);
            Save.Data.highestCampaignLevel = 20;
            _army.SpawnFree(UnitLineId.Melee, 4);
            _army.SpawnFree(UnitLineId.Ranged, 4);
            _army.SpawnFree(UnitLineId.Melee, 3);
            _arena = new ArenaService(Save, _cfg, _backend, _army, NullResearch.Instance, _commanders, Db, Granter, Daily, Time, Analytics, _auth, _boards);
        }

        [Test]
        public void ConfigHasSevenLeagues_BronzeToLegend()
        {
            Assert.AreEqual(7, _cfg.leagues.Count);
            Assert.AreEqual("bronze", _cfg.leagues[0].id);
            Assert.AreEqual("legend", _cfg.leagues[6].id);
            for (int i = 1; i < 7; i++) Assert.Greater(_cfg.leagues[i].minTrophies, _cfg.leagues[i - 1].minTrophies);
            Assert.AreEqual(0, _cfg.LeagueIndexFor(299));
            Assert.AreEqual(1, _cfg.LeagueIndexFor(300));
            Assert.AreEqual(6, _cfg.LeagueIndexFor(99999));
        }

        [Test]
        public void Unlocks_AtConfiguredLevel()
        {
            Save.Data.highestCampaignLevel = 5;
            Assert.IsFalse(_arena.Unlocked);
            Save.Data.highestCampaignLevel = 14;
            Assert.IsTrue(_arena.Unlocked);
        }

        [Test]
        public void Snapshot_CapturesArmyPowerAndOwner()
        {
            var snap = _arena.BuildSnapshot();
            Assert.AreEqual("me", snap.playerId);
            Assert.AreEqual(3, snap.units.Count);
            Assert.Greater(snap.power, 0f);
            Assert.AreEqual(_commanders.EquippedId, snap.commander);
            var json = UnityEngine.JsonUtility.ToJson(snap);
            Assert.AreEqual(3, UnityEngine.JsonUtility.FromJson<ArenaSnapshot>(json).units.Count, "snapshot survives JSON round trip");
        }

        [Test]
        public void Opponents_AreOrderedWeakToStrong_AndAroundPlayerPower()
        {
            List<ArenaSnapshot> opps = null;
            _arena.FindOpponents(o => opps = o);
            Assert.AreEqual(3, opps.Count);
            Assert.LessOrEqual(opps[0].power, opps[1].power);
            Assert.LessOrEqual(opps[1].power, opps[2].power);
            float mine = _arena.BuildSnapshot().power;
            Assert.Less(opps[0].power, mine * 1.05f);
            Assert.Greater(opps[2].power, mine * 0.9f);
            foreach (var o in opps)
            {
                Assert.LessOrEqual(o.units.Count, 15);
                var cells = new HashSet<int>();
                foreach (var u in o.units) Assert.IsTrue(cells.Add(u.row * 5 + u.col), "no overlapping cells");
            }
        }

        [Test]
        public void RealSnapshotsFromOtherPlayers_AppearInMatchmaking()
        {
            var other = _arena.BuildSnapshot();
            other.playerId = "someone_else";
            other.name = "RealPlayer";
            other.isBot = false;
            _backend.Upload(other, null);

            List<ArenaSnapshot> opps = null;
            _arena.FindOpponents(o => opps = o);
            Assert.IsTrue(opps.Exists(o => o.playerId == "someone_else"));
        }

        [Test]
        public void OwnSnapshot_IsNeverServedBackToYou()
        {
            _arena.UploadSnapshot();
            Assert.AreEqual(1, _backend.UploadCount);
            List<ArenaSnapshot> opps = null;
            _arena.FindOpponents(o => opps = o);
            Assert.IsFalse(opps.Exists(o => o.playerId == "me"));
        }

        [Test]
        public void Upload_RefusedWhenLockedOrTampered()
        {
            Save.Data.tamperDetected = true;
            bool ok = true;
            _arena.UploadSnapshot(r => ok = r);
            Assert.IsFalse(ok);
        }

        [Test]
        public void Attempts_ConsumeAndRefill()
        {
            Assert.AreEqual(5, _arena.Attempts);
            var opp = _backend.MakeBot(_arena.BuildSnapshot(), 1f, 1);
            for (int i = 0; i < 5; i++) Assert.IsTrue(_arena.TryStartMatch(opp));
            Assert.IsFalse(_arena.TryStartMatch(opp));
            Assert.AreEqual(0, _arena.Attempts);
            Assert.Greater(_arena.UntilNextAttempt().TotalMinutes, 0);

            Time.Advance(TimeSpan.FromMinutes(31));
            Assert.AreEqual(1, _arena.Attempts);
            Time.Advance(TimeSpan.FromHours(5));
            Assert.AreEqual(5, _arena.Attempts, "capped at max");
        }

        [Test]
        public void WinAndLoss_MoveTrophies_NeverBelowZero_AndSubmitLeaderboard()
        {
            var opp = _backend.MakeBot(_arena.BuildSnapshot(), 1f, 1);
            _arena.TryStartMatch(opp);
            var win = _arena.ReportResult(true);
            Assert.IsTrue(win.Won);
            Assert.Greater(win.TrophyDelta, 0);
            Assert.AreEqual(win.NewTrophies, _arena.Trophies);
            Assert.AreEqual(1, Save.Data.arenaWins);

            _arena.TryStartMatch(opp);
            for (int i = 0; i < 10; i++) { _arena.TryStartMatch(opp); _arena.ReportResult(false); }
            Assert.GreaterOrEqual(_arena.Trophies, 0);

            LeaderboardEntry me = null;
            _boards.GetPlayerEntry(LeaderboardIds.ArenaTrophies, e => me = e);
            Assert.IsNotNull(me);
            Assert.IsTrue(Analytics.Events.Exists(e => e.StartsWith("arena_match")));
        }

        [Test]
        public void ClimbingLeagues_IsReported()
        {
            Save.Data.arenaTrophies = 295;
            var opp = _backend.MakeBot(_arena.BuildSnapshot(), 1f, 1);
            opp.trophies = 295;
            _arena.TryStartMatch(opp);
            var r = _arena.ReportResult(true);
            Assert.IsTrue(r.LeagueChanged);
            Assert.AreEqual("silver", _arena.League.id);
            Assert.AreEqual("gold", _arena.NextLeague.id);
        }

        [Test]
        public void WinPaysCoins_LossPaysLess()
        {
            var opp = _backend.MakeBot(_arena.BuildSnapshot(), 1f, 1);
            _arena.TryStartMatch(opp);
            _arena.ReportResult(true);
            long afterWin = Currency.Get(CurrencyType.Coins);
            _arena.TryStartMatch(opp);
            _arena.ReportResult(false);
            long lossCoins = Currency.Get(CurrencyType.Coins) - afterWin;
            Assert.Greater(afterWin, 0);
            Assert.Less(lossCoins, afterWin);
        }

        [Test]
        public void WeeklyReward_BanksPeakLeague_AndIsClaimedOnce()
        {
            Time.UtcNow = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Local).ToUniversalTime();
            _arena.RolloverWeek();
            Save.Data.arenaTrophies = 750;     // gold
            Save.Data.arenaWins = 3;
            Save.Data.arenaWeekPeakTrophies = 750;
            Assert.IsFalse(_arena.WeeklyRewardAvailable);

            Time.Advance(TimeSpan.FromDays(7));
            Assert.IsTrue(_arena.WeeklyRewardAvailable);
            Assert.AreEqual("gold", _arena.UnclaimedLeague.id);
            var items = _arena.ClaimWeeklyReward();
            Assert.Greater(items.Count, 0);
            Assert.IsNull(_arena.ClaimWeeklyReward());
            Assert.IsFalse(_arena.WeeklyRewardAvailable);
        }

        [Test]
        public void NoMatchesPlayed_NoWeeklyReward()
        {
            _arena.RolloverWeek();
            Time.Advance(TimeSpan.FromDays(7));
            Assert.IsFalse(_arena.WeeklyRewardAvailable);
        }

        [Test]
        public void BuildLevel_MirrorsSnapshotIntoEnemyFormation()
        {
            var snap = _arena.BuildSnapshot();
            snap.hpBonus = 0.5f;
            snap.dmgBonus = 0.25f;
            var level = ArenaService.BuildLevel(snap, 20);
            Assert.AreEqual(snap.units.Count, level.enemies.Count);
            Assert.AreEqual(1.5f, level.hpMult, 0.001);
            Assert.AreEqual(1.25f, level.dmgMult, 0.001);
            Assert.IsFalse(level.hasBoss);
        }

        [Test]
        public void ArenaBattle_PlaysOutAgainstASnapshot()
        {
            var opp = _backend.MakeBot(_arena.BuildSnapshot(), 0.5f, 3);
            var level = ArenaService.BuildLevel(opp, 20);
            var layout = new ArenaLayout(Config.grid);
            var sim = BattleFactoryHelper.Create(Config, Db, layout, level, _army);
            for (int i = 0; i < 5000 && sim.Outcome == Battle.BattleOutcome.Running; i++) { sim.Tick(Config.battle.fixedStep); sim.Drain(); }
            Assert.AreNotEqual(Battle.BattleOutcome.Running, sim.Outcome);
            Assert.AreEqual(Battle.BattleOutcome.Win, sim.Outcome, "half-power bot should lose to the player's army");
        }
    }

    internal static class BattleFactoryHelper
    {
        public static Battle.BattleSim Create(GameConfig cfg, GameDatabase db, ArenaLayout layout, LevelDefinition level, ArmyService army) =>
            Battle.BattleFactory.Create(cfg, db, layout, level, army.BuildBattleArmy(), NullResearch.Instance, new CommanderBonuses(), 4, 1);
    }

    public class CloudSyncTests
    {
        private FakeTimeService _time;
        private SaveService _save;
        private InMemorySaveStorage _cloudStorage;
        private MockCloudSaveService _cloud;
        private MockAuthService _auth;
        private CloudSyncService _sync;

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
            _time = new FakeTimeService();
            _save = new SaveService(new InMemorySaveStorage(), _time);
            _save.Load();
            _cloudStorage = new InMemorySaveStorage();
            _cloud = new MockCloudSaveService(_cloudStorage);
            _auth = new MockAuthService("p");
            _auth.SignInAnonymously(null);
            _sync = new CloudSyncService(_save, _cloud, _auth, _time, new MockAnalyticsService { LogToConsole = false });
        }

        [TearDown]
        public void TearDown() => _sync.Dispose();

        private void PutCloud(int level)
        {
            var other = new SaveService(new InMemorySaveStorage(), _time);
            other.Load();
            other.Data.highestCampaignLevel = level;
            other.Data.gems = 77;
            _cloudStorage.Write(other.ExportJson());
        }

        private SyncOutcome Sync()
        {
            var outcome = SyncOutcome.Failed;
            _sync.SyncOnBoot(o => outcome = o);
            return outcome;
        }

        [Test]
        public void NoCloudSave_UploadsLocal()
        {
            _save.Data.highestCampaignLevel = 12;
            Assert.AreEqual(SyncOutcome.UploadedLocal, Sync());
            Assert.AreEqual(1, _cloudStorage.ReadAll().Count);
        }

        [Test]
        public void FreshDevice_AdoptsCloudWithoutPrompt()
        {
            PutCloud(40);
            bool prompted = false;
            _sync.ConflictPrompt = (l, c, cb) => prompted = true;
            int replaced = 0;
            EventBus.Subscribe<SaveReplacedEvent>(_ => replaced++);

            Assert.AreEqual(SyncOutcome.AdoptedCloud, Sync());
            Assert.IsFalse(prompted);
            Assert.AreEqual(40, _save.Data.highestCampaignLevel);
            Assert.AreEqual(77, _save.Data.gems);
            Assert.AreEqual(1, replaced);
        }

        [Test]
        public void BothHaveProgress_PromptsPlayer_AndHonoursChoice()
        {
            _save.Data.highestCampaignLevel = 10;
            PutCloud(40);
            SaveData seenLocal = null, seenCloud = null;
            _sync.ConflictPrompt = (l, c, cb) => { seenLocal = l; seenCloud = c; cb(MergeChoice.UseLocal); };

            Assert.AreEqual(SyncOutcome.UploadedLocal, Sync());
            Assert.AreEqual(10, seenLocal.highestCampaignLevel);
            Assert.AreEqual(40, seenCloud.highestCampaignLevel);
            Assert.AreEqual(10, _save.Data.highestCampaignLevel, "player kept the device save");
        }

        [Test]
        public void BothHaveProgress_PlayerPicksCloud()
        {
            _save.Data.highestCampaignLevel = 10;
            PutCloud(40);
            _sync.ConflictPrompt = (l, c, cb) => cb(MergeChoice.UseCloud);
            Assert.AreEqual(SyncOutcome.AdoptedCloud, Sync());
            Assert.AreEqual(40, _save.Data.highestCampaignLevel);
        }

        [Test]
        public void NoPromptHandler_HigherProgressWins()
        {
            _save.Data.highestCampaignLevel = 50;
            PutCloud(40);
            _sync.ConflictPrompt = null;
            Assert.AreEqual(SyncOutcome.UploadedLocal, Sync());
            Assert.AreEqual(50, _save.Data.highestCampaignLevel);
        }

        [Test]
        public void Offline_DoesNothing()
        {
            _auth.DeleteAccount(null);
            Assert.AreEqual(SyncOutcome.Offline, Sync());
        }

        [Test]
        public void TamperedLocalSave_IsNeverUploaded()
        {
            _save.Data.tamperDetected = true;
            bool ok = true;
            _sync.Upload(true, r => ok = r);
            Assert.IsFalse(ok);
            Assert.AreEqual(0, _cloudStorage.ReadAll().Count);
        }

        [Test]
        public void AutoUpload_OnWin_IsRateLimited_PauseForcesIt()
        {
            _save.Data.highestCampaignLevel = 3;
            EventBus.Publish(new LevelCompletedEvent(3, true, 3, false, false, 10f));
            Assert.AreEqual(1, _cloudStorage.ReadAll().Count);

            _save.Data.highestCampaignLevel = 4;
            EventBus.Publish(new LevelCompletedEvent(4, true, 3, false, false, 10f));
            Assert.AreEqual(1, _cloudStorage.ReadAll().Count, "within the 60s window");

            EventBus.Publish(new AppPauseEvent(true));
            Assert.AreEqual(2, _cloudStorage.ReadAll().Count, "pause flushes immediately");
        }

        [Test]
        public void DeleteEverything_WipesCloudAccountAndLocalData()
        {
            _save.Data.coins = 500;
            _sync.Upload(true, null);
            bool ok = false;
            _sync.DeleteEverything(r => ok = r);
            Assert.IsTrue(ok);
            Assert.AreEqual(0, _cloudStorage.ReadAll().Count);
            Assert.IsFalse(_auth.IsSignedIn);
            Assert.AreEqual(0, _save.Data.coins);
        }
    }

    public class LeaderboardReporterTests
    {
        [Test]
        public void Wins_SubmitCampaignAndEndlessProgress()
        {
            EventBus.ClearAll();
            var time = new FakeTimeService();
            var save = new SaveService(new InMemorySaveStorage(), time);
            save.Load();
            var auth = new MockAuthService("p");
            auth.SignInAnonymously(null);
            var boards = new MockLeaderboardService(auth);
            using (new LeaderboardReporter(save, boards))
            {
                save.Data.highestCampaignLevel = 33;
                EventBus.Publish(new LevelCompletedEvent(33, true, 3, false, false, 10f));
                save.Data.endlessBestWave = 7;
                EventBus.Publish(new LevelCompletedEvent(207, true, 3, false, true, 10f));
                EventBus.Publish(new LevelCompletedEvent(208, false, 0, false, true, 10f));
            }
            LeaderboardEntry camp = null, endless = null;
            boards.GetPlayerEntry(LeaderboardIds.CampaignLevel, e => camp = e);
            boards.GetPlayerEntry(LeaderboardIds.EndlessWave, e => endless = e);
            Assert.AreEqual(33, camp.Score);
            Assert.AreEqual(7, endless.Score);
        }
    }

    public class PlatformOverridesTests
    {
        [TearDown]
        public void TearDown() => PlatformServiceOverrides.ResetForTests();

        [Test]
        public void CompositeAnalytics_FansOut_AndIsolatesFailures()
        {
            var good = new MockAnalyticsService { LogToConsole = false };
            var composite = new CompositeAnalyticsService(new List<IAnalyticsService> { new ThrowingAnalytics(), good });
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("sdk down"));
            composite.LogEvent("level_start");
            Assert.AreEqual(1, good.Events.Count);
        }

        private sealed class ThrowingAnalytics : IAnalyticsService
        {
            public void Initialize() { }
            public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null) { throw new InvalidOperationException("sdk down"); }
            public void SetUserProperty(string key, string value) { }
            public void SetUserId(string playerId) { }
        }

        [Test]
        public void Installer_UsesOverrides_WhenRegistered_ElseMocks()
        {
            ServiceLocator.Clear();
            PlatformServiceOverrides.Ads = () => new CustomAds();
            var save = ServiceInstaller.InstallCore(new InMemorySaveStorage(), new FakeTimeService(), "{\"entries\":[]}");
            ServiceInstaller.InstallPlatformServices(save);
            Assert.IsInstanceOf<CustomAds>(ServiceLocator.Get<IAdsService>());
            Assert.IsInstanceOf<MockIAPService>(ServiceLocator.Get<IIAPService>());
            ServiceLocator.Clear();
        }

        private sealed class CustomAds : IAdsService
        {
            public bool IsInitialized => true;
            public bool ForcedAdsRemoved { get; set; }
            public event Action<AdRevenueInfo> RevenuePaid { add { } remove { } }
            public void Initialize(Action onInitialized = null) { }
            public bool IsRewardedReady(string placement) => false;
            public void ShowRewarded(string placement, Action<AdResult> onComplete) { }
            public bool IsInterstitialReady(string placement) => false;
            public void ShowInterstitial(string placement, Action<AdResult> onComplete) { }
            public void ShowBanner(string placement) { }
            public void HideBanner() { }
        }
    }
}
