using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Grid;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>
    /// Dresses the arena for a theme: ground, fog, light and props. Props are procedural primitives by default;
    /// when the Addressables define is on, a prefab at ThemeData.addressKey replaces them (keeps the install small).
    /// </summary>
    public sealed class ArenaThemeBuilder : MonoBehaviour
    {
        private ArenaLayout _layout;
        private Camera _camera;
        private Light _light;
        private Renderer _ground;
        private GridView _grid;
        private Transform _props;
        private string _appliedId;
#if MERGELEGION_ADDRESSABLES
        private UnityEngine.GameObject _addressablesInstance;
#endif

        public void Init(ArenaLayout layout, Camera cam, Light light, GridView grid)
        {
            _layout = layout;
            _camera = cam;
            _light = light;
            _grid = grid;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            float width = _layout.HalfWidth * 2f + 14f;
            float depth = _layout.FarZ - _layout.NearZ + 14f;
            ground.transform.position = new Vector3(0f, -0.15f, (_layout.NearZ + _layout.FarZ) * 0.5f);
            ground.transform.localScale = new Vector3(width, 0.3f, depth);
            _ground = ground.GetComponent<Renderer>();
            _ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Apply(string themeId)
        {
            if (_appliedId == themeId) return;
            _appliedId = themeId;
            var t = ThemeLibrary.Get(themeId);

            _ground.sharedMaterial = MaterialLibrary.Lit(t.Ground);
            _camera.backgroundColor = t.Sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = t.Fog;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 62f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(t.Sky, Color.white, 0.45f);
            if (_light != null) { _light.color = t.Light; _light.intensity = t.lightIntensity; }
            _grid.SetTileColors(t.TileA, t.TileB);

            BuildProps(t);
        }

        private void BuildProps(ThemeData t)
        {
            if (_props != null) Destroy(_props.gameObject);
#if MERGELEGION_ADDRESSABLES
            if (_addressablesInstance != null) { UnityEngine.AddressableAssets.Addressables.ReleaseInstance(_addressablesInstance); _addressablesInstance = null; }
            if (!string.IsNullOrEmpty(t.addressKey)) TryLoadAddressable(t);
#endif
            _props = new GameObject("Props").transform;
            _props.SetParent(transform, false);

            var rng = new DeterministicRng(t.id.GetHashCode() & 0x7fffffff);
            float zMin = _layout.NearZ - 3f, zMax = _layout.FarZ + 3f;
            int sideCount = 12;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < sideCount; i++)
                {
                    float x = side * (_layout.HalfWidth + 1.2f + rng.NextFloat() * 3.2f);
                    float z = Mathf.Lerp(zMin, zMax, (i + rng.NextFloat() * 0.8f) / sideCount);
                    MakeProp(t, new Vector3(x, 0f, z), rng);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-_layout.HalfWidth - 3f, _layout.HalfWidth + 3f, (i + rng.NextFloat() * 0.6f) / 7f);
                MakeProp(t, new Vector3(x, 0f, _layout.FarZ + 2.5f + rng.NextFloat() * 2f), rng);
            }
        }

#if MERGELEGION_ADDRESSABLES
        private void TryLoadAddressable(ThemeData t)
        {
            var handle = UnityEngine.AddressableAssets.Addressables.InstantiateAsync(t.addressKey, transform);
            handle.Completed += h =>
            {
                if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && _appliedId == t.id)
                {
                    _addressablesInstance = h.Result;
                    if (_props != null) _props.gameObject.SetActive(false);
                }
            };
        }
