using MergeLegion.Audio;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Data;
using UnityEngine;

namespace MergeLegion.Grid
{
    /// <summary>3D presentation of the player's grid: tiles, unit models, and the juice for spawn/merge/move/swap.</summary>
    public sealed class GridView : MonoBehaviour
    {
        public const float TileTop = 0.1f;

        private ArmyService _army;
        private ArenaLayout _layout;
        private GameDatabase _db;
        private Quaternion _labelFacing = Quaternion.identity;
        private UnitVisual[] _views;
        private Renderer[] _tiles;
        private Material _tileA, _tileB, _tileGlow, _tileMerge;
        private int _highlight = -1;
        private bool _highlightMerge;

        public void Init(ArmyService army, ArenaLayout layout, GameDatabase db, Quaternion cameraRotation)
        {
            _army = army;
            _layout = layout;
            _db = db;
            _labelFacing = cameraRotation;
            _views = new UnitVisual[army.Grid.Count];
            _tiles = new Renderer[army.Grid.Count];
            _tileA = MaterialLibrary.Lit(new Color(0.24f, 0.30f, 0.38f));
            _tileB = MaterialLibrary.Lit(new Color(0.28f, 0.35f, 0.44f));
            _tileGlow = MaterialLibrary.Lit(new Color(0.95f, 0.75f, 0.25f));
            _tileMerge = MaterialLibrary.Lit(new Color(0.3f, 0.9f, 0.45f));

            for (int i = 0; i < _tiles.Length; i++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(tile.GetComponent<Collider>());
                tile.name = "Tile" + i;
                tile.transform.SetParent(transform, false);
                var p = CellWorld(i);
                tile.transform.position = new Vector3(p.x, TileTop * 0.5f, p.z);
                tile.transform.localScale = new Vector3(_layout.CellSize * 0.93f, TileTop, _layout.CellSize * 0.93f);
                var r = tile.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.sharedMaterial = (_army.Grid.ColOf(i) + _army.Grid.RowOf(i)) % 2 == 0 ? _tileA : _tileB;
                _tiles[i] = r;
            }

            Rebuild();
            EventBus.Subscribe<UnitSpawnedEvent>(OnSpawned);
            EventBus.Subscribe<UnitMovedEvent>(OnMoved);
            EventBus.Subscribe<UnitSwappedEvent>(OnSwapped);
            EventBus.Subscribe<UnitMergedEvent>(OnMerged);
            EventBus.Subscribe<GridResetEvent>(OnReset);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UnitSpawnedEvent>(OnSpawned);
            EventBus.Unsubscribe<UnitMovedEvent>(OnMoved);
            EventBus.Unsubscribe<UnitSwappedEvent>(OnSwapped);
            EventBus.Unsubscribe<UnitMergedEvent>(OnMerged);
            EventBus.Unsubscribe<GridResetEvent>(OnReset);
            if (_views != null)
                for (int i = 0; i < _views.Length; i++) UnitVisualFactory.Release(_views[i]);
        }

        public void SetTileColors(Color a, Color b)
        {
            _tileA = MaterialLibrary.Lit(a);
            _tileB = MaterialLibrary.Lit(b);
            for (int i = 0; i < _tiles.Length; i++) ResetTile(i);
            if (_highlight >= 0) _tiles[_highlight].sharedMaterial = _highlightMerge ? _tileMerge : _tileGlow;
        }

        public Vector3 CellWorld(int index)
        {
            var g = _army.Grid;
            var v = _layout.PlayerCell(g.ColOf(index), g.RowOf(index));
            return new Vector3(v.x, TileTop, v.y);
        }

        /// <summary>Cell under a world point on the ground plane, or -1 if outside the grid (plus slack in cells).</summary>
        public int CellAt(Vector3 world, float slackCells = 0.15f)
        {
            var g = _army.Grid;
            float colF = world.x / _layout.CellSize + (g.Cols - 1) * 0.5f;
            float rowF = (_layout.FrontZ - world.z) / _layout.CellSize;
            int col = Mathf.RoundToInt(colF), row = Mathf.RoundToInt(rowF);
            if (Mathf.Abs(colF - col) > 0.5f + slackCells || Mathf.Abs(rowF - row) > 0.5f + slackCells) return -1;
            if (col < 0 || col >= g.Cols || row < 0 || row >= g.Rows) return -1;
            return g.Index(col, row);
        }

