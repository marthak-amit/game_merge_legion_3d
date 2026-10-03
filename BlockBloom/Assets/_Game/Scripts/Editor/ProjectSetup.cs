using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BlockBloom.Editor
{
    /// <summary>One-click, repeatable project configuration (portrait, API levels, Input System backend).</summary>
    public static class ProjectSetup
    {
        // Change before the first store build; these identify the app on Google Play / App Store.
        public const string ProductName = "Block Bloom";
        public const string CompanyName = "YourStudio";
        public const string BundleId = "com.yourstudio.blockbloom";

        [MenuItem("Tools/Block Bloom/Apply Project Settings")]
        public static bool ApplyProjectSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            bool needsRestart = SetActiveInputHandler();
            AssetDatabase.SaveAssets();
            Debug.Log("[BlockBloom] Project settings applied.");
            return needsRestart;
        }

        private static bool SetActiveInputHandler()
        {
            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == 1) return false;
            prop.intValue = 1; // 0 = old, 1 = new Input System, 2 = both
            so.ApplyModifiedProperties();
            return true;
        }
    }
}
