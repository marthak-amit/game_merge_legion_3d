using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlockBloom.Core;
using BlockBloom.Logic;

namespace BlockBloom
{
    public sealed class DragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public TrayView Tray; public int Index;
        public void OnBeginDrag(PointerEventData e) { Tray.BeginDrag(Index, e); }
        public void OnDrag(PointerEventData e) { Tray.Drag(Index, e); }
        public void OnEndDrag(PointerEventData e) { Tray.EndDrag(Index, e); }
    }

    /// <summary>The three-piece tray and the drag-and-drop interaction (lifted piece, snapped ghost, forgiving placement).</summary>
    public sealed class TrayView : MonoBehaviour
    {
        public const float SlotW = 340f, SlotH = 280f, TrayScale = 0.56f, Lift = 190f;

        public Action<int, int, int> Dropped;     // slot, row, col
        public bool InputEnabled = true;

        private GameSession _s;
        private BoardView _board;
        private RectTransform _dragLayer;
        private readonly RectTransform[] _slots = new RectTransform[GameSession.TraySize];
        private readonly RectTransform[] _pieces = new RectTransform[GameSession.TraySize];
        private readonly CanvasGroup[] _groups = new CanvasGroup[GameSession.TraySize];
        private int _dragging = -1;
        private float _lift;
        private int _gr = -99, _gc = -99; private bool _gvalid;
        private float _idle;

        public static TrayView Create(Transform parent, BoardView board, RectTransform dragLayer)
        {
            var rt = Ui.Rect(parent, "Tray");
            rt.sizeDelta = new Vector2(SlotW * 3, SlotH);
            var t = rt.gameObject.AddComponent<TrayView>();
            t._board = board; t._dragLayer = dragLayer;
            for (int i = 0; i < GameSession.TraySize; i++)
            {
                var slot = Ui.Rect(rt, "slot" + i);
                Ui.At(slot, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * SlotW, 0), new Vector2(SlotW, SlotH));
                var hit = slot.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0.001f); hit.raycastTarget = true;
                var ds = slot.gameObject.AddComponent<DragSlot>(); ds.Tray = t; ds.Index = i;
                t._slots[i] = slot;
            }
            return t;
        }

        public static RectTransform BuildPiece(Transform parent, Shape s, int colour)
        {
            float cs = BoardView.Cell;
            var root = Ui.Rect(parent, "piece");
            root.sizeDelta = new Vector2(s.Width * cs, s.Height * cs);
            var sprite = Sprites.Block();
            var col = Palette.Block(colour);
            for (int i = 0; i < s.Count; i++)
            {
                var im = Ui.Img(root, sprite, col, "c");
                Ui.At(im.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(-s.Width * cs * 0.5f + cs * (s.Cols[i] + 0.5f), s.Height * cs * 0.5f - cs * (s.Rows[i] + 0.5f)),
                    new Vector2(cs - 4, cs - 4));
            }
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            return root;
        }

        /// <summary>(Re)builds the pieces of the current tray. Animates them in when 'animate' is set.</summary>
        public void Show(GameSession s, bool animate)
        {
            _s = s;
            for (int i = 0; i < GameSession.TraySize; i++)
            {
                if (_pieces[i] != null) Destroy(_pieces[i].gameObject);
                _pieces[i] = null;
                if (s.Used[i] || s.Tray[i] == null) continue;
                var p = BuildPiece(_slots[i], s.Tray[i], s.TrayColour[i]);
                p.localPosition = Vector3.zero;
                _pieces[i] = p; _groups[i] = p.GetComponent<CanvasGroup>();
                if (animate)
                {
                    p.localScale = Vector3.zero;
                    Tween.ScaleTo(p, Vector3.one * TrayScale, 0.32f, Ease.OutBack, 0.07f * i);
                }
                else p.localScale = Vector3.one * TrayScale;
            }
            Refresh();
        }

        /// <summary>Greys out pieces that currently have no legal spot.</summary>
        public void Refresh()
        {
            if (_s == null) return;
            for (int i = 0; i < GameSession.TraySize; i++)
            {
                if (_pieces[i] == null || i == _dragging) continue;
                bool ok = !_s.Used[i] && _s.Board.CanPlaceAnywhere(_s.Tray[i]);
                _groups[i].alpha = ok ? 1f : 0.38f;
            }
        }

        public void RemovePiece(int slot)
        {
            if (_pieces[slot] != null) { Destroy(_pieces[slot].gameObject); _pieces[slot] = null; }
        }

        public Vector3 SlotWorld(int slot) { return _slots[slot].position; }

        // ---------- drag ----------
        public void BeginDrag(int i, PointerEventData e)
        {
            if (!InputEnabled || _s == null || _pieces[i] == null || _dragging >= 0) return;
            if (_s.Used[i]) return;
            _dragging = i; _lift = 0f; _gr = -99; _gc = -99; _gvalid = false;
            var p = _pieces[i];
            Tween.Kill(p);
            p.SetParent(_dragLayer, true);
            Sfx.Pick();
            Vector3 from = p.localScale;
            Tween.Value(0.14f, k => { if (p != null && _dragging == i) p.localScale = Vector3.LerpUnclamped(from, Vector3.one, k); }, Ease.OutBack, null, 0f, p);
            _groups[i].alpha = 1f;
            Drag(i, e);
        }

        public void Drag(int i, PointerEventData e)
        {
            if (_dragging != i || _pieces[i] == null) return;
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, e.position, null, out lp);
            _lift = Mathf.Lerp(_lift, Lift + _s.Tray[i].Height * BoardView.Cell * 0.35f, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            var p = _pieces[i];
            p.localPosition = new Vector3(lp.x, lp.y + _lift, 0);

            int r, c; bool valid = Snap(i, out r, out c);
            if (r != _gr || c != _gc || valid != _gvalid)
            {
                _gr = r; _gc = c; _gvalid = valid;
                _board.ShowGhost(_s.Board, _s.Tray[i], r, c, _s.TrayColour[i], valid);
                if (valid) Sfx.Tick();
            }
        }

        private bool Snap(int i, out int r, out int c)
        {
            var shape = _s.Tray[i];
            var p = _pieces[i];
            Vector3 centre = _board.Rt.InverseTransformPoint(p.position);
            Vector2 tl = new Vector2(centre.x - shape.Width * BoardView.Cell * 0.5f, centre.y + shape.Height * BoardView.Cell * 0.5f);
            Vector2 g = _board.LocalToGrid(tl);
            int r0 = Mathf.RoundToInt(g.x), c0 = Mathf.RoundToInt(g.y);
            r = r0; c = c0;
            if (_s.CanPlace(i, r0, c0)) return true;
            float best = 1.35f; bool found = false;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int rr = r0 + dr, cc = c0 + dc;
                    if (!_s.CanPlace(i, rr, cc)) continue;
                    float d = Mathf.Sqrt((g.x - rr) * (g.x - rr) + (g.y - cc) * (g.y - cc));
                    if (d < best) { best = d; r = rr; c = cc; found = true; }
                }
            return found;
        }

        public void EndDrag(int i, PointerEventData e)
        {
            if (_dragging != i) return;
            _dragging = -1;
            var p = _pieces[i];
            if (p == null) return;
            int r, c; bool valid = Snap(i, out r, out c);
            _board.ClearGhost();
            if (valid)
            {
                var shape = _s.Tray[i];
                Vector3 target = _board.Rt.TransformPoint(BoardView.CellPos(r, c) +
                    new Vector2((shape.Width - 1) * BoardView.Cell * 0.5f, -(shape.Height - 1) * BoardView.Cell * 0.5f));
                Vector3 from = p.position;
                Tween.Value(0.07f, k => { if (p != null) p.position = Vector3.LerpUnclamped(from, target, k); }, Ease.OutQuad, () =>
                {
                    if (p != null) { Destroy(p.gameObject); }
                    _pieces[i] = null;
                    if (Dropped != null) Dropped(i, r, c);
                }, 0f, p);
            }
            else
            {
                Sfx.Invalid();
                Vector3 from = p.position, fromScale = p.localScale;
                Vector3 home = _slots[i].position;
                Tween.Value(0.22f, k =>
                {
                    if (p == null) return;
                    p.position = Vector3.LerpUnclamped(from, home, k);
                    p.localScale = Vector3.LerpUnclamped(fromScale, Vector3.one * TrayScale, k);
                }, Ease.OutBack, () =>
                {
                    if (p == null) return;
                    p.SetParent(_slots[i], true); p.localPosition = Vector3.zero; p.localScale = Vector3.one * TrayScale;
                }, 0f, p);
            }
        }

        public void CancelDrag()
        {
            if (_dragging < 0) return;
            int i = _dragging; _dragging = -1;
            _board.ClearGhost();
            var p = _pieces[i];
            if (p != null) { p.SetParent(_slots[i], false); p.localPosition = Vector3.zero; p.localScale = Vector3.one * TrayScale; }
        }

        /// <summary>Gentle wiggle on a piece that can be placed, to nudge a stuck player.</summary>
        public void Hint()
        {
            for (int i = 0; i < GameSession.TraySize; i++)
                if (_pieces[i] != null && i != _dragging && _s != null && !_s.Used[i] && _s.Board.CanPlaceAnywhere(_s.Tray[i]))
                { Tween.Punch(_pieces[i], 0.12f, 0.5f); }
        }

        private void Update()
        {
            if (_dragging >= 0) { _idle = 0; return; }
            _idle += Time.unscaledDeltaTime;
            if (_idle > 7f && InputEnabled) { _idle = 0; Hint(); }
        }
    }
}
