using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;

namespace BlockBloom
{
    /// <summary>Particles, floating text, shake and confetti. Everything is pooled UI images, drawn on a full-screen layer above the game.</summary>
    public sealed class Fx : MonoBehaviour
    {
        private sealed class P
        {
            public RectTransform Rt; public Image Img;
            public Vector2 Pos, Vel; public float Rot, Spin, Life, Age, Gravity, Size, Drag; public Color Col; public bool Grow; public bool Active;
            public float EndScale;
        }

        private sealed class FT
        {
            public RectTransform Rt; public Text Txt; public Vector2 Pos; public float Age, Life, Rise; public Color Col; public bool Active; public float Pop;
        }

        public static Fx I { get; private set; }
        private RectTransform _layer;
        private readonly List<P> _ps = new List<P>(256);
        private readonly List<FT> _fts = new List<FT>(16);
        private Sprite _circle, _square, _star, _glow;

        public static Fx Create(Transform canvas)
        {
            var rt = Ui.Rect(canvas, "FxLayer");
            Ui.Stretch(rt);
            var fx = rt.gameObject.AddComponent<Fx>();
            fx._layer = rt;
            fx._circle = Sprites.Circle(); fx._square = Sprites.Round(8); fx._star = Sprites.Star(); fx._glow = Sprites.Glow();
            I = fx;
            return fx;
        }

        private Vector2 Local(Vector3 world)
        {
            Vector3 l = _layer.InverseTransformPoint(world);
            return new Vector2(l.x, l.y);
        }

        private P Get()
        {
            for (int i = 0; i < _ps.Count; i++) if (!_ps[i].Active) return _ps[i];
            if (_ps.Count >= 400) return null;
            var p = new P();
            p.Rt = Ui.Rect(_layer, "p"); p.Img = p.Rt.gameObject.AddComponent<Image>(); p.Img.raycastTarget = false;
            _ps.Add(p);
            return p;
        }

        private void Spawn(Sprite sp, Vector2 pos, Vector2 vel, Color col, float size, float life, float gravity, float spin, bool grow = false, float drag = 0.9f, float endScale = 0f)
        {
            var p = Get(); if (p == null) return;
            p.Active = true; p.Img.sprite = sp; p.Img.color = col; p.Col = col;
            p.Pos = pos; p.Vel = vel; p.Size = size; p.Life = life; p.Age = 0; p.Gravity = gravity; p.Spin = spin; p.Rot = Random.Range(0, 360f);
            p.Grow = grow; p.Drag = drag; p.EndScale = endScale;
            p.Rt.gameObject.SetActive(true);
            p.Rt.sizeDelta = new Vector2(size, size);
            p.Rt.localPosition = pos;
            p.Rt.localRotation = Quaternion.Euler(0, 0, p.Rot);
            p.Rt.localScale = Vector3.one;
        }

