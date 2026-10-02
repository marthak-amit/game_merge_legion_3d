using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Data
{
    /// <summary>One arena theme (section 1.4). Colours are hex strings so the JSON stays readable and Remote-Config friendly.</summary>
    [Serializable]
    public sealed class ThemeData
    {
        public string id;
        public string nameKey;
        public string ground;
        public string tileA;
        public string tileB;
        public string sky;
        public string fog;
        public string light;
        public float lightIntensity = 1.1f;
        public string prop;          // tree, cactus, pine, reed, rock, wall, tomb, cloud, pillar, orb
        public string propColor;
        public string accent;
        public string addressKey;    // optional Addressables prefab that replaces the procedural props

        public Color Ground => BalanceBuilder.ParseColor(ground);
        public Color TileA => BalanceBuilder.ParseColor(tileA);
        public Color TileB => BalanceBuilder.ParseColor(tileB);
        public Color Sky => BalanceBuilder.ParseColor(sky);
        public Color Fog => BalanceBuilder.ParseColor(fog);
        public Color Light => BalanceBuilder.ParseColor(light);
        public Color PropColor => BalanceBuilder.ParseColor(propColor);
        public Color Accent => BalanceBuilder.ParseColor(accent);
    }

    [Serializable]
    public sealed class ThemeFile
    {
        public List<ThemeData> themes = new List<ThemeData>();
    }

    public static class ThemeLibrary
    {
        private static Dictionary<string, ThemeData> _byId;

        public static ThemeData Get(string id)
        {
            Load();
            if (id != null && _byId.TryGetValue(id, out var t)) return t;
            return _byId.Count > 0 ? First() : Fallback();
        }

        public static IEnumerable<ThemeData> All()
        {
            Load();
            return _byId.Values;
        }

        public static void Reset() => _byId = null;

        private static ThemeData First()
        {
            foreach (var kv in _byId) return kv.Value;
            return Fallback();
        }

        private static ThemeData Fallback() => new ThemeData
        {
            id = "grasslands", nameKey = "theme.grasslands", ground = "#5B8F52", tileA = "#3F5F3C", tileB = "#4A6E46",
            sky = "#8FC3EB", fog = "#B7DDF2", light = "#FFF4D8", prop = "tree", propColor = "#2F7A3A", accent = "#7A5A3A"
        };

        private static void Load()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, ThemeData>();
            var asset = Resources.Load<TextAsset>("Config/themes");
            if (asset == null) return;
            var file = JsonUtility.FromJson<ThemeFile>(asset.text);
            if (file?.themes == null) return;
            foreach (var t in file.themes) _byId[t.id] = t;
        }
    }
}
