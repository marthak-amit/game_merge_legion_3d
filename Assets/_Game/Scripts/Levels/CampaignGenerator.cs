using System;
using MergeLegion.Core;
using MergeLegion.Data;

namespace MergeLegion.Levels
{
    [Serializable]
    public sealed class CampaignGenParams
    {
        public float startPower = 110f;      // enemy power budget on level 1
        public float growth = 1.03f;         // per level
        public float bossShare = 0.55f;      // share of a boss level's budget spent on the boss itself
        public float bossBudgetMult = 1.25f; // boss levels are tougher than their neighbours
        public float jitter = 0.05f;         // +/- random variation so the curve is not mechanical
        public int maxUnits = 40;
        public int seed = 2024;
    }

    /// <summary>
    /// Produces the 200 authored campaign levels (10 chapters x 20) from a power curve. The output is written to
    /// Resources/Levels/chapter_XX.json, where designers can hand-tune individual levels.
    /// </summary>
    public static class CampaignGenerator
    {
        public static ChapterFile[] GenerateAll(GameDatabase db, CampaignGenParams p)
        {
            var files = new ChapterFile[LevelRepository.Chapters];
            for (int c = 0; c < files.Length; c++)
            {
                files[c] = new ChapterFile { chapter = c + 1, theme = LevelRepository.ThemeForChapter(c + 1) };
                for (int i = 1; i <= LevelRepository.LevelsPerChapter; i++)
                    files[c].levels.Add(GenerateLevel(db, p, c + 1, i));
            }
            return files;
        }

        public static LevelDefinition GenerateLevel(GameDatabase db, CampaignGenParams p, int chapter, int index)
        {
            int id = (chapter - 1) * LevelRepository.LevelsPerChapter + index;
            var rng = new DeterministicRng(p.seed + id * 31);
            double budget = p.startPower * Math.Pow(p.growth, id - 1) * (1.0 + (rng.NextFloat() * 2f - 1f) * p.jitter);

            var level = new LevelDefinition
            {
                id = id,
                chapter = chapter,
                index = index,
                theme = LevelRepository.ThemeForChapter(chapter),
                enemyCols = 5,
                isBoss = index % 10 == 0
            };

            if (level.isBoss)
            {
                budget *= p.bossBudgetMult;
                level.hasBoss = true;
                level.boss = ProceduralLevelGenerator.BossForStrength((float)(budget * p.bossShare), db, rng);
                budget *= 1.0 - p.bossShare;
            }

            level.power = (float)(budget);
            int lines = id >= 20 ? 4 : id >= 8 ? 3 : 2;
            ProceduralLevelGenerator.FillFormation(level, db, budget, rng, lines, p.maxUnits);
            return level;
        }
    }
}
