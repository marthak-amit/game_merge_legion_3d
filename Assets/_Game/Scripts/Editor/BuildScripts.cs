using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MergeLegion.Editor
{
    /// <summary>
    /// Headless build entry points for CI:
    ///   Unity -batchmode -quit -projectPath . -executeMethod MergeLegion.Editor.BuildScripts.BuildAndroidAab
    ///   Unity -batchmode -quit -projectPath . -executeMethod MergeLegion.Editor.BuildScripts.BuildIosProject
    /// Environment: BUILD_VERSION, BUILD_NUMBER, BUILD_OUTPUT, ANDROID_KEYSTORE_PATH / ANDROID_KEYSTORE_PASS / ANDROID_KEY_ALIAS / ANDROID_KEY_PASS.
    /// </summary>
    public static class BuildScripts
    {
        private static readonly string[] SceneOrder = { "Boot", "Main", "Battle" };

        /// <summary>Scene paths resolved from disk (Boot first), so a stale or unsaved EditorBuildSettings can't produce an empty build.</summary>
        private static string[] Scenes
        {
            get
            {
                var found = SceneOrder.Select(n => $"{SceneGenerator.SceneFolder}/{n}.unity").Where(File.Exists).ToArray();
                return found.Length > 0 ? found : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            }
        }

        [MenuItem("Tools/Merge Legion/Build/Android AAB")]
        public static void BuildAndroidAab()
        {
            ApplyCommon();
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.bundleVersionCode = IntEnv("BUILD_NUMBER", PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            string keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEY_PASS");
            }

            Build(BuildTarget.Android, Path.Combine(Output(), "MergeLegion.aab"));
        }

        /// <summary>Installable debug APK (signed with the Unity debug keystore) for quick device testing.</summary>
        [MenuItem("Tools/Merge Legion/Build/Android debug APK")]
        public static void BuildAndroidApk()
        {
            ApplyCommon();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            PlayerSettings.Android.bundleVersionCode = IntEnv("BUILD_NUMBER", PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.Android.useCustomKeystore = false;
            Build(BuildTarget.Android, Path.Combine(Output(), "MergeLegion.apk"));
        }

        [MenuItem("Tools/Merge Legion/Build/iOS Xcode project")]
        public static void BuildIosProject()
        {
            ApplyCommon();
            PlayerSettings.iOS.buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? PlayerSettings.iOS.buildNumber;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            Build(BuildTarget.iOS, Path.Combine(Output(), "ios"));
        }

        private static void ApplyCommon()
        {
            ProjectSetup.ApplyProjectSettings();
            string version = Environment.GetEnvironmentVariable("BUILD_VERSION");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
            if (Scenes.Length == 0) SceneGenerator.GenerateScenes();
            EditorBuildSettings.scenes = Scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
        }

        private static string Output()
        {
            string dir = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
            if (string.IsNullOrEmpty(dir)) dir = "Builds";
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static int IntEnv(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), out int v) ? v : fallback;

        private static void Build(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {target}: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalWarnings} warnings");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
