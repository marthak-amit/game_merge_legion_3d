using MergeLegion.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>
    /// Base for menu screens: dark background, safe area, optional title bar with back button, and a content rect that
    /// leaves room for the persistent currency bar (top) and tab bar + banner strip (bottom).
    /// </summary>
    public abstract class MenuScreen : UIScreen
    {
        public const float TopBarHeight = 120f;
        public const float TitleBarHeight = 110f;
        public const float BottomChromeHeight = 270f;

        protected RectTransform Safe { get; private set; }
        protected RectTransform Content { get; private set; }
        private float _timer;

        /// <summary>Localization key for the title bar; null = no title bar (root tab screens).</summary>
        protected virtual string TitleKey => null;
        protected virtual float RefreshSeconds => 0.5f;

        protected sealed override void Build()
        {
            var bg = UIKit.PanelImage(transform, UIKit.Bg, "Background");
            bg.sprite = null;
            UIKit.Stretch(bg.rectTransform);
            Safe = UIKit.SafeArea(transform);

            float top = TopBarHeight;
            if (TitleKey != null)
            {
                var title = UIKit.Label(Safe, Loc.Get(TitleKey), 64, UIKit.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -TopBarHeight - 10), new Vector2(760, TitleBarHeight));
                var back = UIKit.Btn(Safe, "<", UIKit.PanelLight, () => UIManager.Instance.Pop(), new Vector2(110, 90), 56);
                UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20, -TopBarHeight - 10), new Vector2(110, 90));
                top += TitleBarHeight;
            }

            Content = UIKit.Rect("Content", Safe);
            UIKit.Stretch(Content, 0, BottomChromeHeight, 0, top);
            BuildContent(Content);
        }

        protected abstract void BuildContent(RectTransform content);
        protected abstract void Refresh();

        protected override void OnShown()
        {
            _timer = 0f;
            Refresh();
        }

        private void Update()
        {
            if (!IsVisible) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer < RefreshSeconds) return;
            _timer = 0f;
            Refresh();
        }

        protected static RectTransform Section(RectTransform parent, string text, float y, float width = 1040f)
        {
            var label = UIKit.Label(parent, text, 44, UIKit.Accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(width, 70));
            return label.rectTransform;
        }
    }
}
