using System;
using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Small reusable popups: rewards, confirmation, drop rates, text info.</summary>
    public static class Popups
    {
        public static void Reward(string title, IList<GrantedItem> items, Action onClosed = null)
        {
            PopupManager.Show(() =>
            {
                int rows = Mathf.Max(1, items.Count);
                float height = Mathf.Min(1500f, 330f + rows * 110f);
                var popup = PopupManager.Create(title, new Vector2(900, height));
                var c = popup.Content;
                float y = 0f;
                foreach (var item in items)
                {
                    var icon = UIKit.Icon(c, ItemDisplay.ColorOf(item.Type), new Vector2(72, 72));
                    UIKit.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(30, y), new Vector2(72, 72));
                    var amount = UIKit.Label(c, "+" + ItemDisplay.Format(item.Amount), 56, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                    UIKit.Place(amount.rectTransform, new Vector2(0f, 1f), new Vector2(125, y + 4), new Vector2(300, 80));
                    var name = UIKit.Label(c, ItemDisplay.NameOf(item), 40, UIKit.TextDim, TextAlignmentOptions.MidlineLeft);
                    UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(420, y + 2), new Vector2(400, 80));
                    y -= 100f;
                }
                var ok = UIKit.Btn(c, Loc.Get("common.collect"), UIKit.Good, () => popup.Close(), new Vector2(560, 120), 52);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(560, 120));
                popup.Closed = onClosed;
                Sfx.Play(SfxId.Reward);
                Haptics.Medium();
                return popup;
            });
        }

        public static void Confirm(string title, string message, string yesLabel, Action onYes, bool destructive = false)
        {
            PopupManager.Show(() =>
            {
                var popup = PopupManager.Create(title, new Vector2(900, 700));
                var c = popup.Content;
                var msg = UIKit.Label(c, message, 42, Color.white);
                UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(820, 300));
                var yes = UIKit.Btn(c, yesLabel, destructive ? UIKit.Bad : UIKit.Good, () => { popup.Close(); onYes?.Invoke(); }, new Vector2(400, 120), 44);
                UIKit.Place((RectTransform)yes.transform, new Vector2(1f, 0f), new Vector2(-10, 10), new Vector2(400, 120));
                var no = UIKit.Btn(c, Loc.Get("common.cancel"), UIKit.PanelLight, () => popup.Close(), new Vector2(400, 120), 44);
                UIKit.Place((RectTransform)no.transform, new Vector2(0f, 0f), new Vector2(10, 10), new Vector2(400, 120));
                return popup;
            }, true);
        }

        public static void Info(string title, string message)
        {
            PopupManager.Show(() =>
            {
                var popup = PopupManager.Create(title, new Vector2(900, 700));
                var msg = UIKit.Label(popup.Content, message, 42, Color.white);
                UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(820, 380));
                var ok = UIKit.Btn(popup.Content, Loc.Get("common.ok"), UIKit.Good, () => popup.Close(), new Vector2(500, 120), 48);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(500, 120));
                return popup;
            }, true);
        }

        /// <summary>Drop-rate disclosure (Apple / Google requirement) for a chest.</summary>
        public static void DropRates(string chestId)
        {
            PopupManager.Show(() =>
            {
                var chests = ServiceLocator.Get<ChestService>();
                var rates = chests.DropRates(chestId);
                var cfg = ServiceLocator.Get<MetaConfig>().Chest(chestId);
                var popup = PopupManager.Create(Loc.Get(ItemDisplay.ChestKey(chestId)) + " - " + Loc.Get("chest.rates"), new Vector2(920, 330f + rates.Count * 120f));
                var c = popup.Content;

                var info = UIKit.Label(c, Loc.Format("chest.rolls", cfg.rolls) + (cfg.guaranteedShardMax > 0 ? "\n" + Loc.Format("chest.guaranteed", cfg.guaranteedShardMin, cfg.guaranteedShardMax) : ""),
                    36, UIKit.TextDim);
                UIKit.Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(820, 110));

                float y = -130f;
                foreach (var r in rates)
                {
                    string name = r.Type == RewardType.CoinsWins ? Loc.Get("item.coins") : r.Type == RewardType.Gems ? Loc.Get("item.gems") : Loc.Get("item.shards_any");
                    string range = r.Min == r.Max ? r.Min.ToString() : r.Min + "-" + r.Max;
                    if (r.Type == RewardType.CoinsWins) range = "x" + range;
                    var row = UIKit.Label(c, name + " (" + range + ")", 40, Color.white, TextAlignmentOptions.MidlineLeft);
                    UIKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(10, y), new Vector2(560, 90));
                    var pct = UIKit.Label(c, r.Percent.ToString("0.#") + "%", 44, UIKit.Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                    UIKit.Place(pct.rectTransform, new Vector2(1f, 1f), new Vector2(-10, y), new Vector2(240, 90));
                    y -= 110f;
                }
                var ok = UIKit.Btn(c, Loc.Get("common.ok"), UIKit.PanelLight, () => popup.Close(), new Vector2(420, 110), 44);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 5), new Vector2(420, 110));
                return popup;
            }, true);
        }
    }
}
