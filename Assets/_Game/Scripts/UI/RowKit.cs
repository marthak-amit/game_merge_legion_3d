using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Positioning helpers for list rows (anchors are relative to the row, so rows adapt to any width).</summary>
    public static class RowKit
    {
        public static TMP_Text Left(RectTransform row, string text, float size, Color color, float x, float y, float width, float height = 60f,
            FontStyles style = FontStyles.Normal)
        {
            var t = UIKit.Label(row, text, size, color, TextAlignmentOptions.MidlineLeft, style);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return t;
        }

        public static TMP_Text Right(RectTransform row, string text, float size, Color color, float xFromRight, float y, float width, float height = 60f,
            FontStyles style = FontStyles.Normal)
        {
            var t = UIKit.Label(row, text, size, color, TextAlignmentOptions.MidlineRight, style);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-xFromRight, -y);
            rt.sizeDelta = new Vector2(width, height);
            return t;
        }

        public static Button RightButton(RectTransform row, string text, Color color, UnityAction onClick, Vector2 size, float fontSize,
            float xFromRight = 16f, float yFromTop = -1f)
        {
            var b = UIKit.Btn(row, text, color, onClick, size, fontSize);
            var rt = (RectTransform)b.transform;
            if (yFromTop < 0f)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-xFromRight, 0f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-xFromRight, -yFromTop);
            }
            rt.sizeDelta = size;
            return b;
        }

        public static Image Bar(RectTransform row, Color fill, float x, float y, float width, float height, out Image fillImage)
        {
            var bar = UIKit.ProgressBar(row, fill, new Vector2(width, height), out fillImage);
            var rt = bar.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return bar;
        }

        /// <summary>Battle-pass cell button: left column = free track, right column = premium track.</summary>
        public static Button Left2Button(RectTransform row, int tier, bool premium, System.Action<int> onClick)
        {
            var b = UIKit.Btn(row, "", UIKit.Disabled, () => onClick(tier), new Vector2(240, 56), 28);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(premium ? 580 : 130, -88);
            rt.sizeDelta = new Vector2(240, 56);
            return b;
        }

        public static Image Dot(RectTransform row, Color color, float x, float y, float size)
        {
            var d = UIKit.Icon(row, color, new Vector2(size, size));
            var rt = d.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            return d;
        }

        public static RectTransform ScrollArea(RectTransform parent, float spacing, float topInset, out RectTransform listContent)
        {
            var scroll = UIKit.ScrollList(parent, spacing, out listContent);
            var rt = (RectTransform)scroll.transform;
            UIKit.Stretch(rt, 0, 0, 0, topInset);
            return rt;
        }
    }
}
