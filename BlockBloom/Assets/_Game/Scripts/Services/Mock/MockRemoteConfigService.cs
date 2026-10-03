using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BlockBloom.Services.Mock
{
    [Serializable]
    public sealed class RemoteConfigEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class RemoteConfigFile
    {
        public List<RemoteConfigEntry> entries = new List<RemoteConfigEntry>();
    }

    /// <summary>Serves values from the bundled defaults JSON, with runtime overrides for tests/debug.</summary>
    public sealed class MockRemoteConfigService : IRemoteConfigService
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public bool IsReady { get; private set; }

        public MockRemoteConfigService(string defaultsJson = null)
        {
            if (string.IsNullOrEmpty(defaultsJson)) return;
            var file = JsonUtility.FromJson<RemoteConfigFile>(defaultsJson);
            if (file?.entries == null) return;
            foreach (var e in file.entries) _values[e.key] = e.value;
        }

        public void Fetch(Action<bool> onComplete)
        {
            IsReady = true;
            onComplete?.Invoke(true);
        }

        public void SetOverride(string key, string value) => _values[key] = value;

        public int GetInt(string key, int fallback = 0) =>
            _values.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public long GetLong(string key, long fallback = 0) =>
            _values.TryGetValue(key, out var s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public float GetFloat(string key, float fallback = 0f) =>
            _values.TryGetValue(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public bool GetBool(string key, bool fallback = false) =>
            _values.TryGetValue(key, out var s) && bool.TryParse(s, out var v) ? v : fallback;

        public string GetString(string key, string fallback = "") =>
            _values.TryGetValue(key, out var s) ? s : fallback;
    }
}
