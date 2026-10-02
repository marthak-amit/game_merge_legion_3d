using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Grid
{
    public enum BuyResult { Ok, Locked, NoCoins, GridFull }

    /// <summary>
    /// Owns the player's army: buying units, merging, persistence. No Unity objects, fully unit-testable.
    /// </summary>
    public sealed class ArmyService
    {
        private readonly GridModel _grid;
        private readonly SaveService _save;
        private readonly CurrencyService _currency;
        private readonly GameConfig _config;
        private readonly GameDatabase _db;
        private readonly DeterministicRng _rng;
        private readonly IAnalyticsService _analytics;
        private readonly IResearchProvider _research;

        public GridModel Grid => _grid;

        public ArmyService(GridModel grid, SaveService save, CurrencyService currency, GameConfig config,
            GameDatabase db, DeterministicRng rng, IAnalyticsService analytics, IResearchProvider research = null)
        {
            _grid = grid;
            _save = save;
            _currency = currency;
            _config = config;
            _db = db;
            _rng = rng;
            _analytics = analytics;
            _research = research ?? NullResearch.Instance;
            EventBus.Subscribe<Services.SaveReplacedEvent>(OnSaveReplaced);
        }

        private void OnSaveReplaced(Services.SaveReplacedEvent e) => Load();

        /// <summary>The level the player is about to play (1-based).</summary>
        public int CurrentLevel => _save.Data.highestCampaignLevel + 1;

        public bool IsUnlocked(UnitLineId line) => CurrentLevel >= _db.GetLine(line).unlockLevel;

        public int BuyCount(UnitLineId line) => IntEntries.Get(_save.Data.buyCounts, line.ToString());

        public long GetBuyCost(UnitLineId line)
        {
            var data = _db.GetLine(line);
            return CostCalculator.UnitCost(data.baseCost, data.costGrowth, BuyCount(line), _research.BuyDiscount(line));
        }

        public BuyResult Buy(UnitLineId line)
        {
            if (!IsUnlocked(line)) return BuyResult.Locked;
            // A full grid still allows a purchase that merges instantly with a level-1 unit of the same line.
            int partner = _grid.EmptyCount() == 0 ? FindLevelOne(line) : -1;
            if (_grid.EmptyCount() == 0 && partner < 0) return BuyResult.GridFull;
            long cost = GetBuyCost(line);
            if (!_currency.TrySpend(CurrencyType.Coins, cost, "unit_buy")) return BuyResult.NoCoins;

            IntEntries.Add(_save.Data.buyCounts, line.ToString(), 1);
            if (partner >= 0) AutoMerge(partner, line);
            else SpawnAt(_grid.PickRandomEmpty(_rng), line, 1);
            EventBus.Publish(new UnitBoughtEvent((int)line, cost));
            _analytics?.LogEvent(AnalyticsEvents.UnitBuy, new Dictionary<string, object>
            {
                { AnalyticsParams.Line, line.ToString() },
                { AnalyticsParams.Amount, cost }
            });
            return BuyResult.Ok;
        }

        private int FindLevelOne(UnitLineId line)
        {
            for (int i = 0; i < _grid.Count; i++)
            {
                var s = _grid.Get(i);
                if (!s.IsEmpty && s.line == (int)line && s.level == 1 && s.level < _config.grid.maxUnitLevel) return i;
            }
            return -1;
        }

        private void AutoMerge(int index, UnitLineId line)
        {
            _grid.Set(index, UnitSlot.Of((int)line, 2));
            if (2 > _save.Data.highestMergedLevel) _save.Data.highestMergedLevel = 2;
            _analytics?.LogEvent(AnalyticsEvents.Merge, new Dictionary<string, object>
            {
                { AnalyticsParams.Line, line.ToString() },
                { AnalyticsParams.ToLevel, 2 }
            });
            EventBus.Publish(new UnitMergedEvent(index, index, (int)line, 2));
            Persist();
        }

        /// <summary>Places a unit without charging (rewarded free unit, reinforcements, tutorial). Returns the cell or -1.</summary>
        public int SpawnFree(UnitLineId line, int level)
        {
            int index = _grid.PickRandomEmpty(_rng);
            if (index < 0) return -1;
            SpawnAt(index, line, level);
            return index;
        }

        public DropResult Drop(int from, int to)
        {
            var result = MergeService.Drop(_grid, from, to, _config.grid.maxUnitLevel);
            switch (result.Kind)
            {
                case DropKind.Move:
                    EventBus.Publish(new UnitMovedEvent(result.From, result.To));
                    break;
                case DropKind.Swap:
                    EventBus.Publish(new UnitSwappedEvent(result.From, result.To));
                    break;
                case DropKind.Merge:
                    if (result.Level > _save.Data.highestMergedLevel) _save.Data.highestMergedLevel = result.Level;
                    _analytics?.LogEvent(AnalyticsEvents.Merge, new Dictionary<string, object>
                    {
                        { AnalyticsParams.Line, ((UnitLineId)result.Line).ToString() },
                        { AnalyticsParams.ToLevel, result.Level }
                    });
                    EventBus.Publish(new UnitMergedEvent(result.From, result.To, result.Line, result.Level));
                    break;
            }
            if (result.Kind != DropKind.Invalid) Persist();
            return result;
        }

        /// <summary>True if any pair of units on the grid can currently merge (used by the tutorial hint).</summary>
        public bool TryFindMergePair(out int a, out int b)
        {
            int max = _config.grid.maxUnitLevel;
            for (int i = 0; i < _grid.Count; i++)
            {
                for (int j = i + 1; j < _grid.Count; j++)
                {
                    if (!MergeService.CanMerge(_grid.Get(i), _grid.Get(j), max)) continue;
                    a = i; b = j;
                    return true;
                }
            }
            a = b = -1;
            return false;
        }

        public void Load()
        {
            _grid.Clear();
            var cells = _save.Data.grid;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (c.col < 0 || c.col >= _grid.Cols || c.row < 0 || c.row >= _grid.Rows) continue;
                _grid.Set(_grid.Index(c.col, c.row), UnitSlot.Of(c.line, c.level));
            }
            EventBus.Publish(new GridResetEvent());
        }

        public void Persist()
        {
            var cells = _save.Data.grid;
            cells.Clear();
            for (int i = 0; i < _grid.Count; i++)
            {
                var s = _grid.Get(i);
                if (s.IsEmpty) continue;
                cells.Add(new GridCellSave { col = _grid.ColOf(i), row = _grid.RowOf(i), line = s.line, level = s.level });
            }
            _save.MarkDirty();
        }

        /// <summary>Snapshot of living army for the battle: every unit on the grid with its cell.</summary>
        public List<BattleSpawn> BuildBattleArmy()
        {
            var list = new List<BattleSpawn>();
            for (int i = 0; i < _grid.Count; i++)
            {
                var s = _grid.Get(i);
                if (s.IsEmpty) continue;
                list.Add(new BattleSpawn { line = s.line, level = s.level, col = _grid.ColOf(i), row = _grid.RowOf(i) });
            }
            return list;
        }

        private void SpawnAt(int index, UnitLineId line, int level)
        {
            _grid.Set(index, UnitSlot.Of((int)line, level));
            if (level > _save.Data.highestMergedLevel) _save.Data.highestMergedLevel = level;
            EventBus.Publish(new UnitSpawnedEvent(index, (int)line, level));
            Persist();
        }
    }

    public struct BattleSpawn
    {
        public int line;
        public int level;
        public int col;
        public int row;
    }
}
