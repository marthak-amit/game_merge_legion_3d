#if MERGELEGION_IAP
using System;
using System.Collections.Generic;
using MergeLegion.Services;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace MergeLegion.Sdk
{
    /// <summary>
    /// Unity IAP v4 (com.unity.purchasing 4.12.x) adapter. Purchases stay "pending" until IapManager has validated the
    /// receipt and delivered the goods and then calls ConfirmPurchase, so a crash mid-delivery never loses a purchase.
    /// </summary>
    public static class IapRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformServiceOverrides.Iap = () => new UnityIapService();
            PlatformServiceOverrides.Receipts = () => new CloudCodeReceiptValidator();
        }
    }

    public sealed class UnityIapService : IIAPService, IConfirmingIapService, IKindAware, IDetailedStoreListener
    {
        private IStoreController _controller;
        private IExtensionProvider _extensions;
        private Action _initDone;
        private string _purchasing;

        public bool IsInitialized => _controller != null;
        public event Action<PurchaseResult> PurchaseCompleted;
        public Func<string, ProductKind> KindLookup { get; set; }

        public void Initialize(IReadOnlyList<string> skus, Action onInitialized = null)
        {
            _initDone = onInitialized;
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var sku in skus)
            {
                var kind = KindLookup != null ? KindLookup(sku) : ProductKind.Consumable;
                builder.AddProduct(sku, kind == ProductKind.Consumable ? ProductType.Consumable
                    : kind == ProductKind.NonConsumable ? ProductType.NonConsumable : ProductType.Subscription);
            }
            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            _initDone?.Invoke();
            _initDone = null;
        }

        public void OnInitializeFailed(InitializationFailureReason error) => FailInit(error.ToString());
        public void OnInitializeFailed(InitializationFailureReason error, string message) => FailInit(error + " " + message);

        private void FailInit(string why)
        {
            Debug.LogWarning("[IAP] initialisation failed: " + why);
            _initDone?.Invoke(); // the game keeps running; the shop shows catalog prices and purchases fail gracefully
            _initDone = null;
        }

        public ProductInfo GetProduct(string sku)
        {
            var p = _controller?.products.WithID(sku);
            if (p == null || !p.availableToPurchase) return null;
            return new ProductInfo
            {
                Sku = sku,
                Kind = p.definition.type == ProductType.Subscription ? ProductKind.Subscription
                    : p.definition.type == ProductType.NonConsumable ? ProductKind.NonConsumable : ProductKind.Consumable,
                LocalizedPrice = p.metadata.localizedPriceString,
                Price = p.metadata.localizedPrice,
                IsoCurrency = p.metadata.isoCurrencyCode
            };
        }

        public bool IsOwned(string sku)
        {
            var p = _controller?.products.WithID(sku);
            return p != null && p.hasReceipt;
        }

        public void Purchase(string sku, Action<PurchaseResult> onComplete)
        {
            if (_controller == null) { Deliver(new PurchaseResult(PurchaseStatus.Failed, sku), onComplete); return; }
            _purchasing = sku;
            _controller.InitiatePurchase(sku);
        }

        public void RestorePurchases(Action<bool> onComplete)
        {
#if UNITY_IOS
            _extensions.GetExtension<IAppleExtensions>().RestoreTransactions((ok, message) => onComplete?.Invoke(ok));
#else
            onComplete?.Invoke(true); // Google Play redelivers owned items through ProcessPurchase on initialisation
#endif
        }

        public void ConfirmPurchase(string sku)
        {
            var p = _controller?.products.WithID(sku);
            if (p != null) _controller.ConfirmPendingPurchase(p);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var p = args.purchasedProduct;
            PurchaseCompleted?.Invoke(new PurchaseResult(PurchaseStatus.Success, p.definition.id, p.receipt));
            return PurchaseProcessingResult.Pending;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) => FailPurchase(product.definition.id, reason);
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription description) => FailPurchase(product.definition.id, description.reason);

        private void FailPurchase(string sku, PurchaseFailureReason reason)
        {
            var status = reason == PurchaseFailureReason.UserCancelled ? PurchaseStatus.Cancelled
                : reason == PurchaseFailureReason.DuplicateTransaction ? PurchaseStatus.AlreadyOwned : PurchaseStatus.Failed;
            PurchaseCompleted?.Invoke(new PurchaseResult(status, sku));
        }

        private static void Deliver(PurchaseResult r, Action<PurchaseResult> cb) { cb?.Invoke(r); }
    }

    /// <summary>
    /// Server-side receipt validation via Unity Cloud Code (script "ValidateReceipt", see CloudCode/ValidateReceipt.js).
    /// Falls back to accepting the receipt when the backend is unreachable, so players are never blocked from goods they paid for.
    /// </summary>
    public sealed class CloudCodeReceiptValidator : IReceiptValidator
    {
        public void Validate(string sku, string receipt, Action<bool> onResult)
        {
#if MERGELEGION_UGS
            Run(sku, receipt, onResult);
#else
            onResult?.Invoke(true);
#endif
        }

#if MERGELEGION_UGS
        private static async void Run(string sku, string receipt, Action<bool> onResult)
        {
            try
            {
                await Unity.Services.Core.UnityServices.InitializeAsync();
                bool ok = await Unity.Services.CloudCode.CloudCodeService.Instance.CallEndpointAsync<bool>("ValidateReceipt",
                    new Dictionary<string, object> { { "sku", sku }, { "receipt", receipt }, { "store", Application.platform == RuntimePlatform.IPhonePlayer ? "apple" : "google" } });
                onResult?.Invoke(ok);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IAP] receipt validation unavailable, accepting: " + e.Message);
                onResult?.Invoke(true);
            }
        }
#endif
    }
}
#endif
