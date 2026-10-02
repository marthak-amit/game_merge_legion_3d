using MergeLegion.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Short floating message on its own overlay canvas. Safe to call from anywhere.</summary>
    public static class Toast
    {
        private static CanvasGroup _group;
        private static TMP_Text _label;

        public static void Show(string message, float seconds = 1.6f)
        {
            Ensure();
            _label.text = message;
            _group.alpha = 1f;
            Tween.Kill(_group);
            Tween.Value(0.35f, k => { if (_group != null) _group.alpha = 1f - k; }, Ease.Linear, null, seconds, _group);
        }

        private static void Ensure()
        {
            if (_group != null) return;
            var canvas = UIKit.CreateCanvas("[Toast]", 500);
            Object.DontDestroyOnLoad(canvas.gameObject);
            var bg = UIKit.PanelImage(canvas.transform, new Color(0f, 0f, 0f, 0.8f), "Toast");
            UIKit.Place(bg.rectTransform, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(860, 110));
            bg.raycastTarget = false;
            _label = UIKit.Label(bg.transform, "", 40, Color.white);
            UIKit.Stretch(_label.rectTransform, 16, 8, 16, 8);
            _group = bg.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
    }
}
