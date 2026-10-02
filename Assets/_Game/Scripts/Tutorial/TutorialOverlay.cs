using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Grid;
using MergeLegion.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeLegion.Tutorial
{
    /// <summary>
    /// Draws the FTUE: darkens everything but the target, points a bouncing hand at it (or drags it from one unit to
    /// another for the merge step) and shows a speech bubble. Persists across scenes; driven by <see cref="TutorialService"/>.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _root;
        private Image _top, _bottom, _left, _right;
        private RectTransform _hand;
        private RectTransform _bubble;
        private TMP_Text _bubbleText;
        private string _lastStep;
        private float _time;

        public static TutorialOverlay Create()
        {
            var go = new GameObject("[TutorialOverlay]");
            DontDestroyOnLoad(go);
            var overlay = go.AddComponent<TutorialOverlay>();
            overlay.Build();
            return overlay;
        }

        private void Build()
        {
            _canvas = UIKit.CreateCanvas("TutorialCanvas", 300);
            _canvas.transform.SetParent(transform, false);
            _root = UIKit.Rect("Root", _canvas.transform);
            UIKit.Stretch(_root);

            _top = Dim("Top");
            _bottom = Dim("Bottom");
            _left = Dim("Left");
            _right = Dim("Right");

            var hand = UIKit.Icon(_root, UIKit.Accent, new Vector2(110, 110), true, "Hand");
            var ring = UIKit.Icon(hand.transform, new Color(1f, 1f, 1f, 0.5f), new Vector2(150, 150), true, "Ring");
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            ring.transform.SetAsFirstSibling();
            _hand = hand.rectTransform;

            var bubble = UIKit.PanelImage(_root, new Color(0.98f, 0.96f, 0.88f, 0.98f), "Bubble");
            bubble.raycastTarget = false;
            _bubble = bubble.rectTransform;
            _bubble.sizeDelta = new Vector2(900, 190);
            _bubbleText = UIKit.Label(bubble.transform, "", 46, new Color(0.15f, 0.12f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(_bubbleText.rectTransform, 24, 12, 24, 12);
            Hide();
        }

        private Image Dim(string name)
        {
            var img = UIKit.PanelImage(_root, new Color(0f, 0f, 0f, 0.78f), name);
            img.sprite = null;
            return img;
        }

        private void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<TutorialService>(out var tut) || !tut.IsActive || PopupManager.IsShowing) { Hide(); return; }

            string sceneName = SceneManager.GetActiveScene().name;
            string scene = sceneName == SceneNames.Battle ? "battle" : sceneName == SceneNames.Main ? "home" : "";
            if (scene == "") { Hide(); return; }
            string phase = "";
            if (scene == "battle")
            {
                var director = BattleSceneRoot.Director;
                if (director == null) { Hide(); return; }
                phase = director.Phase == BattlePhase.Fighting ? "fight" : director.Phase == BattlePhase.Prepare ? "prepare" : "result";
                if (phase == "result") { Hide(); return; }
            }

            var step = tut.Current(scene, phase);
            if (step == null) { Hide(); return; }

            var cam = Camera.main;
            Rect target, second = default(Rect);
            bool hasSecond = false;
            if (step.target == "merge")
            {
                if (!TutorialTargets.TryGetScreenRect("merge_a", cam, out target) || !TutorialTargets.TryGetScreenRect("merge_b", cam, out second)) { Hide(); return; }
                hasSecond = true;
            }
            else if (!TutorialTargets.TryGetScreenRect(step.target, cam, out target)) { Hide(); return; }

            _root.gameObject.SetActive(true);
            if (_lastStep != step.id)
            {
                _lastStep = step.id;
                _bubbleText.text = Loc.Get(step.textKey);
                Sfx(step);
            }
            _time += Time.unscaledDeltaTime;

            Layout(target, step.dim);
            AnimateHand(target, second, hasSecond);
            PlaceBubble(hasSecond ? Rect.MinMaxRect(Mathf.Min(target.xMin, second.xMin), Mathf.Min(target.yMin, second.yMin),
                Mathf.Max(target.xMax, second.xMax), Mathf.Max(target.yMax, second.yMax)) : target);
        }

        private static void Sfx(TutorialStepDef step) => Audio.Sfx.Play(Audio.SfxId.Whoosh, 0.5f);

        private void Layout(Rect target, bool dim)
        {
            _top.gameObject.SetActive(dim);
            _bottom.gameObject.SetActive(dim);
            _left.gameObject.SetActive(dim);
            _right.gameObject.SetActive(dim);
            if (!dim) return;

            float pad = 14f;
            float w = Screen.width, h = Screen.height;
            float x0 = Mathf.Clamp01((target.xMin - pad) / w), x1 = Mathf.Clamp01((target.xMax + pad) / w);
            float y0 = Mathf.Clamp01((target.yMin - pad) / h), y1 = Mathf.Clamp01((target.yMax + pad) / h);
            Fill(_top, 0f, y1, 1f, 1f);
            Fill(_bottom, 0f, 0f, 1f, y0);
            Fill(_left, 0f, y0, x0, y1);
            Fill(_right, x1, y0, 1f, y1);
        }

        private static void Fill(Image img, float x0, float y0, float x1, float y1)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void AnimateHand(Rect a, Rect b, bool drag)
        {
            Vector2 from = a.center;
            Vector2 to = b.center;
            Vector2 pos;
            if (drag)
            {
                float t = Mathf.Repeat(_time / 1.4f, 1f);
                float k = Mathf.Clamp01((t - 0.15f) / 0.7f);
                pos = Vector2.Lerp(from, to, k * k * (3f - 2f * k));
            }
            else
            {
                pos = from + new Vector2(0f, -a.height * 0.15f + Mathf.Sin(_time * 8f) * 14f);
            }
            _hand.anchorMin = _hand.anchorMax = new Vector2(pos.x / Screen.width, pos.y / Screen.height);
            _hand.anchoredPosition = Vector2.zero;
            float s = 1f + 0.08f * Mathf.Sin(_time * 8f);
            _hand.localScale = new Vector3(s, s, 1f);
        }

        private void PlaceBubble(Rect around)
        {
            bool below = around.center.y > Screen.height * 0.5f;
            float ny = below ? Mathf.Clamp01((around.yMin - 60f) / Screen.height) : Mathf.Clamp01((around.yMax + 60f) / Screen.height);
            _bubble.pivot = new Vector2(0.5f, below ? 1f : 0f);
            _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, ny);
            _bubble.anchoredPosition = Vector2.zero;
        }
    }
}
