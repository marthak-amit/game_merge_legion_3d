using System.IO;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeLegion.Editor
{
    /// <summary>Builds the Boot / Main / Battle scenes from code so setup is reproducible. Scene contents are built at runtime.</summary>
    public static class SceneGenerator
    {
        public const string SceneFolder = "Assets/_Game/Scenes";

        [MenuItem("Tools/Merge Legion/Setup All (Settings + Scenes + Data)")]
        public static void SetupAll()
        {
            if (ProjectSetup.ApplyProjectSettings()) return; // restarting; run again afterwards
            BalanceImporter.Import();
            GenerateScenes();
        }

        [MenuItem("Tools/Merge Legion/Generate Scenes")]
        public static void GenerateScenes()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                EditorUtility.DisplayDialog("TextMeshPro resources missing",
                    "Run Window > TextMeshPro > Import TMP Essential Resources, then run Generate Scenes again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectSetup.CreateMaterials();
            Directory.CreateDirectory(SceneFolder);
            string boot = BuildBoot();
            string main = BuildMain();
            string battle = BuildBattle();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(boot, true),
                new EditorBuildSettingsScene(main, true),
                new EditorBuildSettingsScene(battle, true),
            };
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(boot);
            EditorSceneManager.OpenScene(boot);
            Debug.Log("[MergeLegion] Scenes generated. Press Play to boot into the Home screen.");
        }

        private static string BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            new GameObject("GameBootstrap", typeof(GameBootstrap));
            return Save(scene, SceneNames.Boot);
        }

        private static string BuildMain()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return Save(scene, SceneNames.Main);
        }

        private static string BuildBattle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            new GameObject("BattleRoot", typeof(BattleSceneRoot));
            return Save(scene, SceneNames.Battle);
        }

        private static void AddCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.16f);
        }

        private static string Save(Scene scene, string name)
        {
            string path = $"{SceneFolder}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
