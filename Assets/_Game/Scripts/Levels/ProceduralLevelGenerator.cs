using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;

namespace MergeLegion.Levels
{
    /// <summary>Endless mode: builds a level from a power budget curve (power = base * growth^wave) with a random arena.</summary>
    public static class ProceduralLevelGenerator
    {
        public const int MaxEnemyUnits = 45;

        public static readonly string[] Themes =
            { "grasslands", "desert", "snow", "swamp", "volcano", "castle", "undead", "sky", "abyss", "celestial" };

        /// <param name="wave">1-based endless wave.</param>
        public static LevelDefinition Generate(int wave, GameDatabase db, EndlessConfig cfg, int seedSalt = 0)
        {
            var rng = new DeterministicRng(wave * 7919 + seedSalt);
            double budget = cfg.basePower * Math.Pow(cfg.growth, wave - 1);

            var level = new LevelDefinition
            {
                id = cfg.firstEndlessLevel + wave - 1,
                chapter = 11,
                index = wave,
                endless = true,
                theme = Themes[rng.NextInt(Themes.Length)],
                enemyCols = 5,
                power = (float)budget
            };

            bool boss = cfg.bossEvery > 0 && wave % cfg.bossEvery == 0;
            if (boss)
            {
                level.isBoss = true;
                level.hasBoss = true;
                float bossShare = 0.6f;
                float strength = (float)(budget * bossShare);
                level.boss = BossForStrength(strength, db, rng);
                budget *= 1.0 - bossShare;
            }

            FillFormation(level, db, budget, rng, Math.Min(UnitLines.Count, 1 + cfg.linesUnlockedAtWave(wave)) , MaxEnemyUnits);
            return level;
        }

        /// <param name="lineCount">How many unit lines (from Melee) the enemy may use.</param>
        public static void FillFormation(LevelDefinition level, GameDatabase db, double budget, DeterministicRng rng, int lineCount, int maxUnits)
        {
            int cols = level.enemyCols;
            var occupied = new HashSet<int>();
            int guard = 0;
            double remaining = budget;
            int maxLine = Math.Max(1, Math.Min(UnitLines.Count, lineCount));

            while (remaining > 0.5 && level.enemies.Count < maxUnits && guard++ < 400)
            {
                int line = rng.NextInt(maxLine);
                var data = db.GetLine((UnitLineId)line);

                // biggest unit level whose strength fits in a share of what is left, with some randomness
                int pick = 1;
                float share = (float)(remaining * rng.Range(0.25f, 0.6f));
                for (int lv = data.MaxLevel; lv >= 1; lv--)
                {
                    if (LevelPower.UnitStrength(data.GetLevel(lv)) <= share) { pick = lv; break; }
                }
                float s = LevelPower.UnitStrength(data.GetLevel(pick));
                if (s > remaining && pick > 1) pick = 1;

                int slot = FindSlot(occupied, cols, data.isRanged, rng);
                occupied.Add(slot);
                level.enemies.Add(new EnemySpawnDef { line = line, level = pick, col = slot % cols, row = slot / cols });
                remaining -= LevelPower.UnitStrength(data.GetLevel(pick));
            }
        }

        private static int FindSlot(HashSet<int> occupied, int cols, bool ranged, DeterministicRng rng)
        {
            // frontline units fill row 0/1, ranged units prefer the rear rows
            int baseRow = ranged ? 2 : 0;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int row = baseRow + rng.NextInt(2) + attempt / 12;
                int col = rng.NextInt(cols);
                int slot = row * cols + col;
                if (!occupied.Contains(slot)) return slot;
            }
            int s = 0;
            while (occupied.Contains(s)) s++;
            return s;
        }

        public static BossDef BossForStrength(float strength, GameDatabase db, DeterministicRng rng)
        {
            // Boss strength = sqrt(hp * dps). Fixed hp:dps ratio keeps fights readable (long, bursty).
            const float hpToDps = 60f;
            float dps = strength / (float)Math.Sqrt(hpToDps);
            float hp = dps * hpToDps;
            float attackSpeed = 0.5f;
            int minionLine = rng.NextInt(2);
            return new BossDef
            {
                hp = hp,
                damage = dps / attackSpeed,
                attackSpeed = attackSpeed,
                minionLine = minionLine,
                minionLevel = 1
            };
        }
    }

    [Serializable]
    public sealed class EndlessConfig
    {
        public float basePower = 41000f;
        public float growth = 1.12f;
        public int firstEndlessLevel = 201;
        public int bossEvery = 10;
        public int wavesPerNewLine = 5;

        public int linesUnlockedAtWave(int wave) => Math.Min(UnitLines.Count - 1, wave / Math.Max(1, wavesPerNewLine));
    }
}
