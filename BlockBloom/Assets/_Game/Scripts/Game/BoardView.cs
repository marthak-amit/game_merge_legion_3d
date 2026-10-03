using UnityEngine;
using UnityEngine.UI;
using BlockBloom.Core;
using BlockBloom.Logic;

namespace BlockBloom
{
    /// <summary>Draws the 8x8 board: cells, blocks, gems, drop preview and all the clear animations.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float Cell = 104f;
        public const float Pad = 16f;
        public const float Size = Cell * Bits.N + Pad * 2f;

        public RectTransform Rt;
        private Image _bg;
        private readonly Image[] _cells = new Image[Bits.Cells];
        private readonly Image[] _blocks = new Image[Bits.Cells];
        private readonly Image[] _gems = new Image[Bits.Cells];
        private readonly Image[] _ghost = new Image[Bits.Cells];
        private readonly float[] _flash = new float[Bits.Cells];
        private readonly int[] _colour = new int[Bits.Cells];    // palette index + 1, 0 = empty
        private ulong _hot;                                       // cells that would clear (preview)
        private Image _glowFrame;

        public static BoardView Create(Transform parent)
        {
            var rt = Ui.Rect(parent, "Board");
            rt.sizeDelta = new Vector2(Size, Size);
            var v = rt.gameObject.AddComponent<BoardView>();
            v.Rt = rt;
            v.Build();
            return v;
        }

