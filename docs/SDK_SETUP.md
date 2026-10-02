# SDK setup (Phase 9)

Every SDK is **off** until enabled; with nothing enabled the game runs on mocks, in the Editor, without keys.
All menus live in **Tools > Merge Legion > SDKs**. Order for each SDK: *1. Install*, then *2. Enable*.
Keys go in `Assets/_Game/Resources/Config/sdk_keys.json` (create it with *Create sdk_keys.json from example*; it is git-ignored).

| SDK | Menu | Needs from you | Notes |
|---|---|---|---|
| Unity Gaming Services (auth, cloud save, leaderboards, cloud code) | Unity Gaming Services | Link project in Dashboard; `ugs deploy CloudCode/`; create boards `campaign_level`, `endless_wave`, `arena_trophies` | Arena pool lives in Cloud Save custom data (see `CloudCode/README.md`) |
| AppLovin MAX | AppLovin MAX | `maxSdkKey`, rewarded / interstitial / banner unit ids per platform | Enable AdMob, Unity Ads, Mintegral, Meta AN, ironSource in the MAX Integration Manager; enable the Terms & Privacy Policy (consent) flow |
| Unity IAP | Unity IAP | Create the SKUs from `monetization_config.json` in both stores | Adapter targets IAP **v4.12**; v5 has a different API |
| Firebase | Firebase | `google-services.json`, `GoogleService-Info.plist` in `Assets/` | Analytics sink, Remote Config, Crashlytics, Messaging token. Add Remote Config keys from `docs/LIVE_OPS.md` |
| GameAnalytics | GameAnalytics | Game/secret keys in the GA settings window | Declare resource currencies `coins, gems, chestkeys, battlepassxp` and item type `economy` |
| AppsFlyer | AppsFlyer | `appsFlyerDevKey`, `appsFlyerAppIdIos` | Define `MERGELEGION_APPSFLYER_ADREVENUE` too if you install the ad-revenue connector |
| Google Play Games / Game Center | Google Play Games + Game Center | Leaderboard ids in `sdk_keys.json` | Scores mirror from the main leaderboard service |
| Notifications | Notifications | none | Local notifications; FCM token comes from Firebase |
| iOS ATT | iOS ATT | `NSUserTrackingUsageDescription` text in Player Settings | Prompt appears after the tutorial (`PermissionsFlow`) |

The adapters were written against the current public APIs of each SDK without access to the packages. After installing, let
Unity compile once and fix any signature drift the Console reports; the adapters are small (one file per SDK in `Assets/_Game/Sdk`).
Server-side receipt validation: `CloudCode/ValidateReceipt.js` is a structural validator with replay protection; add the
Apple / Google store API calls (marked TODO) before launch.
