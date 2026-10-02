using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace MergeLegion.Editor
{
    /// <summary>
    /// One-click SDK enablement (Tools > Merge Legion > SDKs). Packages are added by name so Package Manager picks the
    /// version verified for the installed Unity, third-party registries are added to Packages/manifest.json, and the
    /// matching scripting define switches on that SDK's adapter code in Assets/_Game/Sdk.
    /// Every SDK stays off (Mock) until you enable it, so the game always runs in the Editor without keys.
    /// </summary>
    public static class SdkInstaller
    {
        private const string Unity = "";
        private const string OpenUpm = "https://package.openupm.com";
        private const string AppLovin = "https://unity.packages.applovin.com/";

        // ---------------------------------------------------------------- Unity Gaming Services

        [MenuItem("Tools/Merge Legion/SDKs/Unity Gaming Services/1. Install packages")]
        public static void InstallUgs() => Install(Unity, null, null,
            "com.unity.services.authentication", "com.unity.services.cloudsave", "com.unity.services.leaderboards", "com.unity.services.cloudcode");

        [MenuItem("Tools/Merge Legion/SDKs/Unity Gaming Services/2. Enable (MERGELEGION_UGS)")]
        public static void EnableUgs() => ScriptingDefines.Set("MERGELEGION_UGS", true);

        // ---------------------------------------------------------------- AppLovin MAX

        [MenuItem("Tools/Merge Legion/SDKs/AppLovin MAX/1. Install plugin")]
        public static void InstallMax() => Install("AppLovin MAX Unity", AppLovin, new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters" },
            "com.applovin.mediation.ads");

        [MenuItem("Tools/Merge Legion/SDKs/AppLovin MAX/2. Enable (MERGELEGION_MAX)")]
        public static void EnableMax() => ScriptingDefines.Set("MERGELEGION_MAX", true);

        // ---------------------------------------------------------------- Unity IAP / notifications / ATT

        [MenuItem("Tools/Merge Legion/SDKs/Unity IAP/1. Install (v4.12)")]
        public static void InstallIap() => Install(Unity, null, null, "com.unity.purchasing@4.12.2");

        [MenuItem("Tools/Merge Legion/SDKs/Unity IAP/2. Enable (MERGELEGION_IAP)")]
        public static void EnableIap() => ScriptingDefines.Set("MERGELEGION_IAP", true);

        [MenuItem("Tools/Merge Legion/SDKs/Notifications/1. Install Mobile Notifications")]
        public static void InstallNotifications() => Install(Unity, null, null, "com.unity.mobilenotifications");

        [MenuItem("Tools/Merge Legion/SDKs/Notifications/2. Enable (MERGELEGION_NOTIFICATIONS)")]
        public static void EnableNotifications() => ScriptingDefines.Set("MERGELEGION_NOTIFICATIONS", true);

        [MenuItem("Tools/Merge Legion/SDKs/iOS ATT/1. Install iOS 14 Advertising Support")]
        public static void InstallAtt() => Install(Unity, null, null, "com.unity.ads.ios-support");

        [MenuItem("Tools/Merge Legion/SDKs/iOS ATT/2. Enable (MERGELEGION_ATT)")]
        public static void EnableAtt() => ScriptingDefines.Set("MERGELEGION_ATT", true);

        // ---------------------------------------------------------------- OpenUPM packages

        [MenuItem("Tools/Merge Legion/SDKs/Firebase/1. Install (Analytics, Remote Config, Crashlytics, Messaging)")]
        public static void InstallFirebase() => Install("OpenUPM", OpenUpm, new[] { "com.google.firebase", "com.google.external-dependency-manager" },
            "com.google.firebase.app", "com.google.firebase.analytics", "com.google.firebase.remote-config",
            "com.google.firebase.crashlytics", "com.google.firebase.messaging");

        [MenuItem("Tools/Merge Legion/SDKs/Firebase/2. Enable (MERGELEGION_FIREBASE)")]
        public static void EnableFirebase() => ScriptingDefines.Set("MERGELEGION_FIREBASE", true);

        [MenuItem("Tools/Merge Legion/SDKs/GameAnalytics/1. Install")]
        public static void InstallGameAnalytics() => Install("OpenUPM", OpenUpm, new[] { "com.gameanalytics" }, "com.gameanalytics.sdk");

        [MenuItem("Tools/Merge Legion/SDKs/GameAnalytics/2. Enable (MERGELEGION_GA)")]
        public static void EnableGameAnalytics() => ScriptingDefines.Set("MERGELEGION_GA", true);

        [MenuItem("Tools/Merge Legion/SDKs/AppsFlyer/1. Install")]
        public static void InstallAppsFlyer() => Install("OpenUPM", OpenUpm, new[] { "com.appsflyer" }, "com.appsflyer.unity");

        [MenuItem("Tools/Merge Legion/SDKs/AppsFlyer/2. Enable (MERGELEGION_APPSFLYER)")]
        public static void EnableAppsFlyer() => ScriptingDefines.Set("MERGELEGION_APPSFLYER", true);

        [MenuItem("Tools/Merge Legion/SDKs/Google Play Games + Game Center/1. Install Play Games plugin")]
        public static void InstallPlayGames() => Install("OpenUPM", OpenUpm, new[] { "com.google.play" }, "com.google.play.games");

        [MenuItem("Tools/Merge Legion/SDKs/Google Play Games + Game Center/2. Enable (MERGELEGION_SOCIAL)")]
        public static void EnableSocial() => ScriptingDefines.Set("MERGELEGION_SOCIAL", true);

        [MenuItem("Tools/Merge Legion/SDKs/Create sdk_keys.json from example")]
        public static void CreateKeysFile()
        {
            const string dir = "Assets/_Game/Resources/Config";
            string target = dir + "/sdk_keys.json";
            if (File.Exists(target)) { EditorUtility.RevealInFinder(target); return; }
            File.Copy(dir + "/sdk_keys.example.json", target);
            AssetDatabase.Refresh();
            Debug.Log("[MergeLegion] Created " + target + " (git-ignored). Fill in your keys.");
        }

        [MenuItem("Tools/Merge Legion/SDKs/Disable ALL SDKs (back to mocks)")]
        public static void DisableAll()
        {
            foreach (var d in new[] { "MERGELEGION_UGS", "MERGELEGION_MAX", "MERGELEGION_IAP", "MERGELEGION_NOTIFICATIONS", "MERGELEGION_ATT",
                         "MERGELEGION_FIREBASE", "MERGELEGION_GA", "MERGELEGION_APPSFLYER", "MERGELEGION_SOCIAL" })
                ScriptingDefines.Set(d, false);
        }

        // ---------------------------------------------------------------- plumbing

        private static void Install(string registryName, string registryUrl, string[] scopes, params string[] packages)
        {
            if (!string.IsNullOrEmpty(registryUrl)) EnsureScopedRegistry(registryName, registryUrl, scopes);
            AddSequentially(new Queue<string>(packages));
        }

        /// <summary>Adds a scoped registry to Packages/manifest.json (idempotent).</summary>
        public static void EnsureScopedRegistry(string name, string url, string[] scopes)
        {
            const string path = "Packages/manifest.json";
            string json = File.ReadAllText(path);
            if (json.Contains(url)) return;

            var scopeText = new System.Text.StringBuilder();
            for (int i = 0; i < scopes.Length; i++) scopeText.Append(i > 0 ? ", " : "").Append('"').Append(scopes[i]).Append('"');
            string entry = "{ \"name\": \"" + name + "\", \"url\": \"" + url + "\", \"scopes\": [ " + scopeText + " ] }";

            int arr = json.IndexOf("\"scopedRegistries\"", System.StringComparison.Ordinal);
            if (arr >= 0)
            {
                int bracket = json.IndexOf('[', arr);
                json = json.Insert(bracket + 1, "\n    " + entry + ",");
            }
            else
            {
                int brace = json.IndexOf('{');
                json = json.Insert(brace + 1, "\n  \"scopedRegistries\": [\n    " + entry + "\n  ],");
            }
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log("[MergeLegion] Added scoped registry " + name);
        }

        public static void AddSequentially(Queue<string> packages)
        {
            if (packages.Count == 0) { Debug.Log("[MergeLegion] SDK packages installed. Now run the matching 'Enable' menu item."); return; }
            string id = packages.Dequeue();
            Debug.Log("[MergeLegion] Adding package " + id);
            var request = Client.Add(id);
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= poll;
                if (request.Status == StatusCode.Failure) Debug.LogError("[MergeLegion] Failed to add " + id + ": " + request.Error.message);
                else AddSequentially(packages);
            };
            EditorApplication.update += poll;
        }
    }
}
