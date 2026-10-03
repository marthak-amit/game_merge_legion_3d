using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBloom.Services.Mock
{
    /// <summary>Purchases succeed instantly. Prices are placeholders; the real catalog price comes from the store.</summary>
    public class MockIAPService : IIAPService, IKindAware
    {
        private readonly Dictionary<string, ProductInfo> _products = new Dictionary<string, ProductInfo>();
        private readonly HashSet<string> _owned = new HashSet<string>();

        public bool IsInitialized { get; private set; }

        /// <summary>Test hook: simulates a delivery that did not come from Purchase() (pending purchase, restore).</summary>
        protected void RaiseExternal(PurchaseResult result) => PurchaseCompleted?.Invoke(result);
        public event Action<PurchaseResult> PurchaseCompleted;

        public PurchaseStatus NextStatus = PurchaseStatus.Success;

        /// <summary>Lets the mock show the catalog's real prices instead of a placeholder.</summary>
        public Func<string, decimal> PriceLookup;
        public Func<string, ProductKind> KindLookup { get; set; }

        public void Initialize(IReadOnlyList<string> skus, Action onInitialized = null)
        {
            _products.Clear();
            for (int i = 0; i < skus.Count; i++)
            {
                decimal price = PriceLookup != null ? PriceLookup(skus[i]) : 0.99m;
                _products[skus[i]] = new ProductInfo
                {
                    Sku = skus[i],
                    Kind = KindLookup != null ? KindLookup(skus[i]) : ProductKind.Consumable,
                    LocalizedPrice = "$" + price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    Price = price,
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
