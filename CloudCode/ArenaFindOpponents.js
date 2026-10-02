// Unity Cloud Code script: returns up to `count` opponent snapshots near the caller's trophies and power, as a JSON string.
const { DataApi } = require("cloud-save-1.0");

const BUCKET_SIZE = 100;
const CUSTOM_ID = "arena_pool";

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi(context);
  const bucket = Math.floor(params.trophies / BUCKET_SIZE);
  const keys = [bucket - 1, bucket, bucket + 1].filter((b) => b >= 0).map((b) => "bucket_" + b);

  const res = await api.getCustomItems(context.projectId, CUSTOM_ID, keys);
  let candidates = [];
  for (const item of res.data.results) candidates = candidates.concat(JSON.parse(item.value));
  candidates = candidates.filter((s) => s.playerId !== params.playerId);

  candidates.sort((a, b) => Math.abs(a.power - params.power) - Math.abs(b.power - params.power));
  return JSON.stringify(candidates.slice(0, params.count));
};
