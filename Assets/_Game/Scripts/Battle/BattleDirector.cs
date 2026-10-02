using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.UI;
using UnityEngine;

namespace MergeLegion.Battle
{
    public readonly struct BattlePhaseEvent
    {
        public readonly BattlePhase Phase;
        public BattlePhaseEvent(BattlePhase phase) { Phase = phase; }
    }

    public readonly struct SkillUsedEvent { }

    public enum BattlePhase { Prepare, Fighting, Result }

    /// <summary>
    /// Runs the level loop: PREPARE (grid + enemy preview) -> FIGHT (fixed-step sim, commander skill, 2x speed) -> WIN/LOSE.
    /// All rewards, ad offers and progress writes happen here.
    /// </summary>
    public sealed class BattleDirector : MonoBehaviour
    {
        private GameConfig _cfg;
        private GameDatabase _db;
        private ArenaLayout _layout;
        private ArmyService _army;
        private CurrencyService _currency;
        private CampaignService _campaign;
        private CommanderService _commanders;
        private LevelRepository _levels;
        private AdsManager _ads;
        private SaveService _save;
        private IAnalyticsService _analytics;
        private BattleView _view;
        private HealthBarBatch _bars;
        private BattleHud _hud;
        private GridView _gridView;
        private DragController _drag;
        private ArenaThemeBuilder _theme;

        private LevelDefinition _level;
        private BattleSim _sim;
        private CommanderSkillSystem _skills;
        private CommanderBonuses _bonuses;
        private float _acc;
        private float _speed = 1f;
        private float _endTimer;
        private bool _ending;
        private bool _reviveUsed;
        private int _attempt;
        private float _hudTimer;
        private float _armyPower;

        public BattlePhase Phase { get; private set; } = BattlePhase.Prepare;
        public LevelDefinition Level => _level;
        public BattleSim Sim => _sim;

        public void Init(ArenaLayout layout, BattleView view, HealthBarBatch bars, BattleHud hud, GridView gridView, DragController drag, ArenaThemeBuilder theme)
        {
            _cfg = ServiceLocator.Get<GameConfig>();
            _db = GameDatabase.Instance;
            _layout = layout;
            _army = ServiceLocator.Get<ArmyService>();
            _currency = ServiceLocator.Get<CurrencyService>();
            _campaign = ServiceLocator.Get<CampaignService>();
            _commanders = ServiceLocator.Get<CommanderService>();
            _levels = ServiceLocator.Get<LevelRepository>();
            _ads = ServiceLocator.Get<AdsManager>();
            _save = ServiceLocator.Get<SaveService>();
            _analytics = ServiceLocator.Get<IAnalyticsService>();
            _view = view;
            _bars = bars;
            _hud = hud;
            _gridView = gridView;
            _drag = drag;
            _theme = theme;

            _hud.FightClicked += StartFight;
            _hud.SkillClicked += CastSkill;
            _hud.SpeedClicked += ToggleSpeed;
            _hud.FreeUnitClicked += OnFreeUnit;
            EnterPrepare();
        }

        // ------------------------------------------------------------------ PREPARE

        public void EnterPrepare()
        {
            Phase = BattlePhase.Prepare;
            _ending = false;
            _reviveUsed = false;
            _speed = 1f;
            Time.timeScale = 1f;
            _level = _levels.Get(_campaign.CurrentLevel);
            _hud.SetLevel(_level.id);
            _theme.Apply(_level.theme);

            var preview = new BattleSim(_cfg.battle, 1);
            BattleFactory.AddEnemies(preview, _level, _layout, _db);
            _view.Rebind(preview);
            _sim = null;

            _gridView.gameObject.SetActive(true);
            _drag.Enabled = true;
            _hud.SetPrepareVisible(true);
            _hud.SetFightVisible(false);
            _hud.Refresh();
            RefreshFreeUnit();
            if (_level.isBoss) Toast.Show(Loc.Get("hud.boss"));
            EventBus.Publish(new BattlePhaseEvent(BattlePhase.Prepare));
        }

        private void RefreshFreeUnit()
        {
            float cd = _ads.CooldownRemaining(AdPlacements.FreeUnit);
            bool canAd = _ads.CanShowRewarded(AdPlacements.FreeUnit) == AdBlockReason.None && _army.Grid.EmptyCount() > 0;
            string text = cd > 0f ? Loc.Format("hud.free_unit_cd", Mathf.CeilToInt(cd) + "s") : Loc.Get("hud.free_unit");
            _hud.SetFreeUnit(canAd, text);
        }

