using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockBloom.Editor
{
    /// <summary>
    /// Headless build entry points for CI (see .github/workflows/blockbloom.yml):
    ///   BlockBloom.Editor.BuildScripts.BuildAndroidApk | BuildAndroidAab | BuildLinuxPlayer | BuildIosProject
    /// Output goes to &lt;repo&gt;/Builds (override with BUILD_OUTPUT). Signing: ANDROID_KEYSTORE_PATH / _PASS / ANDROID_KEY_ALIAS / _PASS.
    /// </summary>
    public static class BuildScripts
    {
        private static string[] Scenes => new[] { CiSetup.ScenePath }.Where(File.Exists).ToArray();

        [MenuItem("Tools/Block Bloom/Build/Android debug APK")]
        public static void BuildAndroidApk()
        {
            ApplyCommon();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.bundleVersionCode = IntEnv("BUILD_NUMBER", PlayerSettings.Android.bundleVersionCode);
            if (Environment.GetEnvironmentVariable("BUILD_FAST") == "1") PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Build(BuildTarget.Android, Path.Combine(Output(), "BlockBloom.apk"));
        }

        [MenuItem("Tools/Block Bloom/Build/Android AAB (Play Store)")]
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
            Build(BuildTarget.Android, Path.Combine(Output(), "BlockBloom.aab"));
        }

        /// <summary>Desktop Linux player; CI runs it under xvfb with "-bbshots" to produce screenshots.</summary>
        public static void BuildLinuxPlayer()
        {
            ApplyCommon();
            Build(BuildTarget.StandaloneLinux64, Path.Combine(Output(), "linux", "BlockBloom.x86_64"));
        }

        [MenuItem("Tools/Block Bloom/Build/iOS Xcode project")]
        public static void BuildIosProject()
        {
            ApplyCommon();
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
            if (Scenes.Length == 0) CiSetup.CreateMainScene();
            EditorBuildSettings.scenes = Scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
        }

        private static string Output()
        {
            string dir = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static int IntEnv(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), out int v) ? v : fallback;

        private static void Build(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions { scenes = Scenes, locationPathName = path, target = target, options = BuildOptions.None };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {target}: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalWarnings} warnings");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
