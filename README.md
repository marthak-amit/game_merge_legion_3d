# Merge Legion 3D

Portrait merge-army battler for Android + iOS (Unity 6 LTS, URP). Buy units, drag-merge them up eight levels, auto-battle
through a 200-level campaign (10 themed chapters, a boss every 10th level), then endless mode, an async PvP arena,
a weekend event, battle pass, castle, chests, missions, daily login and lucky spin. Built for ~US$0.15 ARPDAU with a
hybrid ads (AppLovin MAX) + IAP model.

**Status: all 10 phases implemented** (feature-complete, runs end-to-end in the Editor on mocks, no keys needed).
See `docs/` for architecture, SDK setup, live ops, balance and store compliance.

## First-time setup (Unity Editor)
1. Open with **Unity 6000.0.x LTS** (accept the version upgrade prompt; `ProjectVersion.txt` is a placeholder).
2. `Window > TextMeshPro > Import TMP Essential Resources`.
3. `Tools > Merge Legion > Setup All (Settings + Scenes + Data)` (restart if asked, then run it again).
4. Open `Boot` and press **Play**. First launch: age gate -> consent -> straight into the 30-second tutorial battle.
5. Commit the generated `.meta` files, scenes and `Assets/_Game/Settings`.
6. Change `BundleId` / `CompanyName` in `Scripts/Editor/ProjectSetup.cs` and the legal URLs in `Resources/Config/game_config.json`.

## Tests
`Window > General > Test Runner`: EditMode (logic) and PlayMode (needs generated scenes). CI: `.github/workflows/ci.yml` (GameCI).
Headless builds: `BuildScripts.BuildAndroidAab` / `BuildIosProject`.

## Going live
`docs/SDK_SETUP.md` (enable real SDKs), `docs/LIVE_OPS.md` (Remote Config), `docs/STORE_COMPLIANCE.md` (Data Safety, ATT, privacy).

## Rules of the codebase
* Third-party SDKs only behind `I*Service`; mocks always work.
* No hardcoded balance, prices, caps or strings: JSON/CSV/`en.json` only.