        /// <summary>Cheerful burst of chunky bits from a world position.</summary>
        public void Burst(Vector3 world, Color col, int count, float speed = 520f, float size = 26f)
        {
            Vector2 c = Local(world);
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0, Mathf.PI * 2f), s = Random.Range(0.35f, 1f) * speed;
                Color k = Color.Lerp(col, Color.white, Random.Range(0f, 0.45f));
                Spawn(Random.value < 0.35f ? _star : (Random.value < 0.5f ? _square : _circle), c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, k,
                    Random.Range(0.55f, 1.1f) * size, Random.Range(0.45f, 0.85f), -900f, Random.Range(-420f, 420f));
            }
        }

        public void Ring(Vector3 world, Color col, float size = 260f)
        {
            Spawn(_glow, Local(world), Vector2.zero, Palette.Alpha(col, 0.75f), 60f, 0.45f, 0, 0, true, 1f, size / 60f);
        }

        public void Flash(Vector3 world, Color col, float size)
        {
            Spawn(_glow, Local(world), Vector2.zero, Palette.Alpha(col, 0.9f), size * 0.5f, 0.3f, 0, 0, true, 1f, 1.6f);
        }

        public void Sparkle(Vector3 world, Color col)
        {
            Vector2 c = Local(world) + Random.insideUnitCircle * 30f;
            Spawn(_star, c, new Vector2(Random.Range(-40f, 40f), Random.Range(20f, 90f)), col, Random.Range(20f, 36f), Random.Range(0.5f, 0.9f), 0, Random.Range(-200f, 200f), false, 0.96f);
        }

        public void Confetti(int count)
        {
            float w = _layer.rect.width, h = _layer.rect.height;
            for (int i = 0; i < count; i++)
            {
                Color col = Palette.Block(Random.Range(0, 7));
                Vector2 pos = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), h * 0.5f + Random.Range(0, 200f));
                Spawn(Random.value < 0.5f ? _square : _circle, pos, new Vector2(Random.Range(-160f, 160f), Random.Range(-900f, -300f)), col,
                    Random.Range(14f, 28f), Random.Range(1.6f, 2.8f), -200f, Random.Range(-360f, 360f), false, 0.995f);
            }
        }

        public void Float(Vector3 world, string text, int size, Color col, float rise = 140f, float life = 0.9f)
        {
            FT f = null;
            for (int i = 0; i < _fts.Count; i++) if (!_fts[i].Active) { f = _fts[i]; break; }
            if (f == null)
            {
                f = new FT();
                f.Txt = Ui.Label(_layer, "", size, col, TextAnchor.MiddleCenter, true, "ft");
                f.Rt = f.Txt.rectTransform; f.Rt.sizeDelta = new Vector2(900, 200);
                _fts.Add(f);
            }
            f.Active = true; f.Txt.text = text; f.Txt.fontSize = size; f.Txt.color = col; f.Col = col;
            f.Pos = Local(world); f.Age = 0; f.Life = life; f.Rise = rise;
            // keep inside the screen
            float hw = _layer.rect.width * 0.5f - 260f;
            f.Pos.x = Mathf.Clamp(f.Pos.x, -hw, hw);
            f.Rt.gameObject.SetActive(true);
            f.Rt.localPosition = f.Pos;
            f.Rt.localScale = Vector3.zero;
        }

        /// <summary>A sprite that arcs from one world point to another (collected gems, coins).</summary>
        public void Fly(Sprite sp, Vector3 from, Vector3 to, Color col, float size, float duration, System.Action onArrive)
        {
            var im = Ui.Img(_layer, sp, col, "fly");
            im.rectTransform.sizeDelta = new Vector2(size, size);
            Vector2 a = Local(from), b = Local(to);
            Vector2 mid = (a + b) * 0.5f + new Vector2(Random.Range(-120f, 120f), 160f);
            Tween.Value(duration, k =>
            {
                if (im == null) return;
                Vector2 p1 = Vector2.Lerp(a, mid, k), p2 = Vector2.Lerp(mid, b, k);
                im.rectTransform.localPosition = Vector2.Lerp(p1, p2, k);
                im.rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(k * Mathf.PI));
            }, Ease.Linear, () => { if (im != null) Destroy(im.gameObject); if (onArrive != null) onArrive(); }, 0f, im);
        }

        public void Shake(Transform t, float amount, float duration)
        {
            Vector3 basePos = t.localPosition;
            Tween.Value(duration, k =>
            {
                if (t == null) return;
                float fade = 1f - k;
                t.localPosition = basePos + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * amount * fade;
            }, Ease.Linear, () => { if (t != null) t.localPosition = basePos; }, 0f, t);
        }

        public void Clear()
        {
            for (int i = 0; i < _ps.Count; i++) { _ps[i].Active = false; _ps[i].Rt.gameObject.SetActive(false); }
            for (int i = 0; i < _fts.Count; i++) { _fts[i].Active = false; _fts[i].Rt.gameObject.SetActive(false); }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < _ps.Count; i++)
            {
                var p = _ps[i]; if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= p.Life) { p.Active = false; p.Rt.gameObject.SetActive(false); continue; }
                float k = p.Age / p.Life;
                p.Vel.y += p.Gravity * dt;
                p.Vel *= Mathf.Pow(p.Drag, dt * 60f);
                p.Pos += p.Vel * dt;
                p.Rot += p.Spin * dt;
                p.Rt.localPosition = p.Pos;
                p.Rt.localRotation = Quaternion.Euler(0, 0, p.Rot);
                float sc = p.Grow ? Mathf.Lerp(1f, p.EndScale, 1f - (1f - k) * (1f - k)) : (1f - k * k * 0.7f);
                p.Rt.localScale = Vector3.one * sc;
                var c = p.Col; c.a = p.Col.a * (k < 0.6f ? 1f : (1f - k) / 0.4f);
                p.Img.color = c;
            }
            for (int i = 0; i < _fts.Count; i++)
            {
                var f = _fts[i]; if (!f.Active) continue;
                f.Age += dt;
                if (f.Age >= f.Life) { f.Active = false; f.Rt.gameObject.SetActive(false); continue; }
                float k = f.Age / f.Life;
                float pop = k < 0.15f ? Tween.Evaluate(Ease.OutBack, k / 0.15f) : 1f;
                f.Rt.localScale = Vector3.one * pop * (k > 0.75f ? 1f - (k - 0.75f) / 0.25f * 0.2f : 1f);
                f.Rt.localPosition = f.Pos + new Vector2(0, f.Rise * (1f - (1f - k) * (1f - k)));
                var c = f.Col; c.a = k < 0.7f ? 1f : (1f - k) / 0.3f;
                f.Txt.color = c;
            }
        }
    }
}
