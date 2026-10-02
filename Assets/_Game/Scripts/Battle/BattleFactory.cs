using System.Collections.Generic;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;

namespace MergeLegion.Battle
{
    /// <summary>Turns saved army + level data into a ready-to-run <see cref="BattleSim"/>.</summary>
    public static class BattleFactory
    {
        public static UnitSpec Spec(UnitLineData line, int level, Team team, Vec2 pos, float hpMult, float dmgMult)
        {
            var d = line.GetLevel(level);
            return new UnitSpec
            {
                Team = team,
                Line = (int)line.id,
                Level = level,
                Hp = d.hp * hpMult,
                Damage = d.damage * dmgMult,
                AttackSpeed = d.attackSpeed,
                Range = d.range,
                MoveSpeed = d.moveSpeed,
                Scale = d.scale,
                ProjectileSpeed = line.projectileSpeed,
                IsRanged = line.isRanged,
                IsFlying = line.isFlying,
                IsTaunt = line.isTaunt,
                Pos = pos
            };
        }

        public static BattleSim Create(GameConfig cfg, GameDatabase db, ArenaLayout layout, LevelDefinition level,
            IReadOnlyList<BattleSpawn> army, IResearchProvider research, CommanderBonuses bonuses, int highestMergedLevel, int seed)
        {
            var sim = new BattleSim(cfg.battle, seed)
            {
                MinX = -layout.HalfWidth - 1f,
                MaxX = layout.HalfWidth + 1f,
                MinY = layout.NearZ - 2f,
                MaxY = layout.FarZ + 4f,
                PlayerSpawnZ = layout.FrontZ
            };

            for (int i = 0; i < army.Count; i++)
            {
                var s = army[i];
                var line = db.GetLine((UnitLineId)s.line);
                float hp = (1f + research.HpBonus(line.id)) * bonuses.HpMult(line.id);
                float dmg = (1f + research.DamageBonus(line.id)) * bonuses.DamageMult(line.id);
                sim.AddUnit(Spec(line, s.level, Team.Player, layout.PlayerCell(s.col, s.row), hp, dmg));
            }

            var melee = db.GetLine(UnitLineId.Melee);
            int reinforceLevel = System.Math.Max(1, highestMergedLevel - 2);
            sim.Reinforcement = Spec(melee, System.Math.Min(reinforceLevel, melee.MaxLevel), Team.Player, new Vec2(0, layout.FrontZ),
                (1f + research.HpBonus(UnitLineId.Melee)) * bonuses.HpMult(UnitLineId.Melee),
                (1f + research.DamageBonus(UnitLineId.Melee)) * bonuses.DamageMult(UnitLineId.Melee));

            AddEnemies(sim, level, layout, db);
            return sim;
        }

        public static void AddEnemies(BattleSim sim, LevelDefinition level, ArenaLayout layout, GameDatabase db)
        {
            for (int i = 0; i < level.enemies.Count; i++)
            {
                var e = level.enemies[i];
                var line = db.GetLine((UnitLineId)e.line);
                int lv = System.Math.Max(1, System.Math.Min(e.level, line.MaxLevel));
                sim.AddUnit(Spec(line, lv, Team.Enemy, layout.EnemyCell(e.col, e.row, level.enemyCols), level.hpMult, level.dmgMult));
            }

            if (level.hasBoss) AddBoss(sim, level, layout, db);
        }

        private static void AddBoss(BattleSim sim, LevelDefinition level, ArenaLayout layout, GameDatabase db)
        {
            var b = level.boss;
            var pos = new Vec2(0f, layout.EnemyCell(2, 0, level.enemyCols).y + layout.CellSize * 1.4f);
            var spec = new UnitSpec
            {
                Team = Team.Enemy,
                Line = (int)UnitLineId.Tank,
                Level = 8,
                Hp = b.hp * level.hpMult,
                Damage = b.damage * level.dmgMult,
                AttackSpeed = b.attackSpeed,
                Range = b.range,
                MoveSpeed = b.moveSpeed,
                Scale = b.scale,
                IsBoss = true,
                Pos = pos
            };
            var boss = sim.AddUnit(spec);

            var minionLine = db.GetLine((UnitLineId)b.minionLine);
            var pattern = new BossPattern
            {
                SlamInterval = b.slamInterval,
                SlamRadius = b.slamRadius,
                SlamDamageMult = b.slamDamageMult,
                SummonInterval = b.summonInterval,
                SummonCount = b.summonCount,
                Minion = Spec(minionLine, System.Math.Max(1, System.Math.Min(b.minionLevel, minionLine.MaxLevel)), Team.Enemy, pos, level.hpMult, level.dmgMult)
            };
            sim.SetBoss(boss, pattern);
        }

        /// <summary>Total army power of the player's saved grid (analytics: army_power).</summary>
        public static float ArmyPower(GameDatabase db, IReadOnlyList<BattleSpawn> army)
        {
            float sum = 0f;
            for (int i = 0; i < army.Count; i++) sum += LevelPower.UnitStrength(db, army[i].line, army[i].level);
            return sum;
        }
    }
}
