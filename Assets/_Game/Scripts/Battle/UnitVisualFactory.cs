using MergeLegion.Core;
using MergeLegion.Data;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>
    /// Creates (pooled) unit models. Uses UnitLevelData.prefab when assigned (Synty/Mixamo swap, zero code change),
    /// otherwise builds a colored primitive silhouette per line.
    /// </summary>
    public static class UnitVisualFactory
    {
        public static readonly Color EnemyShade = new Color(0.55f, 0.08f, 0.14f, 1f);

        public static Color TeamTint(Color tint, bool enemy) => enemy ? Color.Lerp(tint, EnemyShade, 0.6f) : tint;

        public static UnitVisual Get(UnitLineData line, int level, bool enemy, Transform parent = null)
        {
            int key = (((int)line.id * 16 + level) * 2 + (enemy ? 1 : 0)) + 1000;
            var data = line.GetLevel(level);
            var go = GoPool.Get(key, () => Build(line, data, enemy), parent);
            var v = go.GetComponent<UnitVisual>();
            go.transform.localScale = Vector3.one * data.scale;
            v.BaseScale = data.scale;
            return v;
        }

        public static void Release(UnitVisual v)
        {
            if (v == null) return;
            v.HideLabel();
            GoPool.Release(v.gameObject);
        }

        private static GameObject Build(UnitLineData line, UnitLevelData data, bool enemy)
        {
            Color tint = TeamTint(data.tint, enemy);
            GameObject root;
            if (data.prefab != null)
            {
                root = Object.Instantiate(data.prefab);
            }
            else
            {
                root = new GameObject("Unit_" + line.id + "_L" + data.level);
                var body = MaterialLibrary.Lit(tint);
                var accent = MaterialLibrary.Lit(Color.Lerp(tint, Color.white, 0.55f));
                switch (line.id)
                {
                    case UnitLineId.Melee:
                        Part(PrimitiveType.Capsule, root, body, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.5f, 0.6f));
                        Part(PrimitiveType.Cube, root, accent, new Vector3(0.42f, 0.6f, 0.15f), new Vector3(0.12f, 0.8f, 0.12f));
                        break;
                    case UnitLineId.Ranged:
                        Part(PrimitiveType.Cylinder, root, body, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f));
                        Part(PrimitiveType.Sphere, root, accent, new Vector3(0, 1.0f, 0), new Vector3(0.4f, 0.4f, 0.4f));
                        Part(PrimitiveType.Cube, root, accent, new Vector3(-0.38f, 0.6f, 0.1f), new Vector3(0.06f, 0.8f, 0.06f));
                        break;
                    case UnitLineId.Tank:
                        Part(PrimitiveType.Cube, root, body, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.8f, 0.8f));
                        Part(PrimitiveType.Cube, root, accent, new Vector3(0, 0.5f, 0.48f), new Vector3(0.7f, 0.7f, 0.12f));
                        Part(PrimitiveType.Sphere, root, accent, new Vector3(0, 1.05f, 0), new Vector3(0.4f, 0.4f, 0.4f));
                        break;
                    default:
                        Part(PrimitiveType.Sphere, root, body, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.5f, 0.7f));
                        var w1 = Part(PrimitiveType.Cube, root, accent, new Vector3(0.5f, 0.6f, 0), new Vector3(0.6f, 0.06f, 0.4f));
                        w1.localRotation = Quaternion.Euler(0, 0, 18);
                        var w2 = Part(PrimitiveType.Cube, root, accent, new Vector3(-0.5f, 0.6f, 0), new Vector3(0.6f, 0.06f, 0.4f));
                        w2.localRotation = Quaternion.Euler(0, 0, -18);
                        break;
                }
            }

            var v = root.AddComponent<UnitVisual>();
            v.Line = (int)line.id;
            v.Level = data.level;
            v.Tint = tint;
            v.HoverHeight = line.isFlying ? 1.1f : 0f;
            return root;
        }

        private static Transform Part(PrimitiveType type, GameObject root, Material mat, Vector3 pos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(type);
            var col = p.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            p.transform.SetParent(root.transform, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return p.transform;
        }
    }
}
