using System;
using System.Collections.Generic;

namespace MergeLegion.Levels
{
    [Serializable]
    public sealed class EnemySpawnDef
    {
        public int line;
        public int level = 1;
        public int col;
        public int row;
    }

    [Serializable]
    public sealed class BossDef
    {
        public float hp = 2000f;
        public float damage = 40f;
        public float attackSpeed = 0.5f;
        public float range = 2.6f;
        public float moveSpeed = 1.6f;
        public float scale = 2.8f;
        public float slamInterval = 6f;
        public float slamRadius = 3.2f;
        public float slamDamageMult = 2.5f;
        public float summonInterval = 10f;
        public int summonCount = 2;
        public int minionLine = 0;
        public int minionLevel = 1;
    }

    /// <summary>One battle. Authored in JSON (Resources/Levels/chapter_XX.json) or generated for endless mode.</summary>
    [Serializable]
    public sealed class LevelDefinition
    {
        public int id;            // 1-based campaign number; > 200 = endless wave
        public int chapter;
        public int index;         // 1..20 inside the chapter
        public string theme = "grasslands";
        public bool isBoss;
        public bool hasBoss;
        public int enemyCols = 5;
        public float power;       // informational: designer's power budget for this level
        public float hpMult = 1f;
        public float dmgMult = 1f;
        public bool endless;
        public List<EnemySpawnDef> enemies = new List<EnemySpawnDef>();
        public BossDef boss = new BossDef();
    }

    [Serializable]
    public sealed class ChapterFile
    {
        public int chapter;
        public string theme;
        public List<LevelDefinition> levels = new List<LevelDefinition>();
    }
}
