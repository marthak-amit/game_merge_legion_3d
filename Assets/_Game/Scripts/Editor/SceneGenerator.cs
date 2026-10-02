using System.IO;
using MergeLegion.Core;
using MergeLegion.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeLegion.Editor
{
    /// <summary>Builds the Boot / Main / Battle scenes from code so setup is reproducible.</summary>
    public static class SceneGenerator
    {
        public const string SceneFolder = "Assets/_Game/Scenes";

        private static readonly Color Background = new Color(0.07f, 0.09f, 0.16f);
        private static readonly Color Accent = new Color(0.95f, 0.62f, 0.12f);

        [MenuItem("Tools/Merge Legion/Setup All (Settings + Scenes)")]
        public static void SetupAll()
        {
            if (ProjectSetup.ApplyProjectSettings()) return; // restarting; run again afterwards
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

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = UIBuilder.NewRect("Background", canvasGo.transform);
            UIBuilder.Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = Background;

            var home = BuildHome(canvasGo.transform);
            UIBuilder.SetRefList(canvasGo.GetComponent<UIManager>(), "screens", new Object[] { home });
            return Save(scene, SceneNames.Main);
        }

        private static HomeScreen BuildHome(Transform canvas)
        {
            var root = UIBuilder.NewRect("HomeScreen", canvas);
            UIBuilder.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var home = root.gameObject.AddComponent<HomeScreen>();
            UIBuilder.SetEnum(home, "id", (int)ScreenId.Home);

            var safe = UIBuilder.NewRect("SafeArea", root);
            UIBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var title = UIBuilder.Text(safe, "Title", "game.title", 120, Accent);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(980, 200));

            var battle = UIBuilder.Button(safe, "BattleButton", "home.battle", Accent, new Vector2(620, 180));
            UIBuilder.Anchor((RectTransform)battle.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 180));

            var soon = UIBuilder.Text(safe, "ComingSoon", "home.coming_soon", 42, new Color(1, 1, 1, 0.6f));
            UIBuilder.Anchor(soon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(900, 80));

            var version = UIBuilder.Text(safe, "Version", null, 32, new Color(1, 1, 1, 0.4f));
            UIBuilder.Anchor(version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(400, 60));

            UIBuilder.SetRef(home, "versionLabel", version);
            UIBuilder.SetRef(home, "battleButton", battle);
            return home;
        }

        private static string BuildBattle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("BattleRoot"); // populated in Phase 3
            return Save(scene, SceneNames.Battle);
        }

        private static void AddCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
        }

        private static string Save(Scene scene, string name)
        {
            string path = $"{SceneFolder}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
