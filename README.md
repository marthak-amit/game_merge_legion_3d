# Merge Legion 3D

Mobile merge-army battler (Unity 6 LTS, URP, portrait 1080x1920). Built phase by phase; see the project brief for the full roadmap.

## Status
**Phase 1 done**: Core (ServiceLocator, EventBus, SceneLoader, TimeService), versioned/checksummed Save with
backup recovery, migrations and cloud-merge policy, all 9 service interfaces (+ consent) with working Mocks,
stack-based UI shell, localization facade, Boot -> Main (Home) flow, and editor generators.

## First-time setup (once, in the Unity Editor)
1. Open the folder with **Unity 6000.0.x LTS** (Unity may offer to upgrade `ProjectVersion.txt`; accept).
2. `Window > TextMeshPro > Import TMP Essential Resources`.
3. `Tools > Merge Legion > Setup All (Settings + Scenes)`.
   - Creates the URP asset, applies player settings, switches Input Handling to the Input System package.
   - If Unity asks to restart, restart, then run `Setup All` again.
4. Open `Boot` (done automatically) and press **Play**: it boots on mocks and lands on the empty Home screen.
5. Commit the generated `.meta` files, scenes and `Assets/_Game/Settings`.

Before any store build, change `BundleId` / `CompanyName` in `Assets/_Game/Scripts/Editor/ProjectSetup.cs`.

## Tests
`Window > General > Test Runner`: EditMode (logic) and PlayMode (`BootFlowTests`, needs generated scenes).

## Rules of the codebase
- Third-party SDKs only behind `I*Service` interfaces; `ServiceInstaller` picks the implementation.
- No hardcoded balance/prices/caps/strings: Remote Config defaults in `Resources/RemoteConfigDefaults.json`, UI text in `Resources/Localization/en.json`.
