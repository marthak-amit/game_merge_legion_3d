using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.UI;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>
    /// Entry point of the Battle scene. Builds the arena, grid, input and HUD in code so the scene file stays trivial.
    /// </summary>
    public sealed class BattleSceneRoot : MonoBehaviour
    {
        private ArmyService _army;
        private GameConfig _config;
        private ArenaLayout _layout;
        private Camera _camera;

        public static BattleDirector Director { get; private set; }

        private void Awake()
        {
            GameBootstrap.EnsureServices();
            var save = ServiceLocator.Get<Save.SaveService>();
            Loc.Load(save.Data.settings.language);

            _config = ServiceLocator.Get<GameConfig>();
            _army = ServiceLocator.Get<ArmyService>();
            _layout = new ArenaLayout(_config.grid);

            BuildCamera();
            BuildLight();
            BuildGround();

            var db = GameDatabase.Instance;
            var gridView = new GameObject("GridView").AddComponent<GridView>();
            gridView.Init(_army, _layout, db, _camera.transform.rotation);

            var drag = new GameObject("DragController").AddComponent<DragController>();
            drag.Init(_army, gridView, _camera);

            var numbers = new GameObject("DamageNumbers").AddComponent<DamageNumbers>();
            numbers.Init(_camera.transform.rotation);

            var view = new GameObject("BattleView").AddComponent<BattleView>();
            view.Init(db, _config.battle, numbers);

            var bars = new GameObject("HealthBars").AddComponent<HealthBarBatch>();
            bars.Init(_camera, view);

            var hud = new GameObject("Hud").AddComponent<BattleHud>();
            hud.Init(_army, ServiceLocator.Get<CurrencyService>(), db, _army.CurrentLevel);
            hud.HomeClicked += () => Director.GoHome();

            Director = new GameObject("BattleDirector").AddComponent<BattleDirector>();
            Director.Init(_layout, view, bars, hud, gridView, drag);
        }

        private void OnDestroy()
        {
            Director = null;
        }

        private void BuildCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                _camera = go.GetComponent<Camera>();
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.45f, 0.62f, 0.78f);
            var arenaCam = _camera.GetComponent<ArenaCamera>();
            if (arenaCam == null) arenaCam = _camera.gameObject.AddComponent<ArenaCamera>();
            arenaCam.Frame(_layout);
        }

        private static void BuildLight()
        {
            var existing = Object.FindFirstObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional) return;
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        }

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground";
            float width = _layout.HalfWidth * 2f + 6f;
            float depth = _layout.FarZ - _layout.NearZ + 8f;
            ground.transform.position = new Vector3(0f, -0.15f, (_layout.NearZ + _layout.FarZ) * 0.5f);
            ground.transform.localScale = new Vector3(width, 0.3f, depth);
            ground.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.36f, 0.55f, 0.32f));
        }
    }
}
