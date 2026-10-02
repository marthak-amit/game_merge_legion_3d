using System.Collections.Generic;
using MergeLegion.Data;
using UnityEngine;

namespace MergeLegion.Levels
{
    /// <summary>Loads the 200 authored campaign levels and serves endless waves beyond them.</summary>
    public sealed class LevelRepository
    {
        public const int Chapters = 10;
        public const int LevelsPerChapter = 20;
        public const int CampaignLevels = Chapters * LevelsPerChapter;

        private readonly Dictionary<int, LevelDefinition> _campaign = new Dictionary<int, LevelDefinition>();
        private readonly GameDatabase _db;
        private readonly EndlessConfig _endless;

        public LevelRepository(GameDatabase db, EndlessConfig endless)
        {
            _db = db;
            _endless = endless;
            for (int c = 1; c <= Chapters; c++)
            {
                var asset = Resources.Load<TextAsset>("Levels/chapter_" + c.ToString("00"));
                if (asset != null) Add(JsonUtility.FromJson<ChapterFile>(asset.text));
            }
        }

        public void Add(ChapterFile file)
        {
            if (file == null) return;
            foreach (var l in file.levels)
            {
                if (string.IsNullOrEmpty(l.theme)) l.theme = file.theme;
                _campaign[l.id] = l;
            }
        }

        public int LoadedCampaignLevels => _campaign.Count;

        public bool IsEndless(int level) => level > CampaignLevels;

        /// <summary>Level 1..200 from data; &gt; 200 generates endless wave (level - 200).</summary>
        public LevelDefinition Get(int level)
        {
            if (level < 1) level = 1;
            if (level <= CampaignLevels)
            {
                if (_campaign.TryGetValue(level, out var def)) return def;
                return ProceduralLevelGenerator.Generate(level, _db, _endless, 13);
            }
            return ProceduralLevelGenerator.Generate(level - CampaignLevels, _db, _endless);
        }

        public static string ThemeForChapter(int chapter)
        {
            int i = Mathf.Clamp(chapter, 1, Chapters) - 1;
            return ProceduralLevelGenerator.Themes[i];
        }
    }
}
