# Architecture

```
Assets/_Game
  Scripts/            MergeLegion.Runtime assembly (everything the game needs)
    Core/             ServiceLocator, EventBus, SceneLoader, Tween, pools, DeterministicRng, PerformanceProfile, GameBootstrap
    Data/             ScriptableObjects (UnitLineData, CommanderData), GameConfig, CSV -> SO builder, themes
    Grid/             GridModel, MergeService, ArmyService (buy/merge/persist), GridView, DragController
    Battle/           BattleSim (pure, deterministic), BattleFactory, CommanderSkillSystem, views, director, HUD
    Levels/           LevelDefinition (JSON), campaign + endless generators, CampaignService
    Economy/          CurrencyService, DailyService, CostCalculator, RewardService
    Meta/             Research, Castle, Chests, Missions/Achievements, Login, Spin, Commanders, Arena/, PushScheduler
    Monetization/     AdsManager, IapManager, OfferService, Piggy, Vip, BattlePass, WeekendEvent
    Services/         IAnalytics/IAds/IIAP/... interfaces, Mock/ implementations, ServiceInstaller, PlatformServiceOverrides
    Save/             SaveService (versioned, checksummed, backup), SaveMerger, SaveRunner
    UI/               code-built uGUI: screens, popups, chrome, first-run flow
    Tutorial/         FTUE step machine + overlay
    Audio/            procedural SFX/music, haptics
    Editor/           scene generator, balance importer, level designer, SDK installer, build scripts
  Sdk/                Real SDK adapters (Assembly-CSharp), each behind its own scripting define
  Resources/          Config/*.json (all tuning), Balance/*.csv, Levels/*.json, Localization/en.json
  Tests/              EditMode + PlayMode
```

## Principles
* **Services behind interfaces.** `ServiceInstaller` builds Mocks; SDK adapters register factories in
  `PlatformServiceOverrides` from a `BeforeSceneLoad` hook, only when their define is set and keys exist.
* **Simulation is separate from presentation.** `BattleSim` has no Unity dependencies; it ticks at a fixed step with a
  seeded RNG, emits `SimEvent`s, and `BattleView` renders them. 240 units cost ~0.1 ms per tick and allocate nothing once warm.
* **Everything tunable is data.** `game_config.json`, `meta_config.json`, `monetization_config.json`, `arena_config.json`,
  `tutorial.json`, `themes.json`, balance CSVs, level JSON, `en.json`. Remote Config can override any config with a JSON
  patch (`config_override`, `meta_override`, `monetization_override`, `arena_override`) and replace the weekend event.
* **UI is built in code** (`UIKit`/`RowKit`), so there are no scene references to wire and screens are reproducible.
* **Events over god-managers.** Missions, battle pass, offers, tutorial, leaderboards and cloud sync listen to `EventBus`.

## Save
`SaveService` -> versioned envelope `{version, checksum, payload}`, atomic file write with `.bak` rotation, migration chain
(`ISaveMigration`), tamper flag (never uploaded to cloud / leaderboards). `SaveMerger`: higher progress wins, prompt when both
sides have progress. Autosave every 30 s (Remote Config) and on pause/quit; purchases and wins save immediately.

## Tests
EditMode (pure logic, 250+ tests): merge rules, cost curve, rewards, save/migration/merge, missions, battle pass, offers,
chests (drop rates), castle, arena, cloud sync, tutorial, audio synthesis, allocation and performance budgets, balance
simulator guards. PlayMode: boot flow, full level loop, deterministic battle, tutorial order.
