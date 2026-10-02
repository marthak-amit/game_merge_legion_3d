using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MergeLegion.Editor
{
    /// <summary>One-click, repeatable project configuration (portrait, API levels, input backend, URP asset).</summary>
    public static class ProjectSetup
    {
        // Change before the first store build; these identify the app on Google Play / App Store.
        public const string ProductName = "Merge Legion 3D";
        public const string CompanyName = "YourStudio";
        public const string BundleId = "com.yourstudio.mergelegion";
        public const string UrpFolder = "Assets/_Game/Settings";

        [MenuItem("Tools/Merge Legion/Apply Project Settings")]
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

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            EditorUserBuildSettings.buildAppBundle = true;

            bool needsRestart = SetActiveInputHandler();
            EnsureUrpAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("[MergeLegion] Project settings applied.");

            if (needsRestart)
            {
                EditorUtility.DisplayDialog("Restart required",
                    "The active Input Handling backend was switched to the Input System package. Unity must restart for it to take effect.", "OK");
            }
            return needsRestart;
        }

        /// <summary>Creates the URP/Lit template material in Resources so the shader is always included in builds.</summary>
        public static void CreateMaterials()
        {
            const string folder = "Assets/_Game/Resources/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Materials");
            string path = folder + "/UnitLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { enableInstancing = true, name = "UnitLit" };
            AssetDatabase.CreateAsset(mat, path);
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

        private static void EnsureUrpAsset()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            if (!AssetDatabase.IsValidFolder(UrpFolder)) AssetDatabase.CreateFolder("Assets/_Game", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, UrpFolder + "/UniversalRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, UrpFolder + "/UniversalRP.asset");

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }
    }
}
