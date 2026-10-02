using System;
using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Castle, daily login, lucky spin and dynamic-offer popups.</summary>
    public static class HomePopups
    {
        // ------------------------------------------------------------------ castle

        public static void Castle(Action onChanged = null)
        {
            PopupManager.Show(() =>
            {
                var castle = ServiceLocator.Get<CastleService>();
                var ads = ServiceLocator.Get<AdsManager>();
                var currency = ServiceLocator.Get<CurrencyService>();
                var popup = PopupManager.Create(Loc.Format("castle.title", castle.Level), new Vector2(940, 1250), true);
                var c = popup.Content;

                var perHour = UIKit.Label(c, Loc.Format("castle.per_hour", ItemDisplay.Format(castle.CoinsPerHour())) + (ads.IsVip ? "  (VIP x2)" : ""), 42, UIKit.TextDim);
                UIKit.Place(perHour.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(860, 70));
                var pending = UIKit.Label(c, "", 84, UIKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(pending.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(860, 120));
                var timer = UIKit.Label(c, "", 38, UIKit.TextDim);
                UIKit.Place(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(860, 60));

                var collect = UIKit.Btn(c, Loc.Get("common.collect"), UIKit.Good, null, new Vector2(820, 120), 54);
                UIKit.Place((RectTransform)collect.transform, new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(820, 120));
                var collect2 = UIKit.Btn(c, "", UIKit.Blue, null, new Vector2(820, 120), 46);
                UIKit.Place((RectTransform)collect2.transform, new Vector2(0.5f, 1f), new Vector2(0, -440), new Vector2(820, 120));
                var upgrade = UIKit.Btn(c, "", UIKit.Accent, null, new Vector2(820, 120), 46);
                UIKit.Place((RectTransform)upgrade.transform, new Vector2(0.5f, 1f), new Vector2(0, -620), new Vector2(820, 120));
                var note = UIKit.Label(c, Loc.Format("castle.cap", castle.CapHours), 34, UIKit.TextDim);
                UIKit.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -760), new Vector2(860, 70));

                Action refresh = () =>
                {
                    long p = castle.Pending();
                    pending.text = "+" + ItemDisplay.Format(p);
                    timer.text = castle.IsFull ? Loc.Get("castle.full") : Loc.Format("castle.full_in", ItemDisplay.Duration(castle.TimeUntilFull()));
                    collect.interactable = p > 0;
                    bool adOk = ads.CanShowRewarded(AdPlacements.Offline2x) == AdBlockReason.None && p > 0;
                    collect2.interactable = adOk;
                    UIKit.SetButtonText(collect2, Loc.Format("castle.collect_x2", ItemDisplay.Format(p * 2)) + "  [AD]");
                    bool max = castle.Level >= castle.MaxLevel;
                    UIKit.SetButtonText(upgrade, max ? Loc.Get("common.max") : Loc.Format("castle.upgrade", ItemDisplay.Format(castle.UpgradeCost())));
                    upgrade.interactable = !max && currency.CanAfford(CurrencyType.Coins, castle.UpgradeCost());
                };

                collect.onClick.AddListener(() =>
                {
                    long got = castle.Collect(1f);
                    Sfx.Play(SfxId.Coin);
                    CoinFly.Play(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f), 12);
                    Toast.Show("+" + ItemDisplay.Format(got) + " " + Loc.Get("item.coins"));
                    popup.Close();
                    onChanged?.Invoke();
                });
                collect2.onClick.AddListener(() =>
                {
                    ads.ShowRewarded(AdPlacements.Offline2x, ok =>
                    {
                        if (!ok) { Toast.Show(Loc.Get("result.ad_unavailable")); return; }
                        long got = castle.Collect(castle_AdMultiplier());
                        Sfx.Play(SfxId.Coin);
                        CoinFly.Play(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f), 18);
                        Toast.Show("+" + ItemDisplay.Format(got) + " " + Loc.Get("item.coins"));
                        popup.Close();
                        onChanged?.Invoke();
                    });
                });
                upgrade.onClick.AddListener(() =>
                {
                    if (castle.Upgrade())
                    {
                        Sfx.Play(SfxId.Reward);
                        Haptics.Medium();
                        UIKit.Flash(popup.Panel.GetComponent<Image>(), Color.white);
                        popup.Panel.localScale = Vector3.one;
                        Tween.Punch(popup.Panel, 0.04f, 0.3f);
                        onChanged?.Invoke();
                    }
                });

                var updater = popup.Root.AddComponent<PopupTicker>();
                updater.Tick = refresh;
                refresh();
                Notify(TutorialTrigger.TargetTapped);
                return popup;
            });
        }

        private static float castle_AdMultiplier() =>
            ServiceLocator.TryGet<MetaConfig>(out var m) ? m.castle.adMultiplier : 2f;

        private static void Notify(TutorialTrigger t)
        {
            if (ServiceLocator.TryGet<Tutorial.TutorialService>(out var tut)) tut.Notify(t);
        }

        // ------------------------------------------------------------------ daily login

        public static void DailyLogin(Action onClosed = null)
        {
            PopupManager.Show(() =>
            {
                var login = ServiceLocator.Get<LoginService>();
                var popup = PopupManager.Create(Loc.Get("login.title"), new Vector2(980, 1150), true);
                var c = popup.Content;
                int next = login.NextDayIndex;
                bool canClaim = login.CanClaim;

                for (int d = 0; d < login.DayCount; d++)
                {
                    bool big = d == login.DayCount - 1;
                    bool claimed = d < next; // 'next' already advanced past today's reward once it is claimed
                    bool isToday = d == next && canClaim;
                    Vector2 size = big ? new Vector2(860, 180) : new Vector2(205, 250);
                    var tile = UIKit.PanelImage(c, claimed ? new Color(0.2f, 0.35f, 0.22f) : isToday ? UIKit.Accent : UIKit.PanelLight, "Day" + d);
                    if (big) UIKit.Place(tile.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -(2 * 270f) - 10), size);
                    else UIKit.Place(tile.rectTransform, new Vector2(0f, 1f), new Vector2(10 + (d % 4) * 215f, -(d / 4) * 270f - 10), size);

                    var day = UIKit.Label(tile.transform, Loc.Format("login.day", d + 1), 34, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
                    UIKit.Place(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(size.x, 44));

                    var rewards = login.RewardsFor(d);
                    var sb = new System.Text.StringBuilder();
                    foreach (var r in rewards)
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(ItemDisplay.Describe(r));
                    }
                    var body = UIKit.Label(tile.transform, sb.ToString(), big ? 38 : 28, Color.white);
                    UIKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(size.x - 14, size.y - 100));
                    if (claimed)
                    {
                        var tick = UIKit.Label(tile.transform, Loc.Get("common.claimed"), 28, UIKit.Good, TextAlignmentOptions.Bottom, FontStyles.Bold);
                        UIKit.Place(tick.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(size.x, 40));
                    }
                }

                var btn = UIKit.Btn(c, canClaim ? Loc.Get("common.claim") : Loc.Get("login.come_back"), canClaim ? UIKit.Good : UIKit.Disabled,
                    null, new Vector2(700, 130), 56);
                UIKit.Place((RectTransform)btn.transform, new Vector2(0.5f, 0f), new Vector2(0, 5), new Vector2(700, 130));
                btn.interactable = canClaim;
                btn.onClick.AddListener(() =>
                {
                    var items = login.Claim();
                    popup.Close();
                    if (items != null) Popups.Reward(Loc.Get("login.title"), items);
                });
                popup.Closed = onClosed;
                return popup;
            });
        }

        // ------------------------------------------------------------------ lucky spin

        public static void Spin(Action onClosed = null)
        {
            PopupManager.Show(() =>
            {
                var spin = ServiceLocator.Get<SpinService>();
                var popup = PopupManager.Create(Loc.Get("spin.title"), new Vector2(980, 1500), true);
                var c = popup.Content;
                const float R = 720f;
                int n = spin.Segments.Count;

                // the disc rotates around its own centre
                var disc = UIKit.Rect("Disc", c);
                UIKit.Place(disc, new Vector2(0.5f, 1f), new Vector2(0, -40 - R / 2f), new Vector2(R, R));
                disc.pivot = new Vector2(0.5f, 0.5f);
                disc.anchoredPosition = new Vector2(0, -40 - R / 2f);

                var rim = UIKit.Icon(disc, new Color(1f, 0.82f, 0.25f), new Vector2(R + 24, R + 24), true, "Rim");
                UIKit.Place(rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(R + 24, R + 24));
                Color[] palette = { new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.55f, 0.95f), new Color(0.3f, 0.75f, 0.4f), new Color(0.95f, 0.7f, 0.2f),
                                    new Color(0.65f, 0.4f, 0.9f), new Color(0.2f, 0.75f, 0.8f), new Color(0.95f, 0.5f, 0.3f), new Color(0.5f, 0.5f, 0.6f) };
                for (int i = 0; i < n; i++)
                {
                    var wedge = UIKit.Icon(disc, palette[i % palette.Length], new Vector2(R, R), true, "Wedge" + i);
                    UIKit.Place(wedge.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(R, R));
                    wedge.sprite = UIKit.Circle;
                    wedge.type = Image.Type.Filled;
                    wedge.fillMethod = Image.FillMethod.Radial360;
                    wedge.fillOrigin = (int)Image.Origin360.Top;
                    wedge.fillClockwise = true;
                    wedge.fillAmount = 1f / n;
                    wedge.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 360f / n);

                    float angle = (i + 0.5f) * 360f / n * Mathf.Deg2Rad;       // clockwise from top
                    var pos = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (R * 0.33f);
                    var seg = spin.Segments[i];
                    var label = UIKit.Label(disc, ItemDisplay.Describe(seg.reward), 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(190, 90));
                    label.rectTransform.localRotation = Quaternion.Euler(0, 0, -(i + 0.5f) * 360f / n);
                }
                var hub = UIKit.Icon(disc, new Color(0.1f, 0.12f, 0.2f), new Vector2(110, 110));
                UIKit.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));

                var pointer = UIKit.Icon(c, UIKit.Bad, new Vector2(60, 60), false, "Pointer");
                UIKit.Place(pointer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(60, 60));
                pointer.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

                var freeBtn = UIKit.Btn(c, "", UIKit.Good, null, new Vector2(820, 120), 48);
                UIKit.Place((RectTransform)freeBtn.transform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(820, 120));
                var ratesBtn = UIKit.Btn(c, Loc.Get("spin.rates"), UIKit.PanelLight, () => SpinRates(spin), new Vector2(300, 90), 34);
                UIKit.Place((RectTransform)ratesBtn.transform, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(300, 90));

                bool spinning = false;
                Action refresh = () =>
                {
                    bool free = spin.FreeRemaining > 0;
                    freeBtn.GetComponent<Image>().color = free ? UIKit.Good : UIKit.Blue;
                    UIKit.SetButtonText(freeBtn, free ? Loc.Get("spin.free") : Loc.Format("spin.ad", spin.AdSpinsRemaining) + "  [AD]");
                    freeBtn.interactable = !spinning && (free || spin.AdSpinsRemaining > 0);
                };

                Action<SpinOutcome> play = outcome =>
                {
                    if (!outcome.Success) { Toast.Show(Loc.Get("result.ad_unavailable")); spinning = false; refresh(); return; }
                    float target = 360f * 6f + (outcome.SegmentIndex + 0.5f) * 360f / n;
                    float start = disc.localEulerAngles.z;
                    Tween.Value(4f, k => { if (disc != null) disc.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(start, target, k)); }, Ease.OutCubic, () =>
                    {
                        spinning = false;
                        refresh();
                        Popups.Reward(Loc.Get("spin.title"), outcome.Items);
                    }, 0f, disc);
                    Sfx.Play(SfxId.Whoosh);
                };

                freeBtn.onClick.AddListener(() =>
                {
                    spinning = true;
                    freeBtn.interactable = false;
                    if (spin.FreeRemaining > 0) play(spin.SpinFree());
                    else spin.SpinWithAd(play);
                });
                refresh();
                popup.Closed = onClosed;
                return popup;
            });
        }

        private static void SpinRates(SpinService spin)
        {
            PopupManager.Show(() =>
            {
                var rates = spin.Rates();
                var popup = PopupManager.Create(Loc.Get("spin.rates"), new Vector2(900, 330f + rates.Count * 90f), true);
                var c = popup.Content;
                for (int i = 0; i < rates.Count; i++)
                {
                    var name = UIKit.Label(c, ItemDisplay.Describe(spin.Segments[i].reward), 38, Color.white, TextAlignmentOptions.MidlineLeft);
                    UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(10, -i * 90f), new Vector2(560, 80));
                    var pct = UIKit.Label(c, rates[i].ToString("0.#") + "%", 40, UIKit.Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                    UIKit.Place(pct.rectTransform, new Vector2(1f, 1f), new Vector2(-10, -i * 90f), new Vector2(220, 80));
                }
                var ok = UIKit.Btn(c, Loc.Get("common.ok"), UIKit.PanelLight, () => popup.Close(), new Vector2(420, 100), 44);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 5), new Vector2(420, 100));
                return popup;
            }, true);
        }

        // ------------------------------------------------------------------ offers

        public static void Offer(ActiveOffer offer, Action onClosed = null)
        {
            PopupManager.Show(() =>
            {
                var iap = ServiceLocator.Get<IapManager>();
                iap.LogView(offer.Product.sku);
                var popup = PopupManager.Create(Loc.Get(offer.Def.titleKey), new Vector2(940, 1150), true);
                var c = popup.Content;

                if (offer.Def.discountPct > 0)
                {
                    var tag = UIKit.PanelImage(c, UIKit.Bad, "Discount");
                    UIKit.Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(0, 20), new Vector2(240, 100));
                    var tl = UIKit.Label(tag.transform, "-" + offer.Def.discountPct + "%", 52, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    UIKit.Stretch(tl.rectTransform);
                }

                var desc = UIKit.Label(c, Loc.Get(offer.Product.descKey), 38, UIKit.TextDim);
                UIKit.Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(820, 110));

                float y = -150f;
                foreach (var r in offer.Product.rewards)
                {
                    var rowIcon = UIKit.Icon(c, ItemDisplay.ColorOf(r.type), new Vector2(64, 64));
                    UIKit.Place(rowIcon.rectTransform, new Vector2(0f, 1f), new Vector2(80, y), new Vector2(64, 64));
                    var row = UIKit.Label(c, ItemDisplay.Describe(r), 46, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                    UIKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(170, y + 2), new Vector2(660, 70));
                    y -= 95f;
                }

                var timer = UIKit.Label(c, "", 40, UIKit.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(timer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(820, 60));
                var buy = UIKit.Btn(c, iap.PriceLabel(offer.Product.sku), UIKit.Good, null, new Vector2(760, 140), 64);
                UIKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(760, 140));
                buy.onClick.AddListener(() =>
                {
                    buy.interactable = false;
                    iap.Purchase(offer.Product.sku, r =>
                    {
                        buy.interactable = true;
                        if (r.Success) { popup.Close(); Sfx.Play(SfxId.Reward); Popups.Info(Loc.Get("shop.thanks"), Loc.Get("shop.purchase_ok")); }
                        else if (r.Status == Services.PurchaseStatus.Failed) Toast.Show(Loc.Get("shop.purchase_failed"));
                    });
                });

                var off = ServiceLocator.Get<OfferService>();
                var ticker = popup.Root.AddComponent<PopupTicker>();
                ticker.Tick = () =>
                {
                    var active = off.Active().Find(o => o.Def.id == offer.Def.id);
                    if (active.Def == null) { popup.Close(); return; }
                    timer.text = ItemDisplay.Duration(active.Remaining);
                };
                ticker.Tick();
                popup.Closed = onClosed;
                return popup;
            });
        }
    }

    /// <summary>Runs a callback twice per second while a popup is on screen (timers, button states).</summary>
    public sealed class PopupTicker : MonoBehaviour
    {
        public Action Tick;
        private float _t;

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_t < 0.5f) return;
            _t = 0f;
            Tick?.Invoke();
        }
    }
}
