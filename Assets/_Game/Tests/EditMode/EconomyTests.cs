using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Save;
using MergeLegion.Services.Mock;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class CostCalculatorTests
    {
        [Test]
        public void Cost_FollowsGrowthCurve()
        {
            Assert.AreEqual(50, CostCalculator.UnitCost(50, 1.15f, 0));
            Assert.AreEqual(58, CostCalculator.UnitCost(50, 1.15f, 1));      // 57.5 -> 58
            Assert.AreEqual(67, CostCalculator.UnitCost(50, 1.15f, 2));      // 66.125 -> 67
        }

        [Test]
        public void Cost_IsMonotonic_AndAppliesDiscount()
        {
            long prev = 0;
            for (int n = 0; n < 60; n++)
            {
                long c = CostCalculator.UnitCost(50, 1.15f, n);
                Assert.Greater(c, prev);
                prev = c;
            }
            Assert.Less(CostCalculator.UnitCost(100, 1.15f, 5, 0.2f), CostCalculator.UnitCost(100, 1.15f, 5, 0f));
            Assert.GreaterOrEqual(CostCalculator.UnitCost(1, 1.0f, 0, 0.9f), 1);
        }
    }

    public class BalanceDataTests
    {
        private static GameDatabase Db() => GameDatabase.BuildFromCsv();

        [Test]
        public void Csv_Parses_QuotesAndComments()
        {
            var t = CsvTable.Parse("# note\na,b,c\n1,\"x,y\",3\r\n4,5,6\n");
            Assert.AreEqual(2, t.RowCount);
            Assert.AreEqual("x,y", t.Str(0, "b"));
            Assert.AreEqual(6, t.Int(1, "c"));
            Assert.AreEqual(9, t.Int(1, "missing", 9));
        }

        [Test]
        public void Database_HasFourLines_EightLevelsEach()
        {
            var db = Db();
            Assert.AreEqual(4, db.lines.Count);
            foreach (var line in db.lines) Assert.AreEqual(8, line.MaxLevel, line.name);
        }

        [Test]
        public void Stats_ScaleAboutNinetenthsPerLevel()
        {
            var db = Db();
            foreach (var line in db.lines)
            {
                for (int l = 2; l <= 8; l++)
                {
                    float hpRatio = line.GetLevel(l).hp / line.GetLevel(l - 1).hp;
                    float dmgRatio = line.GetLevel(l).damage / line.GetLevel(l - 1).damage;
                    Assert.AreEqual(1.9, hpRatio, 0.05, line.name + " hp L" + l);
                    Assert.AreEqual(1.9, dmgRatio, 0.05, line.name + " dmg L" + l);
                }
            }
        }

        [Test]
        public void Lines_MatchSpec()
        {
            var db = Db();
            Assert.AreEqual(1, db.GetLine(UnitLineId.Melee).unlockLevel);
            Assert.AreEqual(1, db.GetLine(UnitLineId.Ranged).unlockLevel);
            Assert.AreEqual(8, db.GetLine(UnitLineId.Tank).unlockLevel);
            Assert.AreEqual(20, db.GetLine(UnitLineId.Flying).unlockLevel);
            Assert.IsTrue(db.GetLine(UnitLineId.Tank).isTaunt);
            Assert.IsTrue(db.GetLine(UnitLineId.Flying).isFlying);
            Assert.IsTrue(db.GetLine(UnitLineId.Ranged).isRanged);
            Assert.Greater(db.GetLine(UnitLineId.Ranged).GetLevel(1).range, db.GetLine(UnitLineId.Melee).GetLevel(1).range);
        }

        [Test]
        public void Config_LoadsAndRemoteOverrideMerges()
        {
            var baseCfg = GameConfig.FromJson("{\"grid\":{\"cols\":4,\"rows\":2}}");
            Assert.AreEqual(4, baseCfg.grid.cols);
            Assert.AreEqual(8, baseCfg.grid.maxUnitLevel, "unspecified values keep defaults");

            var over = GameConfig.FromJson("{\"grid\":{\"cols\":4}}", "{\"grid\":{\"cols\":6},\"rewards\":{\"adMultiplier\":2}}");
            Assert.AreEqual(6, over.grid.cols);
            Assert.AreEqual(2, over.rewards.adMultiplier);

            var bad = GameConfig.FromJson("{\"grid\":{\"cols\":4}}", "{not json");
            Assert.AreEqual(4, bad.grid.cols, "bad override ignored");
        }

        [Test]
        public void ShippedConfig_Loads()
        {
            var cfg = GameConfig.Load(null);
            Assert.AreEqual(5, cfg.grid.cols);
            Assert.AreEqual(3, cfg.grid.rows);
            Assert.AreEqual(8, cfg.grid.maxUnitLevel);
        }
    }

    public class ArmyServiceTests
    {
        private SaveService _save;
        private CurrencyService _currency;
        private ArmyService _army;
        private MockAnalyticsService _analytics;

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
            _save = new SaveService(new InMemorySaveStorage(), new FakeTimeService());
            _save.Load();
            _analytics = new MockAnalyticsService { LogToConsole = false };
            _currency = new CurrencyService(_save, _analytics);
            _currency.Add(CurrencyType.Coins, 1000000, "test");
            var cfg = new GameConfig();
            _army = new ArmyService(new GridModel(cfg.grid.cols, cfg.grid.rows), _save, _currency, cfg,
                GameDatabase.BuildFromCsv(), new DeterministicRng(1), _analytics);
        }

        [Test]
        public void Buy_SpendsCoins_SpawnsLevelOne_AndRaisesNextCost()
        {
            long cost0 = _army.GetBuyCost(UnitLineId.Melee);
            long before = _currency.Get(CurrencyType.Coins);

            Assert.AreEqual(BuyResult.Ok, _army.Buy(UnitLineId.Melee));

            Assert.AreEqual(before - cost0, _currency.Get(CurrencyType.Coins));
            Assert.AreEqual(1, _army.Grid.UnitCount());
            Assert.Greater(_army.GetBuyCost(UnitLineId.Melee), cost0);
            Assert.AreEqual(1, _army.BuyCount(UnitLineId.Melee));
        }

        [Test]
        public void Buy_CostPersistsPerLine()
        {
            _army.Buy(UnitLineId.Melee);
            _army.Buy(UnitLineId.Melee);
            Assert.AreEqual(2, _army.BuyCount(UnitLineId.Melee));
            Assert.AreEqual(0, _army.BuyCount(UnitLineId.Ranged));
            Assert.AreEqual(70, _army.GetBuyCost(UnitLineId.Ranged));
        }

        [Test]
        public void Buy_LockedLineIsRejected_UntilLevelReached()
        {
            Assert.AreEqual(BuyResult.Locked, _army.Buy(UnitLineId.Tank));
            _save.Data.highestCampaignLevel = 7; // about to play level 8
            Assert.AreEqual(BuyResult.Ok, _army.Buy(UnitLineId.Tank));
            Assert.AreEqual(BuyResult.Locked, _army.Buy(UnitLineId.Flying));
            _save.Data.highestCampaignLevel = 19;
            Assert.AreEqual(BuyResult.Ok, _army.Buy(UnitLineId.Flying));
        }

        [Test]
        public void Buy_WithoutCoins_Fails_AndGridFullFails()
        {
            _currency.TrySpend(CurrencyType.Coins, _currency.Get(CurrencyType.Coins), "test");
            Assert.AreEqual(BuyResult.NoCoins, _army.Buy(UnitLineId.Melee));

            _currency.Add(CurrencyType.Coins, 100000000, "test");
            for (int i = 0; i < 15; i++) Assert.AreEqual(BuyResult.Ok, _army.Buy(UnitLineId.Melee));
            // full grid of level-1 melee: another melee merges instantly instead of being rejected
            Assert.AreEqual(BuyResult.Ok, _army.Buy(UnitLineId.Melee));
            Assert.AreEqual(15, _army.Grid.UnitCount());
            Assert.AreEqual(2, _army.Grid.HighestLevel());

            _army.Grid.Clear();
            for (int i = 0; i < 15; i++) _army.SpawnFree(UnitLineId.Ranged, 8);
            Assert.AreEqual(BuyResult.GridFull, _army.Buy(UnitLineId.Melee), "no empty cell and nothing to merge with");
        }

        [Test]
        public void CanBuyAndMergeEveryLineToLevelEight()
        {
            _save.Data.highestCampaignLevel = 50; // everything unlocked
            _currency.Add(CurrencyType.Coins, 1000000000000000000L, "test");
            foreach (UnitLineId line in new[] { UnitLineId.Melee, UnitLineId.Ranged, UnitLineId.Tank, UnitLineId.Flying })
            {
                _army.Grid.Clear();
                int guard = 0;
                while (_army.Grid.HighestLevel() < 8 && guard++ < 1000)
                {
                    if (_army.Grid.EmptyCount() > 0) Assert.AreEqual(BuyResult.Ok, _army.Buy(line));
                    if (_army.TryFindMergePair(out int a, out int b)) Assert.AreEqual(DropKind.Merge, _army.Drop(a, b).Kind);
                }
                Assert.AreEqual(8, _army.Grid.HighestLevel(), line.ToString());
            }
            Assert.AreEqual(8, _save.Data.highestMergedLevel);
        }

        [Test]
        public void Drop_PublishesEvents_AndLogsMergeAnalytics()
        {
            int merged = 0, spawned = 0;
            EventBus.Subscribe<UnitMergedEvent>(_ => merged++);
            EventBus.Subscribe<UnitSpawnedEvent>(_ => spawned++);
            _army.SpawnFree(UnitLineId.Melee, 1);
            _army.SpawnFree(UnitLineId.Melee, 1);
            Assert.IsTrue(_army.TryFindMergePair(out int a, out int b));
            _army.Drop(a, b);
            Assert.AreEqual(2, spawned);
            Assert.AreEqual(1, merged);
            Assert.IsTrue(_analytics.Events.Exists(e => e.StartsWith("merge ")));
        }

        [Test]
        public void Persist_ThenLoad_RestoresGridExactly()
        {
            _army.SpawnFree(UnitLineId.Melee, 3);
            _army.SpawnFree(UnitLineId.Ranged, 5);
            var cfg = new GameConfig();
            var fresh = new ArmyService(new GridModel(cfg.grid.cols, cfg.grid.rows), _save, _currency, cfg,
                GameDatabase.BuildFromCsv(), new DeterministicRng(99), _analytics);
            fresh.Load();
            Assert.AreEqual(2, fresh.Grid.UnitCount());
            Assert.AreEqual(5, fresh.Grid.HighestLevel());
            for (int i = 0; i < fresh.Grid.Count; i++)
                Assert.AreEqual(_army.Grid.Get(i).level, fresh.Grid.Get(i).level);
        }

        [Test]
        public void BuildBattleArmy_ListsEveryUnitWithPosition()
        {
            _army.SpawnFree(UnitLineId.Melee, 2);
            _army.SpawnFree(UnitLineId.Ranged, 4);
            var army = _army.BuildBattleArmy();
            Assert.AreEqual(2, army.Count);
        }
    }

    public class CurrencyServiceTests
    {
        private SaveService _save;
        private CurrencyService _c;

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
            _save = new SaveService(new InMemorySaveStorage(), new FakeTimeService());
            _c = new CurrencyService(_save, new MockAnalyticsService { LogToConsole = false });
        }

        [Test]
        public void AddAndSpend_UpdatesBalanceAndEvents()
        {
            long last = -1;
            EventBus.Subscribe<CurrencyChangedEvent>(e => last = e.Balance);
            _c.Add(CurrencyType.Gems, 50, "test");
            Assert.AreEqual(50, last);
            Assert.IsTrue(_c.TrySpend(CurrencyType.Gems, 20, "test"));
            Assert.AreEqual(30, _c.Get(CurrencyType.Gems));
            Assert.AreEqual(30, last);
        }

        [Test]
        public void Spend_MoreThanBalance_FailsWithoutChange()
        {
            _c.Add(CurrencyType.Coins, 10, "test");
            Assert.IsFalse(_c.TrySpend(CurrencyType.Coins, 11, "test"));
            Assert.AreEqual(10, _c.Get(CurrencyType.Coins));
        }

        [Test]
        public void NonPositiveAdd_IsIgnored()
        {
            _c.Add(CurrencyType.Coins, -5, "test");
            _c.Add(CurrencyType.Coins, 0, "test");
            Assert.AreEqual(0, _c.Get(CurrencyType.Coins));
        }

        [Test]
        public void Shards_AreTrackedPerCommander()
        {
            _c.AddShards("meteor", 10, "test");
            Assert.AreEqual(10, _c.GetShards("meteor"));
            Assert.IsFalse(_c.TrySpendShards("meteor", 11, "test"));
            Assert.IsTrue(_c.TrySpendShards("meteor", 4, "test"));
            Assert.AreEqual(6, _c.GetShards("meteor"));
            Assert.AreEqual(0, _c.GetShards("heal"));
        }
    }
}
