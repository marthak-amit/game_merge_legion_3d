using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlockBloom.Core;

namespace BlockBloom
{
    /// <summary>Press feedback: squashes the button while a finger is down, springs back on release.</summary>
    public sealed class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private bool _down;
        private float _k = 1f, _v;

        public void OnPointerDown(PointerEventData e) { _down = true; _v = -2f; }
        public void OnPointerUp(PointerEventData e) { if (_down) _v = 5f; _down = false; }
        public void OnPointerExit(PointerEventData e) { _down = false; }
        private void OnDisable() { _down = false; _k = 1f; _v = 0f; }

        private void Update()
        {
            // critically-underdamped spring towards the pressed (0.92) or resting (1.0) size: squash on press, overshoot on release
            float target = _down ? 0.92f : 1f;
            if (!_down && Mathf.Abs(_k - 1f) < 0.0008f && Mathf.Abs(_v) < 0.01f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            _v += (target - _k) * 420f * dt;
            _v *= Mathf.Exp(-17f * dt);
            _k += _v * dt;
            transform.localScale = Vector3.one * _k;
        }
    }

    /// <summary>Tiny layout/builder toolbox: the whole UI is created from code, so one consistent look comes from here.</summary>
    public static class Ui
    {
        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 40);
                return _font;
            }
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>Centre-pivot placement relative to an anchor point (0..1).</summary>
        public static RectTransform At(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "img", bool raycast = false)
        {
            var rt = Rect(parent, name);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite; im.color = color; im.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0.1f) im.type = Image.Type.Sliced;
            return im;
        }

        public static Image RoundImg(Transform parent, Color color, int radius = 24, string name = "round", bool raycast = false)
        {
            return Img(parent, Sprites.Round(radius), color, name, raycast);
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter,
            bool outline = true, string name = "label")
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.85f);
                o.effectDistance = new Vector2(Mathf.Max(2f, size * 0.07f), -Mathf.Max(2f, size * 0.07f));
                o.useGraphicAlpha = true;
            }
            return t;
        }

        public static Text LabelAt(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
        {
            var t = Label(parent, text, size, color, align, outline);
            At(t.rectTransform, anchor, pos, box);
            return t;
        }

        /// <summary>Chunky 3D candy button: dark lip below, glossy face on top, centred label. Returns the root.</summary>
        public static Button Btn(Transform parent, string text, Color face, Color lip, Vector2 size, Action onClick, int fontSize = 52, string name = "btn")
        {
            var root = Rect(parent, name);
            root.sizeDelta = size;
            float r = Mathf.Min(size.y * 0.5f, 40f);
            int rad = Mathf.RoundToInt(r * 0.8f);
            var lipImg = Img(root, Sprites.Glossy(28), lip, "lip");
            lipImg.rectTransform.anchorMin = Vector2.zero; lipImg.rectTransform.anchorMax = Vector2.one;
            lipImg.rectTransform.offsetMin = new Vector2(0, -size.y * 0.09f); lipImg.rectTransform.offsetMax = new Vector2(0, -size.y * 0.09f);
            var faceImg = Img(root, Sprites.Glossy(28), face, "face", true);
            Stretch(faceImg.rectTransform);
            if (!string.IsNullOrEmpty(text))
            {
                var lb = Label(root, text, fontSize, Color.white, TextAnchor.MiddleCenter, true, "text");
                Stretch(lb.rectTransform, 12, 6, 12, 6);
                lb.horizontalOverflow = HorizontalWrapMode.Wrap; lb.resizeTextForBestFit = true;
                lb.resizeTextMinSize = Mathf.Max(18, fontSize / 2); lb.resizeTextMaxSize = fontSize;
            }
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = faceImg; btn.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<PressFx>();
            btn.onClick.AddListener(() => { Sfx.Click(); if (onClick != null) onClick(); });
            return btn;
        }

        public static Button IconBtn(Transform parent, Sprite icon, Color face, Color lip, float size, Action onClick, Color? iconColor = null)
        {
            var b = Btn(parent, "", face, lip, new Vector2(size, size), onClick, 40, "iconbtn");
            var ic = Img(b.transform, icon, iconColor ?? Color.white, "icon");
            At(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.58f, size * 0.58f));
            return b;
        }

        public static Text ButtonText(Button b)
        {
            var t = b.transform.Find("text");
            return t != null ? t.GetComponent<Text>() : null;
        }

        /// <summary>Rounded card with a lighter top band; returns the card root.</summary>
        public static RectTransform Card(Transform parent, Vector2 size, Color body, int radius = 40, string name = "card")
        {
            var root = Rect(parent, name);
            root.sizeDelta = size;
            var shadow = Img(root, Sprites.Round(radius), new Color(0, 0, 0, 0.28f), "shadow");
            Stretch(shadow.rectTransform); shadow.rectTransform.offsetMin = new Vector2(-4, -22); shadow.rectTransform.offsetMax = new Vector2(4, -10);
            var bg = Img(root, Sprites.Glossy(28), body, "bg");
            Stretch(bg.rectTransform);
            return root;
        }

        public static void FitCanvas(Canvas canvas)
        {
            var sc = canvas.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 1920);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = Screen.height / (float)Screen.width < 1.7778f ? 1f : 0f;   // always keeps a 1080x1920 safe box
        }

        public static void Later(float delay, Action a)
        {
            Tween.Value(0.001f, k => { }, Ease.Linear, a, delay);
        }

        public static string Num(int n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString("0.#") + "M";
            if (n >= 100000) return (n / 1000f).ToString("0") + "K";
            return n.ToString("#,0");
        }

        public static string Clock(TimeSpan t)
        {
            return ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00");
        }
    }
}

namespace BlockBloom
{
    public static class UiExt
    {
        /// <summary>Centre-anchored position keeping the current size.</summary>
        public static T Pos<T>(this T c, float x, float y) where T : Component
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            return c;
        }

        public static T PosA<T>(this T c, float ax, float ay, float x, float y) where T : Component
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            return c;
        }

        public static T Size<T>(this T c, float w, float h) where T : Component
        {
            ((RectTransform)c.transform).sizeDelta = new Vector2(w, h);
            return c;
        }
    }
}
