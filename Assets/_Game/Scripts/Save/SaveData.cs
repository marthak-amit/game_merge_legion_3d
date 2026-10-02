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
