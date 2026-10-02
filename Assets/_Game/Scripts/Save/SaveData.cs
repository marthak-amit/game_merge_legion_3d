using System;
using System.Collections.Generic;

namespace MergeLegion.Save
{
    /// <summary>Root of the player's persisted state. Add fields freely (JsonUtility defaults them); use a migration only for renames/reshapes.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public string playerId = Guid.NewGuid().ToString("N");
        public long createdUtcTicks;
        public long lastSavedUtcTicks;
        public long totalPlaytimeSeconds;
        public int sessionCount;

        // Progress
        public int highestCampaignLevel;
        public int endlessBestWave;
        public List<IntEntry> levelStars = new List<IntEntry>();
        public int winStreak;
        public int loseStreak;
        public int totalWins;
        public int totalLosses;

        // Currencies (section 2.1)
        public long coins;
        public long gems;
        public long chestKeys;
        public long battlePassXp;
        public List<IntEntry> commanderShards = new List<IntEntry>();

        // Army (persists between levels)
        public List<GridCellSave> grid = new List<GridCellSave>();
        public List<IntEntry> buyCounts = new List<IntEntry>();
        public int highestMergedLevel = 1;
        public long lastFreeUnitUtcTicks;

        // Research Lab: key "{line}_{stat}" -> level
        public List<IntEntry> research = new List<IntEntry>();

        // Commanders
        public string equippedCommander = "";
        public List<IntEntry> commanderLevels = new List<IntEntry>();

        // Time-keyed counters (daily/weekly resets), ad cooldowns
        public DailyData daily = new DailyData();
        public List<LongEntry> lastAdTicks = new List<LongEntry>();

        // Entitlements
        public bool noAds;
        public long vipUntilUtcTicks;

        public SettingsData settings = new SettingsData();
        public ConsentData consent = new ConsentData();

        /// <summary>Free-form one-shot flags (tutorial steps done, offers shown...).</summary>
        public List<string> flags = new List<string>();

        /// <summary>Set when the checksum did not match on load. Never reset by the client.</summary>
        public bool tamperDetected;

        /// <summary>Higher = further along. Used to resolve local vs cloud conflicts.</summary>
        public long ProgressScore => (long)highestCampaignLevel * 100000L + endlessBestWave;

        public bool HasFlag(string flag) => flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!flags.Contains(flag)) flags.Add(flag);
        }
    }

    [Serializable]
    public sealed class IntEntry
    {
        public string key;
        public int value;
    }

    [Serializable]
    public sealed class GridCellSave
    {
        public int col;
        public int row;
        public int line;
        public int level;
    }

    [Serializable]
    public sealed class LongEntry
    {
        public string key;
        public long value;
    }

    [Serializable]
    public sealed class DailyData
    {
        public int dayKey;      // yyyymmdd of the local day the counters belong to
        public int weekKey;     // yyyymmdd of the Monday of the local week
        public List<IntEntry> counts = new List<IntEntry>();
        public List<IntEntry> weeklyCounts = new List<IntEntry>();
    }

    public static class LongEntries
    {
        public static long Get(List<LongEntry> list, string key, long fallback = 0)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == key) return list[i].value;
            return fallback;
        }

        public static void Set(List<LongEntry> list, string key, long value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key != key) continue;
                list[i].value = value;
                return;
            }
            list.Add(new LongEntry { key = key, value = value });
        }
    }

    public static class IntEntries
    {
        public static int Get(List<IntEntry> list, string key, int fallback = 0)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].key == key) return list[i].value;
            return fallback;
        }

        public static void Set(List<IntEntry> list, string key, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key != key) continue;
                list[i].value = value;
                return;
            }
            list.Add(new IntEntry { key = key, value = value });
        }

        public static int Add(List<IntEntry> list, string key, int delta)
        {
            int v = Get(list, key) + delta;
            Set(list, key, v);
            return v;
        }
    }

    [Serializable]
    public sealed class SettingsData
    {
        public bool musicOn = true;
        public bool sfxOn = true;
        public bool hapticsOn = true;
        public string language = "en";
    }

    [Serializable]
    public sealed class ConsentData
    {
        public bool ageGatePassed;
        public bool consentAnswered;
        public bool trackingPromptShown;
    }
}
