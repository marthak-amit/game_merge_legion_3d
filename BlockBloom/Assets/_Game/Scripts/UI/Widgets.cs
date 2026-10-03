using System;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;

namespace BlockBloom
{
    /// <summary>Live coin counter pill (counts up/down smoothly).</summary>
    public sealed class CoinPill : MonoBehaviour
    {
        private Text _t; private float _shown = -1;
        public RectTransform Icon;

        public static CoinPill Create(Transform parent, bool plus, Action onPlus)
        {
            var rt = Ui.Rect(parent, "coinpill");
            rt.sizeDelta = new Vector2(330, 84);
            var bg = Ui.RoundImg(rt, new Color(0.07f, 0.04f, 0.22f, 0.78f), 42, "bg", plus);
            Ui.Stretch(bg.rectTransform);
            var ic = Ui.Img(rt, Sprites.Coin(), Palette.Gold, "coin");
            Ui.At(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(76, 76));
            var p = rt.gameObject.AddComponent<CoinPill>();
            p.Icon = ic.rectTransform;
            p._t = Ui.Label(rt, "0", 46, Color.white, TextAnchor.MiddleCenter);
            Ui.At(p._t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-6, 0), new Vector2(180, 80));
            if (plus)
            {
                var b = Ui.Btn(rt, "+", Palette.Green, Palette.GreenDark, new Vector2(66, 66), onPlus, 48, "plus");
                Ui.At((RectTransform)b.transform, new Vector2(1, 0.5f), new Vector2(-44, 3), new Vector2(66, 66));
            }
            return p;
        }

        private void Update()
        {
            float target = Save.Data.coins;
            if (_shown < 0) _shown = target;
            _shown = Mathf.MoveTowards(_shown, target, Mathf.Max(1f, Mathf.Abs(target - _shown) * 6f * Time.unscaledDeltaTime + 1f));
            _t.text = Ui.Num(Mathf.RoundToInt(_shown));
        }
    }

    public sealed class HeartPill : MonoBehaviour
    {
        private Text _t, _sub;

        public static HeartPill Create(Transform parent, Action onTap)
        {
            var rt = Ui.Rect(parent, "heartpill");
            rt.sizeDelta = new Vector2(300, 84);
            var bg = Ui.RoundImg(rt, new Color(0.07f, 0.04f, 0.22f, 0.78f), 42, "bg", true);
            Ui.Stretch(bg.rectTransform);
            var ic = Ui.Img(rt, Sprites.Heart(), Palette.Red, "heart");
            Ui.At(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 2), new Vector2(72, 72));
            var p = rt.gameObject.AddComponent<HeartPill>();
            p._t = Ui.Label(rt, "5", 48, Color.white, TextAnchor.MiddleCenter);
            Ui.At(p._t.rectTransform, new Vector2(0, 0.5f), new Vector2(112, 2), new Vector2(60, 80));
            p._sub = Ui.Label(rt, "FULL", 32, Palette.Hex("#ffd9e0"), TextAnchor.MiddleCenter);
            Ui.At(p._sub.rectTransform, new Vector2(1, 0.5f), new Vector2(-84, 0), new Vector2(150, 60));
            var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { Sfx.Click(); if (onTap != null) onTap(); });
            rt.gameObject.AddComponent<PressFx>();
            return p;
        }

        private void Update()
        {
            Economy.TickHearts();
            _t.text = Save.Data.hearts.ToString();
            _sub.text = Save.Data.hearts >= Economy.MaxHearts ? "FULL" : Ui.Clock(Economy.UntilNextHeart());
            _sub.fontSize = Save.Data.hearts >= Economy.MaxHearts ? 30 : 36;
        }
    }

    public static class Widgets
    {
        public static Image Stars(Transform parent, int lit, float size, float gap, string name = "stars")
        {
            var root = Ui.Rect(parent, name);
            root.sizeDelta = new Vector2(size * 3 + gap * 2, size);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < lit;
                float s = (i == 1) ? size * 1.12f : size;
                var im = Ui.Img(root, Sprites.Star(), on ? Palette.Gold : new Color(0.1f, 0.06f, 0.3f, 0.55f), "s" + i);
                Ui.At(im.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * (size + gap), i == 1 ? size * 0.12f : 0), new Vector2(s, s));
            }
            return null;
        }

        public static Text Pill(Transform parent, string text, Color bg, Vector2 size, int font = 40)
        {
            var im = Ui.RoundImg(parent, bg, (int)(size.y * 0.5f), "pill");
            im.rectTransform.sizeDelta = size;
            var t = Ui.Label(im.transform, text, font, Color.white, TextAnchor.MiddleCenter);
            Ui.Stretch(t.rectTransform, 10, 0, 10, 4);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = font;
            return t;
        }

        public static Image Bar(Transform parent, Vector2 size, Color back, Color fill, out RectTransform fillRt)
        {
            var bg = Ui.RoundImg(parent, back, (int)(size.y * 0.5f), "bar");
            bg.rectTransform.sizeDelta = size;
            var f = Ui.RoundImg(bg.transform, fill, (int)(size.y * 0.5f), "fill");
            f.rectTransform.anchorMin = f.rectTransform.anchorMax = new Vector2(0, 0.5f);
            f.rectTransform.pivot = new Vector2(0, 0.5f);
            f.rectTransform.anchoredPosition = new Vector2(3, 0);
            f.rectTransform.sizeDelta = new Vector2(size.x - 6, size.y - 6);
            fillRt = f.rectTransform;
            return bg;
        }

        public static void SetBar(RectTransform fill, float outerWidth, float k)
        {
            k = Mathf.Clamp01(k);
            fill.sizeDelta = new Vector2(Mathf.Max(fill.sizeDelta.y, (outerWidth - 6) * k), fill.sizeDelta.y);
            fill.gameObject.SetActive(k > 0.001f);
        }

        /// <summary>Flies a few coins from a world point into the coin counter spot (top-left).</summary>
        public static void CoinBurst(Vector3 from, int n = 8)
        {
            if (Fx.I == null) return;
            for (int i = 0; i < n; i++) Fx.I.Burst(from, Palette.Gold, 1, 420f, 34f);
            Sfx.Coin();
        }
    }
}
