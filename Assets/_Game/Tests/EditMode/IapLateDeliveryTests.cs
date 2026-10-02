using System;
using MergeLegion.Economy;
using MergeLegion.Services;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    /// <summary>Store deliveries that arrive without a button press (app killed mid-purchase, restore) are fulfilled once.</summary>
    public class IapLateDeliveryTests : MonetizationFixture
    {
        private sealed class ConfirmingIap : MergeLegion.Services.Mock.MockIAPService, IConfirmingIapService
        {
            public readonly System.Collections.Generic.List<string> Confirmed = new System.Collections.Generic.List<string>();
            public void ConfirmPurchase(string sku) => Confirmed.Add(sku);
            public void Deliver(PurchaseResult r) => RaiseExternal(r);
        }

        [Test]
        public void StoreDelivery_WithoutUserAction_IsFulfilledAndConfirmedOnce()
        {
            var store = new ConfirmingIap();
            store.Initialize(Iap.Skus());
            var iap = new MergeLegion.Monetization.IapManager(store, Shop, Granter, Save, Analytics, Attribution, Validator, Ads);

            store.Deliver(new PurchaseResult(PurchaseStatus.Success, "gems_small", "receipt"));

            Assert.AreEqual(100, Currency.Get(CurrencyType.Gems));
            CollectionAssert.AreEqual(new[] { "gems_small" }, store.Confirmed);
        }

        [Test]
        public void OneTimeProduct_DeliveredTwice_PaysOnce()
        {
            var store = new ConfirmingIap();
            store.Initialize(Iap.Skus());
            var iap = new MergeLegion.Monetization.IapManager(store, Shop, Granter, Save, Analytics, Attribution, Validator, Ads);

            store.Deliver(new PurchaseResult(PurchaseStatus.Success, "no_ads", "r"));
            long gems = Currency.Get(CurrencyType.Gems);
            store.Deliver(new PurchaseResult(PurchaseStatus.Success, "no_ads", "r"));

            Assert.AreEqual(gems, Currency.Get(CurrencyType.Gems));
            Assert.AreEqual(2, store.Confirmed.Count, "both deliveries are confirmed so the store stops retrying");
        }

        [Test]
        public void RejectedReceipt_IsConfirmedButNotFulfilled()
        {
            var store = new ConfirmingIap();
            store.Initialize(Iap.Skus());
            Validator.Result = false;
            var iap = new MergeLegion.Monetization.IapManager(store, Shop, Granter, Save, Analytics, Attribution, Validator, Ads);

            store.Deliver(new PurchaseResult(PurchaseStatus.Success, "gems_large", "forged"));

            Assert.AreEqual(0, Currency.Get(CurrencyType.Gems));
            Assert.AreEqual(1, store.Confirmed.Count);
        }
    }
}
