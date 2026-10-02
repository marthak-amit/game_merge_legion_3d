# Store compliance checklist

## In the game
* Chest and spin **drop rates** are shown in the UI (`i` on each chest, **Odds** on the wheel) and computed from the same weights used to roll.
* **Restore Purchases**: Settings and Shop.
* **Delete account and data**: Settings > confirms, then deletes cloud save, UGS account and local save.
* **Age gate** (13+) on first launch; under-age players are blocked and no data is collected. Not directed at children, no COPPA data.
* **Consent**: MAX consent flow (GDPR/UMP, CCPA) on first launch; Settings > Privacy choices reopens it.
* **iOS ATT** prompt appears after the tutorial with an explanation screen first; `NSUserTrackingUsageDescription` must be set.
* **Privacy policy / Terms** links (Settings) come from `game_config.json > legal`; replace the placeholders before submission.
* Subscription disclosure text is shown in the Shop (weekly renewal, cancel in store settings).
* No forced ads during the tutorial or in battle; banners only on menu screens; rewarded ads are always optional.

## Google Play - Data Safety form (suggested answers)
| Data | Collected | Shared | Purpose | Optional |
|---|---|---|---|---|
| Device or other IDs (advertising ID, Firebase installation ID, UGS player ID) | Yes | Yes (ad/attribution partners) | Advertising, analytics, fraud prevention | Consent-gated |
| App activity (in-app events, progress) | Yes | Yes (analytics partners) | Analytics, app functionality | No |
| App info and performance (crash logs, diagnostics) | Yes | Yes (Firebase Crashlytics) | Analytics, stability | No |
| Purchase history | Yes | Yes (attribution) | App functionality, analytics | No |
| Approximate location (derived from IP by ad SDKs) | Yes | Yes | Advertising | Consent-gated |
| Name / email / precise location / contacts / photos | No | - | - | - |
Data encrypted in transit: yes. Deletion: in-app (Settings) and by request via the support email.
Select "Contains ads" and "In-app purchases". Target audience: 13+ (not designed for children).

## Apple - App Privacy ("nutrition labels")
* Data used to track you: Device ID (IDFA, only after ATT consent), Product interaction (attribution).
* Data linked to you: Purchases, Product interaction, Device ID, Crash data, Performance data.
* Age rating questionnaire: infrequent/mild cartoon fantasy violence; no gambling (spin wheel awards are free, odds disclosed); in-app purchases yes; unrestricted web access no.
* Provide the privacy policy URL and support URL; enable Sign-in is not required (anonymous accounts).

## Before you submit
1. Replace placeholder legal URLs and support email; publish the privacy policy (template: `docs/PRIVACY_POLICY_TEMPLATE.md`).
2. Create the IAP SKUs; test with licence testers / TestFlight sandbox.
3. Complete `CloudCode/ValidateReceipt.js` store-API checks.
4. Run the performance checklist on a Snapdragon 6-series and a low-RAM device (target 60 / 30 fps, < 400 MB, cold start < 5 s, install < 150 MB).
5. Replace procedural audio and primitive models with final assets if desired (no code change: `AudioManager.Overrides`, `UnitLevelData.prefab`).
