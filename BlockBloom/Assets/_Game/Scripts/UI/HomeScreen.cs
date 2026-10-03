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

            int lv = Mathf.Clamp(Save.Data.unlockedLevel, 1, Adventure.LevelCount);
            var play = Ui.Btn(Rt, "", Palette.Green, Palette.GreenDark, new Vector2(780, 190), () => LevelUi.Open(lv), 80, "play").PosA(0.5f, 0.5f, 0, -160);
            var pt = Ui.Label(play.transform, "PLAY", 96, Color.white, TextAnchor.MiddleCenter); Ui.At(pt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(700, 110));
            var ps = Ui.Label(play.transform, "LEVEL " + lv, 40, Palette.Hex("#d9ffe3"), TextAnchor.MiddleCenter); Ui.At(ps.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(700, 50));
            Pulse(play.transform);

            var classic = Ui.Btn(Rt, "", Palette.Blue, Palette.BlueDark, new Vector2(380, 150), () => App.I.StartClassic(), 50, "classic").PosA(0.5f, 0.5f, -200, -400);
            var ct = Ui.Label(classic.transform, "CLASSIC", 52, Color.white, TextAnchor.MiddleCenter); Ui.At(ct.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(340, 60));
            var cs = Ui.Label(classic.transform, "BEST " + Ui.Num(Save.Data.classicBest), 32, Palette.Hex("#cfe3ff"), TextAnchor.MiddleCenter); Ui.At(cs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(340, 40));

            bool dailyOpen = Save.Data.dailyDone != Save.Today;
            var daily = Ui.Btn(Rt, "", Palette.Hex("#ff8a34"), Palette.Hex("#c25a0f"), new Vector2(380, 150), () => App.I.StartDaily(), 50, "daily").PosA(0.5f, 0.5f, 200, -400);
            var dt = Ui.Label(daily.transform, "DAILY", 52, Color.white, TextAnchor.MiddleCenter); Ui.At(dt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(340, 60));
            var ds = Ui.Label(daily.transform, dailyOpen ? "NEW CHALLENGE" : "DONE TODAY", 30, Palette.Hex("#fff0d4"), TextAnchor.MiddleCenter); Ui.At(ds.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(340, 40));
            _dailyDot = Dot(daily.transform, dailyOpen);

            // bottom bar
            float by = 130;
            Ui.Btn(Rt, "", Palette.Hex("#6f4bd8"), Palette.Hex("#45299c"), new Vector2(180, 180), Popups.Shop, 40, "shop").PosA(0.1f, 0f, 0, by).Size(180, 180);
            Icon("shop", Sprites.Coin(), Palette.Gold, "SHOP", 0.1f, by);
            BarBtn("MAP", Sprites.Play(), Palette.Hex("#19b8a6"), Palette.Hex("#0f7f72"), 0.3f, by, () => App.I.ShowMap(), false);
            _giftDot = BarBtn("GIFT", Sprites.Diamond(), Palette.Hex("#ff5a96"), Palette.Hex("#b82d62"), 0.5f, by, () => Popups.DailyReward(() => Refresh()), Economy.LoginAvailable);
            _spinDot = BarBtn("SPIN", Sprites.Star(), Palette.Hex("#a66cff"), Palette.Hex("#6532c4"), 0.7f, by, () => Popups.Spin(() => Refresh()), Economy.SpinAvailable);
            _questDot = BarBtn("QUESTS", Sprites.Check(), Palette.Hex("#3fb35a"), Palette.Hex("#217a3a"), 0.9f, by, () => Popups.QuestsPopup(() => Refresh()), QuestReady());
            var themes = Ui.Btn(Rt, "THEMES", Palette.Alpha(Color.white, 0.14f), Palette.Alpha(Color.black, 0.2f), new Vector2(240, 80), () => Popups.Themes(null), 34, "themes").PosA(0.5f, 0f, 0, 330);

            // auto-offer the daily reward once per day
            if (Economy.LoginAvailable && Save.Data.tutorialDone) Ui.Later(0.6f, () => { if (this != null && App.I.Popups.Count == 0) Popups.DailyReward(() => Refresh()); });
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

        private GameObject BarBtn(string label, Sprite icon, Color face, Color lip, float ax, float y, Action a, bool dot)
        {
            var b = Ui.Btn(Rt, "", face, lip, new Vector2(170, 170), a, 36, label.ToLower()).PosA(ax, 0f, 0, y);
            var ic = Ui.Img(b.transform, icon, Color.white, "ic"); Ui.At(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(84, 84));
            var t = Ui.Label(b.transform, label, 32, Color.white, TextAnchor.MiddleCenter); Ui.At(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(170, 40));
            return Dot(b.transform, dot);
        }

        private void Icon(string btnName, Sprite icon, Color col, string label, float ax, float y)
        {
            var b = Rt.Find(btnName);
            var ic = Ui.Img(b, icon, col, "ic"); Ui.At(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(84, 84));
            var t = Ui.Label(b, label, 32, Color.white, TextAnchor.MiddleCenter); Ui.At(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(170, 40));
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
