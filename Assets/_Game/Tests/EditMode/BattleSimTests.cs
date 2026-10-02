using System.Collections.Generic;
using MergeLegion.Battle;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class BattleSimTests
    {
        private GameDatabase _db;
        private GameConfig _cfg;
        private ArenaLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _db = GameDatabase.BuildFromCsv();
            _cfg = new GameConfig();
            _layout = new ArenaLayout(_cfg.grid);
        }

        private BattleSim NewSim(int seed = 1) => new BattleSim(_cfg.battle, seed) { MinX = -12, MaxX = 12, MinY = -14, MaxY = 14 };

        private UnitSpec Spec(UnitLineId line, int level, Team team, float x, float y, float hp = 1f, float dmg = 1f) =>
            BattleFactory.Spec(_db.GetLine(line), level, team, new Vec2(x, y), hp, dmg);

        private BattleOutcome Run(BattleSim sim, float maxSeconds = 150f)
        {
            int ticks = (int)(maxSeconds / _cfg.battle.fixedStep);
            for (int i = 0; i < ticks && sim.Outcome == BattleOutcome.Running; i++) { sim.Tick(_cfg.battle.fixedStep); sim.Drain(); }
            return sim.Outcome;
        }

        [Test]
        public void StrongerArmy_Wins_WeakerLoses()
        {
            var win = NewSim();
            win.AddUnit(Spec(UnitLineId.Melee, 5, Team.Player, 0, -4));
            win.AddUnit(Spec(UnitLineId.Melee, 1, Team.Enemy, 0, 4));
            Assert.AreEqual(BattleOutcome.Win, Run(win));

            var lose = NewSim();
            lose.AddUnit(Spec(UnitLineId.Melee, 1, Team.Player, 0, -4));
            lose.AddUnit(Spec(UnitLineId.Melee, 5, Team.Enemy, 0, 4));
            Assert.AreEqual(BattleOutcome.Lose, Run(lose));
        }

        private string Fingerprint(int seed)
        {
            var sim = NewSim(seed);
            for (int i = 0; i < 6; i++)
            {
                sim.AddUnit(Spec(i % 2 == 0 ? UnitLineId.Melee : UnitLineId.Ranged, 3, Team.Player, -4 + i * 1.6f, -5));
                sim.AddUnit(Spec(i % 2 == 0 ? UnitLineId.Ranged : UnitLineId.Melee, 3, Team.Enemy, -4 + i * 1.6f, 5));
            }
            Run(sim);
            float hp = 0;
            foreach (var u in sim.Units) hp += u.Hp * (u.Id % 7 + 1) + u.Pos.x + u.Pos.y;
            return sim.Outcome + ":" + sim.Time.ToString("F4") + ":" + hp.ToString("F3");
        }

        [Test]
        public void SameSeedAndSetup_IsDeterministic()
        {
            Assert.AreEqual(Fingerprint(42), Fingerprint(42));
        }

        [Test]
        public void Ranged_AttacksFromDistance_WithProjectiles()
        {
            var sim = NewSim();
            var archer = sim.AddUnit(Spec(UnitLineId.Ranged, 3, Team.Player, 0, -6));
            sim.AddUnit(Spec(UnitLineId.Tank, 8, Team.Enemy, 0, 3));
            bool launched = false;
            float distAtFirstShot = 0f;
            for (int i = 0; i < 200 && !launched; i++)
            {
                sim.Tick(_cfg.battle.fixedStep);
                foreach (var e in sim.Events)
                    if (e.Type == SimEventType.ProjectileLaunch && !launched) { launched = true; distAtFirstShot = Vec2.Distance(archer.Pos, sim.Units[1].Pos); }
                sim.Drain();
            }
            Assert.IsTrue(launched);
            Assert.Greater(distAtFirstShot, 3f, "archer fires from range, not point blank");
        }

        [Test]
        public void Taunt_RedirectsEnemiesToTank()
        {
            var sim = NewSim();
            var tank = sim.AddUnit(Spec(UnitLineId.Tank, 4, Team.Player, 2, -2));
            var archer = sim.AddUnit(Spec(UnitLineId.Ranged, 4, Team.Player, -1, -3));
            var enemy = sim.AddUnit(Spec(UnitLineId.Melee, 4, Team.Enemy, 0, 0.5f));
            sim.Tick(_cfg.battle.fixedStep);
            Assert.AreEqual(tank.Id, enemy.TargetId, "taunting tank within range draws aggro even if the archer is closer");
            Assert.IsTrue(archer.Alive);
        }

        [Test]
        public void Flying_TargetsBacklineRanged_NotNearestFrontline()
        {
            var sim = NewSim();
            var front = sim.AddUnit(Spec(UnitLineId.Melee, 4, Team.Player, 0, -1));
            var archer = sim.AddUnit(Spec(UnitLineId.Ranged, 4, Team.Player, 0, -7));
            var flyer = sim.AddUnit(Spec(UnitLineId.Flying, 4, Team.Enemy, 0, 6));
            sim.Tick(_cfg.battle.fixedStep);
            Assert.AreEqual(archer.Id, flyer.TargetId);
            Assert.AreNotEqual(front.Id, flyer.TargetId);
        }

        [Test]
        public void Units_DoNotStackOnTheSamePoint()
        {
            var sim = NewSim();
            for (int i = 0; i < 12; i++) sim.AddUnit(Spec(UnitLineId.Melee, 1, Team.Player, 0, -4));
            sim.AddUnit(Spec(UnitLineId.Tank, 8, Team.Enemy, 0, 10, 100f, 0.01f));
            for (int i = 0; i < 60; i++) { sim.Tick(_cfg.battle.fixedStep); sim.Drain(); }
            float minD = float.MaxValue;
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    minD = System.Math.Min(minD, Vec2.Distance(sim.Units[i].Pos, sim.Units[j].Pos));
            Assert.Greater(minD, 0.3f);
        }

        [Test]
        public void StarsFollowSurvivingHp()
        {
            var sim = NewSim();
            sim.AddUnit(Spec(UnitLineId.Melee, 6, Team.Player, 0, -4));
            sim.AddUnit(Spec(UnitLineId.Melee, 1, Team.Enemy, 0, 4));
            Run(sim);
            Assert.AreEqual(BattleOutcome.Win, sim.Outcome);
            Assert.AreEqual(3, sim.GetResult().Stars);

            var close = NewSim();
            close.AddUnit(Spec(UnitLineId.Melee, 3, Team.Player, 0, -4, 1f, 1f));
            close.AddUnit(Spec(UnitLineId.Melee, 3, Team.Enemy, 0, 4, 0.9f, 0.95f));
            Run(close);
            var r = close.GetResult();
            if (r.Outcome == BattleOutcome.Win) Assert.Less(r.Stars, 3);
            else Assert.AreEqual(0, r.Stars);
        }

        [Test]
        public void Timeout_CountsAsLoss()
        {
            _cfg.battle.maxDurationSeconds = 3f;
            var sim = NewSim();
            sim.AddUnit(Spec(UnitLineId.Melee, 1, Team.Player, 0, -10));
            sim.AddUnit(Spec(UnitLineId.Melee, 1, Team.Enemy, 0, 10));
            Assert.AreEqual(BattleOutcome.Lose, Run(sim));
        }

        [Test]
        public void Boss_Slams_AndSummonsMinions()
        {
            var sim = NewSim();
            for (int i = 0; i < 4; i++) sim.AddUnit(Spec(UnitLineId.Tank, 6, Team.Player, -2 + i * 1.3f, -2));
            var level = new LevelDefinition { hasBoss = true, isBoss = true };
            level.boss = new BossDef { hp = 50000, damage = 60, slamInterval = 1f, summonInterval = 1.5f, summonCount = 2, minionLine = 0, minionLevel = 1 };
            BattleFactory.AddEnemies(sim, level, _layout, _db);

            bool slam = false, telegraph = false;
            int startUnits = sim.Units.Count;
            for (int i = 0; i < 400; i++)
            {
                sim.Tick(_cfg.battle.fixedStep);
                foreach (var e in sim.Events)
                {
                    if (e.Type == SimEventType.Slam) slam = true;
                    if (e.Type == SimEventType.Telegraph) telegraph = true;
                }
                sim.Drain();
            }
            Assert.IsTrue(telegraph, "slam is telegraphed first");
            Assert.IsTrue(slam);
            Assert.Greater(sim.Units.Count, startUnits, "boss summoned minions");
        }

        [Test]
        public void Boss_Enrages_WhenLow()
        {
            var sim = NewSim();
            sim.AddUnit(Spec(UnitLineId.Melee, 8, Team.Player, 0, -3));
            var level = new LevelDefinition { hasBoss = true, isBoss = true };
            level.boss = new BossDef { hp = 100, damage = 1, summonCount = 0 };
            BattleFactory.AddEnemies(sim, level, _layout, _db);
            var boss = sim.Units[1];
            boss.Hp = boss.MaxHp * 0.2f;
            bool enraged = false;
            for (int i = 0; i < 5; i++)
            {
                sim.Tick(_cfg.battle.fixedStep);
                foreach (var e in sim.Events) if (e.Type == SimEventType.BossEnrage) enraged = true;
                sim.Drain();
            }
            Assert.IsTrue(enraged);
        }

        [Test]
        public void Revive_BringsBackFallenPlayerUnits_AndResumes()
        {
            var sim = NewSim();
            var p = sim.AddUnit(Spec(UnitLineId.Melee, 1, Team.Player, 0, -4));
            sim.AddUnit(Spec(UnitLineId.Melee, 6, Team.Enemy, 0, 3));
            Assert.AreEqual(BattleOutcome.Lose, Run(sim));
            int revived = sim.Revive(Team.Player, 0.5f);
            Assert.AreEqual(1, revived);
            Assert.AreEqual(BattleOutcome.Running, sim.Outcome);
            Assert.IsTrue(p.Alive);
            Assert.AreEqual(p.MaxHp * 0.5f, p.Hp, 0.01);
        }
    }

    public class CommanderSkillTests
    {
        private GameDatabase _db;
        private GameConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            _db = GameDatabase.BuildFromCsv();
            _cfg = new GameConfig();
        }

        private BattleSim Sim(int enemies = 5)
        {
            var sim = new BattleSim(_cfg.battle, 3) { MinX = -12, MaxX = 12, MinY = -14, MaxY = 14 };
            sim.AddUnit(BattleFactory.Spec(_db.GetLine(UnitLineId.Melee), 4, Team.Player, new Vec2(0, -5), 1, 1));
            sim.AddUnit(BattleFactory.Spec(_db.GetLine(UnitLineId.Ranged), 4, Team.Player, new Vec2(1, -6), 1, 1));
            for (int i = 0; i < enemies; i++)
                sim.AddUnit(BattleFactory.Spec(_db.GetLine(UnitLineId.Melee), 4, Team.Enemy, new Vec2(i * 0.5f, 6), 1, 1));
            sim.Reinforcement = BattleFactory.Spec(_db.GetLine(UnitLineId.Melee), 2, Team.Player, new Vec2(0, -4), 1, 1);
            return sim;
        }

        private static SkillSpec Spec(SkillType t, float power = 1f, float radius = 3f, float duration = 5f, int count = 3) =>
            new SkillSpec { Type = t, Cooldown = 10f, Power = power, Radius = radius, Duration = duration, Count = count };

        [Test]
        public void Skill_StartsOnCooldown_AndBecomesReady()
        {
            var skills = new CommanderSkillSystem(Spec(SkillType.Meteor), 1f);
            Assert.IsFalse(skills.Ready);
            skills.Update(10.1f);
            Assert.IsTrue(skills.Ready);
        }

        [Test]
        public void Meteor_HitsClusterAfterDelay()
        {
            var sim = Sim();
            var skills = new CommanderSkillSystem(Spec(SkillType.Meteor, 6f, 4f), 0f);
            skills.Begin(sim);
            Assert.IsTrue(skills.TryCast(sim));
            float before = 0; foreach (var u in sim.Units) if (u.Team == Team.Enemy) before += u.Hp;
            for (int i = 0; i < 30; i++) sim.Tick(_cfg.battle.fixedStep); // 1s > 0.7s delay
            float after = 0; foreach (var u in sim.Units) if (u.Team == Team.Enemy) after += u.Hp;
            Assert.Less(after, before);
            Assert.IsFalse(skills.Ready, "cooldown restarts");
        }

        [Test]
        public void HealWave_RestoresMissingHp()
        {
            var sim = Sim();
            foreach (var u in sim.Units) if (u.Team == Team.Player) u.Hp = u.MaxHp * 0.2f;
            var skills = new CommanderSkillSystem(Spec(SkillType.HealWave, 0.5f), 0f);
            skills.Begin(sim);
            skills.TryCast(sim);
            Assert.AreEqual(0.7f, sim.Units[0].HpFraction, 0.001);
        }

        [Test]
        public void ShieldWall_ReducesDamageTaken()
        {
            var sim = Sim();
            var skills = new CommanderSkillSystem(Spec(SkillType.ShieldWall, 0.5f, 3f, 5f), 0f);
            skills.Begin(sim);
            skills.TryCast(sim);
            var u = sim.Units[0];
            float hp = u.Hp;
            sim.DealDamage(u, 100f, 99);
            Assert.AreEqual(hp - 50f, u.Hp, 0.01);
        }

        [Test]
        public void Rally_SpeedsUpAttackCooldowns()
        {
            var sim = Sim();
            var skills = new CommanderSkillSystem(Spec(SkillType.Rally, 1f, 3f, 5f), 0f);
            skills.Begin(sim);
            skills.TryCast(sim);
            Assert.AreEqual(2f, sim.Units[0].HasteMult, 0.001);
            Assert.Greater(sim.Units[0].HasteUntil, sim.Time);
        }

        [Test]
        public void Lightning_ChainsThroughMultipleEnemies()
        {
            var sim = Sim(6);
            var skills = new CommanderSkillSystem(Spec(SkillType.LightningChain, 1f, 4f, 0f, 4), 0f);
            skills.Begin(sim);
            skills.TryCast(sim);
            int bolts = 0;
            foreach (var e in sim.Events) if (e.Type == SimEventType.SkillBolt) bolts++;
            Assert.AreEqual(4, bolts);
        }

        [Test]
        public void Summon_AddsPlayerUnits()
        {
            var sim = Sim();
            int before = sim.AliveCount(Team.Player);
            var skills = new CommanderSkillSystem(Spec(SkillType.Summon, 1f, 0f, 0f, 3), 0f);
            skills.Begin(sim);
            skills.TryCast(sim);
            Assert.AreEqual(before + 3, sim.AliveCount(Team.Player));
        }

        [Test]
        public void None_NeverCasts()
        {
            var sim = Sim();
            var skills = new CommanderSkillSystem(new SkillSpec(), 0f);
            Assert.IsFalse(skills.TryCast(sim));
        }
    }

    public class LevelAndRewardTests
    {
        private GameDatabase _db;

        [SetUp]
        public void SetUp() => _db = GameDatabase.BuildFromCsv();

        [Test]
        public void Rewards_GrowWithLevelAndStars()
        {
            var cfg = new RewardConfig();
            Assert.Greater(RewardService.WinCoins(cfg, 10, 1), RewardService.WinCoins(cfg, 1, 1));
            Assert.Greater(RewardService.WinCoins(cfg, 5, 3), RewardService.WinCoins(cfg, 5, 1));
            Assert.AreEqual(RewardService.WinCoins(cfg, 5, 1) * 3, RewardService.WithAdMultiplier(cfg, RewardService.ForWin(cfg, 5, 1, false)).Coins);
            Assert.Less(RewardService.LoseCoins(cfg, 5), RewardService.WinCoins(cfg, 5, 1));
            Assert.AreEqual(cfg.bossBonusGems, RewardService.ForWin(cfg, 10, 3, true).Gems);
            Assert.AreEqual(0, RewardService.ForWin(cfg, 9, 3, false).Gems);
        }

        [Test]
        public void CoinBonus_Multiplies()
        {
            var cfg = new RewardConfig();
            long plain = RewardService.WinCoins(cfg, 20, 2);
            long boosted = RewardService.WinCoins(cfg, 20, 2, 0.10f);
            Assert.AreEqual(plain * 1.1, boosted, 1.0);
        }

        [Test]
        public void EndlessGenerator_IsDeterministic_AndPowerGrowsTwelvePercentPerWave()
        {
            var cfg = new EndlessConfig();
            var a = ProceduralLevelGenerator.Generate(7, _db, cfg);
            var b = ProceduralLevelGenerator.Generate(7, _db, cfg);
            Assert.AreEqual(a.enemies.Count, b.enemies.Count);
            for (int i = 0; i < a.enemies.Count; i++)
            {
                Assert.AreEqual(a.enemies[i].line, b.enemies[i].line);
                Assert.AreEqual(a.enemies[i].level, b.enemies[i].level);
                Assert.AreEqual(a.enemies[i].col, b.enemies[i].col);
            }
            Assert.AreEqual(cfg.growth, ProceduralLevelGenerator.Generate(8, _db, cfg).power / a.power, 0.001);
        }

        [Test]
        public void EndlessGenerator_RespectsBudget_UnitCap_AndBossWaves()
        {
            var cfg = new EndlessConfig();
            for (int wave = 1; wave <= 60; wave++)
            {
                var l = ProceduralLevelGenerator.Generate(wave, _db, cfg);
                Assert.LessOrEqual(l.enemies.Count, ProceduralLevelGenerator.MaxEnemyUnits);
                Assert.IsTrue(l.endless);
                Assert.AreEqual(wave % 10 == 0, l.hasBoss, "boss wave " + wave);
                var cells = new HashSet<int>();
                foreach (var e in l.enemies) Assert.IsTrue(cells.Add(e.row * 100 + e.col), "no overlapping cells on wave " + wave);
            }
        }

        [Test]
        public void Repository_ServesEndlessBeyondCampaign()
        {
            var repo = new LevelRepository(_db, new EndlessConfig());
            var l = repo.Get(LevelRepository.CampaignLevels + 5);
            Assert.IsTrue(l.endless);
            Assert.AreEqual(5, l.index);
            Assert.IsTrue(repo.IsEndless(201));
            Assert.IsFalse(repo.IsEndless(200));
        }
    }
}

