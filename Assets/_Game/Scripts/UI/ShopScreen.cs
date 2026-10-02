using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    public static class ShopScreenPopups
    {
        public static void Piggy()
        {
            PopupManager.Show(() =>
            {
                var piggy = ServiceLocator.Get<PiggyService>();
                var iap = ServiceLocator.Get<IapManager>();
                iap.LogView(piggy.Sku);
                var popup = PopupManager.Create(Loc.Get("piggy.title"), new Vector2(900, 900), true);
                var c = popup.Content;

                var body = UIKit.Icon(c, new Color(0.95f, 0.55f, 0.7f), new Vector2(300, 240), true);
                UIKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(300, 240));
                var gems = UIKit.Label(c, "", 76, new Color(0.35f, 0.75f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(gems.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -270), new Vector2(800, 110));
                var hint = UIKit.Label(c, "", 36, UIKit.TextDim);
                UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -390), new Vector2(800, 120));
                var buy = UIKit.Btn(c, "", UIKit.Good, null, new Vector2(700, 130), 54);
                UIKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(700, 130));

                System.Action refresh = () =>
                {
                    gems.text = piggy.Accumulated + " / " + piggy.Capacity;
                    hint.text = piggy.CanBreak ? Loc.Get("piggy.ready") : Loc.Format("piggy.min", ServiceLocator.Get<MonetizationConfig>().piggy.minBreakGems);
                    UIKit.SetButtonText(buy, Loc.Get("piggy.break") + "  " + iap.PriceLabel(piggy.Sku));
                    buy.interactable = piggy.CanBreak;
                };
                buy.onClick.AddListener(() =>
                {
                    long before = piggy.Accumulated;
                    iap.Purchase(piggy.Sku, r =>
                    {
                        if (!r.Success) { if (r.Status == PurchaseStatus.Failed) Toast.Show(Loc.Get("shop.purchase_failed")); return; }
                        popup.Close();
                        Popups.Reward(Loc.Get("piggy.title"), new[] { new GrantedItem(RewardType.Gems, before) });
                    });
                });
                popup.Root.AddComponent<PopupTicker>().Tick = refresh;
                refresh();
                return popup;
            }, true);
        }
    }

    /// <summary>Shop (section 3.2): no ads, starter pack, gem packs, commander bundles, VIP, piggy bank, battle pass link, restore.</summary>
    public sealed class ShopScreen : MenuScreen
    {
        private sealed class ProductEntry { public string Sku; public Button Button; }
        private readonly List<ProductEntry> _products = new List<ProductEntry>();
        private TMP_Text _piggyText, _vipText;
        private Button _vipClaim;

        protected override void BuildContent(RectTransform c)
        {
            RowKit.ScrollArea(c, 14, 0, out var list);
            var shop = ServiceLocator.Get<MonetizationConfig>();

            Header(list, "shop.special");
            BuildPiggy(list, shop);
            BuildVip(list, shop);
            BuildPass(list, shop);
            Product(list, shop.Product("no_ads"));
            Product(list, shop.Product("starter_pack"));

            Header(list, "shop.gems");
            foreach (var p in shop.products) if (p.group == "gems") Product(list, p);

            Header(list, "shop.bundles");
            foreach (var p in shop.products) if (p.group == "bundle") Product(list, p);

            var restore = UIKit.ListRow(list, 120);
            var btn = RowKit.RightButton(restore, Loc.Get("settings.restore"), UIKit.PanelLight, Restore, new Vector2(1000, 90), 38, 20);
            ((RectTransform)btn.transform).anchorMin = ((RectTransform)btn.transform).anchorMax = new Vector2(0.5f, 0.5f);
            ((RectTransform)btn.transform).anchoredPosition = Vector2.zero;
            ((RectTransform)btn.transform).pivot = new Vector2(0.5f, 0.5f);

            var note = UIKit.ListRow(list, 150);
            var nt = RowKit.Left(note, Loc.Get("shop.legal"), 28, UIKit.TextDim, 20, 10, 1000, 130);
            nt.alignment = TextAlignmentOptions.Center;
        }

        private static void Header(RectTransform list, string key)
        {
            var row = UIKit.ListRow(list, 80, Color.clear);
            row.GetComponent<Image>().color = Color.clear;
            RowKit.Left(row, Loc.Get(key), 42, UIKit.Accent, 10, 8, 900, 64, FontStyles.Bold);
        }

        private void Product(RectTransform list, ProductDef def)
        {
            if (def == null) return;
            var iap = ServiceLocator.Get<IapManager>();
            var row = UIKit.ListRow(list, 190);
            RowKit.Dot(row, def.group == "gems" ? new Color(0.35f, 0.75f, 1f) : def.group == "bundle" ? UIKit.Purple : UIKit.Accent, 20, 40, 100);
            RowKit.Left(row, Loc.Get(def.titleKey), 40, Color.white, 140, 16, 560, 60, FontStyles.Bold);
            RowKit.Left(row, Loc.Get(def.descKey), 28, UIKit.TextDim, 140, 76, 560, 100);
            if (def.bonusPct > 0)
            {
                var tag = UIKit.PanelImage(row, UIKit.Bad, "Bonus");
                UIKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0, 0), new Vector2(150, 44));
                tag.rectTransform.pivot = new Vector2(0f, 1f);
                var tl = UIKit.Label(tag.transform, "+" + def.bonusPct + "%", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Stretch(tl.rectTransform);
            }
            string sku = def.sku;
            var btn = RowKit.RightButton(row, iap.PriceLabel(sku), UIKit.Good, () => Buy(sku), new Vector2(300, 110), 42);
            _products.Add(new ProductEntry { Sku = sku, Button = btn });
        }

        private void BuildPiggy(RectTransform list, MonetizationConfig shop)
        {
            var row = UIKit.ListRow(list, 190);
            RowKit.Dot(row, new Color(0.95f, 0.55f, 0.7f), 20, 40, 100);
            RowKit.Left(row, Loc.Get("iap.piggy_bank"), 40, Color.white, 140, 16, 560, 60, FontStyles.Bold);
            _piggyText = RowKit.Left(row, "", 32, new Color(0.35f, 0.75f, 1f), 140, 80, 560, 80, FontStyles.Bold);
            RowKit.RightButton(row, Loc.Get("piggy.view"), UIKit.Accent, ShopScreenPopups.Piggy, new Vector2(300, 110), 38);
        }

        private void BuildVip(RectTransform list, MonetizationConfig shop)
        {
            var iap = ServiceLocator.Get<IapManager>();
            var row = UIKit.ListRow(list, 260, new Color(0.25f, 0.2f, 0.4f));
            RowKit.Left(row, Loc.Get("iap.vip_weekly"), 44, UIKit.Gold, 24, 14, 640, 64, FontStyles.Bold);
            RowKit.Left(row, Loc.Get("iap.vip_weekly.desc"), 28, Color.white, 24, 80, 640, 90);
            _vipText = RowKit.Left(row, "", 30, UIKit.Good, 24, 180, 640, 60, FontStyles.Bold);
            RowKit.RightButton(row, iap.PriceLabel(shop.vip.sku) + Loc.Get("shop.per_week"), UIKit.Good, () => Buy(shop.vip.sku), new Vector2(300, 110), 34, 16, 20);
            _vipClaim = RowKit.RightButton(row, "", UIKit.Blue, () =>
            {
                var vip = ServiceLocator.Get<VipService>();
                if (vip.ClaimDaily()) Popups.Reward(Loc.Get("iap.vip_weekly"), new[] { new GrantedItem(RewardType.Gems, vip.DailyGems) });
                Refresh();
            }, new Vector2(300, 110), 34, 16, 140);
        }

        private void BuildPass(RectTransform list, MonetizationConfig shop)
        {
            var row = UIKit.ListRow(list, 170);
            RowKit.Dot(row, UIKit.Blue, 20, 35, 100);
            RowKit.Left(row, Loc.Get("iap.battle_pass_premium"), 40, Color.white, 140, 16, 560, 60, FontStyles.Bold);
            RowKit.Left(row, Loc.Get("iap.battle_pass_premium.desc"), 28, UIKit.TextDim, 140, 76, 560, 90);
            RowKit.RightButton(row, Loc.Get("home.pass"), UIKit.Blue, () => UIManager.Instance.Push(ScreenId.BattlePass), new Vector2(300, 110), 42);
        }

        private void Buy(string sku)
        {
            var iap = ServiceLocator.Get<IapManager>();
            iap.LogView(sku);
            iap.Purchase(sku, r =>
            {
                if (r.Success)
                {
                    Sfx.Play(SfxId.Reward);
                    Haptics.Medium();
                    Popups.Info(Loc.Get("shop.thanks"), Loc.Get("shop.purchase_ok"));
                }
                else if (r.Status == PurchaseStatus.Failed) Toast.Show(Loc.Get("shop.purchase_failed"));
                Refresh();
            });
        }

        private void Restore()
        {
            ServiceLocator.Get<IapManager>().Restore((ok, n) =>
                Toast.Show(ok ? Loc.Format("settings.restored", n) : Loc.Get("shop.purchase_failed")));
        }

        protected override void Refresh()
        {
            var iap = ServiceLocator.Get<IapManager>();
            foreach (var entry in _products)
            {
                var def = iap.Product(entry.Sku);
                bool owned = def.oneTime && iap.Owned(entry.Sku);
                entry.Button.interactable = !owned;
                UIKit.SetButtonText(entry.Button, owned ? Loc.Get("shop.owned") : iap.PriceLabel(entry.Sku));
                entry.Button.GetComponent<Image>().color = owned ? UIKit.Disabled : UIKit.Good;
            }

            var piggy = ServiceLocator.Get<PiggyService>();
            _piggyText.text = piggy.Accumulated + " / " + piggy.Capacity;

            var vip = ServiceLocator.Get<VipService>();
            _vipText.text = vip.IsActive ? Loc.Format("shop.vip_active", ItemDisplay.Duration(vip.Remaining)) : "";
            _vipClaim.gameObject.SetActive(vip.IsActive);
            UIKit.SetButtonText(_vipClaim, Loc.Format("shop.vip_daily", vip.DailyGems));
            _vipClaim.interactable = vip.DailyGemsAvailable;
        }
    }
}
