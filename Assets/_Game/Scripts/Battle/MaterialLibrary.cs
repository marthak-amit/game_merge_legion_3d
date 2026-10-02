using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>
    /// Shared materials. Lit materials are cloned from Resources/Materials/UnitLit (made by the scene generator, so the shader
    /// is guaranteed to be in builds) and cached per colour so identical units batch and instance together.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<int, Material> LitCache = new Dictionary<int, Material>();
        private static Material _template;
        private static Material _particle;
        private static Material _barMaterial;
        private static Texture2D _softCircle;

        private static Material Template
        {
            get
            {
                if (_template != null) return _template;
                _template = Resources.Load<Material>("Materials/UnitLit");
                if (_template == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    _template = new Material(shader);
                }
                return _template;
            }
        }

        public static Material Lit(Color c)
        {
            int key = Pack(c);
            if (LitCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template) { enableInstancing = true, name = "Lit_" + key.ToString("X8") };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            LitCache[key] = m;
            return m;
        }

        /// <summary>Additive-looking soft particle material (vertex colour * soft circle texture).</summary>
        public static Material Particle
        {
            get
            {
                if (_particle != null) return _particle;
                _particle = new Material(Shader.Find("Sprites/Default")) { name = "Particle" };
                _particle.mainTexture = SoftCircle;
                return _particle;
            }
        }

        /// <summary>Unlit vertex-coloured material for the batched health bars.</summary>
        public static Material Bars
        {
            get
            {
                if (_barMaterial != null) return _barMaterial;
                _barMaterial = new Material(Shader.Find("Sprites/Default")) { name = "HealthBars" };
                return _barMaterial;
            }
        }

        public static Texture2D SoftCircle
        {
            get
            {
                if (_softCircle != null) return _softCircle;
                const int n = 32;
                _softCircle = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                    }
                }
                _softCircle.SetPixels32(px);
                _softCircle.Apply();
                return _softCircle;
            }
        }

        public static void Clear()
        {
            LitCache.Clear();
        }

        private static int Pack(Color c)
        {
            int r = (int)(Mathf.Clamp01(c.r) * 255f), g = (int)(Mathf.Clamp01(c.g) * 255f),
                b = (int)(Mathf.Clamp01(c.b) * 255f), a = (int)(Mathf.Clamp01(c.a) * 255f);
            return (a << 24) | (r << 16) | (g << 8) | b;
        }
    }
}
