using System.Collections.Generic;
using UnityEngine;

namespace BlockBloom
{
    /// <summary>Every image in the game is generated here at runtime (no art files to ship, perfectly crisp at any resolution).</summary>
    public static class Sprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Sprite Make(string key, int w, int h, System.Func<float, float, Color> px, Vector4 border = default(Vector4), int supersample = 2)
        {
            Sprite s;
            if (Cache.TryGetValue(key, out s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = new Color(0, 0, 0, 0);
                    for (int sy = 0; sy < supersample; sy++)
                        for (int sx = 0; sx < supersample; sx++)
                        {
                            float u = (x + (sx + 0.5f) / supersample), v = (y + (sy + 0.5f) / supersample);
                            Color c = px(u, v);
                            acc.r += c.r * c.a; acc.g += c.g * c.a; acc.b += c.b * c.a; acc.a += c.a;
                        }
                    float n = supersample * supersample;
                    if (acc.a > 0.0001f) cols[y * w + x] = new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a / n);
                    else cols[y * w + x] = new Color(1, 1, 1, 0);
                }
            tex.SetPixels(cols);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            Cache[key] = s;
            return s;
        }

        // signed distance to a rounded box centred in (w,h)
        private static float RoundBox(float x, float y, float w, float h, float r)
        {
            float px = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float py = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(px, 0), oy = Mathf.Max(py, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(px, py), 0) - r;
        }

        private static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

