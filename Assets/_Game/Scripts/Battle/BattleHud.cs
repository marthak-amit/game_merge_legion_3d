using System;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.Battle
{
    /// <summary>In-battle-scene UI: currency, level, unit buy buttons, fight and home buttons.</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        private sealed class BuySlot
        {
            public UnitLineId Line;
            public Button Button;
            public TMP_Text Cost;
            public GameObject Lock;
            public TMP_Text LockText;
        }

        private ArmyService _army;
        private CurrencyService _currency;
        private GameDatabase _db;
        private readonly BuySlot[] _slots = new BuySlot[UnitLines.Count];
        private TMP_Text _coins;
        private TMP_Text _levelLabel;
        private RectTransform _prepareGroup;
        private Button _fight;

        private Button _freeUnit;
        private RectTransform _fightGroup;
        private Image _skillFill;
        private TMP_Text _skillLabel;
        private Button _skillButton;
        private Button _speedButton;

        public event Action FightClicked;
        public event Action HomeClicked;
        public event Action FreeUnitClicked;
        public event Action SkillClicked;
        public event Action SpeedClicked;

        public RectTransform Root { get; private set; }
        public Canvas Canvas { get; private set; }

        public void Init(ArmyService army, CurrencyService currency, GameDatabase db, int level)
        {
            _army = army;
            _currency = currency;
            _db = db;

            Canvas = UIKit.CreateCanvas("BattleCanvas");
            Canvas.transform.SetParent(transform, false);
            UIKit.EnsureEventSystem();
            Root = UIKit.SafeArea(Canvas.transform);

            BuildTopBar(level);
            BuildPrepareGroup();
            BuildFightGroup();
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
            EventBus.Subscribe<UnitSpawnedEvent>(OnGridChanged);
            EventBus.Subscribe<UnitMergedEvent>(OnMergedEvt);
            Refresh();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);
            EventBus.Unsubscribe<UnitSpawnedEvent>(OnGridChanged);
            EventBus.Unsubscribe<UnitMergedEvent>(OnMergedEvt);
        }

        public void SetPrepareVisible(bool visible) => _prepareGroup.gameObject.SetActive(visible);

        public void SetFightVisible(bool visible) => _fightGroup.gameObject.SetActive(visible);

        public void SetFreeUnit(bool available, string text)
        {
            _freeUnit.interactable = available;
            UIKit.SetButtonText(_freeUnit, text);
        }

        public void SetSpeedText(string text) => UIKit.SetButtonText(_speedButton, text);

        public void SetSkill(string name, float readyFraction, bool ready)
        {
            _skillFill.fillAmount = readyFraction;
            _skillLabel.text = name;
            _skillButton.interactable = ready;
            _skillFill.color = ready ? UIKit.Good : UIKit.Blue;
        }

        private void BuildFightGroup()
        {
            _fightGroup = UIKit.Rect("Fight", Root);
            UIKit.Stretch(_fightGroup);

            _skillButton = UIKit.Btn(_fightGroup, "", UIKit.PanelLight, () => SkillClicked?.Invoke(), new Vector2(230, 230), 30);
            var rt = (RectTransform)_skillButton.transform;
            UIKit.Place(rt, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(230, 230));
            _skillButton.GetComponent<Image>().sprite = UIKit.Circle;
            Tutorial.TutorialTargets.Register("skill", rt);

            _skillFill = UIKit.PanelImage(rt, UIKit.Blue, "Cooldown");
            UIKit.Stretch(_skillFill.rectTransform, 10, 10, 10, 10);
            _skillFill.sprite = UIKit.Circle;
            _skillFill.type = Image.Type.Filled;
            _skillFill.fillMethod = Image.FillMethod.Radial360;
            _skillFill.fillOrigin = (int)Image.Origin360.Top;
            _skillFill.raycastTarget = false;
            _skillLabel = UIKit.Label(rt, "", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(_skillLabel.rectTransform, 24, 24, 24, 24);

            _speedButton = UIKit.Btn(_fightGroup, "x1", UIKit.PanelLight, () => SpeedClicked?.Invoke(), new Vector2(170, 90), 44);
            UIKit.Place((RectTransform)_speedButton.transform, new Vector2(0f, 0f), new Vector2(30, 120), new Vector2(170, 90));
            _fightGroup.gameObject.SetActive(false);
        }

        public void SetFightInteractable(bool interactable) => _fight.interactable = interactable;

        public void SetLevel(int level) => _levelLabel.text = Loc.Format("hud.level", level);

        public void SetTitle(string text) => _levelLabel.text = text;

        private void BuildTopBar(int level)
        {
            var pill = UIKit.PanelImage(Root, new Color(0, 0, 0, 0.55f), "CoinPill");
            UIKit.Place(pill.rectTransform, new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(360, 90));
            var coinIcon = UIKit.Icon(pill.transform, UIKit.Gold, new Vector2(56, 56));
            UIKit.Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(56, 56));
            Tutorial.TutorialTargets.Register("pill_coins", pill.rectTransform);
            _coins = UIKit.Label(pill.transform, "0", 44, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIKit.Stretch(_coins.rectTransform, 84, 0, 20, 0);

            _levelLabel = UIKit.Label(Root, "", 52, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_levelLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -25), new Vector2(360, 80));
            SetLevel(level);

            var home = UIKit.Btn(Root, Loc.Get("hud.home"), UIKit.PanelLight, () => HomeClicked?.Invoke(), new Vector2(210, 90), 38);
            UIKit.Place((RectTransform)home.transform, new Vector2(1f, 1f), new Vector2(-20, -20), new Vector2(210, 90));
        }

        private void BuildPrepareGroup()
        {
            _prepareGroup = UIKit.Rect("Prepare", Root);
            UIKit.Stretch(_prepareGroup);

            _fight = UIKit.Btn(_prepareGroup, Loc.Get("hud.fight"), UIKit.Good, () => FightClicked?.Invoke(), new Vector2(760, 140), 64);
            UIKit.Place((RectTransform)_fight.transform, new Vector2(0.5f, 0f), new Vector2(0, 275), new Vector2(760, 140));
            Tutorial.TutorialTargets.Register("fight", (RectTransform)_fight.transform);

            _freeUnit = UIKit.Btn(_prepareGroup, "", UIKit.Blue, () => FreeUnitClicked?.Invoke(), new Vector2(420, 90), 34);
            UIKit.Place((RectTransform)_freeUnit.transform, new Vector2(0f, 1f), new Vector2(20, -125), new Vector2(420, 90));
            SetFreeUnit(false, Loc.Get("hud.free_unit"));

            const float w = 245f, gap = 12f;
            float total = UnitLines.Count * w + (UnitLines.Count - 1) * gap;
            for (int i = 0; i < UnitLines.Count; i++)
            {
                var line = (UnitLineId)i;
                var data = _db.GetLine(line);
                var slot = new BuySlot { Line = line };
                slot.Button = UIKit.Btn(_prepareGroup, "", UIKit.Panel, () => OnBuy(slot), new Vector2(w, 230), 36);
                var rt = (RectTransform)slot.Button.transform;
                UIKit.Place(rt, new Vector2(0.5f, 0f), new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), 25), new Vector2(w, 230));

                var icon = UIKit.Icon(rt, data.GetLevel(1).tint, new Vector2(84, 84));
                UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(84, 84));
                var name = UIKit.Label(rt, Loc.Get(data.nameKey), 34, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(w - 10, 44));
                slot.Cost = UIKit.Label(rt, "", 38, UIKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(slot.Cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(w - 10, 54));

                var lockImg = UIKit.PanelImage(rt, new Color(0, 0, 0, 0.72f), "Lock");
                UIKit.Stretch(lockImg.rectTransform);
                slot.Lock = lockImg.gameObject;
                slot.LockText = UIKit.Label(lockImg.transform, "", 34, Color.white);
                UIKit.Stretch(slot.LockText.rectTransform, 8, 8, 8, 8);
                _slots[i] = slot;
                Tutorial.TutorialTargets.Register("buy_" + line, rt);
            }
        }

        private void OnBuy(BuySlot slot)
        {
            switch (_army.Buy(slot.Line))
            {
                case BuyResult.NoCoins:
                    Toast.Show(Loc.Get("toast.no_coins"));
                    Audio.Sfx.Play(Audio.SfxId.Error);
                    Tween.Punch(slot.Button.transform, -0.08f, 0.2f);
                    Audio.Haptics.Medium();
                    break;
                case BuyResult.GridFull:
                    Toast.Show(Loc.Get("toast.grid_full"));
                    Audio.Sfx.Play(Audio.SfxId.Error);
                    break;
                case BuyResult.Ok:
                    Audio.Sfx.Play(Audio.SfxId.Buy);
                    break;
            }
        }

        private void OnCurrency(CurrencyChangedEvent e) => Refresh();
        private void OnGridChanged(UnitSpawnedEvent e) => Refresh();
        private void OnMergedEvt(UnitMergedEvent e) => Refresh();

        public void Refresh()
        {
            if (_coins == null) return;
            _coins.text = FormatNumber(_currency.Get(CurrencyType.Coins));
            long coins = _currency.Get(CurrencyType.Coins);
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                bool unlocked = _army.IsUnlocked(s.Line);
                s.Lock.SetActive(!unlocked);
                if (!unlocked)
                {
                    s.LockText.text = Loc.Format("hud.unlock_at", _db.GetLine(s.Line).unlockLevel);
                    s.Button.interactable = false;
                    continue;
                }
                long cost = _army.GetBuyCost(s.Line);
                s.Cost.text = FormatNumber(cost);
                s.Cost.color = coins >= cost ? UIKit.Gold : UIKit.Bad;
                s.Button.interactable = true;
            }
        }

        public static string FormatNumber(long n)
        {
            if (n >= 1000000000L) return (n / 1000000000.0).ToString("0.##") + "B";
            if (n >= 1000000L) return (n / 1000000.0).ToString("0.##") + "M";
            if (n >= 10000L) return (n / 1000.0).ToString("0.#") + "K";
            return n.ToString();
        }
    }
}
