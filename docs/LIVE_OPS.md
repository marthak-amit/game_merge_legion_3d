# Live ops without an app update

## Remote Config keys
| Key | Type | Purpose |
|---|---|---|
| `config_override` | JSON string | Patch over `game_config.json` (grid, battle, rewards, ads caps/cooldowns, research, endless, legal links) |
| `meta_override` | JSON string | Patch over `meta_config.json` (castle, chests + drop rates, missions, achievements, login calendar, spin wheel) |
| `monetization_override` | JSON string | Patch over `monetization_config.json` (prices shown as fallback, product contents, offers + triggers, piggy, VIP, battle pass tiers) |
| `arena_override` | JSON string | Patch over `arena_config.json` (leagues, trophies, attempts, rewards) |
| `weekend_event_json` | JSON string | Full `EventConfig` for a custom event (id, `startUtc`/`endUtc`, track, difficulty). Empty = recurring Sat-Sun event |
| `save_autosave_seconds`, `boot_timeout_seconds` | number | Runtime behaviour |

Patches are *partial*: only the fields present are replaced (`JsonUtility.FromJsonOverwrite`). Lists are replaced whole.
Examples:
```json
{"rewards":{"adMultiplier":2},"ads":{"interstitialGapSeconds":90}}
{"chests":[ ...full chest list... ]}
```

## Schedules
* Weekend event: default Saturday 00:00 -> Monday 00:00 local. Set `weekend_event_json` with explicit UTC dates for holiday events.
* Battle Pass: 30-day seasons from 2026-01-01 UTC (`BattlePassService.Epoch`); premium unlock and claims reset each season.
* Daily/weekly missions reset at local midnight / Monday.

## Authoring content
* Levels: `Resources/Levels/chapter_XX.json`, edit in **Tools > Merge Legion > Level Designer**.
* Units/commanders: edit `tools/gen_balance.py` or the CSVs, then **Import Balance**. Art swap: assign prefabs on the generated
  `UnitLineData` assets (kept across re-imports).
* Strings: `tools/loc/en.txt` -> `python3 tools/build_loc.py` (also fails when code references a missing key).
