using System;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;
using BlockBloom.Logic;

namespace BlockBloom
{
    public static class TopBar
    {
        public static void Create(Transform parent, bool withSettings)
        {
            var h = HeartPill.Create(parent, () => Popups.NoHearts(null));
            h.PosA(0f, 1f, 190, -90);
            var c = CoinPill.Create(parent, true, Popups.Shop);
            c.PosA(1f, 1f, withSettings ? -400 : -215, -90);
            if (withSettings)
            {
                var g = Ui.IconBtn(parent, Sprites.Gear(), Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), 96, () => Popups.Settings(null));
                g.PosA(1f, 1f, -75, -90);
            }
        }
    }

    public sealed class HomeScreen : ScreenBase
    {
        private readonly System.Collections.Generic.List<RectTransform> _logo = new System.Collections.Generic.List<RectTransform>();
        private readonly System.Collections.Generic.List<RectTransform> _floaters = new System.Collections.Generic.List<RectTransform>();
        private GameObject _giftDot, _spinDot, _questDot, _dailyDot;

        public void Build()
        {
            TopBar.Create(Rt, true);
            BuildLogo();
            BuildFloaters();

            // themes shortcut (top-left under the hearts)
            var th = Ui.IconBtn(Rt, Icons.Palette4(), Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), 96, () => Popups.Themes(null), Color.white);
            th.PosA(0f, 1f, 90, -200);

            int lv = Mathf.Clamp(Save.Data.unlockedLevel, 1, Adventure.LevelCount);
            var play = Ui.Btn(Rt, "", Palette.Green, Palette.GreenDark, new Vector2(780, 190), () => LevelUi.Open(lv), 80, "play").PosA(0.5f, 0.5f, 0, -150);
            var pt = Ui.Label(play.transform, "PLAY", 98, Color.white, TextAnchor.MiddleCenter); Ui.At(pt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(700, 110));
            var ps = Ui.Label(play.transform, "LEVEL " + lv, 42, Palette.Hex("#d9ffe3"), TextAnchor.MiddleCenter); Ui.At(ps.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(700, 50));
            Anim.AddShine(play, 2.6f);
            Anim.Breathe(play.transform.Find("face"), 0.03f, 1.4f);
            Anim.Breathe(play.transform.Find("lip"), 0.03f, 1.4f);

            var classic = Ui.Btn(Rt, "", Palette.Blue, Palette.BlueDark, new Vector2(380, 150), () => App.I.StartClassic(), 50, "classic").PosA(0.5f, 0.5f, -200, -380);
            var ct = Ui.Label(classic.transform, "CLASSIC", 52, Color.white, TextAnchor.MiddleCenter); Ui.At(ct.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(340, 60));
            var cs = Ui.Label(classic.transform, "BEST " + Ui.Num(Save.Data.classicBest), 32, Palette.Hex("#cfe3ff"), TextAnchor.MiddleCenter); Ui.At(cs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(340, 40));

            bool dailyOpen = Save.Data.dailyDone != Save.Today;
            var daily = Ui.Btn(Rt, "", Palette.Hex("#ff8a34"), Palette.Hex("#c25a0f"), new Vector2(380, 150), () => App.I.StartDaily(), 50, "daily").PosA(0.5f, 0.5f, 200, -380);
            var dt = Ui.Label(daily.transform, "DAILY", 52, Color.white, TextAnchor.MiddleCenter); Ui.At(dt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(340, 60));
            var ds = Ui.Label(daily.transform, dailyOpen ? "NEW CHALLENGE" : "DONE TODAY", 30, Palette.Hex("#fff0d4"), TextAnchor.MiddleCenter); Ui.At(ds.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(340, 40));
            _dailyDot = Dot(daily.transform, dailyOpen);
            if (dailyOpen) Anim.AddShine(daily, 3.6f);

            // star chest progress
            var chest = Ui.Rect(Rt, "chest"); chest.sizeDelta = new Vector2(780, 110); chest.PosA(0.5f, 0.5f, 0, -545);
            var chBg = Ui.RoundImg(chest, new Color(0.06f, 0.03f, 0.22f, 0.6f), 50, "bg"); Ui.Stretch(chBg.rectTransform);
            var chIc = Ui.Img(chest, Icons.Gift(), Color.white, "gift"); Ui.At(chIc.rectTransform, new Vector2(0, 0.5f), new Vector2(68, 6), new Vector2(104, 104));
            Anim.Wiggle(chIc.transform, 9f, 1f);
            Ui.Later(2.5f, () => { if (chIc != null) Anim.Wiggle(chIc.transform, 9f, 0.8f); });
            Ui.LabelAt(chest, "STAR CHEST", 32, Palette.Gold, new Vector2(0, 0.5f), new Vector2(330, 26), new Vector2(380, 40), TextAnchor.MiddleLeft, false);
            RectTransform chFill;
            var chBar = Widgets.Bar(chest, new Vector2(520, 38), new Color(0, 0, 0, 0.4f), Palette.Hex("#ff9f1c"), out chFill);
            Ui.At(chBar.rectTransform, new Vector2(0, 0.5f), new Vector2(380, -16), new Vector2(520, 38));
            float cf = Mathf.Clamp01(Save.Data.chestProgress / (float)Economy.ChestStars);
            Widgets.SetBar(chFill, 520, 0f);
            Tween.Value(0.9f, k => { if (chFill != null) Widgets.SetBar(chFill, 520, cf * k); }, Ease.OutCubic, null, 0.6f, chFill);
            Ui.LabelAt(chBar.transform, Mathf.Min(Save.Data.chestProgress, Economy.ChestStars) + " / " + Economy.ChestStars + " STARS", 26, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 36));

            // bottom nav
            var nav = Ui.Rect(Rt, "nav");
            nav.anchorMin = new Vector2(0, 0); nav.anchorMax = new Vector2(1, 0); nav.pivot = new Vector2(0.5f, 0);
            nav.sizeDelta = new Vector2(0, 210); nav.anchoredPosition = Vector2.zero;
            var navBg = Ui.Img(nav, Sprites.Square(), new Color(0.07f, 0.04f, 0.26f, 0.96f), "bg"); Ui.Stretch(navBg.rectTransform);
            var navTop = Ui.Img(nav, Sprites.Square(), new Color(1, 1, 1, 0.12f), "edge"); navTop.rectTransform.anchorMin = new Vector2(0, 1); navTop.rectTransform.anchorMax = new Vector2(1, 1);
            navTop.rectTransform.pivot = new Vector2(0.5f, 1); navTop.rectTransform.sizeDelta = new Vector2(0, 4); navTop.rectTransform.anchoredPosition = Vector2.zero;

            BarBtn(nav, "SHOP", Icons.Bag(), Palette.Hex("#8a5cf0"), Palette.Hex("#5428b0"), 0.1f, Popups.Shop, false);
            _questDot = BarBtn(nav, "QUESTS", Icons.Scroll(), Palette.Hex("#3fb35a"), Palette.Hex("#217a3a"), 0.3f, () => Popups.QuestsPopup(() => Refresh()), QuestReady());
            BarBtn(nav, "MAP", Icons.Flag(), Palette.Hex("#19b8a6"), Palette.Hex("#0f7f72"), 0.5f, () => App.I.ShowMap(), false);
            _spinDot = BarBtn(nav, "SPIN", Icons.Wheel(), Palette.Hex("#a66cff"), Palette.Hex("#6532c4"), 0.7f, () => Popups.Spin(() => Refresh()), Economy.SpinAvailable);
            _giftDot = BarBtn(nav, "GIFT", Icons.Gift(), Palette.Hex("#ff5a96"), Palette.Hex("#b82d62"), 0.9f, () => Popups.DailyReward(() => Refresh()), Economy.LoginAvailable);

            // auto-offer the daily reward once per day
            if (Economy.LoginAvailable && Save.Data.tutorialDone) Ui.Later(0.9f, () => { if (this != null && App.I.Popups.Count == 0) Popups.DailyReward(() => Refresh()); });
        }

        private static bool QuestReady()
        {
            Economy.EnsureQuestDay();
            for (int i = 0; i < 3; i++) if (Save.Data.questProgress[i] >= Economy.Quests[i].Target && !Save.Data.questClaimed[i]) return true;
            return false;
        }

        private void Refresh()
        {
            if (_giftDot != null) _giftDot.SetActive(Economy.LoginAvailable);
            if (_spinDot != null) _spinDot.SetActive(Economy.SpinAvailable);
            if (_questDot != null) _questDot.SetActive(QuestReady());
        }

        private GameObject BarBtn(Transform parent, string label, Sprite icon, Color face, Color lip, float ax, Action a, bool dot)
        {
            var b = Ui.Btn(parent, "", face, lip, new Vector2(176, 158), a, 36, label.ToLower());
            Ui.At((RectTransform)b.transform, new Vector2(ax, 0.5f), new Vector2(0, 8), new Vector2(176, 158));
            var ic = Ui.Img(b.transform, icon, Color.white, "ic"); Ui.At(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(92, 92));
            var t = Ui.Label(b.transform, label, 32, Color.white, TextAnchor.MiddleCenter); Ui.At(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(176, 40));
            var d = Dot(b.transform, dot);
            if (dot) { Anim.Bob((RectTransform)ic.transform, 5f, 3f, ax * 6f); Anim.AddShine(b, 3.4f); }
            return d;
        }

        private static GameObject Dot(Transform parent, bool on)
        {
            var d = Ui.Img(parent, Sprites.Circle(), Palette.Red, "dot");
            Ui.At(d.rectTransform, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(44, 44));
            var n = Ui.Label(d.transform, "!", 32, Color.white, TextAnchor.MiddleCenter, false); Ui.Stretch(n.rectTransform);
            d.gameObject.SetActive(on);
            return d.gameObject;
        }

        private void Pulse(Transform t)
        {
            Tween.Value(1.4f, k => { if (t != null) t.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(k * Mathf.PI * 2f)); }, Ease.Linear, () => { if (t != null) Pulse(t); }, 0f, t);
        }

        private void BuildLogo()
        {
            string[] words = { "BLOCK", "BLOOM" };
            for (int w = 0; w < 2; w++)
            {
                for (int i = 0; i < 5; i++)
                {
                    var tile = Ui.Img(Rt, Sprites.Block(), Palette.Block(w * 5 + i + 1), "L");
                    float x = (i - 2) * 168;
                    float y = 640 - w * 175;
                    Ui.At(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(158, 158));
                    var ch = Ui.Label(tile.transform, words[w][i].ToString(), 112, Color.white, TextAnchor.MiddleCenter);
                    Ui.At(ch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(150, 140));
                    _logo.Add(tile.rectTransform);
                    tile.rectTransform.localScale = Vector3.zero;
                    Tween.ScaleTo(tile.rectTransform, Vector3.one, 0.5f, Ease.OutBack, 0.08f * (w * 5 + i));
                }
            }
        }

        private void BuildFloaters()
        {
            // a little cluster of pieces hovering between the logo and the buttons
            int[] ids = { 7, 20, 31 };
            float[] xs = { -300, 0, 300 };
            for (int i = 0; i < 3; i++)
            {
                var shape = ShapeCatalog.All[Mathf.Min(ids[i], ShapeCatalog.All.Length - 1)];
                var p = TrayView.BuildPiece(Rt, shape, i * 2 + 1);
                p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f);
                p.anchoredPosition = new Vector2(xs[i], 270 - (i == 1 ? 40 : 0));
                p.localScale = Vector3.one * 0.55f;
                p.localRotation = Quaternion.Euler(0, 0, (i - 1) * 8f);
                _floaters.Add(p);
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < _logo.Count; i++)
            {
                var p = _logo[i].anchoredPosition;
                _logo[i].anchoredPosition = new Vector2(p.x, (i < 5 ? 640f : 465f) + Mathf.Sin(t * 1.6f + i * 0.7f) * 9f);
            }
            for (int i = 0; i < _floaters.Count; i++)
            {
                var p = _floaters[i].anchoredPosition;
                _floaters[i].anchoredPosition = new Vector2(p.x, (270 - (i == 1 ? 40 : 0)) + Mathf.Sin(t * 1.2f + i * 2f) * 16f);
                _floaters[i].localRotation = Quaternion.Euler(0, 0, (i - 1) * 8f + Mathf.Sin(t * 0.9f + i) * 4f);
            }
        }

        public override void OnBack() { Application.Quit(); }
    }
}
