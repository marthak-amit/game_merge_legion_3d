using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.UI
{
    [Serializable]
    public sealed class LocEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class LocFile
    {
        public List<LocEntry> entries = new List<LocEntry>();
    }

    /// <summary>
    /// Thin string-table facade so no UI string is hardcoded. Backed by Resources/Localization/{lang}.json;
    /// the Unity Localization package slots in behind this API in Phase 8.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>();
        private static string _loaded;

        public static void Load(string language)
        {
            if (_loaded == language) return;
            Table.Clear();
            var asset = Resources.Load<TextAsset>("Localization/" + language);
            if (asset == null && language != "en") asset = Resources.Load<TextAsset>("Localization/en");
            if (asset != null) Parse(asset.text);
            _loaded = language;
        }

        public static void Parse(string json)
        {
            var file = JsonUtility.FromJson<LocFile>(json);
            if (file?.entries == null) return;
            foreach (var e in file.entries) Table[e.key] = e.value;
        }

        public static string Get(string key) => Table.TryGetValue(key, out var v) ? v : "#" + key;

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        public static void Reset()
        {
            Table.Clear();
            _loaded = null;
        }
    }
}
