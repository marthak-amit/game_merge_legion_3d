using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Keeps a RectTransform inside the device safe area (notches, rounded corners, gesture bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _last;
        private Vector2Int _lastSize;

        private void OnEnable() => Apply();
        private void Update() => Apply();

        private void Apply()
        {
            var area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (area == _last && size == _lastSize) return;
            _last = area;
            _lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            rt.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
