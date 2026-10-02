using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MergeLegion.Services.Mock
{
    public sealed class MockAnalyticsService : IAnalyticsService
    {
        /// <summary>Recorded for tests and the in-editor debug overlay.</summary>
        public readonly List<string> Events = new List<string>();
        public bool LogToConsole = true;

        public void Initialize() { }

        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
            var sb = new StringBuilder(name);
            if (parameters != null)
                foreach (var kv in parameters) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            string line = sb.ToString();
            Events.Add(line);
            if (LogToConsole) Debug.Log("[Analytics] " + line);
        }

        public void SetUserProperty(string key, string value) { }
        public void SetUserId(string playerId) { }
    }
}
