using System;
using System.Collections.Generic;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Meta.Arena
{
    [Serializable]
    public sealed class ArenaUnit
    {
        public int line;
        public int level;
        public int col;
        public int row;
    }

    /// <summary>A player's army frozen for others to fight (async PvP). Small enough to upload as a Cloud Save / Cloud Code payload.</summary>
    [Serializable]
    public sealed class ArenaSnapshot
    {
        public string playerId;
        public string name;
        public int trophies;
        public float power;
        public string commander;
        public float hpBonus;
        public float dmgBonus;
        public bool isBot;
        public long createdUtcTicks;
        public List<ArenaUnit> units = new List<ArenaUnit>();
    }

    [Serializable]
    public sealed class LeagueDef
    {
        public string id;
        public string nameKey;
        public int minTrophies;
        public List<Reward> weeklyRewards = new List<Reward>();
    }

    [Serializable]
    public sealed class ArenaConfig
    {
        public int unlockLevel = 15;
        public int maxAttempts = 5;
        public float attemptRefillMinutes = 30f;
        public int opponentChoices = 3;
        public int trophyK = 36;          // max swing of one match
        public float eloScale = 400f;
        public int minLoss = 8;
        public int minGain = 10;
        public float winCoinsInWins = 2f; // coins (in 1-star-win units) for a win
        public float loseCoinsInWins = 0.5f;
        public float botPowerEasy = 0.8f;
        public float botPowerEven = 1.0f;
        public float botPowerHard = 1.25f;
        public List<LeagueDef> leagues = new List<LeagueDef>();

        public static ArenaConfig FromJson(string json, string overrideJson = null)
        {
            var cfg = new ArenaConfig();
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, cfg);
            if (!string.IsNullOrEmpty(overrideJson))
            {
                try { JsonUtility.FromJsonOverwrite(overrideJson, cfg); }
                catch (Exception e) { Debug.LogWarning("[Config] ignoring bad arena override: " + e.Message); }
            }
            return cfg;
        }

        public static ArenaConfig Load(IRemoteConfigService remote)
        {
            var asset = Resources.Load<TextAsset>("Config/arena_config");
            string over = remote != null ? remote.GetString(RemoteKeys.ArenaOverride, "") : "";
            return FromJson(asset != null ? asset.text : null, over);
        }

        public int LeagueIndexFor(int trophies)
        {
            int idx = 0;
            for (int i = 0; i < leagues.Count; i++) if (trophies >= leagues[i].minTrophies) idx = i;
            return idx;
        }
    }

    public static class LeaderboardIds
    {
        public const string CampaignLevel = "campaign_level";
        public const string EndlessWave = "endless_wave";
        public const string ArenaTrophies = "arena_trophies";
    }

    /// <summary>Backend for async PvP. Mock = bots + local snapshots; production = Unity Cloud Code + Cloud Save (see Services/Ugs).</summary>
    public interface IArenaBackend
    {
        void Upload(ArenaSnapshot snapshot, Action<bool> onDone);
        void FindOpponents(ArenaSnapshot me, int count, Action<List<ArenaSnapshot>> onDone);
    }
}
