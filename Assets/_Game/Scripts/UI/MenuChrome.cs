using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services.Mock;
using MergeLegion.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Persistent frame around menu screens: currency bar, settings/profile, tab bar and the banner strip.</summary>
    public sealed class MenuChrome : MonoBehaviour
    {
        private static readonly ScreenId[] Tabs = { ScreenId.Army, ScreenId.Commanders, ScreenId.Home, ScreenId.Shop, ScreenId.Missions };

        private readonly Image[] _tabBg = new Image[5];
        private readonly Image[] _tabIcon = new Image[5];
        private readonly GameObject[] _tabBadge = new GameObject[5];
        private TMP_Text _coins, _gems, _keys;
        private GameObject _bannerStrip;
        private UIManager _ui;
        private float _timer;

        public static MenuChrome Create(Transform canvas, UIManager ui)
        {
            var go = new GameObject("MenuChrome", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            UIKit.Stretch((RectTransform)go.transform);
            var chrome = go.AddComponent<MenuChrome>();
            chrome.Build(ui);
            return chrome;
        }

        private void Build(UIManager ui)
        {
            _ui = ui;
            var safe = UIKit.SafeArea(transform);

            // ---- top bar
            var bar = UIKit.PanelImage(safe, new Color(0.04f, 0.05f, 0.1f, 0.92f), "TopBar");
            bar.sprite = null;
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1080, MenuScreen.TopBarHeight));
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0, MenuScreen.TopBarHeight);
            bar.rectTransform.anchoredPosition = Vector2.zero;

            var profile = UIKit.Btn(bar.transform, "", UIKit.Blue, () => ui.Push(ScreenId.Profile), new Vector2(92, 92), 40);
            profile.GetComponent<Image>().sprite = UIKit.Circle;
            UIKit.Place((RectTransform)profile.transform, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(92, 92));
            UIKit.SetButtonText(profile, "P");

            _coins = Pill(bar.transform, UIKit.Gold, 130, 270, null);
            TutorialTargets.Register("pill_coins", (RectTransform)_coins.transform.parent);
            _gems = Pill(bar.transform, new Color(0.35f, 0.75f, 1f), 410, 250, () => ui.OpenTab(ScreenId.Shop));
            _keys = Pill(bar.transform, UIKit.Purple, 670, 190, null);

            var gear = UIKit.Btn(bar.transform, "=", UIKit.PanelLight, () => ui.Push(ScreenId.Settings), new Vector2(92, 92), 50);
            UIKit.Place((RectTransform)gear.transform, new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(92, 92));

            // ---- tab bar
            var nav = UIKit.PanelImage(safe, new Color(0.04f, 0.05f, 0.1f, 0.96f), "TabBar");
            nav.sprite = null;
            nav.rectTransform.anchorMin = new Vector2(0f, 0f);
            nav.rectTransform.anchorMax = new Vector2(1f, 0f);
            nav.rectTransform.pivot = new Vector2(0.5f, 0f);
            nav.rectTransform.sizeDelta = new Vector2(0, 160);
            nav.rectTransform.anchoredPosition = new Vector2(0, 100);

            string[] labels = { "tab.army", "tab.commanders", "tab.home", "tab.shop", "tab.missions" };
            Color[] colors = { UIKit.Bad, UIKit.Purple, UIKit.Accent, UIKit.Good, UIKit.Blue };
            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                var tab = UIKit.PanelImage(nav.transform, Color.clear, "Tab" + Tabs[i]);
                tab.rectTransform.anchorMin = new Vector2(i / 5f, 0f);
                tab.rectTransform.anchorMax = new Vector2((i + 1) / 5f, 1f);
                tab.rectTransform.offsetMin = new Vector2(4, 4);
                tab.rectTransform.offsetMax = new Vector2(-4, -4);
                var btn = tab.gameObject.AddComponent<Button>();
                btn.targetGraphic = tab;
                btn.onClick.AddListener(() => { Audio.Sfx.Play(Audio.SfxId.Click); ui.OpenTab(Tabs[idx]); });
                _tabBg[i] = tab;

                var icon = UIKit.Icon(tab.transform, colors[i], new Vector2(70, 70), false);
                UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(70, 70));
                _tabIcon[i] = icon;
                var label = UIKit.Label(tab.transform, Loc.Get(labels[i]), 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(200, 44));
                _tabBadge[i] = UIKit.Badge(tab.transform);
                TutorialTargets.Register("tab_" + Tabs[i], tab.rectTransform);
            }

            // ---- banner strip (mock ads only; real SDKs draw their own native banner)
            var strip = UIKit.PanelImage(safe, new Color(0.15f, 0.15f, 0.2f, 1f), "BannerStrip");
            strip.sprite = null;
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.sizeDelta = new Vector2(0, 100);
            strip.rectTransform.anchoredPosition = Vector2.zero;
            var bl = UIKit.Label(strip.transform, "[ banner ad ]", 30, UIKit.TextDim);
            UIKit.Stretch(bl.rectTransform);
            _bannerStrip = strip.gameObject;

            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
            EventBus.Subscribe<ScreenChangedEvent>(OnScreen);
            RefreshCurrencies();
            RefreshTabs();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);
            EventBus.Unsubscribe<ScreenChangedEvent>(OnScreen);
            foreach (var t in Tabs) TutorialTargets.Unregister("tab_" + t);
        }

        private TMP_Text Pill(Transform parent, Color iconColor, float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            var img = UIKit.PanelImage(parent, new Color(0, 0, 0, 0.5f), "Pill");
            UIKit.Place(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0), new Vector2(width, 76));
            if (onClick != null)
            {
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(onClick);
            }
            else img.raycastTarget = false;
            var icon = UIKit.Icon(img.transform, iconColor, new Vector2(48, 48));
            UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14, 0), new Vector2(48, 48));
            var text = UIKit.Label(img.transform, "0", 38, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIKit.Stretch(text.rectTransform, 70, 0, 14, 0);
            return text;
        }

        private void OnCurrency(CurrencyChangedEvent e) => RefreshCurrencies();

        private void OnScreen(ScreenChangedEvent e) => RefreshTabs();

        private void RefreshCurrencies()
        {
            if (!ServiceLocator.TryGet<CurrencyService>(out var c)) return;
            _coins.text = ItemDisplay.Format(c.Get(CurrencyType.Coins));
            _gems.text = ItemDisplay.Format(c.Get(CurrencyType.Gems));
            _keys.text = ItemDisplay.Format(c.Get(CurrencyType.ChestKeys));
        }

        private void RefreshTabs()
        {
            var current = _ui.Current;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool active = Tabs[i] == current;
                _tabBg[i].color = active ? new Color(1f, 1f, 1f, 0.14f) : Color.clear;
                _tabIcon[i].rectTransform.sizeDelta = active ? new Vector2(84, 84) : new Vector2(70, 70);
            }
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 1f) return;
            _timer = 0f;
            if (ServiceLocator.TryGet<Services.IAdsService>(out var ads) && ads is MockAdsService mock)
                _bannerStrip.SetActive(mock.BannerVisible);
            else _bannerStrip.SetActive(false);

            if (!ServiceLocator.TryGet<MissionService>(out var missions)) return;
            _tabBadge[4].SetActive(missions.HasClaimable());
            _tabBadge[3].SetActive(ServiceLocator.TryGet<OfferService>(out var offers) && offers.Active().Count > 0);
            bool home = ServiceLocator.Get<CastleService>().IsFull || ServiceLocator.Get<ChestService>().WoodenReady
                        || ServiceLocator.Get<LoginService>().CanClaim;
            _tabBadge[2].SetActive(home);
            bool anyCommanderUpgrade = false;
            var commanders = ServiceLocator.Get<CommanderService>();
            foreach (var cmd in Data.GameDatabase.Instance.commanders)
            {
                if (!commanders.IsUnlocked(cmd.id)) { if (commanders.Shards(cmd.id) >= cmd.unlockShards) anyCommanderUpgrade = true; }
                else if (commanders.GetLevel(cmd.id) < cmd.maxLevel && commanders.Shards(cmd.id) >= commanders.UpgradeCost(cmd.id)) anyCommanderUpgrade = true;
            }
            _tabBadge[1].SetActive(anyCommanderUpgrade);
        }
    }
}
