using System;
using System.Collections.Generic;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Save;
using MergeLegion.Services.Mock;

namespace MergeLegion.Tests
{
    /// <summary>
    /// Headless "average player" used by balance tests and the balance tool. It plays through the real services
    /// (ArmyService, CurrencyService, ResearchService): concentrates purchases on a couple of lines, merges greedily,
    /// puts spare coins into research, fights each level (no skill, no ads) and retries on defeat.
    /// </summary>
    public sealed class PlayerSimulator
    {
        public struct LevelLog
        {
            public int Level;
            public int Attempts;
            public bool Cleared;
            public float ArmyStrength;
            public int Stars;
            public long Coins;
            public int Units;
            public int HighestUnit;
            public int ResearchLevels;
        }

        private readonly GameDatabase _db;
        private readonly GameConfig _cfg;
        private readonly LevelRepository _levels;
        private readonly ArenaLayout _layout;
        private readonly SaveService _save;
        private readonly CurrencyService _currency;
        private readonly ResearchService _research;
        private readonly ArmyService _army;
        private readonly CommanderBonuses _bonuses;

        public PlayerSimulator(GameDatabase db, GameConfig cfg, LevelRepository levels, int seed, CommanderBonuses bonuses = default(CommanderBonuses))
        {
            _db = db;
            _cfg = cfg;
            _levels = levels;
            _layout = new ArenaLayout(cfg.grid);
            _bonuses = bonuses;
            EventBus.ClearAll();
            _save = new SaveService(new InMemorySaveStorage(), new FakeTimeService());
            _save.Load();
            var analytics = new MockAnalyticsService();
            _currency = new CurrencyService(_save, new NullAnalytics());
            _currency.Add(CurrencyType.Coins, cfg.grid.startCoins, "start");
            _research = new ResearchService(_save, _currency, cfg.research);
            _army = new ArmyService(new GridModel(cfg.grid.cols, cfg.grid.rows), _save, _currency, cfg, db, new DeterministicRng(seed), new NullAnalytics(), _research);
        }

        private sealed class NullAnalytics : MergeLegion.Services.IAnalyticsService
        {
            public void Initialize() { }
            public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null) { }
            public void SetUserProperty(string key, string value) { }
            public void SetUserId(string playerId) { }
        }

        public List<LevelLog> Run(int toLevel, int maxAttemptsPerLevel = 15)
        {
            var log = new List<LevelLog>();
            for (int level = 1; level <= toLevel; level++)
            {
                var def = _levels.Get(level);
                int attempts = 0;
                bool cleared = false;
                int stars = 0;
                float strength = 0;
                while (attempts < maxAttemptsPerLevel && !cleared)
                {
                    attempts++;
                    SpendCoins();
                    var army = _army.BuildBattleArmy();
                    strength = BattleFactory.ArmyPower(_db, army);
                    var sim = BattleFactory.Create(_cfg, _db, _layout, def, army, _research, _bonuses, _save.Data.highestMergedLevel, level * 1009 + attempts);
                    int ticks = (int)(_cfg.battle.maxDurationSeconds / _cfg.battle.fixedStep) + 5;
                    for (int t = 0; t < ticks && sim.Outcome == BattleOutcome.Running; t++) { sim.Tick(_cfg.battle.fixedStep); sim.Drain(); }
                    var res = sim.GetResult();
                    if (res.Outcome == BattleOutcome.Win)
                    {
                        cleared = true;
                        stars = res.Stars;
                        _currency.Add(CurrencyType.Coins, RewardService.WinCoins(_cfg.rewards, level, res.Stars, _bonuses.CoinBonus), "win");
                        _save.Data.highestCampaignLevel = level;
                    }
                    else _currency.Add(CurrencyType.Coins, RewardService.LoseCoins(_cfg.rewards, level, _bonuses.CoinBonus), "lose");
                }
                int rl = 0;
                foreach (var e in _save.Data.research) rl += e.value;
                log.Add(new LevelLog
                {
                    Level = level, Attempts = attempts, Cleared = cleared, ArmyStrength = strength, Stars = stars,
                    Coins = _currency.Get(CurrencyType.Coins), Units = _army.Grid.UnitCount(), HighestUnit = _army.Grid.HighestLevel(), ResearchLevels = rl
                });
                if (!cleared) break;
            }
            return log;
        }

