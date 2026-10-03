using UnityEngine;

namespace BlockBloom
{
    /// <summary>Full-colour icons (use with Image.color = white). Same procedural approach as Sprites, but layered shapes with their own colours.</summary>
    public static class Icons
    {
        private static Color Over(Color under, Color over, float cov)
        {
            float a = Mathf.Clamp01(cov) * over.a;
            if (a <= 0f) return under;
            float outA = a + under.a * (1f - a);
            if (outA <= 0f) return new Color(0, 0, 0, 0);
            return new Color((over.r * a + under.r * under.a * (1f - a)) / outA, (over.g * a + under.g * under.a * (1f - a)) / outA,
                             (over.b * a + under.b * under.a * (1f - a)) / outA, outA);
        }

        private static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            return Sprites.RoundBox(x - (cx - hw), y - (cy - hh), hw * 2, hh * 2, r);
        }
        private static float Circ(float x, float y, float cx, float cy, float r)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }
        private static Color H(string s) { return Palette.Hex(s); }
        private static Color Shade(Color c, float y, float lo, float hi) { float k = Mathf.Lerp(lo, hi, y / 96f); return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a); }
        private static Color Clear { get { return new Color(1, 1, 1, 0); } }

        public static Sprite Gift()
        {
            return Sprites.Make("i_gift", 96, 96, (x, y) =>
            {
                Color c = Clear;
                c = Over(c, new Color(0, 0, 0, 0.18f), Sprites.Cov(Box(x, y, 48, 24, 32, 20, 6)));
                c = Over(c, Shade(H("#ff4f87"), y, 0.8f, 1.1f), Sprites.Cov(Box(x, y, 48, 32, 31, 22, 6)));
                c = Over(c, H("#ffd54a"), Sprites.Cov(Box(x, y, 48, 32, 6, 22, 1)));
                c = Over(c, Shade(H("#ff7aa5"), y, 0.85f, 1.1f), Sprites.Cov(Box(x, y, 48, 58, 37, 10, 5)));
                c = Over(c, H("#ffd54a"), Sprites.Cov(Box(x, y, 48, 58, 6.5f, 10, 1)));
                c = Over(c, new Color(1, 1, 1, 0.3f), Sprites.Cov(Box(x, y, 26, 36, 3, 14, 2)));
                float rl = Mathf.Abs(Circ(x, y, 35, 76, 10)) - 3.4f, rr = Mathf.Abs(Circ(x, y, 61, 76, 10)) - 3.4f;
                if (y > 66f) { c = Over(c, H("#ffc01f"), Sprites.Cov(Mathf.Min(rl, rr))); }
                c = Over(c, H("#ffd54a"), Sprites.Cov(Circ(x, y, 48, 70, 6)));
                return c;
            });
        }

        public static Sprite Bag()
        {
            var body = new[] { new Vector2(20, 8), new Vector2(76, 8), new Vector2(84, 60), new Vector2(12, 60) };
            return Sprites.Make("i_bag", 96, 96, (x, y) =>
            {
                Color c = Clear;
                float ring = Mathf.Abs(Circ(x, y, 48, 62, 17)) - 3.5f;
                if (y > 58f) c = Over(c, H("#8a5a2b"), Sprites.Cov(ring));
                if (Sprites.InPoly(x, y, body)) c = Over(c, Shade(H("#ffb02e"), y, 0.72f, 1.12f), 1f);
                else c = Over(c, Shade(H("#ffb02e"), y, 0.72f, 1.12f), Sprites.Cov(Box(x, y, 48, 34, 32, 26, 6)) * 0.0f);
                if (Sprites.InPoly(x, y, body))
                {
                    c = Over(c, H("#fff3c4"), Sprites.Cov(Circ(x, y, 48, 34, 14)));
                    c = Over(c, H("#ffb02e"), Sprites.Cov(Circ(x, y, 48, 34, 9)));
                    c = Over(c, new Color(1, 1, 1, 0.25f), Sprites.Cov(Box(x, y, 24, 36, 3, 18, 2)));
                }
                return c;
            });
        }

        public static Sprite Wheel()
        {
            return Sprites.Make("i_wheel", 96, 96, (x, y) =>
            {
                Color c = Clear;
                float dx = x - 48, dy = y - 46;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                c = Over(c, H("#ffc93c"), Sprites.Cov(r - 42));
                float ang = Mathf.Atan2(dy, dx) + Mathf.PI;
                int sec = Mathf.FloorToInt(ang / (Mathf.PI * 2f) * 8f) % 8;
                Color[] cols = { H("#4a8dff"), H("#ff5a96"), H("#55dc57"), H("#a66cff"), H("#ff8a34"), H("#19d4c1"), H("#ffca2b"), H("#ff5468") };
                c = Over(c, cols[sec], Sprites.Cov(r - 36));
                if (r < 36f && Mathf.Abs(Mathf.Sin(ang * 4f)) < 0.04f) c = Over(c, new Color(1, 1, 1, 0.85f), 1f);
                c = Over(c, H("#2a1f5c"), Sprites.Cov(r - 9));
                c = Over(c, H("#ffc93c"), Sprites.Cov(r - 5));
                var tri = new[] { new Vector2(48, 94), new Vector2(40, 78), new Vector2(56, 78) };
                if (Sprites.InPoly(x, y, tri)) c = Over(c, H("#ff3d5a"), 1f);
                return c;
            });
        }

        public static Sprite Scroll()
        {
            return Sprites.Make("i_scroll", 96, 96, (x, y) =>
            {
                Color c = Clear;
                c = Over(c, H("#e9e3ff"), Sprites.Cov(Box(x, y, 48, 46, 30, 38, 7)));
                c = Over(c, Shade(H("#6f4bd8"), y, 0.8f, 1.05f), Sprites.Cov(Box(x, y, 48, 82, 14, 7, 4)));
                float[] ys = { 62, 44, 26 };
                for (int i = 0; i < 3; i++)
                {
                    c = Over(c, i == 0 ? H("#3fd36a") : H("#b9afe8"), Sprites.Cov(Circ(x, y, 32, ys[i], 5.5f)));
                    c = Over(c, H("#8f84cf"), Sprites.Cov(Sprites.DistSeg(x, y, 44, ys[i], 66, ys[i]) - 2.6f));
                }
                c = Over(c, Color.white, Sprites.Cov(Sprites.DistSeg(x, y, 29, 62, 31.5f, 59) - 1.4f) * 0f);
                return c;
            });
        }

        public static Sprite Flag()
        {
            var flag = new[] { new Vector2(30, 88), new Vector2(80, 72), new Vector2(30, 54) };
            return Sprites.Make("i_flag", 96, 96, (x, y) =>
            {
                Color c = Clear;
                c = Over(c, H("#7a5a3a"), Sprites.Cov(Sprites.DistSeg(x, y, 29, 8, 29, 90) - 3.4f));
                if (Sprites.InPoly(x, y, flag)) c = Over(c, Shade(H("#19d4c1"), y, 0.8f, 1.1f), 1f);
                c = Over(c, H("#ffffff"), Sprites.Cov(Circ(x, y, 29, 8, 8)) * 0.0f);
                c = Over(c, H("#5a4128"), Sprites.Cov(Box(x, y, 29, 8, 14, 5, 3)));
                return c;
            });
        }

        public static Sprite Hourglass()
        {
            var top = new[] { new Vector2(24, 84), new Vector2(72, 84), new Vector2(52, 48), new Vector2(44, 48) };
            var bot = new[] { new Vector2(44, 48), new Vector2(52, 48), new Vector2(74, 12), new Vector2(22, 12) };
            var sand = new[] { new Vector2(30, 12), new Vector2(66, 12), new Vector2(58, 30), new Vector2(38, 30) };
            return Sprites.Make("i_hourglass", 96, 96, (x, y) =>
            {
                Color c = Clear;
                if (Sprites.InPoly(x, y, top) || Sprites.InPoly(x, y, bot)) c = Over(c, new Color(0.75f, 0.9f, 1f, 0.55f), 1f);
                if (Sprites.InPoly(x, y, sand)) c = Over(c, Shade(H("#ffc93c"), y, 0.8f, 1.1f), 1f);
                c = Over(c, H("#ffc93c"), Sprites.Cov(Sprites.DistSeg(x, y, 48, 70, 48, 48) - 1.6f) * (y < 70 ? 1f : 0f));
                c = Over(c, H("#a8642b"), Sprites.Cov(Box(x, y, 48, 88, 30, 5, 3)));
                c = Over(c, H("#a8642b"), Sprites.Cov(Box(x, y, 48, 8, 30, 5, 3)));
                return c;
            });
        }

        public static Sprite Video()
        {
            var tri = new[] { new Vector2(40, 34), new Vector2(40, 62), new Vector2(64, 48) };
            return Sprites.Make("i_video", 96, 96, (x, y) =>
            {
                Color c = Clear;
                c = Over(c, new Color(1, 1, 1, 1), Sprites.Cov(Box(x, y, 48, 48, 38, 28, 10)));
                if (Sprites.InPoly(x, y, tri)) c = Over(c, H("#2fbf5b"), 1f);
                return c;
            });
        }

        public static Sprite Flame()
        {
            var outer = new[] { new Vector2(48, 92), new Vector2(66, 66), new Vector2(80, 40), new Vector2(72, 16), new Vector2(48, 6), new Vector2(24, 16), new Vector2(16, 40), new Vector2(28, 58), new Vector2(38, 50), new Vector2(40, 72) };
            var inner = new[] { new Vector2(48, 64), new Vector2(62, 40), new Vector2(58, 20), new Vector2(48, 14), new Vector2(38, 20), new Vector2(34, 40) };
            return Sprites.Make("i_flame", 96, 96, (x, y) =>
            {
                Color c = Clear;
                if (Sprites.InPoly(x, y, outer)) c = Over(c, Shade(H("#ff7a1f"), y, 0.8f, 1.15f), 1f);
                if (Sprites.InPoly(x, y, inner)) c = Over(c, Shade(H("#ffd23f"), y, 0.9f, 1.1f), 1f);
                return c;
            });
        }

        public static Sprite Palette4()
        {
            return Sprites.Make("i_palette", 96, 96, (x, y) =>
            {
                Color c = Clear;
                Color[] cols = { H("#19d4c1"), H("#ff5a96"), H("#ffca2b"), H("#4a8dff") };
                float[] xs = { 26, 68, 26, 68 }, ys = { 68, 68, 26, 26 };
                for (int i = 0; i < 4; i++) c = Over(c, Shade(cols[i], y, 0.8f, 1.1f), Sprites.Cov(Box(x, y, xs[i], ys[i], 19, 19, 6)));
                return c;
            });
        }

        public static Sprite Sparkle()
        {
            var pts = new[] { new Vector2(48, 94), new Vector2(57, 57), new Vector2(94, 48), new Vector2(57, 39), new Vector2(48, 2), new Vector2(39, 39), new Vector2(2, 48), new Vector2(39, 57) };
            return Sprites.Make("i_sparkle", 96, 96, (x, y) => new Color(1, 1, 1, Sprites.InPoly(x, y, pts) ? 1f : 0f));
        }

        public static Sprite HomeColour()
        {
            var roof = new[] { new Vector2(6, 46), new Vector2(48, 86), new Vector2(90, 46), new Vector2(80, 46), new Vector2(48, 76), new Vector2(16, 46) };
            var wall = new[] { new Vector2(18, 46), new Vector2(48, 76), new Vector2(78, 46), new Vector2(78, 10), new Vector2(18, 10) };
            return Sprites.Make("i_home", 96, 96, (x, y) =>
            {
                Color c = Clear;
                if (Sprites.InPoly(x, y, wall)) c = Over(c, Shade(H("#ffe9b0"), y, 0.8f, 1.05f), 1f);
                if (Sprites.InPoly(x, y, roof)) c = Over(c, Shade(H("#ff5a5a"), y, 0.85f, 1.1f), 1f);
                c = Over(c, H("#8a5a2b"), Sprites.Cov(Box(x, y, 48, 24, 9, 16, 3)));
                return c;
            });
        }

        public static Sprite Trophy()
        {
            var cup = new[] { new Vector2(26, 88), new Vector2(70, 88), new Vector2(66, 54), new Vector2(54, 44), new Vector2(42, 44), new Vector2(30, 54) };
            return Sprites.Make("i_trophy", 96, 96, (x, y) =>
            {
                Color c = Clear;
                c = Over(c, H("#e0a21a"), Sprites.Cov(Mathf.Abs(Circ(x, y, 22, 68, 12)) - 3.4f) * (x < 28 ? 1f : 0f));
                c = Over(c, H("#e0a21a"), Sprites.Cov(Mathf.Abs(Circ(x, y, 74, 68, 12)) - 3.4f) * (x > 68 ? 1f : 0f));
                if (Sprites.InPoly(x, y, cup)) c = Over(c, Shade(H("#ffc93c"), y, 0.78f, 1.15f), 1f);
                c = Over(c, H("#e0a21a"), Sprites.Cov(Box(x, y, 48, 36, 5, 8, 2)));
                c = Over(c, Shade(H("#ffc93c"), y, 0.8f, 1.1f), Sprites.Cov(Box(x, y, 48, 22, 20, 6, 3)));
                c = Over(c, new Color(1, 1, 1, 0.35f), Sprites.Cov(Box(x, y, 36, 68, 3, 14, 2)));
                return c;
            });
        }
    }
}
