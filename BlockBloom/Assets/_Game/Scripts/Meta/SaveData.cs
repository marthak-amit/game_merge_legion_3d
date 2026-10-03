using System;
using UnityEngine;

namespace BlockBloom
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int coins = 150;
        public int hearts = 5;
        public long heartNextTicks;            // UTC ticks when the next heart is restored (0 = full)
        public int unlockedLevel = 1;          // highest playable adventure level
        public int[] stars = new int[300];     // best stars per level
        public int[] bestLevelScore = new int[300];
        public int classicBest;
        public int dailyBest;
        public string dailyDone = "";          // yyyyMMdd of the last finished daily challenge
        public int boosterUndo = 2, boosterBomb = 2, boosterShuffle = 2;
        public int streak;
        public string lastLoginClaim = "";     // yyyyMMdd
        public string lastSpin = "";
        public int totalLines, gamesPlayed, totalGems, perfectClears;
        public int bestCombo;
        public bool sfxOn = true, musicOn = true, hapticsOn = true;
        public bool tutorialDone;
        public bool ageGateDone, isUnder13, adsPersonalised = true, ratePromptShown;
        public int bloomsTriggered;
        public bool adsRemoved, starterBought;
        public int theme;
        public bool[] themeOwned = new bool[] { true, false, false, false };
        public int chestProgress;              // stars towards the next chest
        public string questDay = "";
        public int[] questProgress = new int[3];
        public bool[] questClaimed = new bool[3];
        public bool questBonusClaimed;
        public int sessionCount;
        public long firstRunTicks;
    }

    public static class Save
    {
        private const string Key = "blockbloom_save_v1";
        public static SaveData Data { get; private set; }
        public static event Action Changed;

        public static void Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                Data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception) { Data = new SaveData(); }
            if (Data == null) Data = new SaveData();
            if (Data.stars == null || Data.stars.Length < 300) Data.stars = new int[300];
            if (Data.bestLevelScore == null || Data.bestLevelScore.Length < 300) Data.bestLevelScore = new int[300];
            if (Data.themeOwned == null || Data.themeOwned.Length < Palette.Themes.Length) Data.themeOwned = new bool[] { true, false, false, false };
            if (Data.questProgress == null || Data.questProgress.Length < 3) Data.questProgress = new int[3];
            if (Data.questClaimed == null || Data.questClaimed.Length < 3) Data.questClaimed = new bool[3];
            if (Data.firstRunTicks == 0) Data.firstRunTicks = DateTime.UtcNow.Ticks;
        }

        public static void Commit()
        {
            try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); } catch (Exception) { }
            var h = Changed; if (h != null) h();
        }

        public static void Wipe() { PlayerPrefs.DeleteKey(Key); Load(); Commit(); }

        public static string Today { get { return DateTime.Now.ToString("yyyyMMdd"); } }
    }
}
