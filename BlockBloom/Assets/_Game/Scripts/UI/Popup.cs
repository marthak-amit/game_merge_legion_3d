using System;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;

namespace BlockBloom
{
    /// <summary>Modal card with a dimmed backdrop, ribbon title and a springy entrance.</summary>
    public sealed class Popup : MonoBehaviour
    {
        public RectTransform Card;
        public Action OnClosed;
        private Image _dim;
        private bool _closing;
        public bool Closable = true;

        public static Popup Create(Vector2 size, string title, bool closable = true, Color? ribbon = null)
        {
            var app = App.I;
            var root = Ui.Rect(app.PopupLayer, "Popup");
            Ui.Stretch(root);
            var p = root.gameObject.AddComponent<Popup>();
            p.Closable = closable;
            p._dim = Ui.Img(root, Sprites.Square(), new Color(0.03f, 0.01f, 0.12f, 0f), "dim", true);
            Ui.Stretch(p._dim.rectTransform);
            // widen the dim beyond the safe area so notches are covered
            p._dim.rectTransform.offsetMin = new Vector2(-400, -400); p._dim.rectTransform.offsetMax = new Vector2(400, 400);
            var th = Palette.Themes[Mathf.Clamp(Save.Data.theme, 0, Palette.Themes.Length - 1)];
            p.Card = Ui.Card(root, size, th.Panel, 44, "card");
            p.Card.anchorMin = p.Card.anchorMax = new Vector2(0.5f, 0.5f);
            p.Card.anchoredPosition = Vector2.zero;
            // inner darker well
            var inner = Ui.Img(p.Card, Sprites.Round(36), Palette.Alpha(th.PanelDark, 0.55f), "well");
            Ui.Stretch(inner.rectTransform, 26, 26, 26, 70);

            if (!string.IsNullOrEmpty(title))
            {
                var rb = Ui.Rect(p.Card, "ribbon");
                Ui.At(rb, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(Mathf.Min(size.x - 60, 700), 112));
                var lip = Ui.Img(rb, Sprites.Glossy(28), Palette.Dark(ribbon ?? Palette.Gold, 0.65f), "lip");
                Ui.Stretch(lip.rectTransform); lip.rectTransform.offsetMin = new Vector2(0, -10); lip.rectTransform.offsetMax = new Vector2(0, -10);
                var face = Ui.Img(rb, Sprites.Glossy(28), ribbon ?? Palette.Gold, "face");
                Ui.Stretch(face.rectTransform);
                var t = Ui.Label(rb, title, 54, Color.white, TextAnchor.MiddleCenter);
                Ui.Stretch(t.rectTransform, 20, 0, 20, 8); t.resizeTextForBestFit = true; t.resizeTextMinSize = 24; t.resizeTextMaxSize = 54;
            }
            if (closable)
            {
                var x = Ui.IconBtn(p.Card, Sprites.Cross(), Palette.Red, Palette.RedDark, 92, () => p.Close());
                x.transform.SetParent(p.Card, false);
                Ui.At((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(-30, -30), new Vector2(92, 92));
            }

            app.Popups.Add(p);
            p.Card.localScale = Vector3.one * 0.6f;
            Tween.Value(0.22f, k => { if (p != null) { p._dim.color = new Color(0.03f, 0.01f, 0.12f, 0.72f * Mathf.Clamp01(k)); } }, Ease.Linear, null, 0f, p._dim);
            Tween.Value(0.42f, k =>
            {
                if (p == null) return;
                p.Card.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, k);
                p.Card.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-120f, 0f, k));
            }, Ease.OutBack, null, 0f, p.Card);
            Sfx.Pop();
            return p;
        }

        private void Start() { Anim.StaggerChildren(Card); }

        public void Close(bool instant = false)
        {
            if (_closing) return;
            _closing = true;
            if (App.I != null) App.I.Popups.Remove(this);
            var cb = OnClosed;
            if (instant) { Destroy(gameObject); if (cb != null) cb(); return; }
            Tween.Kill(Card);
            Sfx.Tick();
            Tween.Value(0.16f, k =>
            {
                if (this == null) return;
                Card.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, k);
                _dim.color = new Color(0.03f, 0.01f, 0.12f, 0.72f * (1f - k));
            }, Ease.Linear, () => { if (this != null) Destroy(gameObject); if (cb != null) cb(); }, 0f, _dim);
        }
    }
}
