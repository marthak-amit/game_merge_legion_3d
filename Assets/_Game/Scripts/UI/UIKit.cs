using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Code-built uGUI helpers. Screens are constructed at runtime so there are no scene references to wire by hand.</summary>
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.07f, 0.09f, 0.16f, 1f);
        public static readonly Color Panel = new Color(0.12f, 0.15f, 0.25f, 0.96f);
        public static readonly Color PanelLight = new Color(0.18f, 0.22f, 0.35f, 1f);
        public static readonly Color Accent = new Color(0.97f, 0.64f, 0.12f, 1f);
        public static readonly Color Good = new Color(0.25f, 0.75f, 0.35f, 1f);
        public static readonly Color Bad = new Color(0.86f, 0.28f, 0.28f, 1f);
        public static readonly Color Blue = new Color(0.25f, 0.5f, 0.95f, 1f);
        public static readonly Color Purple = new Color(0.6f, 0.35f, 0.9f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color Disabled = new Color(0.35f, 0.37f, 0.45f, 1f);

        private static Sprite _round;
        private static Sprite _circle;
        private static Sprite _white;

        public static Sprite Round => _round != null ? _round : (_round = MakeRounded(64, 18));
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                return _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeRounded(64, 32));

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, 0f, x + 0.5f - (size - radius));
                    float dy = Mathf.Max(radius - y - 0.5f, 0f, y + 0.5f - (size - radius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        // ---------- rect helpers ----------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchors to a point (0..1) with pivot at the same point.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // ---------- widgets ----------

        public static Image PanelImage(Transform parent, Color color, string name = "Panel")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Image Icon(Transform parent, Color color, Vector2 size, bool circle = true, string name = "Icon")
        {
            var img = PanelImage(parent, color, name);
            img.sprite = circle ? Circle : Round;
            img.rectTransform.sizeDelta = size;
            img.raycastTarget = false;
            return img;
        }

        public static TMP_Text Label(Transform parent, string text, float size = 40f, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect("Label", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Color.white;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string text, Color color, UnityAction onClick, Vector2 size, float fontSize = 44f)
        {
            var img = PanelImage(parent, color, "Button");
            img.rectTransform.sizeDelta = size;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            img.gameObject.AddComponent<PressScale>();
            var label = Label(img.transform, text, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform, 8, 4, 8, 4);
            label.name = "Text";
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.transform.Find("Text");
            if (t != null) t.GetComponent<TMP_Text>().text = text;
        }

        public static ScrollRect ScrollList(Transform parent, float spacing, out RectTransform content, float padding = 12f)
        {
            var root = Rect("Scroll", parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var img = viewport.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.001f);

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        public static RectTransform ListRow(RectTransform content, float height, Color? color = null)
        {
            var img = PanelImage(content, color ?? Panel, "Row");
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return img.rectTransform;
        }

        public static Image ProgressBar(Transform parent, Color fill, Vector2 size, out Image fillImage)
        {
            var back = PanelImage(parent, new Color(0, 0, 0, 0.45f), "Bar");
            back.rectTransform.sizeDelta = size;
            fillImage = PanelImage(back.transform, fill, "Fill");
            Stretch(fillImage.rectTransform);
            fillImage.type = Image.Type.Filled;
            fillImage.sprite = White;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;
            return back;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform SafeArea(Transform parent)
        {
            var rt = Rect("SafeArea", parent);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }
    }

    /// <summary>Slight scale-down while a button is held; cheap juice for every button.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData) => transform.localScale = new Vector3(0.95f, 0.95f, 1f);
        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;
        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
