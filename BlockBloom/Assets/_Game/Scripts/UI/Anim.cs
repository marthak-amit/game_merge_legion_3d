using System;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;

namespace BlockBloom
{
    /// <summary>Reusable UI motion: pop-ins, slides, idle loops, count-ups, button shine.</summary>
    public static class Anim
    {
        public static void PopIn(Transform t, float delay = 0f, float from = 0f, float duration = 0.38f)
        {
            if (t == null) return;
            t.localScale = Vector3.one * from;
            Tween.Value(duration, k => { if (t != null) t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, k); }, Ease.OutBack, null, delay, t);
        }

        public static void SlideIn(RectTransform rt, Vector2 offset, float delay = 0f, float duration = 0.4f)
        {
            if (rt == null) return;
            Vector2 target = rt.anchoredPosition;
            rt.anchoredPosition = target + offset;
            Tween.Value(duration, k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(target + offset, target, k); }, Ease.OutCubic, null, delay, rt);
        }

        /// <summary>Gentle endless bob (position) - alive-feeling idle.</summary>
        public static void Bob(RectTransform rt, float amplitude = 8f, float speed = 2f, float phase = 0f)
        {
            if (rt == null) return;
            var b = rt.gameObject.AddComponent<BobFx>();
            b.Amp = amplitude; b.Speed = speed; b.Phase = phase; b.Base = rt.anchoredPosition;
        }

        /// <summary>Slow scale breathing for call-to-action buttons.</summary>
        public static void Breathe(Transform t, float amount = 0.035f, float period = 1.5f)
        {
            if (t == null) return;
            Tween.Value(period, k => { if (t != null) t.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI * 2f)); }, Ease.Linear,
                () => { if (t != null) Breathe(t, amount, period); }, 0f, t);
        }

        public static void Wiggle(Transform t, float degrees = 8f, float duration = 0.5f)
        {
            if (t == null) return;
            Tween.Value(duration, k => { if (t != null) t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 4f) * degrees * (1f - k)); },
                Ease.Linear, () => { if (t != null) t.localRotation = Quaternion.identity; }, 0f, t);
        }

        public static void Shake(Transform t, float amount = 12f, float duration = 0.4f)
        {
            if (t == null) return;
            Vector3 b = t.localPosition;
            Tween.Value(duration, k => { if (t != null) t.localPosition = b + new Vector3(Mathf.Sin(k * 60f) * amount * (1f - k), 0, 0); },
                Ease.Linear, () => { if (t != null) t.localPosition = b; }, 0f, t);
        }

        /// <summary>Counts a text from a to b with an ease-out.</summary>
        public static void CountUp(Text t, int from, int to, float duration, Func<int, string> fmt = null, float delay = 0f)
        {
            if (t == null) return;
            Tween.Value(duration, k => { if (t != null) { int v = Mathf.RoundToInt(Mathf.Lerp(from, to, k)); t.text = fmt != null ? fmt(v) : v.ToString(); } },
                Ease.OutCubic, () => { if (t != null) t.text = fmt != null ? fmt(to) : to.ToString(); }, delay, t);
        }

        /// <summary>Staggered pop-in of the direct children of a card (skips chrome such as the background and close button).</summary>
        public static void StaggerChildren(RectTransform card, float start = 0.12f, float step = 0.045f, float maxTotal = 0.5f)
        {
            int n = 0;
            for (int i = 0; i < card.childCount; i++)
            {
                var c = card.GetChild(i);
                string nm = c.name;
                if (nm == "shadow" || nm == "bg" || nm == "well" || nm == "ribbon" || nm == "iconbtn" || nm == "dim") continue;
                if (c.GetComponent<NoStagger>() != null) continue;
                float d = Mathf.Min(maxTotal, start + n * step);
                Vector3 s = c.localScale;
                c.localScale = s * 0.6f;
                Transform ct = c;
                Tween.Value(0.34f, k => { if (ct != null) ct.localScale = Vector3.LerpUnclamped(s * 0.6f, s, k); }, Ease.OutBack, null, d, ct);
                n++;
            }
        }

        /// <summary>A soft light band sweeping across a button face every few seconds.</summary>
        public static void AddShine(Button b, float interval = 3.2f)
        {
            var face = b.transform.Find("face");
            if (face == null) return;
            var faceImg = face.GetComponent<Image>();
            var mask = face.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var shine = Ui.Img(face, Sprites.Glow(), new Color(1, 1, 1, 0.55f), "shine");
            var rt = shine.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150, ((RectTransform)b.transform).sizeDelta.y * 2.2f);
            rt.localRotation = Quaternion.Euler(0, 0, 18);
            float w = ((RectTransform)b.transform).sizeDelta.x;
            Action loop = null;
            loop = () =>
            {
                if (rt == null) return;
                Tween.Value(0.7f, k => { if (rt != null) rt.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.7f, w * 0.7f, k), 0); },
                    Ease.InOutSine, () => Ui.Later(interval, () => { if (loop != null) loop(); }), 0f, rt);
            };
            rt.anchoredPosition = new Vector2(-w, 0);
            Ui.Later(UnityEngine.Random.Range(0.4f, 1.5f), () => { if (loop != null) loop(); });
        }

        /// <summary>Screen entrance: gentle zoom + fade.</summary>
        public static void ScreenIn(RectTransform rt)
        {
            if (rt == null) return;
            var cg = rt.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; rt.localScale = Vector3.one * 1.04f;
            Tween.Value(0.34f, k =>
            {
                if (rt == null) return;
                cg.alpha = Mathf.Clamp01(k * 1.6f);
                rt.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, k);
            }, Ease.OutCubic, () => { if (rt != null) { cg.alpha = 1f; rt.localScale = Vector3.one; } }, 0f, rt);
        }

        public static void Flash(Graphic g, Color to, float duration = 0.25f)
        {
            if (g == null) return;
            Color from = g.color;
            Tween.Value(duration, k => { if (g != null) g.color = Color.Lerp(to, from, k); }, Ease.Linear, () => { if (g != null) g.color = from; }, 0f, g);
        }
    }

    public sealed class NoStagger : MonoBehaviour { }

    public sealed class BobFx : MonoBehaviour
    {
        public float Amp, Speed, Phase; public Vector2 Base;
        private RectTransform _rt;
        private void Awake() { _rt = (RectTransform)transform; }
        private void Update() { if (_rt != null) _rt.anchoredPosition = Base + new Vector2(0, Mathf.Sin(Time.unscaledTime * Speed + Phase) * Amp); }
    }
}
