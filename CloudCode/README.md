# Cloud Code scripts (async PvP arena)

1. Link the project in the Unity Dashboard (Edit > Project Settings > Services) and install the UGS CLI.
2. `ugs deploy CloudCode/` publishes `ArenaUpload` and `ArenaFindOpponents`.
3. In the dashboard create the Leaderboards `campaign_level`, `endless_wave`, `arena_trophies` (sort: descending, update: keep best).
4. Enable the define `MERGELEGION_UGS` (Tools > Merge Legion > SDKs > Enable Unity Gaming Services).

The scripts keep snapshots in Cloud Save *custom data* `arena_pool` (one bucket per 100 trophies, max 40 snapshots each).
That is fine up to a few thousand daily players; move to a dedicated database via a Cloud Code C# module when it grows.
