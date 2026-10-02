using System;
using System.Collections.Generic;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Meta
{
    [Serializable]
    public sealed class CastleConfig
    {
        public float winsPerHour = 1.5f;     // idle income = this many 1-star wins of the current level per hour
        public float levelStep = 0.25f;      // +25% per castle level
        public int upgradeBaseCost = 500;
        public float upgradeGrowth = 1.6f;
        public int maxLevel = 30;
        public float capHours = 8f;
        public float vipMultiplier = 2f;
        public float adMultiplier = 2f;
    }

    [Serializable]
    public sealed class DropEntry
    {
        public RewardType type;
        public float weight = 1f;
        public int min = 1;
        public int max = 1;
        public string target;
    }

    [Serializable]
    public sealed class ChestConfig
    {
        public string id;
        public string nameKey;
        public int costGems;
        public int costKeys;
        public float cooldownHours;
        public string adPlacement;
        public int rolls = 2;
        public int guaranteedShardMin;
        public int guaranteedShardMax;
        public List<DropEntry> drops = new List<DropEntry>();
    }

    public enum MissionType
    {
        WinLevels, MergeUnits, BuyUnits, UseSkill, WatchAds, EarnCoins, SpendCoins, OpenChests, WinBoss, ThreeStarWins, UpgradeResearch
    }

    [Serializable]
    public sealed class MissionTemplate
    {
        public string id;
        public string titleKey;
        public MissionType type;
        public int target = 1;
        public Reward reward;
        public int minLevel = 1;
    }

    [Serializable]
    public sealed class AchievementTier
    {
        public int target;
        public Reward reward;
    }

    [Serializable]
    public sealed class AchievementTemplate
    {
        public string id;
        public string titleKey;
        public MissionType type;
        public List<AchievementTier> tiers = new List<AchievementTier>();
    }

    [Serializable]
    public sealed class MissionConfig
    {
        public int dailyCount = 3;
        public int weeklyCount = 3;
        public Reward dailyAllBonus;
        public Reward weeklyAllBonus;
        public List<MissionTemplate> daily = new List<MissionTemplate>();
        public List<MissionTemplate> weekly = new List<MissionTemplate>();
        public List<AchievementTemplate> achievements = new List<AchievementTemplate>();
    }

    [Serializable]
    public sealed class LoginConfig
    {
        public bool resetOnMiss = false;
        public List<RewardList> days = new List<RewardList>();
    }

    [Serializable]
    public sealed class SpinSegment
    {
        public string labelKey;
        public float weight = 1f;
        public Reward reward;
    }

    [Serializable]
    public sealed class SpinConfig
    {
        public int freePerDay = 1;
        public List<SpinSegment> segments = new List<SpinSegment>();
    }

    /// <summary>Meta-system tuning (castle, chests, missions, login, spin). Resources/Config/meta_config.json + Remote Config override.</summary>
    [Serializable]
    public sealed class MetaConfig
    {
        public CastleConfig castle = new CastleConfig();
        public List<ChestConfig> chests = new List<ChestConfig>();
        public MissionConfig missions = new MissionConfig();
        public LoginConfig login = new LoginConfig();
        public SpinConfig spin = new SpinConfig();

        public ChestConfig Chest(string id)
        {
            for (int i = 0; i < chests.Count; i++)
                if (chests[i].id == id) return chests[i];
            return null;
        }

        public static MetaConfig FromJson(string json, string overrideJson = null)
        {
            var cfg = new MetaConfig();
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, cfg);
            if (!string.IsNullOrEmpty(overrideJson))
            {
                try { JsonUtility.FromJsonOverwrite(overrideJson, cfg); }
                catch (Exception e) { Debug.LogWarning("[Config] ignoring bad meta override: " + e.Message); }
            }
            return cfg;
        }

        public static MetaConfig Load(IRemoteConfigService remote)
        {
            var asset = Resources.Load<TextAsset>("Config/meta_config");
            string over = remote != null ? remote.GetString(RemoteKeys.MetaOverride, "") : "";
            return FromJson(asset != null ? asset.text : null, over);
        }
    }
}
