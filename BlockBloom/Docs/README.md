# Block Bloom

A polished block-puzzle game (Block Blast style core + Candy-Crush style level journey) built in Unity 6.

## What is in the game
* **Adventure** – 300 procedural levels, move limits, goals (score / lines / gems / combo / colour), 1-3 stars from efficiency, boss level every 10, star chest, hearts (5, +1 per 20 min).
* **Classic** – endless high-score mode, no hearts.
* **Daily challenge** – same seed for every player each day, bonus coins on first finish.
* **Meta** – daily login reward (7-day streak), lucky spin, daily quests, 4 themes, coin shop.
* **Boosters** – Undo, Bomb (3x3), Shuffle; refill by rewarded video or coins.
* **Feel** – everything animated: drag lift + forgiving snap, ghost preview with line pre-highlight, wave clear animation, combo pitch-rising SFX, screen shake, confetti, perfect-clear banner, procedural audio and haptics.
* **Fair randomness** – every tray of 3 pieces is guaranteed solvable; crowded boards get smaller pieces and often a line-completing piece (`Logic/TrayGenerator.cs`).

## Code map (`Assets/_Game/Scripts`)
| Folder | Purpose |
|---|---|
| `Logic/` | Pure C#, unit-tested (`BlockBloom/Tests`): bit-board, 42 shapes, tray generator, scoring, adventure levels, `GameSession` |
| `Game/` | Views and juice: board, tray drag, FX, audio, game screen, ads/IAP policy (`Monet`) |
| `UI/` | Procedural sprites, UI toolkit, Home, Map, pop-ups |
| `Meta/` | Save data and economy numbers (one file to tune everything) |
| `Services/` | Ads / IAP / analytics / remote-config interfaces + mock implementations |
| `Editor/` | CI build scripts |

All art is generated in code and the UI is built at runtime, so there are no art files to import.

## Monetisation design
* **Rewarded video (always optional):** extra moves / revive, x2 level coins, free booster, free heart refill, spin again, free coins in shop.
* **Interstitial:** only after level 4, every 3rd level end, minimum 100 s apart, never mid-play, removed by "No Ads".
* **IAP (INR):** Starter pack Rs 49 (once), No Ads Rs 199, coin packs Rs 79 / 199 / 399 / 749. Prices are placeholders – set the real tiers in Play Console.
* **Retention loops:** hearts, daily reward streak, daily challenge, quests, star chest, level map.

Tuning knobs: `Meta/Economy.cs` (hearts, prices, rewards, ad pacing) and `Logic/Adventure.cs` (level difficulty).

## Going live checklist
1. Replace `com.yourstudio.blockbloom` / `YourStudio` in `Editor/ProjectSetup.cs`.
2. Create a Play Console app, upload the signed AAB (`BuildScripts.BuildAndroidAab`), set the keystore in Player Settings.
3. Integrate real SDKs behind the existing interfaces in `Services/IServices.cs` (replace the Mock registrations in `Core/App.cs → InstallServices`):
   * Ads: AppLovin MAX (rewarded + interstitial + banner) – implement `IAdsService`.
   * IAP: Unity IAP – implement `IIAPService`; create the SKUs listed in `Meta/Economy.cs → Products`.
   * Analytics: Firebase / GameAnalytics – implement `IAnalyticsService`; events are already logged through `Monet.Log` (`level_start`, `level_win`, `level_fail`, `ad_rewarded_*`, `iap_*`, `revive`, `booster_used`).
   * Attribution: AppsFlyer/Adjust for paid user acquisition.
4. Add the consent flow (UMP / GDPR) and a privacy policy URL before enabling ads.
5. Soft-launch in a few countries, watch D1 / D7 retention and ad ARPDAU, then scale user acquisition.

## Honest note on revenue
Rs 10 lakh per month is a business target, not a code feature. Roughly: 10 lakh ≈ US$12k/month. With a blended ARPDAU of about US$0.06 that needs ~7k daily active users who stay; with Indian eCPMs it is higher. Retention and cost per install decide whether this works – plan on iterating levels, economy and ad placement from live data.

## CI
`.github/workflows/blockbloom.yml` builds a debug APK and a screenshot set on every push to `claude/**` (artifact `BlockBloom-debug-apk`, screenshots on branch `bb-screenshots`).