        private void SpendCoins()
        {
            int guard = 0;
            while (guard++ < 600)
            {
                MergeAll();
                var line = ChooseLine();
                if (line < 0) break;
                if (_army.Buy((UnitLineId)line) != BuyResult.Ok) break;
            }
            MergeAll();
            SpendResearch();
        }

        // Prefer the unlocked line that already holds the most unit mass (so units pair up), cheapest on ties.
        private int ChooseLine()
        {
            int empty = _army.Grid.EmptyCount();
            long coins = _currency.Get(CurrencyType.Coins);

            // Focus: only the two lines holding the most unit mass keep growing (extra lines just fragment the grid).
            var masses = new long[UnitLines.Count];
            for (int i = 0; i < _army.Grid.Count; i++)
            {
                var s0 = _army.Grid.Get(i);
                if (!s0.IsEmpty) masses[s0.line] += 1L << (s0.level - 1);
            }
            int first = -1, second = -1;
            for (int l = 0; l < UnitLines.Count; l++)
            {
                if (!_army.IsUnlocked((UnitLineId)l)) continue;
                if (first < 0 || masses[l] > masses[first]) { second = first; first = l; }
                else if (second < 0 || masses[l] > masses[second]) second = l;
            }
            int best = -1;
            long bestMass = -1;
            long bestCost = long.MaxValue;
            for (int l = 0; l < UnitLines.Count; l++)
            {
                if (!_army.IsUnlocked((UnitLineId)l)) continue;
                if (l != first && l != second) continue;
                long cost = _army.GetBuyCost((UnitLineId)l);
                if (cost > coins) continue;
                // keep two cells free for merging space unless this purchase merges immediately
                bool mergesNow = false;
                for (int i = 0; i < _army.Grid.Count; i++)
                {
                    var us = _army.Grid.Get(i);
                    if (!us.IsEmpty && us.line == l && us.level == 1) { mergesNow = true; break; }
                }
                if (empty <= 1 && !mergesNow) continue;
                long mass = 0;
                for (int i = 0; i < _army.Grid.Count; i++)
                {
                    var s = _army.Grid.Get(i);
                    if (!s.IsEmpty && s.line == l) mass += 1L << (s.level - 1);
                }
                // a line that is far more expensive than the cheapest alternative is skipped unless it is the army's core
                if (mass > bestMass || (mass == bestMass && cost < bestCost)) { best = l; bestMass = mass; bestCost = cost; }
            }
            return best;
        }

        private void SpendResearch()
        {
            int guard = 0;
            while (guard++ < 400)
            {
                long coins = _currency.Get(CurrencyType.Coins);
                UnitLineId bestLine = UnitLineId.Melee;
                ResearchStat bestStat = ResearchStat.Hp;
                long bestCost = long.MaxValue;
                for (int l = 0; l < UnitLines.Count; l++)
                {
                    int mass = 0;
                    for (int i = 0; i < _army.Grid.Count; i++) if (!_army.Grid.IsEmpty(i) && _army.Grid.Get(i).line == l) mass++;
                    if (mass == 0) continue;
                    foreach (ResearchStat st in new[] { ResearchStat.Hp, ResearchStat.Damage })
                    {
                        if (_research.GetLevel((UnitLineId)l, st) >= _research.MaxLevel(st)) continue;
                        long c = _research.Cost((UnitLineId)l, st);
                        if (c < bestCost) { bestCost = c; bestLine = (UnitLineId)l; bestStat = st; }
                    }
                }
                if (bestCost > coins || bestCost == long.MaxValue) break;
                _research.Upgrade(bestLine, bestStat);
            }
        }

        private void MergeAll()
        {
            bool merged = true;
            while (merged)
            {
                merged = _army.TryFindMergePair(out int a, out int b);
                if (merged) _army.Drop(a, b);
            }
        }
    }
}
