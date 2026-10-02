using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Tutorial
{
    /// <summary>
    /// Registry the tutorial overlay uses to find things to point at. UI widgets register their RectTransform,
    /// world objects register a position provider; both resolve to a screen-space rectangle.
    /// </summary>
    public static class TutorialTargets
    {
        private static readonly Dictionary<string, RectTransform> UiTargets = new Dictionary<string, RectTransform>();
        private static readonly Dictionary<string, Func<Vector3>> WorldTargets = new Dictionary<string, Func<Vector3>>();
        private static readonly Vector3[] Corners = new Vector3[4];

        public static void Register(string id, RectTransform rt) => UiTargets[id] = rt;
        public static void RegisterWorld(string id, Func<Vector3> position) => WorldTargets[id] = position;
        public static void Unregister(string id) { UiTargets.Remove(id); WorldTargets.Remove(id); }
        public static void Clear() { UiTargets.Clear(); WorldTargets.Clear(); }

        /// <summary>Screen-space pixel rectangle of the target (overlay canvases use screen coordinates directly).</summary>
        public static bool TryGetScreenRect(string id, Camera worldCamera, out Rect rect)
        {
            rect = default(Rect);
            if (UiTargets.TryGetValue(id, out var rt))
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) return false;
                rt.GetWorldCorners(Corners);
                float xMin = Corners[0].x, yMin = Corners[0].y, xMax = Corners[2].x, yMax = Corners[2].y;
                rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                return true;
            }
            if (WorldTargets.TryGetValue(id, out var func) && worldCamera != null)
            {
                var sp = worldCamera.WorldToScreenPoint(func());
                if (sp.z <= 0f) return false;
                rect = new Rect(sp.x - 70f, sp.y - 70f, 140f, 140f);
                return true;
            }
            return false;
        }
    }
}
