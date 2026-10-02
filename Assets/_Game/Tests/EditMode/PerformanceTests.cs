using System;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Data;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class PerformanceTests
    {
        [Test]
        public void DeviceTiers_MatchTargets()
        {
            var low = PerformanceProfile.Choose(2048, 8, 1800);
            Assert.AreEqual(DeviceTier.Low, low.Tier);
            Assert.AreEqual(30, low.TargetFps);
            Assert.Less(low.ResolutionScale, 1f);

            var snapdragon6 = PerformanceProfile.Choose(4096, 8, 2200);
            Assert.AreEqual(DeviceTier.Mid, snapdragon6.Tier);
            Assert.AreEqual(60, snapdragon6.TargetFps);

            var flagship = PerformanceProfile.Choose(12288, 8, 3200);
            Assert.AreEqual(DeviceTier.High, flagship.Tier);
            Assert.AreEqual(1f, flagship.ResolutionScale);
        }

        [Test]
        public void BattleTick_AllocatesNothingOnceWarm()
        {
            var db = GameDatabase.BuildFromCsv();
            var cfg = new GameConfig();
            var sim = new BattleSim(cfg.battle, 3) { MinX = -12, MaxX = 12, MinY = -16, MaxY = 16 };
            var rng = new DeterministicRng(8);
            for (int i = 0; i < 60; i++)
            {
                var line = db.GetLine((UnitLineId)rng.NextInt(4));
                sim.AddUnit(BattleFactory.Spec(line, 1 + rng.NextInt(3), Team.Player, new Vec2(rng.Range(-6f, 6f), rng.Range(-9f, -2f)), 20f, 1f));
                sim.AddUnit(BattleFactory.Spec(line, 1 + rng.NextInt(3), Team.Enemy, new Vec2(rng.Range(-6f, 6f), rng.Range(2f, 9f)), 20f, 1f));
            }
            // warm up: grows internal lists / pools to steady state
            for (int i = 0; i < 120; i++) { sim.Tick(cfg.battle.fixedStep); sim.Events.Clear(); }

            GC.Collect();
            long before = GC.GetTotalMemory(true);
            for (int i = 0; i < 300 && sim.Outcome == BattleOutcome.Running; i++) { sim.Tick(cfg.battle.fixedStep); sim.Events.Clear(); }
            long after = GC.GetTotalMemory(false);
            long grown = after - before;
            Assert.Less(grown, 96 * 1024, "300 ticks of a 120-unit fight allocated " + grown + " bytes");
        }

        [Test]
        public void BaselineFights_LastAboutHalfAMinute()
        {
            var db = GameDatabase.BuildFromCsv();
            var repo = new MergeLegion.Levels.LevelRepository(db, new MergeLegion.Levels.EndlessConfig());
            var log = new PlayerSimulator(db, new GameConfig(), repo, 1).Run(50);
            float total = 0f; int n = 0;
            foreach (var l in log) { total += l.FightSeconds; n++; }
            float avg = total / n;
            System.Console.WriteLine("[balance] average fight length (levels 1-50): " + avg.ToString("F1") + "s");
            Assert.Greater(avg, 6f, "fights should not be instant");
            Assert.Less(avg, 90f, "a level (prepare + fight) must fit the 60-120 second loop");
        }
    }
}
