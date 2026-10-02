using System.Collections;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergeLegion.Tests
{
    /// <summary>Play mode: the real Battle scene, real services (mock SDKs), real frame loop.</summary>
    public class GameLoopPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventBus.ClearAll();
            GameSession.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot != null) Object.Destroy(boot.gameObject);
            ServiceLocator.Clear();
        }

        private static IEnumerator LoadBattle()
        {
            SceneManager.LoadScene(SceneNames.Battle);
            float timeout = 5f;
            while (BattleSceneRoot.Director == null && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
            Assert.IsNotNull(BattleSceneRoot.Director, "Battle scene did not build");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullLevelLoop_Buy_Merge_Fight_Win()
        {
            // a fresh profile: skip the guided tutorial so only the loop itself is under test
            GameBootstrap.EnsureServices();
            ServiceLocator.Get<TutorialService>().SkipAll();
            ServiceLocator.Get<CurrencyService>().Add(CurrencyType.Coins, 5000, "test");

            yield return LoadBattle();
            var director = BattleSceneRoot.Director;
            var army = ServiceLocator.Get<ArmyService>();
            Assert.AreEqual(BattlePhase.Prepare, director.Phase);

            Assert.AreEqual(BuyResult.Ok, army.Buy(UnitLineId.Melee));
            Assert.AreEqual(BuyResult.Ok, army.Buy(UnitLineId.Melee));
            Assert.IsTrue(army.TryFindMergePair(out int a, out int b));
            Assert.AreEqual(DropKind.Merge, army.Drop(a, b).Kind);
            yield return null;

            director.StartFight();
            Assert.AreEqual(BattlePhase.Fighting, director.Phase);

            float timeout = 60f;
            while (director.Phase == BattlePhase.Fighting && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(BattleOutcome.Win, director.Sim.Outcome, "a level-2 unit beats level 1's lone enemy squad");
            yield return null;

            Assert.AreEqual(1, ServiceLocator.Get<SaveService>().Data.highestCampaignLevel, "winning advances the campaign");
            Assert.Greater(ServiceLocator.Get<CurrencyService>().Get(CurrencyType.Coins), 0);
        }

        [UnityTest]
        public IEnumerator BattleResolution_IsDeterministicForTheSameSeed()
        {
            GameBootstrap.EnsureServices();
            var db = GameDatabase.Instance;
            var cfg = ServiceLocator.Get<GameConfig>();
            var layout = new ArenaLayout(cfg.grid);
            var level = ServiceLocator.Get<LevelRepository>().Get(12);
            var army = new System.Collections.Generic.List<BattleSpawn>
            {
                new BattleSpawn { line = 0, level = 3, col = 1, row = 0 }, new BattleSpawn { line = 1, level = 3, col = 3, row = 1 },
                new BattleSpawn { line = 0, level = 2, col = 2, row = 0 }
            };

            string Run()
            {
                var sim = BattleFactory.Create(cfg, db, layout, level, army, Economy.NullResearch.Instance, new Meta.CommanderBonuses(), 3, 777);
                for (int i = 0; i < 4000 && sim.Outcome == BattleOutcome.Running; i++) { sim.Tick(cfg.battle.fixedStep); sim.Drain(); }
                float hp = 0f;
                foreach (var u in sim.Units) hp += u.Hp;
                return sim.Outcome + "|" + sim.Time.ToString("F3") + "|" + hp.ToString("F2");
            }

            Assert.AreEqual(Run(), Run());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tutorial_FirstLevelStepsAreShownInOrder()
        {
            GameBootstrap.EnsureServices();
            var tut = ServiceLocator.Get<TutorialService>();
            Assert.IsTrue(tut.NeedsIntroBattle);

            yield return LoadBattle();
            Assert.AreEqual("buy1", tut.Current("battle", "prepare").id);

            var army = ServiceLocator.Get<ArmyService>();
            army.Buy(UnitLineId.Melee);
            Assert.AreEqual("buy2", tut.Current("battle", "prepare").id);
            army.Buy(UnitLineId.Melee);
            Assert.AreEqual("merge", tut.Current("battle", "prepare").id);
            Assert.IsTrue(army.TryFindMergePair(out int a, out int b));
            army.Drop(a, b);
            Assert.AreEqual("fight", tut.Current("battle", "prepare").id);
            BattleSceneRoot.Director.StartFight();
            Assert.IsTrue(tut.IsDone("fight"));
        }

        [UnityTest]
        public IEnumerator MainScene_ShowsHome_AndNoInterstitialsDuringTutorial()
        {
            GameBootstrap.EnsureServices();
            var ads = ServiceLocator.Get<AdsManager>();
            Assert.IsFalse(ads.CanShowInterstitial(50), "tutorial active");

            SceneManager.LoadScene(SceneNames.Main);
            yield return null;
            yield return null;
            var ui = Object.FindFirstObjectByType<UI.UIManager>();
            Assert.IsNotNull(ui);
            Assert.AreEqual(UI.ScreenId.Home, ui.Current);
        }
    }
}
