using System;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Data
{
    [Serializable]
    public sealed class GridConfig
    {
        public int cols = 5;
        public int rows = 3;
        public int maxUnitLevel = 8;
        public float cellSize = 1.7f;
        public float frontZ = -3.0f;
        public int startCoins = 200;
        public float freeUnitCooldownSeconds = 180f;
        public int freeUnitLevelOffset = 2;   // free unit = highest merged level - offset (min 1)
    }

    [Serializable]
    public sealed class BattleConfig
    {
        public float fixedStep = 0.0333333f;
        public float maxDurationSeconds = 120f;
        public float retargetInterval = 0.4f;
        public float separationRadius = 0.9f;
        public float flyingSpeedMultiplier = 1f;
        public float tauntRange = 6f;
        public float threeStarHpPct = 0.66f;
        public float twoStarHpPct = 0.33f;
        public float revivePct = 0.5f;
        public float enemyHpScalePerChapter = 1f;
        public float damageNumberMinValue = 1f;
        public float slowMoScale = 0.25f;
        public float slowMoSeconds = 0.6f;
    }

    [Serializable]
    public sealed class RewardConfig
    {
        public int levelCoinBase = 60;
        public float levelCoinGrowth = 1.09f;
        public float starBonusPct = 0.15f;      // per extra star
        public float loseConsolationPct = 0.25f;
        public int adMultiplier = 3;
        public int keyProgressPerWin = 1;
        public int keysPerChestCycle = 3;
        public int bossBonusGems = 10;
        public int battlePassXpPerWin = 40;
        public int battlePassXpPerBossWin = 120;
    }

    /// <summary>
    /// All tunable numbers. Loaded from Resources/Config/game_config.json, then partially overridden by the
    /// Remote Config string "config_override" (JsonUtility.FromJsonOverwrite), so any value can change without an app update.
    /// </summary>
    [Serializable]
    public sealed class GameConfig
    {
        public GridConfig grid = new GridConfig();
        public BattleConfig battle = new BattleConfig();
        public RewardConfig rewards = new RewardConfig();

        public static GameConfig FromJson(string json, string overrideJson = null)
        {
            var cfg = new GameConfig();
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, cfg);
            if (!string.IsNullOrEmpty(overrideJson))
            {
                try { JsonUtility.FromJsonOverwrite(overrideJson, cfg); }
                catch (Exception e) { Debug.LogWarning("[Config] ignoring bad remote override: " + e.Message); }
            }
            return cfg;
        }

        public static GameConfig Load(IRemoteConfigService remote)
        {
            var asset = Resources.Load<TextAsset>("Config/game_config");
            string over = remote != null ? remote.GetString(RemoteKeys.ConfigOverride, "") : "";
            return FromJson(asset != null ? asset.text : null, over);
        }
    }
}
