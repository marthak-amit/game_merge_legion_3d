using System;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    public sealed class WinPopupArgs
    {
        public int Stars;
        public LevelReward Reward;
        public int AdMultiplier = 3;
        public bool AdAvailable;
        /// <summary>Starts the rewarded ad; the callback receives true when the extra reward should be granted.</summary>
        public Action<Action<bool>> WatchAd;
        public Action Next;
        public Action Home;
    }

    public sealed class LosePopupArgs
    {
        public long Consolation;
        public bool ReviveAvailable;
        public Action<Action<bool>> Revive;
        public Action Retry;
        public Action Home;
    }

    /// <summary>Win / lose screens of a battle (section 1.1 steps 5).</summary>
    public static class ResultPopups
    {
        public static Popup ShowWin(WinPopupArgs a)
        {
            var popup = PopupManager.Create(Loc.Get("result.victory"), new Vector2(920, 1180));
            var c = popup.Content;

            for (int i = 0; i < 3; i++)
            {
                bool earned = i < a.Stars;
                var star = UIKit.Icon(c, earned ? UIKit.Gold : new Color(0.25f, 0.28f, 0.38f), new Vector2(130, 130), false, "Star" + i);
                UIKit.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 190f, -30), new Vector2(130, 130));
                star.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                if (earned)
                {
                    star.rectTransform.localScale = Vector3.zero;
                    Tween.ScaleTo(star.rectTransform, Vector3.one, 0.35f, Ease.OutBack, 0.3f + i * 0.25f);
                }
            }

            var coins = Row(c, UIKit.Gold, "+" + Format(a.Reward.Coins), -250);
            Tween.Value(0.1f, k => { }, Ease.Linear, () => CoinFly.Play(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 10), 0.9f);
            if (a.Reward.Gems > 0) Row(c, UIKit.Blue, "+" + a.Reward.Gems + " " + Loc.Get("result.gems"), -340);
            if (a.Reward.Keys > 0) Row(c, UIKit.Purple, "+" + a.Reward.Keys + " " + Loc.Get("result.keys"), a.Reward.Gems > 0 ? -430 : -340);

            Button adBtn = null;
            if (a.AdAvailable)
            {
                adBtn = UIKit.Btn(c, Loc.Format("result.collect_x3", a.AdMultiplier) + "  [AD]", UIKit.Blue, null, new Vector2(760, 130), 46);
                UIKit.Place((RectTransform)adBtn.transform, new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(760, 130));
                adBtn.onClick.AddListener(() =>
                {
                    adBtn.interactable = false;
                    a.WatchAd(ok =>
                    {
                        if (ok)
                        {
                            coins.text = "+" + Format(a.Reward.Coins * a.AdMultiplier);
                            Tween.Punch(coins.transform, 0.3f, 0.35f);
                            Sfx.Play(SfxId.Coin);
                            adBtn.gameObject.SetActive(false);
                        }
                        else
                        {
                            adBtn.interactable = true;
                            Toast.Show(Loc.Get("result.ad_unavailable"));
                        }
                    });
                });
            }

            var next = UIKit.Btn(c, Loc.Get("result.next"), UIKit.Good, () => { popup.Close(); a.Next?.Invoke(); }, new Vector2(760, 130), 56);
            UIKit.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(760, 130));
            if (!a.AdAvailable) UIKit.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(760, 130));
            return popup;
        }

        public static Popup ShowLose(LosePopupArgs a)
        {
            var popup = PopupManager.Create(Loc.Get("result.defeat"), new Vector2(920, 900));
            var c = popup.Content;
            Row(c, UIKit.Gold, "+" + Format(a.Consolation), -40);

            float y = 20;
            var retry = UIKit.Btn(c, Loc.Get("result.retry"), UIKit.Good, () => { popup.Close(); a.Retry?.Invoke(); }, new Vector2(760, 130), 56);
            UIKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(0, y + 300), new Vector2(760, 130));

            if (a.ReviveAvailable)
            {
                var revive = UIKit.Btn(c, Loc.Get("result.revive") + "  [AD]", UIKit.Blue, null, new Vector2(760, 130), 44);
                UIKit.Place((RectTransform)revive.transform, new Vector2(0.5f, 0f), new Vector2(0, y + 150), new Vector2(760, 130));
                revive.onClick.AddListener(() =>
                {
                    revive.interactable = false;
                    a.Revive(ok =>
                    {
                        if (ok) popup.Close();
                        else
                        {
                            revive.interactable = true;
                            Toast.Show(Loc.Get("result.ad_unavailable"));
                        }
                    });
                });
            }

            var home = UIKit.Btn(c, Loc.Get("result.home"), UIKit.PanelLight, () => { popup.Close(); a.Home?.Invoke(); }, new Vector2(760, 110), 46);
            UIKit.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0, y), new Vector2(760, 110));
            return popup;
        }

        public static Popup ShowArena(bool won, int trophyDelta, int trophies, long coins, bool leagueChanged, Action onHome)
        {
            var popup = PopupManager.Create(Loc.Get(won ? "result.victory" : "result.defeat"), new Vector2(920, 800));
            var c = popup.Content;
            string sign = trophyDelta >= 0 ? "+" : "";
            var t = UIKit.Label(c, sign + trophyDelta + " " + Loc.Get("arena.trophies"), 72, won ? UIKit.Gold : UIKit.Bad, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(820, 100));
            var total = UIKit.Label(c, Loc.Format("arena.total", trophies), 44, UIKit.TextDim);
            UIKit.Place(total.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(820, 70));
            if (leagueChanged)
            {
                var l = UIKit.Label(c, Loc.Get(won ? "arena.league_up" : "arena.league_down"), 48, won ? UIKit.Good : UIKit.Bad, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(820, 70));
            }
            Row(c, UIKit.Gold, "+" + Format(coins), -300);
            var home = UIKit.Btn(c, Loc.Get("arena.back"), UIKit.Good, () => { popup.Close(); onHome?.Invoke(); }, new Vector2(760, 130), 56);
            UIKit.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(760, 130));
            return popup;
        }

        private static TMP_Text Row(RectTransform parent, Color iconColor, string text, float y)
        {
            var icon = UIKit.Icon(parent, iconColor, new Vector2(70, 70));
            UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-250, y), new Vector2(70, 70));
            var label = UIKit.Label(parent, text, 64, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(-190, y - 2), new Vector2(560, 80));
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(-190, y + 4);
            return label;
        }

        public static string Format(long n)
        {
            if (n >= 1000000000L) return (n / 1000000000.0).ToString("0.##") + "B";
            if (n >= 1000000L) return (n / 1000000.0).ToString("0.##") + "M";
            if (n >= 100000L) return (n / 1000.0).ToString("0.#") + "K";
            return n.ToString("N0");
        }
    }
}
