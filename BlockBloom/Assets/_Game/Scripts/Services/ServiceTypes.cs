using System;
using System.Collections.Generic;

namespace BlockBloom.Services
{
    public enum AdResult { Completed, Skipped, Failed, NotReady }

    public readonly struct AdRevenueInfo
    {
        public readonly string Network;
        public readonly string Placement;
        public readonly string Format;
        public readonly double RevenueUsd;

        public AdRevenueInfo(string network, string placement, string format, double revenueUsd)
        {
            Network = network; Placement = placement; Format = format; RevenueUsd = revenueUsd;
        }
    }

    public enum ProductKind { Consumable, NonConsumable, Subscription }

    [Serializable]
    public sealed class ProductInfo
    {
        public string Sku;
        public ProductKind Kind;
        public string LocalizedPrice;
        public decimal Price;
        public string IsoCurrency;
    }

    public enum PurchaseStatus { Success, Cancelled, Failed, AlreadyOwned }

    public readonly struct PurchaseResult
    {
        public readonly PurchaseStatus Status;
        public readonly string Sku;
        public readonly string Receipt;

        public PurchaseResult(PurchaseStatus status, string sku, string receipt = null)
        {
            Status = status; Sku = sku; Receipt = receipt;
        }

        public bool Success => Status == PurchaseStatus.Success;
    }

    [Serializable]
    public sealed class LeaderboardEntry
    {
        public string PlayerId;
        public string DisplayName;
        public long Score;
        public int Rank;
    }

    public enum CloudLoadStatus { Ok, NotFound, Failed }

    public readonly struct CloudLoadResult
    {
        public readonly CloudLoadStatus Status;
        public readonly string Json;
        public CloudLoadResult(CloudLoadStatus status, string json = null) { Status = status; Json = json; }
    }

    public enum ConsentStatus { Unknown, NotRequired, Granted, Denied }
}