#endif

        private void MakeProp(ThemeData t, Vector3 pos, DeterministicRng rng)
        {
            var body = MaterialLibrary.Lit(Color.Lerp(t.PropColor, Color.black, rng.NextFloat() * 0.25f));
            var accent = MaterialLibrary.Lit(t.Accent);
            var root = new GameObject(t.prop).transform;
            root.SetParent(_props, false);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
            float s = rng.Range(0.8f, 1.5f);

            switch (t.prop)
            {
                case "cactus":
                    Part(PrimitiveType.Capsule, root, body, new Vector3(0, 0.9f * s, 0), new Vector3(0.45f, 0.9f, 0.45f) * s);
                    Part(PrimitiveType.Capsule, root, body, new Vector3(0.4f * s, 1.0f * s, 0), new Vector3(0.25f, 0.4f, 0.25f) * s);
                    break;
                case "pine":
                    Part(PrimitiveType.Cylinder, root, accent, new Vector3(0, 0.4f * s, 0), new Vector3(0.18f, 0.4f, 0.18f) * s);
                    Part(PrimitiveType.Capsule, root, body, new Vector3(0, 1.5f * s, 0), new Vector3(0.9f, 1.1f, 0.9f) * s);
                    break;
                case "reed":
                    for (int i = 0; i < 4; i++)
                        Part(PrimitiveType.Cylinder, root, body, new Vector3(rng.Range(-0.3f, 0.3f), 0.6f * s, rng.Range(-0.3f, 0.3f)), new Vector3(0.07f, 0.6f * rng.Range(0.8f, 1.4f), 0.07f) * s);
                    Part(PrimitiveType.Cylinder, root, accent, new Vector3(0, 0.02f, 0), new Vector3(1.3f, 0.02f, 1.3f) * s);
                    break;
                case "rock":
                    var rock = Part(PrimitiveType.Cube, root, body, new Vector3(0, 0.5f * s, 0), new Vector3(0.9f, 1.0f, 0.8f) * s);
                    rock.localRotation = Quaternion.Euler(rng.Range(-10f, 10f), rng.Range(0f, 90f), rng.Range(-10f, 10f));
                    Part(PrimitiveType.Cube, root, accent, new Vector3(0.7f * s, 0.04f, 0.2f), new Vector3(0.9f, 0.05f, 0.5f) * s);
                    break;
                case "wall":
                    Part(PrimitiveType.Cube, root, body, new Vector3(0, 0.6f * s, 0), new Vector3(1.8f, 1.2f, 0.6f) * s);
                    for (int i = -1; i <= 1; i++) Part(PrimitiveType.Cube, root, body, new Vector3(i * 0.6f * s, 1.3f * s, 0), new Vector3(0.4f, 0.3f, 0.6f) * s);
                    Part(PrimitiveType.Cube, root, accent, new Vector3(0, 1.1f * s, 0.32f), new Vector3(0.5f, 0.35f, 0.04f) * s);
                    break;
                case "tomb":
                    Part(PrimitiveType.Cube, root, body, new Vector3(0, 0.45f * s, 0), new Vector3(0.55f, 0.9f, 0.18f) * s);
                    Part(PrimitiveType.Sphere, root, body, new Vector3(0, 0.95f * s, 0), new Vector3(0.55f, 0.4f, 0.18f) * s);
                    Part(PrimitiveType.Sphere, root, accent, new Vector3(0, 0.1f, 0.3f), new Vector3(0.18f, 0.18f, 0.18f) * s);
                    break;
                case "cloud":
                    pos.y = rng.Range(-1.5f, -0.6f);
                    root.position = pos;
                    for (int i = 0; i < 3; i++)
                        Part(PrimitiveType.Sphere, root, body, new Vector3((i - 1) * 0.9f * s, rng.Range(0f, 0.3f), rng.Range(-0.3f, 0.3f)), new Vector3(1.6f, 1.0f, 1.4f) * s);
                    break;
                case "pillar":
                    Part(PrimitiveType.Cylinder, root, body, new Vector3(0, 1.8f * s, 0), new Vector3(0.7f, 1.8f, 0.7f) * s);
                    Part(PrimitiveType.Sphere, root, accent, new Vector3(0, 3.8f * s, 0), new Vector3(0.45f, 0.45f, 0.45f) * s);
                    break;
                case "orb":
                    Part(PrimitiveType.Cylinder, root, MaterialLibrary.Lit(Color.white), new Vector3(0, 0.5f * s, 0), new Vector3(0.35f, 0.5f, 0.35f) * s);
                    Part(PrimitiveType.Sphere, root, body, new Vector3(0, 1.5f * s, 0), new Vector3(0.7f, 0.7f, 0.7f) * s);
                    break;
                default: // tree
                    Part(PrimitiveType.Cylinder, root, accent, new Vector3(0, 0.5f * s, 0), new Vector3(0.22f, 0.5f, 0.22f) * s);
                    Part(PrimitiveType.Sphere, root, body, new Vector3(0, 1.45f * s, 0), new Vector3(1.2f, 1.1f, 1.2f) * s);
                    break;
            }
        }

        private static Transform Part(PrimitiveType type, Transform parent, Material mat, Vector3 localPos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(type);
            var col = p.GetComponent<Collider>();
            if (col != null) Destroy(col);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return p.transform;
        }
    }
}
