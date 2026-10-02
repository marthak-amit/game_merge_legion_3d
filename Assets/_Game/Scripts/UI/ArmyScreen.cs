using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Meta;
using MergeLegion.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Army / Research Lab (section 2.2): permanent HP %, damage % and buy-cost discount per unit line.</summary>
    public sealed class ArmyScreen : MenuScreen
    {
        private sealed class StatRow
        {
            public UnitLineId Line;
            public ResearchStat Stat;
            public TMP_Text Label;
            public Button Button;
        }

        private readonly List<StatRow> _rows = new List<StatRow>();
        private readonly Dictionary<UnitLineId, TMP_Text> _headers = new Dictionary<UnitLineId, TMP_Text>();

        protected override void BuildContent(RectTransform c)
        {
            var intro = UIKit.Label(c, Loc.Get("army.intro"), 34, UIKit.TextDim);
            UIKit.Place(intro.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1000, 90));

            RowKit.ScrollArea(c, 18, 100, out var list);
            var db = GameDatabase.Instance;
            for (int l = 0; l < UnitLines.Count; l++)
            {
                var line = (UnitLineId)l;
                var data = db.GetLine(line);
                var card = UIKit.ListRow(list, 440);

                RowKit.Dot(card, data.GetLevel(4).tint, 24, 20, 70);
                _headers[line] = RowKit.Left(card, "", 40, Color.white, 112, 18, 700, 74, FontStyles.Bold);
                RowKit.Right(card, Loc.Get(data.roleKey), 28, UIKit.TextDim, 20, 26, 440, 50);

                float y = 110f;
                foreach (ResearchStat stat in new[] { ResearchStat.Hp, ResearchStat.Damage, ResearchStat.Discount })
                {
                    var row = new StatRow { Line = line, Stat = stat };
                    row.Label = RowKit.Left(card, "", 34, Color.white, 28, y, 560, 90);
                    var capLine = line; var capStat = stat;
                    row.Button = RowKit.RightButton(card, "", UIKit.Good, () => Upgrade(capLine, capStat), new Vector2(300, 90), 34, 20, y);
                    _rows.Add(row);
                    if (line == UnitLineId.Melee && stat == ResearchStat.Hp) TutorialTargets.Register("research_Melee_Hp", (RectTransform)row.Button.transform);
                    y += 104f;
                }
            }
        }

        private void Upgrade(UnitLineId line, ResearchStat stat)
        {
            var research = ServiceLocator.Get<ResearchService>();
            switch (research.Upgrade(line, stat))
            {
                case ResearchResult.Ok:
                    Sfx.Play(SfxId.Reward);
                    Haptics.Light();
                    Refresh();
                    var row = _rows.Find(r => r.Line == line && r.Stat == stat);
                    if (row != null) Tween.Punch(row.Label.transform, 0.12f, 0.25f);
                    break;
                case ResearchResult.NoCoins:
                    Toast.Show(Loc.Get("toast.no_coins"));
                    Sfx.Play(SfxId.Error);
                    break;
            }
        }

        protected override void Refresh()
        {
            var research = ServiceLocator.Get<ResearchService>();
            var currency = ServiceLocator.Get<CurrencyService>();
            var army = ServiceLocator.Get<ArmyService>();
            var db = GameDatabase.Instance;

            foreach (var kv in _headers)
            {
                var data = db.GetLine(kv.Key);
                bool unlocked = army.IsUnlocked(kv.Key);
                kv.Value.text = Loc.Get(data.nameKey) + (unlocked ? "" : "   (" + Loc.Format("hud.unlock_at", data.unlockLevel).Replace("\n", " ") + ")");
            }

            long coins = currency.Get(CurrencyType.Coins);
            foreach (var r in _rows)
            {
                int lv = research.GetLevel(r.Line, r.Stat);
                int max = research.MaxLevel(r.Stat);
                string statName;
                float value;
                switch (r.Stat)
                {
                    case ResearchStat.Hp: statName = Loc.Get("research.hp"); value = research.HpBonus(r.Line); break;
                    case ResearchStat.Damage: statName = Loc.Get("research.damage"); value = research.DamageBonus(r.Line); break;
                    default: statName = Loc.Get("research.discount"); value = research.BuyDiscount(r.Line); break;
                }
                r.Label.text = statName + "  +" + Mathf.RoundToInt(value * 100f) + "%\n<size=70%><color=#FFFFFF99>Lv " + lv + " / " + max + "</color></size>";
                if (lv >= max) { UIKit.SetButtonText(r.Button, Loc.Get("common.max")); r.Button.interactable = false; }
                else
                {
                    long cost = research.Cost(r.Line, r.Stat);
                    UIKit.SetButtonText(r.Button, ItemDisplay.Format(cost));
                    r.Button.interactable = true;
                    r.Button.GetComponent<Image>().color = coins >= cost ? UIKit.Good : UIKit.Disabled;
                }
            }
        }
    }
}
