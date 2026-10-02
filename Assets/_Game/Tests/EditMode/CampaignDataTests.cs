using System.Collections.Generic;
using MergeLegion.Data;
using MergeLegion.Levels;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class CampaignDataTests
    {
        private GameDatabase _db;
        private LevelRepository _repo;

        [SetUp]
        public void SetUp()
        {
            _db = GameDatabase.BuildFromCsv();
            _repo = new LevelRepository(_db, new EndlessConfig());
        }

        [Test]
        public void All200LevelsLoad_FromTenChapterFiles()
        {
            Assert.AreEqual(200, _repo.LoadedCampaignLevels);
            for (int id = 1; id <= 200; id++)
            {
                var l = _repo.Get(id);
                Assert.AreEqual(id, l.id);
                Assert.AreEqual((id - 1) / 20 + 1, l.chapter, "chapter of " + id);
                Assert.AreEqual((id - 1) % 20 + 1, l.index);
                Assert.IsFalse(l.endless);
            }
        }

        [Test]
        public void EveryTenthLevelIsABossLevel()
        {
            for (int id = 1; id <= 200; id++)
            {
                var l = _repo.Get(id);
                bool expected = id % 10 == 0;
                Assert.AreEqual(expected, l.isBoss, "isBoss " + id);
                Assert.AreEqual(expected, l.hasBoss, "hasBoss " + id);
                if (expected)
                {
                    Assert.Greater(l.boss.hp, 0f);
                    Assert.Greater(l.boss.slamInterval, 0f);
                }
            }
        }

        [Test]
        public void ChaptersUseTheTenThemesInOrder()
        {
            string[] expected = { "grasslands", "desert", "snow", "swamp", "volcano", "castle", "undead", "sky", "abyss", "celestial" };
            for (int c = 1; c <= 10; c++)
            {
                Assert.AreEqual(expected[c - 1], _repo.Get((c - 1) * 20 + 1).theme);
                Assert.AreEqual(expected[c - 1], _repo.Get(c * 20).theme);
                Assert.AreEqual(expected[c - 1], ThemeLibrary.Get(expected[c - 1]).id);
            }
        }

        [Test]
        public void Formations_AreValid()
        {
            for (int id = 1; id <= 200; id++)
            {
                var l = _repo.Get(id);
                Assert.Greater(l.enemies.Count, 0, "level " + id + " has enemies");
                Assert.LessOrEqual(l.enemies.Count, ProceduralLevelGenerator.MaxEnemyUnits, "unit cap " + id);
                var cells = new HashSet<int>();
                foreach (var e in l.enemies)
                {
                    Assert.IsTrue(e.level >= 1 && e.level <= 8, "unit level range " + id);
                    Assert.IsTrue(e.col >= 0 && e.col < l.enemyCols, "col range " + id);
                    Assert.IsTrue(e.row >= 0, "row range " + id);
                    Assert.IsTrue(cells.Add(e.row * 10 + e.col), "overlap at level " + id);
                    if (id < 8) Assert.Less(e.line, 2, "no tanks/flyers before player can field them (" + id + ")");
                    if (id < 20) Assert.Less(e.line, 3, "no flyers before level 20 (" + id + ")");
                }
            }
        }

        [Test]
        public void PowerCurve_GrowsAcrossTheCampaign_AndEndlessContinuesFromIt()
        {
            float first = _repo.Get(1).power, last = _repo.Get(200).power;
            Assert.Greater(last, first * 100f);

            var endless = new EndlessConfig();
            float wave1 = ProceduralLevelGenerator.Generate(1, _db, endless).power;
            var curve = new CampaignGenParams();
            float campaignEnd = curve.startPower * (float)System.Math.Pow(curve.growth, 199);
            Assert.AreEqual(1.0, wave1 / campaignEnd, 0.3, "endless wave 1 should start near the end of the campaign curve");
        }

        [Test]
        public void Level1_IsAWalkover()
        {
            var l = _repo.Get(1);
            Assert.LessOrEqual(l.enemies.Count, 4);
            foreach (var e in l.enemies) Assert.AreEqual(1, e.level);
        }

        [Test]
        public void Themes_AllTenResolveWithColors()
        {
            int n = 0;
            foreach (var t in ThemeLibrary.All())
            {
                n++;
                Assert.IsFalse(string.IsNullOrEmpty(t.prop), t.id);
                Assert.Greater(t.lightIntensity, 0f, t.id);
                Assert.Greater(t.Ground.a, 0.99f, t.id);
            }
            Assert.AreEqual(10, n);
        }

        [Test]
        public void BaselinePlayer_ClearsEarlyCampaignWithoutGrinding()
        {
            var sim = new PlayerSimulator(_db, new GameConfig(), _repo, 1);
            var log = sim.Run(60);
            Assert.AreEqual(60, log.Count, "baseline player (no skills/ads) must reach level 60");
            Assert.IsTrue(log[log.Count - 1].Cleared);
            for (int i = 0; i < 20; i++) Assert.LessOrEqual(log[i].Attempts, 2, "tutorial stretch level " + (i + 1));
        }

        [Test]
        public void BossLevels_AreHarderThanTheirNeighbours()
        {
            for (int boss = 10; boss <= 190; boss += 10)
            {
                float bossPower = Power(_repo.Get(boss));
                float prev = Power(_repo.Get(boss - 1));
                Assert.Greater(bossPower, prev * 0.95f, "boss " + boss);
            }
        }

        private float Power(LevelDefinition l)
        {
            float p = LevelPower.ArmyPower(_db, l.enemies);
            if (l.hasBoss) p += (float)System.Math.Sqrt(l.boss.hp * l.boss.damage * l.boss.attackSpeed);
            return p;
        }
    }
}
