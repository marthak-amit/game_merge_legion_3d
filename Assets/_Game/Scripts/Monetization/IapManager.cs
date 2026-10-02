using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Meta;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Monetization
{
    public readonly struct PurchaseFulfilledEvent
    {
        public readonly string Sku;
        public PurchaseFulfilledEvent(string sku) { Sku = sku; }
    }

    /// <summary>
    /// Purchase flow on top of IIAPService: catalog lookup, receipt validation hook, fulfilment through the
    /// RewardGranter, one-time/ownership tracking, analytics and attribution. Prices/contents all come from config.
    /// </summary>
    public sealed class IapManager
    {
        private readonly IIAPService _iap;
        private readonly MonetizationConfig _cfg;
        private readonly RewardGranter _granter;
        private readonly SaveService _save;
        private readonly IAnalyticsService _analytics;
        private readonly IAttributionService _attribution;
        private readonly IReceiptValidator _validator;
        private readonly AdsManager _ads;
        private readonly Dictionary<string, Action<ProductDef>> _special = new Dictionary<string, Action<ProductDef>>();

        public IapManager(IIAPService iap, MonetizationConfig cfg, RewardGranter granter, SaveService save,
            IAnalyticsService analytics, IAttributionService attribution, IReceiptValidator validator, AdsManager ads)
        {
            _iap = iap;
            _cfg = cfg;
            _granter = granter;
            _save = save;
            _analytics = analytics;
            _attribution = attribution;
            _validator = validator;
            _ads = ads;
            _iap.PurchaseCompleted += OnStorePurchase;
        }

        public IReadOnlyList<ProductDef> Products => _cfg.products;

        public List<string> Skus()
        {
            var list = new List<string>();
            foreach (var p in _cfg.products) list.Add(p.sku);
            return list;
        }

        /// <summary>Registers custom fulfilment (battle pass, VIP, piggy bank, commander bundles) for a SKU.</summary>
        public void RegisterSpecial(string sku, Action<ProductDef> fulfil) => _special[sku] = fulfil;

        public ProductDef Product(string sku) => _cfg.Product(sku);

        public bool Owned(string sku) => _save.Data.purchasedSkus.Contains(sku);

        public string PriceLabel(string sku)
        {
            var info = _iap.GetProduct(sku);
            if (info != null && !string.IsNullOrEmpty(info.LocalizedPrice)) return info.LocalizedPrice;
            var def = _cfg.Product(sku);
            return def != null ? "$" + def.priceUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "";
        }

        public void LogView(string sku)
        {
            _analytics.LogEvent(AnalyticsEvents.IapView, new Dictionary<string, object> { { AnalyticsParams.Sku, sku } });
        }

        private readonly Dictionary<string, Action<PurchaseResult>> _pending = new Dictionary<string, Action<PurchaseResult>>();

        public void Purchase(string sku, Action<PurchaseResult> onDone)
        {
            var def = _cfg.Product(sku);
            if (def == null) { onDone?.Invoke(new PurchaseResult(PurchaseStatus.Failed, sku)); return; }
            if (def.oneTime && Owned(sku)) { onDone?.Invoke(new PurchaseResult(PurchaseStatus.AlreadyOwned, sku)); return; }

            _pending[sku] = onDone;
            _iap.Purchase(sku, null); // the result arrives through PurchaseCompleted so late/restored deliveries use the same path
        }

        /// <summary>
        /// Single entry point for every store delivery: user purchases, purchases interrupted by an app kill, and restores.
        /// </summary>
        private void OnStorePurchase(PurchaseResult result)
        {
            _pending.TryGetValue(result.Sku, out var callback);
            _pending.Remove(result.Sku);

            if (!result.Success) { callback?.Invoke(result); return; }

            var def = _cfg.Product(result.Sku);
            if (def == null) { Confirm(result.Sku); callback?.Invoke(new PurchaseResult(PurchaseStatus.Failed, result.Sku)); return; }

            _validator.Validate(result.Sku, result.Receipt, valid =>
            {
                if (!valid)
                {
                    Confirm(result.Sku); // do not let the store redeliver a receipt we rejected
                    callback?.Invoke(new PurchaseResult(PurchaseStatus.Failed, result.Sku));
                    return;
                }

                // a one-time product delivered twice (restore, reinstall) must not pay out twice
                bool alreadyOwned = def.oneTime && Owned(def.sku);
                if (!alreadyOwned) Fulfil(def);
                Confirm(result.Sku);

                var info = _iap.GetProduct(result.Sku);
                decimal price = info != null ? info.Price : (decimal)def.priceUsd;
                string currency = info != null ? info.IsoCurrency : "USD";
                if (!alreadyOwned)
                {
                    _analytics.LogEvent(AnalyticsEvents.IapPurchase, new Dictionary<string, object>
                    {
                        { AnalyticsParams.Sku, result.Sku },
                        { AnalyticsParams.Price, price }
                    });
                    _attribution.LogPurchase(result.Sku, price, currency);
                }
                callback?.Invoke(alreadyOwned ? new PurchaseResult(PurchaseStatus.AlreadyOwned, result.Sku) : result);
            });
        }

        private void Confirm(string sku)
        {
            if (_iap is IConfirmingIapService confirming) confirming.ConfirmPurchase(sku);
        }

        private void Fulfil(ProductDef def)
        {
            if ((def.oneTime || def.kind == ProductKind.NonConsumable) && !_save.Data.purchasedSkus.Contains(def.sku))
                _save.Data.purchasedSkus.Add(def.sku);

            if (_special.TryGetValue(def.sku, out var special)) special(def);
            if (def.rewards.Count > 0) _granter.Grant(def.rewards, "iap_" + def.sku);

            _ads.RefreshEntitlements();
            _save.Save(); // purchases are a key event: persist immediately
            EventBus.Publish(new PurchaseFulfilledEvent(def.sku));
        }

        /// <summary>Restore Purchases (App Store requirement): re-applies non-consumable entitlements.</summary>
        public void Restore(Action<bool, int> onDone)
        {
            _iap.RestorePurchases(ok =>
            {
                int restored = 0;
                foreach (var sku in new List<string>(_save.Data.purchasedSkus))
                {
                    var def = _cfg.Product(sku);
                    if (def == null || def.kind != ProductKind.NonConsumable) continue;
                    if (def.sku == "no_ads") { _save.Data.noAds = true; restored++; }
                }
                _ads.RefreshEntitlements();
                _save.MarkDirty();
                onDone?.Invoke(ok, restored);
            });
        }
    }
}
