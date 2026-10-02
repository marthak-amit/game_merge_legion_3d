// Unity Cloud Code script: stores a player's arena snapshot in a shared pool (Cloud Save custom data), bucketed by trophies.
// Deploy with the UGS CLI:  ugs deploy CloudCode/
const { DataApi } = require("cloud-save-1.0");

const BUCKET_SIZE = 100;
const POOL_MAX = 40;
const CUSTOM_ID = "arena_pool";

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const snap = JSON.parse(params.snapshot);
  if (!snap || !snap.playerId || !Array.isArray(snap.units) || snap.units.length === 0 || snap.units.length > 15) {
    throw new Error("invalid snapshot");
  }
  snap.isBot = false;

  const key = "bucket_" + Math.floor(snap.trophies / BUCKET_SIZE);
  const res = await api.getCustomItems(context.projectId, CUSTOM_ID, [key]);
  let pool = res.data.results.length > 0 ? JSON.parse(res.data.results[0].value) : [];
  pool = pool.filter((s) => s.playerId !== snap.playerId);
  pool.push(snap);
  while (pool.length > POOL_MAX) pool.shift();

  await api.setCustomItem(context.projectId, CUSTOM_ID, { key: key, value: JSON.stringify(pool) });
  logger.info("arena snapshot stored in " + key);
  return true;
};