        private void Build()
        {
            var th = Palette.Themes[Mathf.Clamp(Save.Data.theme, 0, Palette.Themes.Length - 1)];
            var shadow = Ui.Img(Rt, Sprites.Round(44), new Color(0, 0, 0, 0.30f), "shadow");
            Ui.Stretch(shadow.rectTransform); shadow.rectTransform.offsetMin = new Vector2(-6, -24); shadow.rectTransform.offsetMax = new Vector2(6, -8);
            _bg = Ui.Img(Rt, Sprites.Round(44), th.Board, "bg");
            Ui.Stretch(_bg.rectTransform);
            _glowFrame = Ui.Img(Rt, Sprites.Round(44), Palette.Alpha(th.Accent, 0f), "frame");
            Ui.Stretch(_glowFrame.rectTransform, -6, -6, -6, -6);
            _glowFrame.transform.SetAsFirstSibling();
            shadow.transform.SetAsFirstSibling();

            var cellSprite = Sprites.Cell(); var blockSprite = Sprites.Block(); var gemSprite = Sprites.Diamond();
            for (int r = 0; r < Bits.N; r++)
                for (int c = 0; c < Bits.N; c++)
                {
                    int i = r * Bits.N + c;
                    Vector2 p = CellPos(r, c);
                    var ci = Ui.Img(Rt, cellSprite, th.Cell, "c" + i);
                    Ui.At(ci.rectTransform, new Vector2(0.5f, 0.5f), p, new Vector2(Cell - 6, Cell - 6));
                    _cells[i] = ci;
                }
            for (int i = 0; i < Bits.Cells; i++)
            {
                int r = i / Bits.N, c = i % Bits.N;
                Vector2 p = CellPos(r, c);
                var g = Ui.Img(Rt, blockSprite, Color.white, "g" + i);
                Ui.At(g.rectTransform, new Vector2(0.5f, 0.5f), p, new Vector2(Cell - 4, Cell - 4));
                g.gameObject.SetActive(false); _ghost[i] = g;
            }
            for (int i = 0; i < Bits.Cells; i++)
            {
                int r = i / Bits.N, c = i % Bits.N;
                Vector2 p = CellPos(r, c);
                var b = Ui.Img(Rt, blockSprite, Color.white, "b" + i);
                Ui.At(b.rectTransform, new Vector2(0.5f, 0.5f), p, new Vector2(Cell - 4, Cell - 4));
                b.gameObject.SetActive(false); _blocks[i] = b;
                var gem = Ui.Img(b.transform, gemSprite, Color.white, "gem");
                Ui.At(gem.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Cell * 0.62f, Cell * 0.62f));
                gem.gameObject.SetActive(false); _gems[i] = gem;
            }
            // ghost sits above blocks so a preview over an almost-complete line is visible
            for (int i = 0; i < Bits.Cells; i++) _ghost[i].transform.SetAsLastSibling();
        }

        public void ApplyTheme()
        {
            var th = Palette.Themes[Mathf.Clamp(Save.Data.theme, 0, Palette.Themes.Length - 1)];
            _bg.color = th.Board;
            for (int i = 0; i < Bits.Cells; i++) _cells[i].color = th.Cell;
        }

        public static Vector2 CellPos(int r, int c)
        {
            float half = Cell * Bits.N * 0.5f;
            return new Vector2(-half + Cell * (c + 0.5f), half - Cell * (r + 0.5f));
        }

        public Vector3 CellWorld(int r, int c)
        {
            return Rt.TransformPoint(CellPos(r, c));
        }

        /// <summary>Screen point -> fractional (row, col) of the piece's top-left cell.</summary>
        public Vector2 LocalToGrid(Vector2 local)
        {
            float half = Cell * Bits.N * 0.5f;
            return new Vector2((half - local.y) / Cell, (local.x + half) / Cell);   // x = row, y = col
        }

        // ---------- cell state ----------
        public void SetCell(int i, int colourPlusOne, bool gem)
        {
            _colour[i] = colourPlusOne;
            var b = _blocks[i];
            if (colourPlusOne <= 0) { b.gameObject.SetActive(false); return; }
            b.gameObject.SetActive(true);
            b.color = Palette.Block(colourPlusOne - 1);
            b.rectTransform.localScale = Vector3.one;
            _gems[i].gameObject.SetActive(gem);
            _flash[i] = 0;
        }

        public void Sync(BoardState st)
        {
            for (int i = 0; i < Bits.Cells; i++)
            {
                bool occ = (st.Occ & (1UL << i)) != 0;
                SetCell(i, occ ? Mathf.Max(1, st.Colors[i]) : 0, occ && (st.Gems & (1UL << i)) != 0);
            }
            ClearGhost();
        }

        // ---------- drag preview ----------
        public void ShowGhost(BoardState st, Shape s, int r, int c, int colour, bool valid)
        {
            ClearGhost();
            if (!valid) return;
            ulong m = s.MaskAt(r, c);
            int rm, cm;
            ulong clearing = st.PreviewClear(s, r, c, out rm, out cm);
            var col = Palette.Block(colour);
            for (int i = 0; i < Bits.Cells; i++)
            {
                if ((m & (1UL << i)) == 0) continue;
                _ghost[i].gameObject.SetActive(true);
                _ghost[i].color = Palette.Alpha(col, (clearing & (1UL << i)) != 0 ? 0.95f : 0.55f);
            }
            _hot = clearing;
            var th = Palette.Themes[Mathf.Clamp(Save.Data.theme, 0, Palette.Themes.Length - 1)];
            _glowFrame.color = Palette.Alpha(clearing != 0 ? col : th.Accent, clearing != 0 ? 0.35f : 0f);
        }

        public void ClearGhost()
        {
            for (int i = 0; i < Bits.Cells; i++) if (_ghost[i].gameObject.activeSelf) _ghost[i].gameObject.SetActive(false);
            _hot = 0;
            _glowFrame.color = Palette.Alpha(_glowFrame.color, 0f);
        }

        private void Update()
        {
            // cells in lines about to be completed pulse towards white
            for (int i = 0; i < Bits.Cells; i++)
            {
                if (_colour[i] <= 0) continue;
                bool hot = (_hot & (1UL << i)) != 0;
                float target = hot ? 1f : 0f;
                if (Mathf.Abs(_flash[i] - target) < 0.001f) continue;
                _flash[i] = Mathf.MoveTowards(_flash[i], target, Time.unscaledDeltaTime * 9f);
                _blocks[i].color = Color.Lerp(Palette.Block(_colour[i] - 1), Color.white, _flash[i] * 0.55f);
                float sc = 1f + 0.06f * _flash[i];
                _blocks[i].rectTransform.localScale = new Vector3(sc, sc, 1f);
            }
        }

        // ---------- animations ----------
        /// <summary>Cells swell in as a diagonal wave when a game starts.</summary>
        public void IntroWave()
        {
            for (int i = 0; i < Bits.Cells; i++)
            {
                int r = i / Bits.N, c = i % Bits.N;
                float delay = 0.025f * (r + c);
                var ct = _cells[i].rectTransform; var bt = _blocks[i].rectTransform;
                ct.localScale = Vector3.zero;
                Tween.ScaleTo(ct, Vector3.one, 0.35f, Ease.OutBack, delay);
                if (_blocks[i].gameObject.activeSelf)
                {
                    bt.localScale = Vector3.zero;
                    Tween.ScaleTo(bt, Vector3.one, 0.4f, Ease.OutBack, delay + 0.12f);
                }
            }
        }

        /// <summary>A soft shockwave through the cells around a placement.</summary>
        public void Ripple(int r0, int c0, int r1, int c1)
        {
            for (int i = 0; i < Bits.Cells; i++)
            {
                if (_colour[i] <= 0) continue;
                int r = i / Bits.N, c = i % Bits.N;
                int dr = r < r0 ? r0 - r : (r > r1 ? r - r1 : 0), dc = c < c0 ? c0 - c : (c > c1 ? c - c1 : 0);
                int d = dr + dc;
                if (d == 0 || d > 3) continue;
                var t = _blocks[i].rectTransform;
                int idx = i;
                Tween.Value(0.28f, k =>
                {
                    if (t == null || _flash[idx] > 0.01f) return;
                    float s = 1f + 0.07f / d * Mathf.Sin(k * Mathf.PI);
                    t.localScale = new Vector3(s, s, 1f);
                }, Ease.Linear, () => { if (t != null && _flash[idx] <= 0.01f) t.localScale = Vector3.one; }, 0.04f * d, t);
            }
        }

        public void PopCells(ulong mask)
        {
            for (int i = 0; i < Bits.Cells; i++)
            {
                if ((mask & (1UL << i)) == 0) continue;
                var t = _blocks[i].rectTransform;
                t.localScale = new Vector3(1.28f, 1.28f, 1f);
                Tween.ScaleTo(t, Vector3.one, 0.22f, Ease.OutBack);
            }
        }

        /// <summary>Clears the cells with a staggered wave from the origin cell. Calls onCellGem(world) for collected gems.</summary>
        public float AnimateClear(ulong mask, int originR, int originC, int combo, System.Action<Vector3, int> onGem)
        {
            float maxDelay = 0f;
            for (int i = 0; i < Bits.Cells; i++)
            {
                if ((mask & (1UL << i)) == 0) continue;
                int r = i / Bits.N, c = i % Bits.N;
                float dist = Mathf.Abs(r - originR) + Mathf.Abs(c - originC);
                float delay = 0.035f * dist + 0.02f;
                if (delay > maxDelay) maxDelay = delay;
                int idx = i; int colour = _colour[i];
                bool gem = _gems[i].gameObject.activeSelf;
                Tween.Value(0.001f, k => { }, Ease.Linear, () =>
                {
                    if (this == null) return;
                    var t = _blocks[idx].rectTransform;
                    Vector3 w = CellWorld(idx / Bits.N, idx % Bits.N);
                    if (Fx.I != null)
                    {
                        Fx.I.Burst(w, Palette.Block(Mathf.Max(0, colour - 1)), combo >= 3 ? 7 : 5, 480f + combo * 40f, 28f);
                        if (combo >= 2) Fx.I.Sparkle(w, Color.white);
                    }
                    if (gem && onGem != null) onGem(w, idx);
                    _blocks[idx].color = Color.white;
                    Tween.Value(0.16f, k =>
                    {
                        if (t == null) return;
                        float s = 1f + 0.25f * Mathf.Sin(k * Mathf.PI * 0.5f) - k * 1.1f;
                        t.localScale = Vector3.one * Mathf.Max(0f, s);
                    }, Ease.Linear, () => { if (this != null) SetCell(idx, 0, false); }, 0f, t);
                }, delay, _blocks[idx]);
            }
            return maxDelay + 0.18f;
        }

        /// <summary>Lines that are about to clear glow white for a moment before the wave starts.</summary>
        public void FlashLines(int rowMask, int colMask, Color col)
        {
            for (int r = 0; r < Bits.N; r++)
                if ((rowMask & (1 << r)) != 0)
                    LineSweep(CellWorld(r, 0), CellWorld(r, Bits.N - 1), col, true);
            for (int c = 0; c < Bits.N; c++)
                if ((colMask & (1 << c)) != 0)
                    LineSweep(CellWorld(0, c), CellWorld(Bits.N - 1, c), col, false);
        }

        private void LineSweep(Vector3 a, Vector3 b, Color col, bool horizontal)
        {
            if (Fx.I == null) return;
            Fx.I.Flash((a + b) * 0.5f, col, horizontal ? Size * 1.1f : Size * 1.1f);
        }

        public void Shake(float amount) { if (Fx.I != null) Fx.I.Shake(Rt, amount, 0.28f); }
    }
}
