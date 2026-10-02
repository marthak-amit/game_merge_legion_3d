using System;
using UnityEngine;

namespace MergeLegion.Services
{
    /// <summary>
    /// All third-party keys and ids in one JSON file: Resources/Config/sdk_keys.json (copy sdk_keys.example.json).
    /// Real keys are not committed (see .gitignore). Empty values keep the matching SDK in mock mode.
    /// </summary>
    [Serializable]
    public sealed class SdkKeys
    {
        public string maxSdkKey = "";
        public string maxRewardedAndroid = "", maxRewardedIos = "";
        public string maxInterstitialAndroid = "", maxInterstitialIos = "";
        public string maxBannerAndroid = "", maxBannerIos = "";
        public string gameAnalyticsNote = "GameAnalytics game/secret keys are stored in the GameAnalytics settings asset (Window > GameAnalytics > Select Settings).";
        public string appsFlyerDevKey = "";
        public string appsFlyerAppIdIos = "";
        public string playGamesLeaderboardCampaign = "", playGamesLeaderboardEndless = "", playGamesLeaderboardArena = "";
        public string gameCenterLeaderboardCampaign = "", gameCenterLeaderboardEndless = "", gameCenterLeaderboardArena = "";
        public string firebaseNote = "Firebase uses google-services.json (Android) and GoogleService-Info.plist (iOS) placed in Assets/.";

        private static SdkKeys _instance;

        public static SdkKeys Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var asset = Resources.Load<TextAsset>("Config/sdk_keys");
                _instance = new SdkKeys();
                if (asset != null) JsonUtility.FromJsonOverwrite(asset.text, _instance);
                return _instance;
            }
        }

        public string MaxRewarded => Application.platform == RuntimePlatform.IPhonePlayer ? maxRewardedIos : maxRewardedAndroid;
        public string MaxInterstitial => Application.platform == RuntimePlatform.IPhonePlayer ? maxInterstitialIos : maxInterstitialAndroid;
        public string MaxBanner => Application.platform == RuntimePlatform.IPhonePlayer ? maxBannerIos : maxBannerAndroid;

        public string LeaderboardId(string boardId)
        {
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            switch (boardId)
            {
                case "campaign_level": return ios ? gameCenterLeaderboardCampaign : playGamesLeaderboardCampaign;
                case "endless_wave": return ios ? gameCenterLeaderboardEndless : playGamesLeaderboardEndless;
                case "arena_trophies": return ios ? gameCenterLeaderboardArena : playGamesLeaderboardArena;
                default: return "";
            }
        }
    }
}
