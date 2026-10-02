using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>
    /// All health bars in a single dynamic mesh (one draw call). Bars appear for damaged units and bosses.
    /// Vertex buffers are preallocated; nothing is allocated per frame.
    /// </summary>
    public sealed class HealthBarBatch : MonoBehaviour
    {
        private const int Capacity = 320;
        private const float Width = 1.1f, Height = 0.14f;

        private Mesh _mesh;
        private Vector3[] _verts;
        private Color32[] _colors;
        private int _lastUsed;
        private Camera _camera;
        private BattleView _view;

        public void Init(Camera cam, BattleView view)
        {
            _camera = cam;
            _view = view;
            _verts = new Vector3[Capacity * 8];
            _colors = new Color32[Capacity * 8];
            var tris = new int[Capacity * 12];
            for (int q = 0; q < Capacity * 2; q++)
            {
                int v = q * 4, t = q * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }

            _mesh = new Mesh { name = "HealthBars" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.colors32 = _colors;
            _mesh.triangles = tris;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 50f, 200f));

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialLibrary.Bars;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        public void Draw(BattleSim sim)
        {
            if (_mesh == null || sim == null) return;
            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var toCam = -_camera.transform.forward * 0.02f;

            int bars = 0;
            var units = sim.Units;
            for (int i = 0; i < units.Count && bars < Capacity; i++)
            {
                var u = units[i];
                if (!u.Alive || (u.Hp >= u.MaxHp && !u.IsBoss)) continue;
                if (!_view.TryGetWorldPosition(u.Id, out var p)) continue;

                float w = u.IsBoss ? Width * 2.4f : Width * Mathf.Clamp(0.7f + u.Scale * 0.3f, 0.8f, 1.4f);
                float h = u.IsBoss ? Height * 1.6f : Height;
                var center = p + Vector3.up * _view.HeadHeight(u.Id) + toCam;
                float frac = Mathf.Clamp01(u.Hp / u.MaxHp);

                Color32 fill = u.IsBoss ? new Color32(255, 120, 30, 255)
                    : u.Team == Team.Player ? new Color32(80, 220, 110, 255) : new Color32(235, 70, 70, 255);

                int b = bars * 8;
                Quad(b, center, right * (w * 0.5f), up * (h * 0.5f), new Color32(0, 0, 0, 170));
                var left = center - right * (w * 0.5f);
                Quad(b + 4, left + right * (w * frac * 0.5f) + toCam, right * (w * frac * 0.5f), up * (h * 0.38f), fill);
                bars++;
            }

            // collapse bars that were visible last frame but not now (degenerate quads cost nothing)
            for (int i = bars * 8; i < _lastUsed * 8; i++) _verts[i] = Vector3.zero;
            _lastUsed = bars;

            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
        }

        private void Quad(int index, Vector3 center, Vector3 halfRight, Vector3 halfUp, Color32 color)
        {
            _verts[index] = center - halfRight - halfUp;
            _verts[index + 1] = center - halfRight + halfUp;
            _verts[index + 2] = center + halfRight + halfUp;
            _verts[index + 3] = center + halfRight - halfUp;
            _colors[index] = _colors[index + 1] = _colors[index + 2] = _colors[index + 3] = color;
        }
    }
}