        private void OnFreeUnit()
        {
            if (Phase != BattlePhase.Prepare) return;
            if (_army.Grid.EmptyCount() == 0) { Toast.Show(Loc.Get("toast.grid_full")); return; }
            _ads.ShowRewarded(AdPlacements.FreeUnit, ok =>
            {
                if (!ok) { Toast.Show(Loc.Get("result.ad_unavailable")); return; }
                var unlocked = new List<UnitLineId>();
                for (int i = 0; i < UnitLines.Count; i++) if (_army.IsUnlocked((UnitLineId)i)) unlocked.Add((UnitLineId)i);
                var line = unlocked[Random.Range(0, unlocked.Count)];
                int level = Mathf.Max(1, _save.Data.highestMergedLevel - _cfg.grid.freeUnitLevelOffset);
                _army.SpawnFree(line, Mathf.Min(level, _cfg.grid.maxUnitLevel));
                RefreshFreeUnit();
            });
        }

        // ------------------------------------------------------------------ FIGHT

        public void StartFight()
        {
            if (Phase != BattlePhase.Prepare) return;
            var armyList = _army.BuildBattleArmy();
            if (armyList.Count == 0)
            {
                Toast.Show(Loc.Get("toast.no_units"));
                return;
            }

            _attempt++;
            IResearchProvider research = ServiceLocator.TryGet<IResearchProvider>(out var r) ? r : NullResearch.Instance;
            _bonuses = _commanders.GetBonuses();
            _sim = BattleFactory.Create(_cfg, _db, _layout, _level, armyList, research, _bonuses, _save.Data.highestMergedLevel,
                _level.id * 1009 + _attempt);
            _skills = new CommanderSkillSystem(_commanders.GetSkill());
            _skills.Begin(_sim);
            _view.Rebind(_sim);

            _armyPower = BattleFactory.ArmyPower(_db, armyList);
            _campaign.OnStart(_level.id, _armyPower);
            _army.Persist();
            _save.Flush();

            _gridView.gameObject.SetActive(false);
            _drag.Enabled = false;
            _hud.SetPrepareVisible(false);
            _hud.SetFightVisible(true);
            _hud.SetSpeedText(Loc.Format("hud.speed", 1));
            UpdateSkillHud();
            _acc = 0f;
            Phase = BattlePhase.Fighting;
            Sfx.Play(SfxId.Whoosh);
            EventBus.Publish(new BattlePhaseEvent(BattlePhase.Fighting));
        }

        private void CastSkill()
        {
            if (Phase != BattlePhase.Fighting || _sim == null || _ending) return;
            if (!_skills.TryCast(_sim)) return;
            var cmd = _commanders.Equipped;
            _analytics.LogEvent(AnalyticsEvents.CommanderSkillUsed, new Dictionary<string, object>
            {
                { "commander", cmd != null ? cmd.id : "none" },
                { "skill", _skills.Type.ToString() },
                { AnalyticsParams.Level, _level.id }
            });
            EventBus.Publish(new SkillUsedEvent());
            EventBus.Publish(new MergeLegion.Meta.SkillUsedEventProxy());
            Sfx.Play(SfxId.Skill);
            Haptics.Medium();
            if (ArenaCamera.Instance != null) ArenaCamera.Instance.Shake(0.15f, 0.2f);
        }

        private void ToggleSpeed()
        {
            _speed = _speed > 1.5f ? 1f : 2f;
            _hud.SetSpeedText(Loc.Format("hud.speed", (int)_speed));
        }

        private void UpdateSkillHud()
        {
            var cmd = _commanders.Equipped;
            _hud.SetSkill(cmd != null ? Loc.Get(cmd.skillKey) : "-", _skills.ReadyFraction, _skills.Ready);
        }

        private void Update()
        {
            if (Phase == BattlePhase.Prepare)
            {
                _hudTimer += Time.unscaledDeltaTime;
                if (_hudTimer > 0.5f) { _hudTimer = 0f; RefreshFreeUnit(); }
                _view.Sync(0f);
                return;
            }
            if (Phase != BattlePhase.Fighting || _sim == null) return;

            float step = _cfg.battle.fixedStep;
            if (!_ending)
            {
                _acc += Time.unscaledDeltaTime * _speed * (Time.timeScale);
                int guard = 0;
                while (_acc >= step && guard++ < 6 && _sim.Outcome == BattleOutcome.Running)
                {
                    _sim.Tick(step);
                    _skills.Update(step);
                    _acc -= step;
                }
                if (guard >= 6) _acc = 0f;
            }

            _view.ProcessEvents();
            _view.Sync(Mathf.Clamp01(_acc / step));

            _hudTimer += Time.unscaledDeltaTime;
            if (_hudTimer > 0.1f) { _hudTimer = 0f; UpdateSkillHud(); }

            if (_sim.Outcome != BattleOutcome.Running)
            {
                if (!_ending) BeginEnding();
                _endTimer -= Time.unscaledDeltaTime;
                if (_endTimer <= 0f)
                {
                    Time.timeScale = 1f;
                    Phase = BattlePhase.Result;
                    ShowResult();
                }
            }
        }

