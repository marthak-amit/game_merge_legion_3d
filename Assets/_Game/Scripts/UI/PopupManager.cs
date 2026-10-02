using System;
using System.Collections.Generic;
using MergeLegion.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    public sealed class Popup
    {
        public GameObject Root;
        public RectTransform Panel;
        public RectTransform Content;
        public Action Closed;

        public void Close() => PopupManager.Close(this);
    }

    /// <summary>
    /// Modal popup queue on its own overlay canvas: popups show one at a time, in the order they were requested
    /// (or at the front for urgent ones).
    /// </summary>
    public static class PopupManager
    {
        private static readonly LinkedList<Func<Popup>> Queue = new LinkedList<Func<Popup>>();
        private static Popup _current;
        private static Canvas _canvas;

        public static bool IsShowing => _current != null;
        public static int Pending => Queue.Count;

        public static Transform Root
        {
            get
            {
                if (_canvas == null)
                {
                    _canvas = UIKit.CreateCanvas("[Popups]", 400);
                    UnityEngine.Object.DontDestroyOnLoad(_canvas.gameObject);
                    UIKit.EnsureEventSystem();
                }
                return _canvas.transform;
            }
        }

        /// <summary>Queue a popup. The builder runs only when it is this popup's turn.</summary>
        public static void Show(Func<Popup> builder, bool front = false)
        {
            if (front) Queue.AddFirst(builder); else Queue.AddLast(builder);
            if (_current == null) Next();
        }

        public static void Close(Popup popup)
        {
            if (popup == null || popup != _current) return;
            _current = null;
            if (popup.Root != null) UnityEngine.Object.Destroy(popup.Root);
            popup.Closed?.Invoke();
            Next();
        }

        public static void CloseAll()
        {
            Queue.Clear();
            if (_current != null)
            {
                var p = _current;
                _current = null;
                if (p.Root != null) UnityEngine.Object.Destroy(p.Root);
            }
        }

        private static void Next()
        {
            if (Queue.Count == 0) return;
            var builder = Queue.First.Value;
            Queue.RemoveFirst();
            _current = builder();
        }

        /// <summary>Standard dimmed modal with a title bar. Close with popup.Close().</summary>
        public static Popup Create(string title, Vector2 size, bool dismissOnBackdrop = false)
        {
            var popup = new Popup();
            var root = UIKit.Rect("Popup", Root);
            UIKit.Stretch(root);
            popup.Root = root.gameObject;

            var dim = UIKit.PanelImage(root, new Color(0f, 0f, 0f, 0.72f), "Dim");
            dim.sprite = null;
            UIKit.Stretch(dim.rectTransform);
            if (dismissOnBackdrop)
            {
                var b = dim.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(popup.Close);
            }

            var panel = UIKit.PanelImage(root, UIKit.Panel, "Panel");
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            popup.Panel = panel.rectTransform;

            if (!string.IsNullOrEmpty(title))
            {
                var t = UIKit.Label(panel.transform, title, 62, UIKit.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(size.x - 40, 90));
            }

            var content = UIKit.Rect("Content", panel.transform);
            UIKit.Stretch(content, 24, 24, 24, string.IsNullOrEmpty(title) ? 24 : 130);
            popup.Content = content;

            panel.rectTransform.localScale = Vector3.one * 0.8f;
            Tween.ScaleTo(panel.rectTransform, Vector3.one, 0.22f, Ease.OutBack);
            return popup;
        }
    }
}
