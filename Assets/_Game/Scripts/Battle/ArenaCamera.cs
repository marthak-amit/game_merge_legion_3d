using MergeLegion.Grid;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>Fixed angled camera that auto-frames the arena for any aspect ratio, with shake.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCamera : MonoBehaviour
    {
        public static ArenaCamera Instance { get; private set; }

        private Camera _camera;
        private Vector3 _basePosition;
        private float _shakeAmp, _shakeTime, _shakeDuration;

        public Camera Camera => _camera;

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <param name="bottomInset">Viewport fraction reserved for bottom HUD.</param>
        /// <param name="topInset">Viewport fraction reserved for top HUD.</param>
        public void Frame(ArenaLayout layout, float pitchDegrees = 58f, float fov = 36f, float bottomInset = 0.27f, float topInset = 0.10f)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _camera.fieldOfView = fov;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 120f;

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            var dir = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch));
            var rot = Quaternion.LookRotation(-dir, Vector3.up);
            float centerZ = (layout.NearZ + layout.FarZ) * 0.5f;
            var target = new Vector3(0f, 0f, centerZ);

            var corners = new[]
            {
                new Vector3(-layout.HalfWidth, 0f, layout.NearZ), new Vector3(layout.HalfWidth, 0f, layout.NearZ),
                new Vector3(-layout.HalfWidth, 0f, layout.FarZ), new Vector3(layout.HalfWidth, 0f, layout.FarZ)
            };

            float lo = 5f, hi = 90f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                transform.SetPositionAndRotation(target + dir * mid, rot);
                bool fits = true;
                for (int c = 0; c < corners.Length && fits; c++)
                {
                    var vp = _camera.WorldToViewportPoint(corners[c]);
                    fits = vp.z > 0f && vp.x >= 0.02f && vp.x <= 0.98f && vp.y >= bottomInset && vp.y <= 1f - topInset;
                }
                if (fits) hi = mid; else lo = mid;
            }
            transform.SetPositionAndRotation(target + dir * hi, rot);
            _basePosition = transform.position;
        }

        public void Shake(float amplitude, float duration)
        {
            _shakeAmp = Mathf.Max(_shakeAmp, amplitude);
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        private void LateUpdate()
        {
            if (_shakeTime <= 0f)
            {
                if (_shakeAmp > 0f) { transform.position = _basePosition; _shakeAmp = 0f; }
                return;
            }
            _shakeTime -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_shakeTime / _shakeDuration);
            float a = _shakeAmp * k;
            transform.position = _basePosition + new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
        }
    }
}
