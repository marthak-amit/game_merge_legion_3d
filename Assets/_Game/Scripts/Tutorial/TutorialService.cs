using System;
using System.Collections.Generic;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Save;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Tutorial
{
    public enum TutorialTrigger
    {
        UnitBought, UnitMerged, FightStarted, SkillUsed, ResearchChanged, ChestOpened, CommanderChanged,
        LevelWon, ScreenOpened, TargetTapped
    }

    [Serializable]
    public sealed class TutorialStepDef
    {
        public string id;
        public string scene;            // "battle" or "home"
        public string phase;            // "", "prepare", "fight"
        public int minLevel = 1;        // player's current level (highest cleared + 1)
        public int maxLevel = 9999;
        public string textKey;
        public string target;           // id registered in TutorialTargets ("merge" = the first mergeable pair)
        public TutorialTrigger completeOn;
        public string completeArg;      // e.g. screen name for ScreenOpened
        public int count = 1;
        public bool dim = true;         // darken everything except the target
        public int startCoins;          // gifted when the step becomes active so the player can afford it
        public int startShards;
        public string startShardsTarget;
    }

    [Serializable]
    public sealed class TutorialConfig
    {
        public List<TutorialStepDef> steps = new List<TutorialStepDef>();

        public static TutorialConfig FromJson(string json)
        {
            var cfg = new TutorialConfig();
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, cfg);
            return cfg;
        }

        public static TutorialConfig Load()
        {
            var asset = Resources.Load<TextAsset>("Config/tutorial");
            return FromJson(asset != null ? asset.text : null);
        }
    }

    public readonly struct TutorialStepCompletedEvent
    {
        public readonly string Id;
        public TutorialStepCompletedEvent(string id) { Id = id; }
    }

    public readonly struct TutorialFinishedEvent { }

    /// <summary>
    /// FTUE (section 6): buy -> merge -> fight -> win inside the first 30 seconds, then guided unlocks of research, chest,
    /// commander, castle and missions over the first levels. Steps are strictly ordered and defined in tutorial.json.
    /// While any step is pending, <see cref="SaveFlags.TutorialDone"/> is false, which keeps interstitials off.
    /// </summary>
    public sealed class TutorialService : IDisposable
    {
        private const string FlagPrefix = "tut_";

        private readonly SaveService _save;
        private readonly TutorialConfig _cfg;
        private readonly Func<int> _playerLevel;
        private readonly CurrencyService _currency;
        private readonly IAnalyticsService _analytics;
        private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();
        private readonly HashSet<string> _started = new HashSet<string>();
        private readonly int _doneAtLevel;

        public TutorialService(SaveService save, TutorialConfig cfg, Func<int> playerLevel, CurrencyService currency,
            IAnalyticsService analytics, int finishAtLevel = 12)
        {
            _save = save;
            _cfg = cfg;
            _playerLevel = playerLevel;
            _currency = currency;
            _analytics = analytics;
            _doneAtLevel = finishAtLevel;

            // returning players (cloud save, reinstall) never see the tutorial
            if (_playerLevel() >= _doneAtLevel) MarkFinished();

            EventBus.Subscribe<UnitBoughtEvent>(OnBought);
            EventBus.Subscribe<UnitMergedEvent>(OnMerged);
            EventBus.Subscribe<BattlePhaseEvent>(OnPhase);
            EventBus.Subscribe<SkillUsedEvent>(OnSkill);
            EventBus.Subscribe<ResearchChangedEvent>(OnResearch);
            EventBus.Subscribe<ChestOpenedEvent>(OnChest);
            EventBus.Subscribe<CommanderChangedEvent>(OnCommander);
            EventBus.Subscribe<LevelCompletedEvent>(OnLevel);
            EventBus.Subscribe<ScreenChangedEvent>(OnScreen);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<UnitBoughtEvent>(OnBought);
            EventBus.Unsubscribe<UnitMergedEvent>(OnMerged);
            EventBus.Unsubscribe<BattlePhaseEvent>(OnPhase);
            EventBus.Unsubscribe<SkillUsedEvent>(OnSkill);
            EventBus.Unsubscribe<ResearchChangedEvent>(OnResearch);
            EventBus.Unsubscribe<ChestOpenedEvent>(OnChest);
            EventBus.Unsubscribe<CommanderChangedEvent>(OnCommander);
            EventBus.Unsubscribe<LevelCompletedEvent>(OnLevel);
            EventBus.Unsubscribe<ScreenChangedEvent>(OnScreen);
        }

        public bool IsActive => !_save.Data.HasFlag(SaveFlags.TutorialDone);

        /// <summary>True until the very first win: the first launch goes straight into a battle for the 30-second hook.</summary>
        public bool NeedsIntroBattle => IsActive && !IsDone("buy1") && _save.Data.highestCampaignLevel == 0;

        public bool IsDone(string stepId) => _save.Data.HasFlag(FlagPrefix + stepId);

        /// <summary>The step to show now for the given scene/phase, or null. Steps never skip ahead of an unfinished one.</summary>
        public TutorialStepDef Current(string scene, string phase)
        {
            if (!IsActive) return null;
            foreach (var step in _cfg.steps)
            {
                if (IsDone(step.id)) continue;
                int level = _playerLevel();
                if (level < step.minLevel) return null;
                if (level > step.maxLevel) { Complete(step.id, true); continue; } // missed its window: don't block the rest
                if (step.scene != scene) return null;
                if (!string.IsNullOrEmpty(step.phase) && step.phase != phase) return null;
                Begin(step);
                return step;
            }
            MarkFinished();
            return null;
        }

        private void Begin(TutorialStepDef step)
        {
            if (!_started.Add(step.id)) return;
            if (step.startCoins > 0) _currency.Add(CurrencyType.Coins, step.startCoins, "tutorial");
            if (step.startShards > 0) _currency.AddShards(step.startShardsTarget, step.startShards, "tutorial");
        }

        public void Notify(TutorialTrigger trigger, string arg = null)
        {
            if (!IsActive) return;
            foreach (var step in _cfg.steps)
            {
                if (IsDone(step.id)) continue;
                // only the first unfinished step can progress
                if (_playerLevel() < step.minLevel || _playerLevel() > step.maxLevel) return;
                if (step.completeOn != trigger) return;
                if (!string.IsNullOrEmpty(step.completeArg) && step.completeArg != arg) return;
                int n = _progress.TryGetValue(step.id, out var v) ? v + 1 : 1;
                _progress[step.id] = n;
                if (n >= step.count) Complete(step.id, false);
                return;
            }
        }

        private void Complete(string id, bool skipped)
        {
            if (IsDone(id)) return;
            _save.Data.SetFlag(FlagPrefix + id);
            _save.MarkDirty();
            if (!skipped)
            {
                _analytics.LogEvent(AnalyticsEvents.TutorialStep, new Dictionary<string, object> { { AnalyticsParams.Step, id } });
                EventBus.Publish(new TutorialStepCompletedEvent(id));
            }
            bool all = true;
            foreach (var s in _cfg.steps) if (!IsDone(s.id)) { all = false; break; }
            if (all) MarkFinished();
        }

        /// <summary>Completes everything (debug / returning player).</summary>
        public void SkipAll()
        {
            foreach (var s in _cfg.steps) _save.Data.SetFlag(FlagPrefix + s.id);
            MarkFinished();
        }

        private void MarkFinished()
        {
            if (_save.Data.HasFlag(SaveFlags.TutorialDone)) return;
            _save.Data.SetFlag(SaveFlags.TutorialDone);
            _save.MarkDirty();
            EventBus.Publish(new TutorialFinishedEvent());
        }

        // ---------------------------------------------------------------- event adapters

        private void OnBought(UnitBoughtEvent e) => Notify(TutorialTrigger.UnitBought);
        private void OnMerged(UnitMergedEvent e) => Notify(TutorialTrigger.UnitMerged);
        private void OnPhase(BattlePhaseEvent e) { if (e.Phase == BattlePhase.Fighting) Notify(TutorialTrigger.FightStarted); }
        private void OnSkill(SkillUsedEvent e) => Notify(TutorialTrigger.SkillUsed);
        private void OnResearch(ResearchChangedEvent e) => Notify(TutorialTrigger.ResearchChanged);
        private void OnChest(ChestOpenedEvent e) => Notify(TutorialTrigger.ChestOpened);
        private void OnCommander(CommanderChangedEvent e) => Notify(TutorialTrigger.CommanderChanged);
        private void OnLevel(LevelCompletedEvent e) { if (e.Won) Notify(TutorialTrigger.LevelWon); }
        private void OnScreen(ScreenChangedEvent e) => Notify(TutorialTrigger.ScreenOpened, e.Screen.ToString());
    }
}
