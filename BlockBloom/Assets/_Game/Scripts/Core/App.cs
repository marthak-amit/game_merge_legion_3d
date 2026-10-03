using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using BlockBloom.Core;
using BlockBloom.Logic;
using BlockBloom.Services;
using BlockBloom.Services.Mock;

namespace BlockBloom
{
    public abstract class ScreenBase : MonoBehaviour
    {
        public RectTransform Rt;
        public virtual void OnBack() { }
    }

    /// <summary>App root: boots services, owns the canvas layers, navigation, toast and the shared background.</summary>
    public sealed class App : MonoBehaviour
    {
        public static App I { get; private set; }

        public Canvas Canvas;
        public RectTransform Safe, ScreenLayer, PopupLayer, DragLayer;
        public Fx Fx;
        public ScreenBase Current;
        public readonly List<Popup> Popups = new List<Popup>();

        private Image _fade;
        private Image _bgBottom, _bgTop;
        private readonly List<RectTransform> _bokeh = new List<RectTransform>();
        private Rect _lastSafe;
        private Text _toast; private Image _toastBg; private float _toastT;
        private bool _booting;

        private void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Save.Load();
            Economy.TickHearts();
            Economy.EnsureQuestDay();
            Save.Data.sessionCount++;
            Save.Commit();
            InstallServices();
            Sfx.Init();
            BuildRoot();
        }

        private void Start()
        {
            Monet.Log("session_start", "n", Save.Data.sessionCount);
            if (AutoScreenshot.TryAttach(gameObject)) { ShowHome(); return; }
            if (!Save.Data.tutorialDone) StartLevel(1);
            else ShowHome();
        }

        // ---------- services ----------
        private void InstallServices()
        {
            ServiceLocator.Clear();
            var ads = new MockAdsService();
            ads.ForcedAdsRemoved = Save.Data.adsRemoved;
            ads.Initialize();
            ServiceLocator.Register<IAdsService>(ads);
            var iap = new MockIAPService();
            var prices = new Dictionary<string, decimal>();
            var kinds = new Dictionary<string, ProductKind>();
            var skus = new List<string>();
            foreach (var p in Economy.Products)
            {
                prices[p.Sku] = p.Price; skus.Add(p.Sku);
                kinds[p.Sku] = p.Kind == Economy.ProductKindEx.Coins ? ProductKind.Consumable : ProductKind.NonConsumable;
            }
            iap.PriceLookup = s => prices[s];
            iap.KindLookup = s => kinds[s];
            iap.Initialize(skus);
            ServiceLocator.Register<IIAPService>(iap);
            var an = new MockAnalyticsService(); an.LogToConsole = false;
            ServiceLocator.Register<IAnalyticsService>(an);
        }

        // ---------- canvas ----------
        private void BuildRoot()
        {
            var cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.layer = 5;
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Ui.FitCanvas(Canvas);

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
                EventSystem.current.pixelDragThreshold = 6;
            }

            var root = (RectTransform)cgo.transform;
            BuildBackground(root);
            Safe = Ui.Rect(root, "Safe"); Ui.Stretch(Safe);
            ScreenLayer = Ui.Rect(Safe, "Screens"); Ui.Stretch(ScreenLayer);
            DragLayer = Ui.Rect(root, "Drag"); Ui.Stretch(DragLayer);
            Fx = Fx.Create(root);
            PopupLayer = Ui.Rect(Safe, "Popups"); Ui.Stretch(PopupLayer);

            var toast = Ui.Rect(root, "Toast");
            Ui.At(toast, new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(820, 96));
            _toastBg = Ui.RoundImg(toast, new Color(0.08f, 0.05f, 0.25f, 0.92f), 40, "bg");
            Ui.Stretch(_toastBg.rectTransform);
            _toast = Ui.Label(toast, "", 38, Color.white, TextAnchor.MiddleCenter, false);
            Ui.Stretch(_toast.rectTransform, 20, 0, 20, 0);
            _toast.resizeTextForBestFit = true; _toast.resizeTextMinSize = 20; _toast.resizeTextMaxSize = 38;
            toast.gameObject.SetActive(false);

