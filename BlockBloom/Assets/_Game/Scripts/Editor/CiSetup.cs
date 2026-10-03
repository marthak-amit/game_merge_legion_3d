using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlockBloom.Editor
{
    /// <summary>CI helpers. The game builds its whole scene graph at runtime, so the only asset needed is one empty scene.</summary>
    public static class CiSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        /// <summary>First CI pass: just opens the project so packages resolve into Library/PackageCache.</summary>
        public static void ResolvePackages() => EditorApplication.Exit(0);

        /// <summary>Second CI pass: project settings (needs a restart for the input backend) and the empty bootstrap scene.</summary>
        public static void Prepare()
        {
            ProjectSetup.ApplyProjectSettings();
            EnsureTmpEssentials();
            CreateMainScene();
            bool ok = File.Exists(ScenePath);
            if (!ok) Debug.LogError("[CiSetup] Scene was not created.");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Tools/Block Bloom/Setup All (Settings + Scene)")]
        public static void SetupAll()
        {
            ProjectSetup.ApplyProjectSettings();
            EnsureTmpEssentials();
            CreateMainScene();
        }

        public static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var c = cam.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.07f, 0.08f, 0.16f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        /// <summary>TMP essentials normally need an interactive import; CI extracts them with tools/extract_unitypackage.py first.</summary>
        public static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
            const string pkg = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            if (File.Exists(pkg)) AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            return AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;
        }
    }
}
