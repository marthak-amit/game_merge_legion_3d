using System.Collections.Generic;
using UnityEngine;

namespace BlockBloom.Services.Mock
{
    public sealed class MockAttributionService : IAttributionService
    {
        public void Initialize() { }

        public void LogPurchase(string sku, decimal price, string isoCurrency) =>
            Debug.Log($"[Attribution] purchase {sku} {price} {isoCurrency}");

        public void LogAdRevenue(AdRevenueInfo info) =>
            Debug.Log($"[Attribution] ad revenue {info.Format} {info.RevenueUsd:F4} via {info.Network}");

        public void LogEvent(string name, IReadOnlyDictionary<string, string> parameters = null) { }
    }
}