            _fade = Ui.Img(root, Sprites.Square(), Color.black, "fade", false);
            Ui.Stretch(_fade.rectTransform);
            _fade.color = new Color(0, 0, 0, 1);
            ApplySafeArea();
        }

        private void BuildBackground(RectTransform root)
        {
            var bg = Ui.Rect(root, "Background"); Ui.Stretch(bg);
            _bgBottom = Ui.Img(bg, Sprites.Square(), Color.black, "bottom");
            Ui.Stretch(_bgBottom.rectTransform);
            _bgTop = Ui.Img(bg, Sprites.Fade(), Color.white, "top");
            Ui.Stretch(_bgTop.rectTransform);
            var glow = Sprites.Glow();
            for (int i = 0; i < 14; i++)
            {
                var b = Ui.Img(bg, glow, new Color(1, 1, 1, UnityEngine.Random.Range(0.04f, 0.10f)), "bokeh");
                float s = UnityEngine.Random.Range(160f, 420f);
                Ui.At(b.rectTransform, new Vector2(UnityEngine.Random.value, UnityEngine.Random.value), Vector2.zero, new Vector2(s, s));
                _bokeh.Add(b.rectTransform);
            }
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            var th = Palette.Themes[Mathf.Clamp(Save.Data.theme, 0, Palette.Themes.Length - 1)];
            _bgBottom.color = th.BgBottom;
            _bgTop.color = th.BgTop;
        }

        private void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa.width <= 0 || Screen.width <= 0) return;
            _lastSafe = sa;
            Safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            Safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            Safe.offsetMin = Safe.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafe) ApplySafeArea();
            for (int i = 0; i < _bokeh.Count; i++)
            {
                var p = _bokeh[i].anchoredPosition;
                p.x += Mathf.Sin(Time.unscaledTime * 0.1f + i) * 6f * Time.unscaledDeltaTime;
                p.y += (8f + i % 4 * 3f) * Time.unscaledDeltaTime;
                if (p.y > Screen.height + 300) p.y = -Screen.height - 300;
                _bokeh[i].anchoredPosition = p;
            }
            if (_toastT > 0)
            {
                _toastT -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(Mathf.Min(_toastT, 0.25f) / 0.25f);
                _toastBg.color = new Color(0.08f, 0.05f, 0.25f, 0.92f * a);
                _toast.color = new Color(1, 1, 1, a);
                if (_toastT <= 0) _toastBg.transform.parent.gameObject.SetActive(false);
            }
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
        }

        public void Back()
        {
            if (Popups.Count > 0) { Popups[Popups.Count - 1].Close(); return; }
            if (Current != null) Current.OnBack();
        }

        public void Toast(string msg)
        {
            _toast.text = msg; _toastT = 2.2f;
            var go = _toastBg.transform.parent.gameObject;
            go.SetActive(true); go.transform.SetAsLastSibling();
            _toastBg.color = new Color(0.08f, 0.05f, 0.25f, 0.92f);
        }

        // ---------- navigation ----------
        private T Open<T>() where T : ScreenBase
        {
            var rt = Ui.Rect(ScreenLayer, typeof(T).Name);
            Ui.Stretch(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.Rt = rt;
            return s;
        }

        private void Swap(Action build)
        {
            Tween.Kill(_fade);
            float from = _fade.color.a;
            _fade.raycastTarget = true;
            Tween.Value(0.16f * (1f - from) + 0.0001f, k => SetFade(Mathf.Lerp(from, 1f, k)), Ease.Linear, () =>
            {
                for (int i = Popups.Count - 1; i >= 0; i--) Popups[i].Close(true);
                if (Current != null) Destroy(Current.gameObject);
                if (Fx != null) Fx.Clear();
                Current = null;
                build();
                Tween.Value(0.22f, k => SetFade(1f - k), Ease.Linear, () => { _fade.raycastTarget = false; }, 0f, _fade);
            }, 0f, _fade);
        }

        private void SetFade(float a) { _fade.color = new Color(0, 0, 0, a); }

        public void ShowHome() { Swap(() => { var s = Open<HomeScreen>(); Current = s; s.Build(); }); }
        public void ShowMap() { Swap(() => { var s = Open<MapScreen>(); Current = s; s.Build(); }); }

        public void StartLevel(int level)
        {
            Swap(() => { var s = Open<GameScreen>(); Current = s; s.Begin(Mode.Adventure, level); });
        }
        public void StartClassic() { Swap(() => { var s = Open<GameScreen>(); Current = s; s.Begin(Mode.Classic, 0); }); }
        public void StartDaily() { Swap(() => { var s = Open<GameScreen>(); Current = s; s.Begin(Mode.Daily, 0); }); }

        private void OnApplicationPause(bool paused)
        {
            if (paused) { Save.Commit(); }
            else Economy.TickHearts();
        }
    }
}
