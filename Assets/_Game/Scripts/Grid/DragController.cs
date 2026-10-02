using MergeLegion.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MergeLegion.Grid
{
    /// <summary>
    /// Pointer (mouse or touch) drag-and-drop of units across the grid: empty cell = move, same unit = merge, other = swap.
    /// </summary>
    public sealed class DragController : MonoBehaviour
    {
        private const float LiftHeight = 0.9f;

        private ArmyService _army;
        private GridView _view;
        private Camera _camera;
        private int _from = -1;
        private UnitVisual _dragged;

        public bool Enabled { get; set; } = true;

        public void Init(ArmyService army, GridView view, Camera cam)
        {
            _army = army;
            _view = view;
            _camera = cam;
        }

        private void Update()
        {
            if (_army == null) return;
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (_from >= 0 && !Enabled) { Cancel(); return; }

            bool pressed = pointer.press.wasPressedThisFrame;
            bool released = pointer.press.wasReleasedThisFrame;
            bool held = pointer.press.isPressed;

            if (pressed && Enabled && _from < 0)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                if (TryGround(pointer, out var p))
                {
                    int idx = _view.CellAt(p);
                    if (idx >= 0 && !_army.Grid.IsEmpty(idx)) BeginDrag(idx);
                }
            }
            else if (_from >= 0 && released)
            {
                TryGround(pointer, out var p);
                EndDrag(p);
            }
            else if (_from >= 0 && held)
            {
                if (TryGround(pointer, out var p))
                {
                    _dragged.transform.position = new Vector3(p.x, GridView.TileTop + LiftHeight + _dragged.HoverHeight, p.z);
                    UpdateHighlight(_view.CellAt(p, 0.3f));
                }
            }
        }

        private void BeginDrag(int index)
        {
            _from = index;
            _dragged = _view.ViewAt(index);
            if (_dragged == null) { _from = -1; return; }
            Core.Tween.Kill(_dragged.transform);
            Audio.Haptics.Light();
        }

        private void EndDrag(Vector3 groundPoint)
        {
            int from = _from;
            int to = _view.CellAt(groundPoint, 0.3f);
            _from = -1;
            _dragged = null;
            _view.SetHighlight(-1, false);

            if (to < 0)
            {
                _view.SnapBack(from);
                return;
            }
            var result = _army.Drop(from, to);
            if (result.Kind == DropKind.Invalid) _view.SnapBack(from);
        }

        private void Cancel()
        {
            if (_from >= 0) _view.SnapBack(_from);
            _from = -1;
            _dragged = null;
            _view.SetHighlight(-1, false);
        }

        private void UpdateHighlight(int target)
        {
            if (target < 0 || target == _from) { _view.SetHighlight(-1, false); return; }
            bool mergeable = MergeService.CanMerge(_army.Grid.Get(_from), _army.Grid.Get(target), 99)
                             && _army.Grid.Get(_from).level < MaxLevel();
            _view.SetHighlight(target, mergeable);
        }

        private int MaxLevel() => Core.ServiceLocator.Get<Data.GameConfig>().grid.maxUnitLevel;

        private bool TryGround(Pointer pointer, out Vector3 point)
        {
            Vector2 sp = pointer.position.ReadValue();
            var ray = _camera.ScreenPointToRay(sp);
            var plane = new Plane(Vector3.up, new Vector3(0f, GridView.TileTop, 0f));
            if (plane.Raycast(ray, out float d))
            {
                point = ray.GetPoint(d);
                return true;
            }
            point = Vector3.zero;
            return false;
        }
    }
}
