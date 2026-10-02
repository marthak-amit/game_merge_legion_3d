using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Battle Pass (section 2.8): 30 tiers, free + premium track, XP from levels and missions.</summary>
    public sealed class BattlePassScreen : MenuScreen
    {
        private sealed class TierRow
        {
            public int Tier;
            public Image Bg;
            public TMP_Text FreeText, PremiumText;
            public Button FreeBtn, PremiumBtn;
        }

        private readonly List<TierRow> _rows = new List<TierRow>();
        private TMP_Text _season, _tierLabel, _xpText;
        private Image _xpFill;
        private Button _unlock, _claimAll;

        protected override string TitleKey => "pass.title";

        protected override void BuildContent(RectTransform c)
        {
            var bp = ServiceLocator.Get<BattlePassService>();
            _season = UIKit.Label(c, "", 32, UIKit.TextDim);
            UIKit.Place(_season.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(1000, 46));
            _tierLabel = UIKit.Label(c, "", 52, UIKit.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_tierLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(-240, -50), new Vector2(500, 80));
            var bar = UIKit.ProgressBar(c, UIKit.Good, new Vector2(500, 34), out _xpFill);
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(-240, -130), new Vector2(500, 34));
            _xpText = UIKit.Label(c, "", 28, Color.white);
            UIKit.Place(_xpText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-240, -168), new Vector2(500, 40));

            var iap = ServiceLocator.Get<IapManager>();
            _unlock = UIKit.Btn(c, Loc.Get("pass.unlock") + "\n" + iap.PriceLabel(bp.PremiumSku), UIKit.Purple, UnlockPremium, new Vector2(400, 130), 34);
            UIKit.Place((RectTransform)_unlock.transform, new Vector2(1f, 1f), new Vector2(-15, -60), new Vector2(400, 130));
            _claimAll = UIKit.Btn(c, Loc.Get("pass.claim_all"), UIKit.Good, ClaimAll, new Vector2(400, 80), 34);
            UIKit.Place((RectTransform)_claimAll.transform, new Vector2(1f, 1f), new Vector2(-15, -60), new Vector2(400, 80));

            var header = UIKit.Rect("Header", c);
            UIKit.Place(header, new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(1040, 50));
            var h1 = UIKit.Label(header, Loc.Get("pass.free"), 32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(h1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-120, 0), new Vector2(380, 50));
            var h2 = UIKit.Label(header, Loc.Get("pass.premium"), 32, UIKit.Purple, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(h2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300, 0), new Vector2(380, 50));

            RowKit.ScrollArea(c, 10, 270, out var list);
            for (int t = 1; t <= bp.TierCount; t++)
            {
                int tier = t;
                var row = UIKit.ListRow(list, 150);
                var tr = new TierRow { Tier = tier, Bg = row.GetComponent<Image>() };
                RowKit.Left(row, tier.ToString(), 48, UIKit.Accent, 24, 40, 100, 70, FontStyles.Bold);
                tr.FreeText = RowKit.Left(row, "", 30, Color.white, 130, 12, 280, 80);
                tr.FreeBtn = RowKit.Left2Button(row, tier, false, ClaimFree);
                tr.PremiumText = RowKit.Left(row, "", 30, new Color(0.85f, 0.7f, 1f), 580, 12, 280, 80);
                tr.PremiumBtn = RowKit.Left2Button(row, tier, true, ClaimPremium);
                _rows.Add(tr);
            }
        }

        private void UnlockPremium()
        {
            var bp = ServiceLocator.Get<BattlePassService>();
            var iap = ServiceLocator.Get<IapManager>();
            iap.LogView(bp.PremiumSku);
            iap.Purchase(bp.PremiumSku, r =>
            {
                if (r.Success) { Sfx.Play(SfxId.Reward); Toast.Show(Loc.Get("pass.unlocked")); }
                else if (r.Status == PurchaseStatus.Failed) Toast.Show(Loc.Get("shop.purchase_failed"));
                Refresh();
            });
        }

        private void ClaimAll()
        {
            var items = ServiceLocator.Get<BattlePassService>().ClaimAll();
            if (items.Count > 0) Popups.Reward(Loc.Get("pass.title"), items);
            Refresh();
        }

        private void ClaimFree(int tier) => Claim(ServiceLocator.Get<BattlePassService>().ClaimFree(tier));
        private void ClaimPremium(int tier) => Claim(ServiceLocator.Get<BattlePassService>().ClaimPremium(tier));

        private void Claim(List<GrantedItem> items)
        {
            if (items != null) Popups.Reward(Loc.Get("pass.title"), items);
            Refresh();
        }

        protected override void Refresh()
        {
            var bp = ServiceLocator.Get<BattlePassService>();
            bp.EnsureSeason();
            _season.text = Loc.Format("pass.season", bp.SeasonId + 1, ItemDisplay.Duration(bp.TimeLeft));
            int tier = bp.Tier;
            _tierLabel.text = Loc.Format("pass.tier", tier, bp.TierCount);
            bp.TierProgress(out long have, out long need);
            _xpFill.fillAmount = need > 0 ? (float)have / need : 1f;
            _xpText.text = need > 0 ? have + " / " + need + " XP" : Loc.Get("common.max");
            _unlock.gameObject.SetActive(!bp.Premium);
            _claimAll.gameObject.SetActive(bp.Premium);
            _claimAll.interactable = bp.HasClaimable();

            foreach (var r in _rows)
            {
                var def = bp.TierDef(r.Tier);
                bool reached = r.Tier <= tier;
                r.Bg.color = reached ? UIKit.Panel : new Color(0.09f, 0.11f, 0.18f, 0.95f);
                r.FreeText.text = ItemDisplay.Describe(def.free);
                r.PremiumText.text = ItemDisplay.Describe(def.premium);
                Style(r.FreeBtn, bp.IsFreeClaimed(r.Tier), bp.CanClaimFree(r.Tier), reached);
                Style(r.PremiumBtn, bp.IsPremiumClaimed(r.Tier), bp.CanClaimPremium(r.Tier), reached && bp.Premium);
            }
        }

        private static void Style(Button b, bool claimed, bool canClaim, bool unlocked)
        {
            UIKit.SetButtonText(b, claimed ? Loc.Get("common.claimed") : canClaim ? Loc.Get("common.claim") : unlocked ? "" : Loc.Get("common.locked"));
            b.interactable = canClaim;
            b.GetComponent<Image>().color = claimed ? new Color(0.2f, 0.3f, 0.22f) : canClaim ? UIKit.Good : UIKit.Disabled;
        }
    }
}
