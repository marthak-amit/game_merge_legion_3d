using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace MergeLegion.Editor
{
    /// <summary>
    /// One-click SDK enablement. Packages are added by name (no version pin) so Package Manager picks the version
    /// verified for the installed Unity; the matching scripting define then switches on that SDK's adapter assembly.
    /// </summary>
    public static class SdkInstaller
    {
        private static readonly string[] UgsPackages =
        {
            "com.unity.services.authentication",
            "com.unity.services.cloudsave",
            "com.unity.services.leaderboards",
            "com.unity.services.cloudcode"
        };

        [MenuItem("Tools/Merge Legion/SDKs/1. Install Unity Gaming Services packages")]
        public static void InstallUgs() => AddSequentially(new Queue<string>(UgsPackages));

        [MenuItem("Tools/Merge Legion/SDKs/2. Enable Unity Gaming Services (MERGELEGION_UGS)")]
        public static void EnableUgs() => ScriptingDefines.Set("MERGELEGION_UGS", true);

        [MenuItem("Tools/Merge Legion/SDKs/Disable Unity Gaming Services")]
        public static void DisableUgs() => ScriptingDefines.Set("MERGELEGION_UGS", false);

        public static void AddSequentially(Queue<string> packages)
        {
            if (packages.Count == 0) { Debug.Log("[MergeLegion] SDK packages installed."); return; }
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
