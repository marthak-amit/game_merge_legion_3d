using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Data;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Meta.Arena;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Home hub (section 6): battle button, castle, chests and shortcuts to every feature.</summary>
    public sealed class HomeScreen : MenuScreen
    {
        private sealed class ChestCard
        {
            public string Id;
            public Button Action;
            public TMP_Text State;
            public Image Icon;
        }

        private TMP_Text _levelLabel;
        private TMP_Text _castlePending, _castleTimer, _castleLevel;
        private Image _castleBar;
        private Button _castleCollect;
        private readonly Dictionary<string, GameObject> _badges = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, ChestCard> _chests = new Dictionary<string, ChestCard>();
        private bool _autoPopupsShown;

        protected override float RefreshSeconds => 0.4f;

        protected override void BuildContent(RectTransform c)
        {
            _levelLabel = UIKit.Label(c, "", 46, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_levelLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1000, 130));

            BuildCastle(c);

            var battle = UIKit.Btn(c, Loc.Get("home.battle"), UIKit.Accent, OnBattle, new Vector2(760, 200), 84);
            UIKit.Place((RectTransform)battle.transform, new Vector2(0.5f, 1f), new Vector2(0, -490), new Vector2(760, 200));
            TutorialTargets.Register("home_battle", (RectTransform)battle.transform);

            BuildShortcuts(c);
            BuildChests(c);
        }

        // ------------------------------------------------------------------ castle

        private void BuildCastle(RectTransform c)
        {
            var card = UIKit.PanelImage(c, UIKit.Panel, "Castle");
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(1010, 320));
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = card;
            cardBtn.onClick.AddListener(() => HomePopups.Castle(Refresh));
            TutorialTargets.Register("castle", card.rectTransform);

            // castle drawn from simple shapes
            var keep = UIKit.PanelImage(card.transform, new Color(0.62f, 0.64f, 0.7f), "Keep");
            UIKit.Place(keep.rectTransform, new Vector2(0f, 0.5f), new Vector2(40, -10), new Vector2(160, 150));
            for (int i = 0; i < 3; i++)
            {
                var tooth = UIKit.PanelImage(card.transform, new Color(0.7f, 0.72f, 0.78f), "Tooth");
                UIKit.Place(tooth.rectTransform, new Vector2(0f, 0.5f), new Vector2(40 + i * 62, 80), new Vector2(36, 40));
            }
            var door = UIKit.PanelImage(card.transform, new Color(0.35f, 0.22f, 0.12f), "Door");
            UIKit.Place(door.rectTransform, new Vector2(0f, 0.5f), new Vector2(92, -60), new Vector2(56, 80));

            _castleLevel = UIKit.Label(card.transform, "", 40, UIKit.Accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(_castleLevel.rectTransform, new Vector2(0f, 1f), new Vector2(240, -20), new Vector2(520, 60));
            _castlePending = UIKit.Label(card.transform, "", 64, UIKit.Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(_castlePending.rectTransform, new Vector2(0f, 1f), new Vector2(240, -85), new Vector2(520, 90));
            _castleTimer = UIKit.Label(card.transform, "", 32, UIKit.TextDim, TextAlignmentOptions.MidlineLeft);
            UIKit.Place(_castleTimer.rectTransform, new Vector2(0f, 1f), new Vector2(240, -180), new Vector2(520, 50));
            var bar = UIKit.ProgressBar(card.transform, UIKit.Gold, new Vector2(520, 24), out _castleBar);
            UIKit.Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(240, 36), new Vector2(520, 24));

            _castleCollect = UIKit.Btn(card.transform, Loc.Get("common.collect"), UIKit.Good, () => HomePopups.Castle(Refresh), new Vector2(210, 110), 36);
            UIKit.Place((RectTransform)_castleCollect.transform, new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(210, 110));
        }

        // ------------------------------------------------------------------ shortcuts

        private void BuildShortcuts(RectTransform c)
        {
            string[] ids = { "daily", "spin", "pass", "event", "arena", "board", "piggy", "offers" };
            string[] keys = { "home.daily", "home.spin", "home.pass", "home.event", "home.arena", "home.board", "home.piggy", "home.offers" };
            Color[] colors = { UIKit.Good, UIKit.Purple, UIKit.Blue, UIKit.Bad, new Color(0.9f, 0.5f, 0.2f), new Color(0.3f, 0.7f, 0.7f), new Color(0.95f, 0.55f, 0.7f), UIKit.Accent };
            System.Action[] actions =
            {
                () => HomePopups.DailyLogin(Refresh),
                () => HomePopups.Spin(Refresh),
                () => UIManager.Instance.Push(ScreenId.BattlePass),
                OpenEvent,
                OpenArena,
                () => UIManager.Instance.Push(ScreenId.Leaderboard),
                OpenPiggy,
                OpenOffers
            };
            for (int i = 0; i < ids.Length; i++)
            {
                int idx = i;
                var b = UIKit.Btn(c, Loc.Get(keys[i]), colors[i], () => actions[idx](), new Vector2(236, 120), 32);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(14 + (i % 4) * 250f, -740 - (i / 4) * 140f), new Vector2(236, 120));
                _badges[ids[i]] = UIKit.Badge(b.transform);
            }
        }

        private void OpenEvent()
        {
            var ev = ServiceLocator.Get<WeekendEventService>();
            if (!ev.Unlocked) { Toast.Show(Loc.Format("toast.unlock_level", ev.Config.minPlayerLevel)); return; }
            UIManager.Instance.Push(ScreenId.Events);
        }

        private void OpenArena()
        {
            var arena = ServiceLocator.Get<ArenaService>();
            if (!arena.Unlocked) { Toast.Show(Loc.Format("toast.unlock_level", arena.Config.unlockLevel)); return; }
            UIManager.Instance.Push(ScreenId.Arena);
        }

        private void OpenPiggy()
        {
            ShopScreenPopups.Piggy();
        }

        private void OpenOffers()
        {
            var offers = ServiceLocator.Get<OfferService>().Active();
            if (offers.Count == 0) { Toast.Show(Loc.Get("toast.no_offers")); return; }
            HomePopups.Offer(offers[0]);
        }

        // ------------------------------------------------------------------ chests

        private void BuildChests(RectTransform c)
        {
            Section(c, Loc.Get("home.chests"), -1010);
            string[] ids = { "wooden", "silver", "gold", "legendary" };
            Color[] colors = { new Color(0.6f, 0.4f, 0.2f), new Color(0.7f, 0.75f, 0.85f), UIKit.Gold, UIKit.Purple };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                var card = UIKit.PanelImage(c, UIKit.Panel, "Chest_" + id);
                UIKit.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(14 + i * 252f, -1075), new Vector2(240, 420));
                var icon = UIKit.Icon(card.transform, colors[i], new Vector2(120, 100), false);
                UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(120, 100));
                var lid = UIKit.PanelImage(card.transform, Color.Lerp(colors[i], Color.white, 0.25f), "Lid");
                UIKit.Place(lid.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(130, 40));
                var name = UIKit.Label(card.transform, Loc.Get(ItemDisplay.ChestKey(id)), 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(230, 70));

                var state = UIKit.Label(card.transform, "", 30, UIKit.TextDim);
                UIKit.Place(state.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(230, 80));
                var action = UIKit.Btn(card.transform, "", UIKit.Good, () => OnChest(id), new Vector2(210, 100), 32);
                UIKit.Place((RectTransform)action.transform, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(210, 100));
                var info = UIKit.Btn(card.transform, "i", UIKit.PanelLight, () => Popups.DropRates(id), new Vector2(60, 60), 34);
                UIKit.Place((RectTransform)info.transform, new Vector2(1f, 1f), new Vector2(-6, -6), new Vector2(60, 60));
                _chests[id] = new ChestCard { Id = id, Action = action, State = state, Icon = icon };
                TutorialTargets.Register("chest_" + id, card.rectTransform);
            }
        }

        private void OnChest(string id)
        {
            var chests = ServiceLocator.Get<ChestService>();
            var cfg = ServiceLocator.Get<MetaConfig>().Chest(id);
            switch (id)
            {
                case "wooden": Show(id, chests.OpenFree(id)); break;
                case "silver": chests.OpenWithAd(id, r => Show(id, r)); break;
                default:
                    var currency = ServiceLocator.Get<CurrencyService>();
                    var payment = cfg.costKeys > 0 && currency.CanAfford(CurrencyType.ChestKeys, cfg.costKeys) ? ChestPayment.Keys : ChestPayment.Gems;
                    Show(id, chests.OpenPaid(id, payment));
                    break;
            }
        }

        private void Show(string id, ChestOpenResult r)
        {
            switch (r.Status)
            {
                case ChestOpenStatus.Ok:
                    Sfx.Play(SfxId.Chest);
                    Popups.Reward(Loc.Get(ItemDisplay.ChestKey(id)), r.Items, Refresh);
                    break;
                case ChestOpenStatus.NotEnough:
                    Toast.Show(Loc.Get("toast.not_enough_gems"));
                    UIManager.Instance.OpenTab(ScreenId.Shop);
                    break;
                case ChestOpenStatus.AdFailed:
                    Toast.Show(Loc.Get("result.ad_unavailable"));
                    break;
                case ChestOpenStatus.NotReady:
                    Toast.Show(Loc.Get("toast.not_ready"));
                    break;
            }
            Refresh();
        }

        // ------------------------------------------------------------------ battle

        private void OnBattle()
        {
            GameSession.Begin(GameMode.Campaign);
            LoadBattle();
        }

        public static void LoadBattle()
        {
            if (ServiceLocator.TryGet<SceneLoader>(out var loader)) loader.Load(SceneNames.Battle);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Battle);
        }

        // ------------------------------------------------------------------ refresh

        protected override void OnShown()
        {
            base.OnShown();
            if (ServiceLocator.TryGet<IAudioService>(out var audio)) audio.PlayMusic("menu");
            if (!_autoPopupsShown) ShowAutoPopups();
        }

        /// <summary>Daily reward, newly triggered offers and the weekly arena reward appear once per visit to Home.</summary>
        private void ShowAutoPopups()
        {
            _autoPopupsShown = true;
            var save = ServiceLocator.Get<SaveService>();
            if (save.Data.highestCampaignLevel < 1) return;                       // let the intro finish first
            var tutorial = ServiceLocator.Get<TutorialService>();
            if (tutorial.Current("home", "") != null) return;                     // a guided step is on screen

            if (ServiceLocator.Get<LoginService>().CanClaim && save.Data.highestCampaignLevel >= 2) HomePopups.DailyLogin(Refresh);
            var arena = ServiceLocator.Get<ArenaService>();
            if (arena.Unlocked && arena.WeeklyRewardAvailable)
                Popups.Confirm(Loc.Get("arena.weekly_title"), Loc.Format("arena.weekly_msg", Loc.Get(arena.UnclaimedLeague.nameKey)), Loc.Get("common.claim"),
                    () => Popups.Reward(Loc.Get("arena.weekly_title"), arena.ClaimWeeklyReward()));
            if (save.Data.highestCampaignLevel >= 3 && ServiceLocator.Get<OfferService>().TryTakePresentable(out var offer)) HomePopups.Offer(offer);
        }

        protected override void Refresh()
        {
            var save = ServiceLocator.Get<SaveService>();
            int level = save.Data.highestCampaignLevel + 1;
            var repo = ServiceLocator.Get<LevelRepository>();
            var def = repo.Get(level);
            _levelLabel.text = def.endless
                ? Loc.Format("home.endless", def.index)
                : Loc.Format("home.chapter", def.chapter, Loc.Get(ThemeLibrary.Get(def.theme).nameKey)) + "\n" + Loc.Format("home.level", level)
                  + (def.isBoss ? "  -  " + Loc.Get("home.boss_next") : "");

            var castle = ServiceLocator.Get<CastleService>();
            long pending = castle.Pending();
            _castleLevel.text = Loc.Format("castle.title", castle.Level);
            _castlePending.text = "+" + ItemDisplay.Format(pending);
            _castleTimer.text = castle.IsFull ? Loc.Get("castle.full") : Loc.Format("castle.full_in", ItemDisplay.Duration(castle.TimeUntilFull()));
            _castleBar.fillAmount = Mathf.Clamp01((float)(castle.Elapsed().TotalHours / castle.CapHours));
            _castleCollect.interactable = pending > 0;

            RefreshChests();
            RefreshBadges();
        }

        private void RefreshChests()
        {
            var chests = ServiceLocator.Get<ChestService>();
            var cfg = ServiceLocator.Get<MetaConfig>();
            var currency = ServiceLocator.Get<CurrencyService>();
            var ads = ServiceLocator.Get<AdsManager>();

            var w = _chests["wooden"];
            if (chests.WoodenReady) { w.State.text = Loc.Get("common.free"); UIKit.SetButtonText(w.Action, Loc.Get("common.open")); w.Action.interactable = true; }
            else { w.State.text = ItemDisplay.Duration(chests.WoodenRemaining()); UIKit.SetButtonText(w.Action, "..."); w.Action.interactable = false; }

            var s = _chests["silver"];
            int left = chests.SilverRemainingToday;
            s.State.text = Loc.Format("chest.ads_left", Mathf.Min(left, 99), 3);
            UIKit.SetButtonText(s.Action, "[AD] " + Loc.Get("common.open"));
            s.Action.interactable = ads.CanShowRewarded(AdPlacements.SilverChest) == AdBlockReason.None;

            var g = _chests["gold"];
            var gc = cfg.Chest("gold");
            bool keys = currency.CanAfford(CurrencyType.ChestKeys, gc.costKeys);
            g.State.text = Loc.Format("chest.keys_have", currency.Get(CurrencyType.ChestKeys), gc.costKeys);
            UIKit.SetButtonText(g.Action, keys ? Loc.Format("chest.cost_keys", gc.costKeys) : Loc.Format("chest.cost_gems", gc.costGems));

            var l = _chests["legendary"];
            l.State.text = Loc.Get("chest.legendary_hint");
            UIKit.SetButtonText(l.Action, Loc.Format("chest.cost_gems", cfg.Chest("legendary").costGems));
        }

        private void RefreshBadges()
        {
            _badges["daily"].SetActive(ServiceLocator.Get<LoginService>().CanClaim);
            _badges["spin"].SetActive(ServiceLocator.Get<SpinService>().FreeRemaining > 0);
            _badges["pass"].SetActive(ServiceLocator.Get<BattlePassService>().HasClaimable());
            var ev = ServiceLocator.Get<WeekendEventService>();
            _badges["event"].SetActive(ev.Available || ev.HasClaimable());
            var arena = ServiceLocator.Get<ArenaService>();
            _badges["arena"].SetActive(arena.Unlocked && (arena.WeeklyRewardAvailable || arena.Attempts > 0));
            _badges["piggy"].SetActive(ServiceLocator.Get<PiggyService>().CanBreak);
            _badges["offers"].SetActive(ServiceLocator.Get<OfferService>().Active().Count > 0);
        }
    }
}
