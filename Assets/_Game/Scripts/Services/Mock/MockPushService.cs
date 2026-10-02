using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Services.Mock
{
    public sealed class MockPushService : IPushService
    {
        public readonly Dictionary<string, DateTime> Scheduled = new Dictionary<string, DateTime>();

        public void RequestPermission(Action<bool> onComplete) => onComplete?.Invoke(true);

        public void ScheduleLocal(string id, string title, string body, DateTime fireAtUtc)
        {
            Scheduled[id] = fireAtUtc;
            Debug.Log($"[Push] mock schedule {id} at {fireAtUtc:u}: {title}");
        }

        public void CancelLocal(string id) => Scheduled.Remove(id);
        public void CancelAllLocal() => Scheduled.Clear();
        public string GetPushToken() => "mock-token";
    }
}
