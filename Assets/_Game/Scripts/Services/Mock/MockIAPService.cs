using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Services.Mock
{
    /// <summary>Purchases succeed instantly. Prices are placeholders; the real catalog price comes from the store.</summary>
    public sealed class MockIAPService : IIAPService
    {
        private readonly Dictionary<string, ProductInfo> _products = new Dictionary<string, ProductInfo>();
        private readonly HashSet<string> _owned = new HashSet<string>();

        public bool IsInitialized { get; private set; }
        public event Action<PurchaseResult> PurchaseCompleted;

        public PurchaseStatus NextStatus = PurchaseStatus.Success;

        public void Initialize(IReadOnlyList<string> skus, Action onInitialized = null)
        {
            _products.Clear();
            for (int i = 0; i < skus.Count; i++)
            {
                _products[skus[i]] = new ProductInfo
                {
                    Sku = skus[i],
                    Kind = ProductKind.Consumable,
                    LocalizedPrice = "$0.99",
                    Price = 0.99m,
                    IsoCurrency = "USD"
                };
            }
            IsInitialized = true;
            onInitialized?.Invoke();
        }

        public ProductInfo GetProduct(string sku) => _products.TryGetValue(sku, out var p) ? p : null;

        public bool IsOwned(string sku) => _owned.Contains(sku);

        public void Purchase(string sku, Action<PurchaseResult> onComplete)
        {
            PurchaseResult result;
            if (!_products.ContainsKey(sku)) result = new PurchaseResult(PurchaseStatus.Failed, sku);
            else if (NextStatus == PurchaseStatus.Success)
            {
                _owned.Add(sku);
                result = new PurchaseResult(PurchaseStatus.Success, sku, "mock-receipt-" + sku);
            }
            else result = new PurchaseResult(NextStatus, sku);

            Debug.Log($"[IAP] mock purchase {sku}: {result.Status}");
            PurchaseCompleted?.Invoke(result);
            onComplete?.Invoke(result);
        }

        public void RestorePurchases(Action<bool> onComplete) => onComplete?.Invoke(true);
    }
}
