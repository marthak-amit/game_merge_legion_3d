using System;

namespace MergeLegion.Services
{
    /// <summary>
    /// Server-side receipt validation hook. The Mock validates everything; the production implementation calls a
    /// Unity Cloud Code endpoint that verifies the Apple / Google receipt before the purchase is fulfilled.
    /// </summary>
    public interface IReceiptValidator
    {
        void Validate(string sku, string receipt, Action<bool> onResult);
    }

    public sealed class MockReceiptValidator : IReceiptValidator
    {
        public bool Result = true;
        public void Validate(string sku, string receipt, Action<bool> onResult) => onResult?.Invoke(Result);
    }
}