        public UnitVisual ViewAt(int index) => _views[index];

        public void SetHighlight(int index, bool mergeable)
        {
            if (_highlight == index && _highlightMerge == mergeable) return;
            ResetTile(_highlight);
            _highlight = index;
            _highlightMerge = mergeable;
            if (index >= 0) _tiles[index].sharedMaterial = mergeable ? _tileMerge : _tileGlow;
        }

        private void ResetTile(int index)
        {
            if (index < 0) return;
            _tiles[index].sharedMaterial = (_army.Grid.ColOf(index) + _army.Grid.RowOf(index)) % 2 == 0 ? _tileA : _tileB;
        }

        public Vector3 RestPosition(int index, UnitVisual v) => CellWorld(index) + Vector3.up * v.HoverHeight;

        public void SnapBack(int index)
        {
            var v = _views[index];
            if (v != null) Tween.MoveTo(v.transform, RestPosition(index, v), 0.15f);
        }

        private void Rebuild()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                UnitVisualFactory.Release(_views[i]);
                _views[i] = null;
                var s = _army.Grid.Get(i);
                if (!s.IsEmpty) _views[i] = Spawn(i, s.line, s.level, false);
            }
        }

        private UnitVisual Spawn(int index, int line, int level, bool animate)
        {
            var data = _db.GetLine((UnitLineId)line);
            var v = UnitVisualFactory.Get(data, level, false, transform);
            v.transform.position = RestPosition(index, v);
            v.transform.rotation = Quaternion.identity;
            v.SetLabel(level.ToString(), _labelFacing);
            if (animate)
            {
                v.transform.localScale = Vector3.zero;
                Tween.ScaleTo(v.transform, Vector3.one * v.BaseScale, 0.28f, Ease.OutBack);
            }
            return v;
        }

        private void Release(int index)
        {
            Tween.Kill(_views[index] != null ? _views[index].transform : null);
            UnitVisualFactory.Release(_views[index]);
            _views[index] = null;
        }

        private void OnReset(GridResetEvent e) => Rebuild();

        private void OnSpawned(UnitSpawnedEvent e)
        {
            Release(e.Index);
            var v = Spawn(e.Index, e.Line, e.Level, true);
            _views[e.Index] = v;
            Vfx.Emit(VfxType.Pop, CellWorld(e.Index) + Vector3.up * 0.3f, v.Tint, 8);
            Haptics.Light();
        }

        private void OnMoved(UnitMovedEvent e)
        {
            _views[e.To] = _views[e.From];
            _views[e.From] = null;
            Tween.MoveTo(_views[e.To].transform, RestPosition(e.To, _views[e.To]), 0.14f);
        }

        private void OnSwapped(UnitSwappedEvent e)
        {
            var a = _views[e.A];
            _views[e.A] = _views[e.B];
            _views[e.B] = a;
            Tween.MoveTo(_views[e.A].transform, RestPosition(e.A, _views[e.A]), 0.16f);
            Tween.MoveTo(_views[e.B].transform, RestPosition(e.B, _views[e.B]), 0.16f);
            Haptics.Light();
        }

        private void OnMerged(UnitMergedEvent e)
        {
            Release(e.From);
            Release(e.To);
            var v = Spawn(e.To, e.Line, e.NewLevel, false);
            _views[e.To] = v;
            Tween.Punch(v.transform, 0.4f, 0.32f);
            var at = CellWorld(e.To) + Vector3.up * 0.5f;
            Vfx.Emit(VfxType.Pop, at, v.Tint, 26);
            Vfx.Emit(VfxType.Glow, at, Color.Lerp(v.Tint, Color.white, 0.5f), 6);
            Haptics.Medium();
            if (ArenaCamera.Instance != null) ArenaCamera.Instance.Shake(0.10f + 0.01f * e.NewLevel, 0.18f);
        }
    }
}