        private void LateUpdate()
        {
            if (_sim != null && Phase != BattlePhase.Result) _bars.Draw(_sim);
            else if (_sim == null && _view != null) _bars.Draw(null);
        }

        private void BeginEnding()
        {
            _ending = true;
            if (_sim.Outcome == BattleOutcome.Win)
            {
                // slow-motion on the final kill, then the result
                Time.timeScale = _cfg.battle.slowMoScale;
                _endTimer = _cfg.battle.slowMoSeconds * _cfg.battle.slowMoScale;
                Sfx.Play(SfxId.Win);
            }
            else
            {
                _endTimer = 0.9f;
                Sfx.Play(SfxId.Lose);
            }
        }

        // ------------------------------------------------------------------ RESULT

        private void ShowResult()
        {
            _hud.SetFightVisible(false);
            var result = _sim.GetResult();
            if (result.Outcome == BattleOutcome.Win) ShowWin(result); else ShowLose(result);
            EventBus.Publish(new BattlePhaseEvent(BattlePhase.Result));
        }

        private void ShowWin(BattleResult result)
        {
            var reward = RewardService.ForWin(_cfg.rewards, _level.id, result.Stars, _level.isBoss, _bonuses.CoinBonus);
            _currency.Add(CurrencyType.Coins, reward.Coins, "level_win");
            if (reward.Gems > 0) _currency.Add(CurrencyType.Gems, reward.Gems, "boss_win");
            if (reward.Keys > 0) _currency.Add(CurrencyType.ChestKeys, reward.Keys, "level_win");
            if (reward.BattlePassXp > 0) _currency.Add(CurrencyType.BattlePassXp, reward.BattlePassXp, "level_win");
            _campaign.OnWin(_level.id, result.Stars, _level.isBoss, result.Duration, _armyPower);
            _save.Save();

            var args = new WinPopupArgs
            {
                Stars = result.Stars,
                Reward = reward,
                AdMultiplier = _cfg.rewards.adMultiplier,
                AdAvailable = _ads.CanShowRewarded(AdPlacements.Win3x) == AdBlockReason.None,
                WatchAd = cb => _ads.ShowRewarded(AdPlacements.Win3x, ok =>
                {
                    if (ok) _currency.Add(CurrencyType.Coins, reward.Coins * (_cfg.rewards.adMultiplier - 1), "win_ad_bonus");
                    cb(ok);
                }),
                Next = () => _ads.TryShowInterstitial(_level.id + 1, ReloadBattle),
                Home = GoHome
            };
            if (args.AdAvailable) _ads.LogOffered(AdPlacements.Win3x);
            PopupManager.Show(() => ResultPopups.ShowWin(args));
        }

        private void ShowLose(BattleResult result)
        {
            var reward = RewardService.ForLoss(_cfg.rewards, _level.id, _bonuses.CoinBonus);
            _currency.Add(CurrencyType.Coins, reward.Coins, "level_lose");
            _campaign.OnLoss(_level.id, _level.isBoss, result.Duration);
            _save.Save();

            var args = new LosePopupArgs
            {
                Consolation = reward.Coins,
                ReviveAvailable = !_reviveUsed && _ads.CanShowRewarded(AdPlacements.Revive) == AdBlockReason.None,
                Revive = cb => _ads.ShowRewarded(AdPlacements.Revive, ok =>
                {
                    if (ok) Revive();
                    cb(ok);
                }),
                Retry = EnterPrepare,
                Home = GoHome
            };
            if (args.ReviveAvailable) _ads.LogOffered(AdPlacements.Revive);
            PopupManager.Show(() => ResultPopups.ShowLose(args));
        }

        private void Revive()
        {
            _reviveUsed = true;
            _sim.Revive(Team.Player, _cfg.battle.revivePct);
            _sim.ExtendTime(30f);
            _ending = false;
            _acc = 0f;
            Phase = BattlePhase.Fighting;
            _hud.SetFightVisible(true);
        }

        private void ReloadBattle()
        {
            if (ServiceLocator.TryGet<SceneLoader>(out var loader)) loader.Load(SceneNames.Battle);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Battle);
        }

        public void GoHome()
        {
            Time.timeScale = 1f;
            _army.Persist();
            _save.Flush();
            if (ServiceLocator.TryGet<SceneLoader>(out var loader)) loader.Load(SceneNames.Main);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Main);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_hud != null)
            {
                _hud.FightClicked -= StartFight;
                _hud.SkillClicked -= CastSkill;
                _hud.SpeedClicked -= ToggleSpeed;
                _hud.FreeUnitClicked -= OnFreeUnit;
            }
        }
    }
}
