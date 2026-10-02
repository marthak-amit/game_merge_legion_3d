using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Juice: a burst of coins that fly from a screen point into the coin counter (target id "pill_coins").</summary>
    public static class CoinFly
    {
        private static Canvas _canvas;

        public static void Play(Vector2 fromScreen, int count = 10, string targetId = "pill_coins")
        {
            if (!TutorialTargets.TryGetScreenRect(targetId, null, out var target)) return;
            if (_canvas == null)
            {
                _canvas = UIKit.CreateCanvas("[CoinFly]", 450);
                Object.DontDestroyOnLoad(_canvas.gameObject);
            }

            Vector2 to = target.center;
            for (int i = 0; i < count; i++)
            {
                var coin = UIKit.Icon(_canvas.transform, UIKit.Gold, new Vector2(46, 46));
                var rt = coin.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                Vector2 start = fromScreen + new Vector2(Random.Range(-60f, 60f), Random.Range(-40f, 40f));
                Vector2 mid = (start + to) * 0.5f + new Vector2(Random.Range(-250f, 250f), Random.Range(80f, 260f));
                float scale = ScreenToCanvasScale();
                Tween.Value(0.65f, k =>
                {
                    if (rt == null) return;
                    float u = 1f - k;
                    Vector2 p = u * u * start + 2f * u * k * mid + k * k * to;
                    rt.anchoredPosition = p / scale;
                    float s = k < 0.85f ? 1f : 1f - (k - 0.85f) / 0.15f;
                    rt.localScale = new Vector3(s, s, 1f);
                }, Ease.Linear, () =>
                {
                    if (rt != null) Object.Destroy(rt.gameObject);
                    Sfx.Play(SfxId.Coin, 0.3f);
                }, i * 0.04f, rt);
            }
        }

        private static float ScreenToCanvasScale()
        {
            // CanvasScaler(1080x1920, match 0.5): canvas units = pixels / scale
            float sx = Screen.width / 1080f, sy = Screen.height / 1920f;
            return Mathf.Pow(sx, 0.5f) * Mathf.Pow(sy, 0.5f);
        }
    }
}
