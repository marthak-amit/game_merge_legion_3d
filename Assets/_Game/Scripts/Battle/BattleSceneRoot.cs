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
            var sun = BuildLight();

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

            var theme = new GameObject("ArenaTheme").AddComponent<ArenaThemeBuilder>();
            theme.Init(_layout, _camera, sun, gridView);

            // menus only show banners; never in battle
            if (ServiceLocator.TryGet<Monetization.AdsManager>(out var ads)) ads.SetBannerVisible(false);
            if (ServiceLocator.TryGet<Audio.IAudioService>(out var audio)) audio.PlayMusic("battle");

            // tutorial pointers into the 3D grid
            Tutorial.TutorialTargets.RegisterWorld("merge_a", () => MergePairWorld(gridView, true));
            Tutorial.TutorialTargets.RegisterWorld("merge_b", () => MergePairWorld(gridView, false));

            Director = new GameObject("BattleDirector").AddComponent<BattleDirector>();
            Director.Init(_layout, view, bars, hud, gridView, drag, theme);
        }

        private Vector3 MergePairWorld(GridView grid, bool first)
        {
            if (!_army.TryFindMergePair(out int a, out int b)) return new Vector3(0, -100, 0);
            return grid.CellWorld(first ? a : b) + Vector3.up * 0.6f;
        }

        private void OnDestroy()
        {
            Director = null;
            Tutorial.TutorialTargets.Unregister("merge_a");
            Tutorial.TutorialTargets.Unregister("merge_b");
            Tutorial.TutorialTargets.Unregister("fight");
            Tutorial.TutorialTargets.Unregister("skill");
            for (int i = 0; i < Data.UnitLines.Count; i++) Tutorial.TutorialTargets.Unregister("buy_" + (Data.UnitLineId)i);
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

        private static Light BuildLight()
        {
            var existing = Object.FindFirstObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional) return existing;
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            return light;
        }
    }
}
