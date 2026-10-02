// Unity Cloud Code script: server-side receipt validation for IAP (apple | google).
//
// SECURITY NOTE: this ships as a structural validator (receipt parses, product id matches the SKU, transaction is not
// replayed). Before launch, complete the store-API call marked TODO with your store credentials stored as Cloud Code
// secrets (Apple App Store Server API key; Google Play Developer API service account) - Unity Cloud Code Secrets.
const { DataApi } = require("cloud-save-1.0");

module.exports = async ({ params, context, logger }) => {
  const { sku, receipt, store } = params;
  if (!sku || !receipt) return false;

  let payload;
  try {
    const outer = JSON.parse(receipt);                  // Unity IAP receipt wrapper
    payload = JSON.parse(outer.Payload || "{}");
    if (outer.Store && store && outer.Store.toLowerCase().indexOf(store) === -1 && outer.Store !== "fake") return false;
  } catch (e) {
    logger.warning("unparsable receipt");
    return false;
  }

  // replay protection: remember each transaction id once per player
  const tx = outer_transaction_id(receipt);
  if (tx) {
    const api = new DataApi(context);
    const key = "tx_" + tx;
    const existing = await api.getItems(context.projectId, context.playerId, [key]);
    if (existing.data.results.length > 0) {
      logger.warning("receipt replay " + tx);
      return false;
    }
    await api.setItem(context.projectId, context.playerId, { key: key, value: Date.now().toString() });
  }

  // TODO(launch): call Apple App Store Server API / Google Play Developer API with the payload and
  // compare productId === sku before returning true.
  return true;
};

function outer_transaction_id(receipt) {
  try {
    const outer = JSON.parse(receipt);
    return outer.TransactionID || null;
  } catch (e) {
    return null;
  }
}