        /// <summary>Flat white rounded rectangle (9-sliced) - tint it with Image.color.</summary>
        public static Sprite Round(int radius = 24)
        {
            int size = radius * 2 + 4;
            return Make("round" + radius, size, size, (x, y) => new Color(1, 1, 1, Cov(RoundBox(x, y, size, size, radius))),
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }

        /// <summary>Chunky button / panel face: soft top light, darker bottom edge. Tint with the main colour.</summary>
        public static Sprite Glossy(int radius = 28)
        {
            int size = radius * 2 + 8;
            return Make("glossy" + radius, size, size, (x, y) =>
            {
                float d = RoundBox(x, y, size, size, radius);
                float a = Cov(d);
                float t = y / size;                                   // 0 bottom .. 1 top
                float shade = Mathf.Lerp(0.78f, 1.05f, Mathf.SmoothStep(0, 1, t));
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d + 3f) / 3f) * 0.12f * t;
                float top = Mathf.Clamp01((t - 0.55f) * 2f) * 0.10f;
                float v = Mathf.Clamp01(shade + rim + top);
                return new Color(v, v, v, a);
            }, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        /// <summary>Jelly block: tinted body, bright bevel on top/left, shadow bottom/right, gloss blob.</summary>
        public static Sprite Block()
        {
            const int S = 96; const float R = 22f;
            return Make("block", S, S, (x, y) =>
            {
                float d = RoundBox(x, y, S, S, R);
                float a = Cov(d);
                if (a <= 0) return new Color(1, 1, 1, 0);
                float t = y / S, l = x / S;
                float body = Mathf.Lerp(0.74f, 1.0f, t * 0.8f + (1f - l) * 0.2f);
                // bevel: inner ring
                float inner = RoundBox(x - 0.0f, y - 0.0f, S, S, R) + 7f;           // distance inside
                float bevelBand = Mathf.Clamp01(1f - Mathf.Abs(inner) / 5f);
                float lightSide = Mathf.Clamp01((t - 0.5f) * 1.2f + (1f - l - 0.5f) * 0.6f);
                body += bevelBand * (lightSide - 0.35f) * 0.45f;
                // inner flat face is slightly lighter
                if (inner < -1f) body += 0.05f;
                // gloss blob at top-left
                float gx = (x - S * 0.30f) / (S * 0.30f), gy = (y - S * 0.76f) / (S * 0.14f);
                float gloss = Mathf.Clamp01(1f - (gx * gx + gy * gy)) * 0.55f;
                float v = Mathf.Clamp01(body + gloss);
                float tintK = Mathf.Clamp01(gloss * 0.8f);
                return new Color(Mathf.Lerp(v, 1f, tintK), Mathf.Lerp(v, 1f, tintK), Mathf.Lerp(v, 1f, tintK), a);
            });
        }

        /// <summary>Empty board cell: slightly inset rounded square.</summary>
        public static Sprite Cell()
        {
            const int S = 64; const float R = 14f;
            return Make("cell", S, S, (x, y) =>
            {
                float d = RoundBox(x, y, S, S, R);
                float a = Cov(d);
                float t = y / S;
                float v = Mathf.Lerp(0.88f, 1.0f, t);        // inner shadow at the bottom edge looks recessed
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d + 2f) / 2.5f);
                v -= edge * 0.06f * (1f - t);
                return new Color(v, v, v, a);
            });
        }

        public static Sprite Circle()
        {
            const int S = 64;
            return Make("circle", S, S, (x, y) => new Color(1, 1, 1, Cov(Mathf.Sqrt((x - S / 2f) * (x - S / 2f) + (y - S / 2f) * (y - S / 2f)) - S / 2f + 1f)));
        }

        public static Sprite Glow()
        {
            const int S = 64;
            return Make("glow", S, S, (x, y) =>
            {
                float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d); a = a * a * (3 - 2 * a);
                return new Color(1, 1, 1, a);
            }, default(Vector4), 1);
        }

        /// <summary>Alpha ramp: transparent at the bottom, opaque at the top (tint with a colour to fake a gradient).</summary>
        public static Sprite Fade()
        {
            return Make("fade", 2, 128, (x, y) => new Color(1, 1, 1, y / 128f), default(Vector4), 1);
        }

        public static Sprite Square() { return Make("square", 4, 4, (x, y) => Color.white, default(Vector4), 1); }

        /// <summary>Vertical gradient (top colour at the top). Sampled via vertex colour so it is just a white square with a tint ramp.</summary>
        public static Sprite Vertical()
        {
            return Make("vertical", 2, 64, (x, y) => new Color(1, 1, 1, 1), default(Vector4), 1);
        }

        private static bool InPoly(float x, float y, Vector2[] p)
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if (((p[i].y > y) != (p[j].y > y)) && (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)) c = !c;
            return c;
        }

        public static Sprite Star()
        {
            const int S = 96;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2 + i * Mathf.PI / 5;
                float rad = (i % 2 == 0) ? 44f : 20f;
                pts[i] = new Vector2(S / 2f + Mathf.Cos(ang) * rad, S / 2f - 2f + Mathf.Sin(ang) * rad);
            }
            return Make("star", S, S, (x, y) =>
            {
                if (!InPoly(x, y, pts)) return new Color(1, 1, 1, 0);
                float t = y / S;
                float v = Mathf.Lerp(0.82f, 1f, t);
                return new Color(v, v, v, 1);
            });
        }

        public static Sprite Heart()
        {
            const int S = 96;
            return Make("heart", S, S, (x, y) =>
            {
                float nx = (x - S / 2f) / (S * 0.42f), ny = (y - S * 0.46f) / (S * 0.42f);
                float f = Mathf.Pow(nx * nx + ny * ny - 1f, 3) - nx * nx * ny * ny * ny;
                if (f > 0) return new Color(1, 1, 1, 0);
                float t = y / S;
                float v = Mathf.Lerp(0.8f, 1f, t);
                float gx = (x - S * 0.33f) / (S * 0.12f), gy = (y - S * 0.66f) / (S * 0.09f);
                v = Mathf.Clamp01(v + Mathf.Clamp01(1f - gx * gx - gy * gy) * 0.5f);
                return new Color(v, v, v, 1);
            });
        }

        public static Sprite Diamond()
        {
            const int S = 96;
            var pts = new[] { new Vector2(S / 2f, S - 6), new Vector2(S - 12, S * 0.62f), new Vector2(S / 2f, 6), new Vector2(12, S * 0.62f) };
            return Make("diamond", S, S, (x, y) =>
            {
                if (!InPoly(x, y, pts)) return new Color(1, 1, 1, 0);
                float v = (x < S / 2f) ? 1f : 0.82f;
                if (y > S * 0.62f) v += 0.1f;
                return new Color(Mathf.Clamp01(v), Mathf.Clamp01(v), Mathf.Clamp01(v), 1);
            });
        }

        public static Sprite Coin()
        {
            const int S = 96;
            return Make("coin", S, S, (x, y) =>
            {
                float dx = x - S / 2f, dy = y - S / 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Cov(r - 44f);
                if (a <= 0) return new Color(1, 1, 1, 0);
                float v = r > 36f ? 0.78f : Mathf.Lerp(0.92f, 1.04f, y / S);
                // simple embossed star-ish dot in the middle
                if (r < 16f) v = 0.86f + 0.1f * (y / S);
                return new Color(Mathf.Clamp01(v), Mathf.Clamp01(v), Mathf.Clamp01(v), a);
            });
        }

        public static Sprite Lock()
        {
            const int S = 64;
            return Make("lock", S, S, (x, y) =>
            {
                // body
                float body = Cov(RoundBox(x - 14f, y - 6f, 36f, 28f, 5f));
                // shackle ring
                float dx = x - 32f, dy = y - 34f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = (y > 26f) ? Cov(Mathf.Abs(r - 12f) - 3.2f) : 0f;
                float a = Mathf.Max(body, ring);
                return new Color(1, 1, 1, a);
            });
        }

        public static Sprite Check()
        {
            const int S = 64;
            return Make("check", S, S, (x, y) =>
            {
                float d1 = DistSeg(x, y, 14, 34, 27, 20), d2 = DistSeg(x, y, 27, 20, 50, 46);
                return new Color(1, 1, 1, Cov(Mathf.Min(d1, d2) - 4.5f));
            });
        }

        public static Sprite Cross()
        {
            const int S = 64;
            return Make("cross", S, S, (x, y) =>
            {
                float d1 = DistSeg(x, y, 16, 16, 48, 48), d2 = DistSeg(x, y, 16, 48, 48, 16);
                return new Color(1, 1, 1, Cov(Mathf.Min(d1, d2) - 4.5f));
            });
        }

        public static Sprite Gear()
        {
            const int S = 96;
            return Make("gear", S, S, (x, y) =>
            {
                float dx = x - S / 2f, dy = y - S / 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float teeth = 0.5f + 0.5f * Mathf.Cos(ang * 8f);
                float outer = Mathf.Lerp(34f, 44f, Mathf.SmoothStep(0.35f, 0.65f, teeth));
                float a = Cov(r - outer) * (r > 15f ? 1f : 0f);
                if (r <= 15f) a = Cov(-(15f - r)) * 0f;
                return new Color(1, 1, 1, a);
            });
        }

        public static Sprite Play()
        {
            const int S = 64;
            var pts = new[] { new Vector2(20, 10), new Vector2(54, 32), new Vector2(20, 54) };
            return Make("play", S, S, (x, y) => new Color(1, 1, 1, InPoly(x, y, pts) ? 1f : 0f));
        }

        public static Sprite Bomb()
        {
            const int S = 96;
            return Make("bomb", S, S, (x, y) =>
            {
                float dx = x - 46f, dy = y - 38f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float body = Cov(r - 30f);
                float fuse = Cov(DistSeg(x, y, 58, 62, 70, 76) - 4f);
                float spark = Cov(Mathf.Sqrt((x - 74f) * (x - 74f) + (y - 80f) * (y - 80f)) - 7f);
                float a = Mathf.Max(body, Mathf.Max(fuse, spark));
                float v = 0.7f;
                if (body > 0.5f) { v = Mathf.Lerp(0.55f, 1.0f, (y - 8f) / 60f); float gx = (x - 36f) / 9f, gy = (y - 50f) / 6f; v += Mathf.Clamp01(1f - gx * gx - gy * gy) * 0.5f; }
                return new Color(Mathf.Clamp01(v), Mathf.Clamp01(v), Mathf.Clamp01(v), a);
            });
        }

        public static Sprite Undo()
        {
            const int S = 64;
            return Make("undo", S, S, (x, y) =>
            {
                float dx = x - 32f, dy = y - 30f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float arc = (ang > -2.2f && ang < 2.6f) ? Cov(Mathf.Abs(r - 17f) - 4f) : 0f;
                var tri = new[] { new Vector2(10, 44), new Vector2(30, 52), new Vector2(26, 28) };
                float head = InPoly(x, y, tri) ? 1f : 0f;
                return new Color(1, 1, 1, Mathf.Max(arc, head));
            });
        }

        public static Sprite Shuffle()
        {
            const int S = 64;
            return Make("shuffle", S, S, (x, y) =>
            {
                float d = Mathf.Min(DistSeg(x, y, 10, 44, 28, 44), Mathf.Min(DistSeg(x, y, 28, 44, 44, 20), DistSeg(x, y, 44, 20, 54, 20)));
                float d2 = Mathf.Min(DistSeg(x, y, 10, 20, 28, 20), Mathf.Min(DistSeg(x, y, 28, 20, 44, 44), DistSeg(x, y, 44, 44, 54, 44)));
                return new Color(1, 1, 1, Cov(Mathf.Min(d, d2) - 3.5f));
            });
        }

        public static Sprite Note()
        {
            const int S = 64;
            return Make("note", S, S, (x, y) =>
            {
                float head = Cov(Mathf.Sqrt((x - 22f) * (x - 22f) + (y - 16f) * (y - 16f)) - 9f);
                float stem = Cov(DistSeg(x, y, 30, 18, 30, 50) - 3.5f);
                float flag = Cov(DistSeg(x, y, 30, 50, 46, 38) - 3.5f);
                return new Color(1, 1, 1, Mathf.Max(head, Mathf.Max(stem, flag)));
            });
        }

        public static Sprite Speaker()
        {
            const int S = 64;
            return Make("speaker", S, S, (x, y) =>
            {
                var tri = new[] { new Vector2(8, 24), new Vector2(22, 24), new Vector2(38, 8), new Vector2(38, 56), new Vector2(22, 40), new Vector2(8, 40) };
                float body = InPoly(x, y, tri) ? 1f : 0f;
                float dx = x - 36f, dy = y - 32f; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float wave = (dx > 4f) ? Cov(Mathf.Abs(r - 14f) - 2.6f) : 0f;
                float wave2 = (dx > 4f) ? Cov(Mathf.Abs(r - 22f) - 2.6f) : 0f;
                return new Color(1, 1, 1, Mathf.Max(body, Mathf.Max(wave, wave2)));
            });
        }

        public static Sprite Home()
        {
            const int S = 64;
            var roof = new[] { new Vector2(6, 32), new Vector2(32, 56), new Vector2(58, 32), new Vector2(52, 32), new Vector2(32, 50), new Vector2(12, 32) };
            var house = new[] { new Vector2(14, 30), new Vector2(32, 48), new Vector2(50, 30), new Vector2(50, 8), new Vector2(14, 8) };
            return Make("home", S, S, (x, y) => new Color(1, 1, 1, (InPoly(x, y, roof) || InPoly(x, y, house)) ? 1f : 0f));
        }

        public static Sprite Pause()
        {
            const int S = 64;
            return Make("pause", S, S, (x, y) => new Color(1, 1, 1, Mathf.Max(Cov(RoundBox(x - 14f, y - 12f, 14f, 40f, 4f)), Cov(RoundBox(x - 36f, y - 12f, 14f, 40f, 4f)))));
        }

        private static float DistSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float cx = ax + abx * t, cy = ay + aby * t;
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }
    }
}