namespace MergeLegion.Tests
{
    public class BattlePerformanceTests
    {
        [Test]
        public void Sim_Handles240Units_WithinFrameBudget()
        {
            var db = GameDatabase.BuildFromCsv();
            var cfg = new GameConfig();
            var sim = new BattleSim(cfg.battle, 7) { MinX = -12, MaxX = 12, MinY = -16, MaxY = 16 };
            var rng = new MergeLegion.Core.DeterministicRng(5);
            for (int i = 0; i < 120; i++)
            {
                var line = db.GetLine((UnitLineId)rng.NextInt(4));
                sim.AddUnit(BattleFactory.Spec(line, 1 + rng.NextInt(4), Team.Player, new Vec2(rng.Range(-7f, 7f), rng.Range(-9f, -2f)), 3f, 1f));
                sim.AddUnit(BattleFactory.Spec(line, 1 + rng.NextInt(4), Team.Enemy, new Vec2(rng.Range(-7f, 7f), rng.Range(2f, 9f)), 3f, 1f));
            }
            sim.Drain();
            for (int i = 0; i < 10; i++) { sim.Tick(cfg.battle.fixedStep); sim.Drain(); } // warm up pools / JIT

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int ticks = 0;
            while (sim.Outcome == BattleOutcome.Running && ticks < 600)
            {
                sim.Tick(cfg.battle.fixedStep);
                sim.Drain();
                ticks++;
            }
            sw.Stop();
            double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
            System.Console.WriteLine("[perf] 240 units: " + msPerTick.ToString("F3") + " ms/tick over " + ticks + " ticks");
            Assert.Less(msPerTick, 4.0, "30Hz sim tick must leave most of the 16ms frame for rendering");
        }
    }
}
