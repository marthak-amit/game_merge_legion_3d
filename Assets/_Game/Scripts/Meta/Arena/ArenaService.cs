using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Save;
using MergeLegion.Services;

namespace MergeLegion.Meta.Arena
{
    public readonly struct ArenaChangedEvent { }

    public struct ArenaMatchResult
    {
        public bool Won;
        public int TrophyDelta;
        public int NewTrophies;
        public bool LeagueChanged;
        public List<GrantedItem> Rewards;
    }

    /// <summary>
    /// Async PvP arena (section 2.11): fight snapshots of other players' armies for trophies, climb leagues
    /// (Bronze -> Legend) and collect weekly league rewards.
    /// </summary>
    public sealed class ArenaService
    {
        private readonly SaveService _save;
        private readonly ArenaConfig _cfg;
        private readonly IArenaBackend _backend;
        private readonly ArmyService _army;
        private readonly IResearchProvider _research;
        private readonly CommanderService _commanders;
        private readonly GameDatabase _db;
        private readonly RewardGranter _granter;
        private readonly DailyService _daily;
        private readonly ITimeService _time;
        private readonly IAnalyticsService _analytics;
        private readonly IAuthService _auth;
        private readonly ILeaderboardService _leaderboards;

        public ArenaService(SaveService save, ArenaConfig cfg, IArenaBackend backend, ArmyService army, IResearchProvider research,
            CommanderService commanders, GameDatabase db, RewardGranter granter, DailyService daily, ITimeService time,
            IAnalyticsService analytics, IAuthService auth, ILeaderboardService leaderboards)
        {
            _save = save;
            _cfg = cfg;
            _backend = backend;
            _army = army;
            _research = research;
            _commanders = commanders;
            _db = db;
            _granter = granter;
            _daily = daily;
            _time = time;
            _analytics = analytics;
            _auth = auth;
            _leaderboards = leaderboards;
            RolloverWeek();
        }

        public ArenaConfig Config => _cfg;
        public ArenaSnapshot PendingOpponent { get; private set; }

        public bool Unlocked => _army.CurrentLevel >= _cfg.unlockLevel;
        public int Trophies => _save.Data.arenaTrophies;
        public int LeagueIndex => _cfg.LeagueIndexFor(Trophies);
        public LeagueDef League => _cfg.leagues[LeagueIndex];
        public LeagueDef NextLeague => LeagueIndex + 1 < _cfg.leagues.Count ? _cfg.leagues[LeagueIndex + 1] : null;

        // ---------------------------------------------------------------- attempts

        public int Attempts
        {
            get { RefillAttempts(); return _save.Data.arenaAttempts; }
        }

        public TimeSpan UntilNextAttempt()
        {
            RefillAttempts();
            if (_save.Data.arenaAttempts >= _cfg.maxAttempts) return TimeSpan.Zero;
            var next = new DateTime(_save.Data.arenaLastRefillUtcTicks, DateTimeKind.Utc).AddMinutes(_cfg.attemptRefillMinutes);
            var left = next - _time.UtcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        private void RefillAttempts()
        {
            var d = _save.Data;
            long now = _time.UtcNow.Ticks;
            if (d.arenaAttempts < 0) { d.arenaAttempts = _cfg.maxAttempts; d.arenaLastRefillUtcTicks = now; return; }
            if (d.arenaAttempts >= _cfg.maxAttempts) { d.arenaLastRefillUtcTicks = now; return; }
            if (now < d.arenaLastRefillUtcTicks) { d.arenaLastRefillUtcTicks = now; return; }

            long step = TimeSpan.FromMinutes(_cfg.attemptRefillMinutes).Ticks;
            long gained = (now - d.arenaLastRefillUtcTicks) / step;
            if (gained <= 0) return;
            d.arenaAttempts = (int)Math.Min(_cfg.maxAttempts, d.arenaAttempts + gained);
            d.arenaLastRefillUtcTicks = d.arenaAttempts >= _cfg.maxAttempts ? now : d.arenaLastRefillUtcTicks + gained * step;
            _save.MarkDirty();
        }

        // ---------------------------------------------------------------- snapshots + matchmaking

        public ArenaSnapshot BuildSnapshot()
        {
            var snap = new ArenaSnapshot
            {
                playerId = _auth.PlayerId ?? _save.Data.playerId,
                name = _auth.DisplayName,
                trophies = Trophies,
                commander = _commanders.EquippedId,
                createdUtcTicks = _time.UtcNow.Ticks
            };

            float massSum = 0f, hp = 0f, dmg = 0f;
            var spawns = _army.BuildBattleArmy();
            foreach (var s in spawns)
            {
                snap.units.Add(new ArenaUnit { line = s.line, level = s.level, col = s.col, row = s.row });
                float strength = LevelPower.UnitStrength(_db, s.line, s.level);
                snap.power += strength;
                massSum += strength;
                hp += strength * _research.HpBonus((UnitLineId)s.line);
                dmg += strength * _research.DamageBonus((UnitLineId)s.line);
            }
            if (massSum > 0f) { snap.hpBonus = hp / massSum; snap.dmgBonus = dmg / massSum; }
            var b = _commanders.GetBonuses();
            snap.hpBonus += b.AllHp;
            snap.dmgBonus += b.AllDamage;
            snap.power *= (float)Math.Sqrt((1f + snap.hpBonus) * (1f + snap.dmgBonus));
            return snap;
        }

        public void UploadSnapshot(Action<bool> done = null)
        {
            if (!Unlocked || _save.Data.tamperDetected) { done?.Invoke(false); return; }
            var snap = BuildSnapshot();
            if (snap.units.Count == 0) { done?.Invoke(false); return; }
            _backend.Upload(snap, done);
        }

        public void FindOpponents(Action<List<ArenaSnapshot>> done)
        {
            var me = BuildSnapshot();
            _backend.FindOpponents(me, _cfg.opponentChoices, done);
        }

        public bool TryStartMatch(ArenaSnapshot opponent)
        {
            if (!Unlocked || opponent == null || Attempts <= 0) return false;
            _save.Data.arenaAttempts--;
            PendingOpponent = opponent;
            _save.MarkDirty();
            EventBus.Publish(new ArenaChangedEvent());
            return true;
        }

        /// <summary>Converts an opponent snapshot into a level the battle scene can run (their stats are folded into hp/dmg multipliers).</summary>
        public static LevelDefinition BuildLevel(ArenaSnapshot opponent, int playerLevel)
        {
            var level = new LevelDefinition
            {
                id = playerLevel,
                chapter = 0,
                index = 1,
                theme = ProceduralLevelGenerator.Themes[Math.Abs(opponent.playerId.GetHashCode()) % ProceduralLevelGenerator.Themes.Length],
                enemyCols = 5,
                hpMult = 1f + opponent.hpBonus,
                dmgMult = 1f + opponent.dmgBonus,
                power = opponent.power
            };
            foreach (var u in opponent.units)
                level.enemies.Add(new EnemySpawnDef { line = u.line, level = u.level, col = u.col, row = u.row });
            return level;
        }

        // ---------------------------------------------------------------- results

        public ArenaMatchResult ReportResult(bool won)
        {
            var opp = PendingOpponent;
            int theirs = opp != null ? opp.trophies : Trophies;
            int before = Trophies;
            int leagueBefore = LeagueIndex;

            int delta = won ? ArenaMath.WinGain(_cfg, before, theirs) : -ArenaMath.LossCost(_cfg, before, theirs);
            _save.Data.arenaTrophies = Math.Max(0, before + delta);
            delta = _save.Data.arenaTrophies - before;
            if (won) _save.Data.arenaWins++; else _save.Data.arenaLosses++;
            if (_save.Data.arenaTrophies > _save.Data.arenaWeekPeakTrophies) _save.Data.arenaWeekPeakTrophies = _save.Data.arenaTrophies;

            var rewards = new List<GrantedItem>();
            long coins = _granter.CoinsForWins(won ? _cfg.winCoinsInWins : _cfg.loseCoinsInWins);
            if (coins > 0)
            {
                var item = new GrantedItem(RewardType.Coins, coins);
                _granter.Apply(new[] { item }, "arena_match");
                rewards.Add(item);
            }

            PendingOpponent = null;
            _analytics.LogEvent(AnalyticsEvents.ArenaMatch, new Dictionary<string, object>
            {
                { "won", won },
                { "trophies", _save.Data.arenaTrophies },
                { "opp_trophies", theirs },
                { "opp_bot", opp != null && opp.isBot }
            });
            _leaderboards.SubmitScore(LeaderboardIds.ArenaTrophies, _save.Data.arenaTrophies, null);
            _save.MarkDirty();
            EventBus.Publish(new ArenaChangedEvent());

            return new ArenaMatchResult
            {
                Won = won,
                TrophyDelta = delta,
                NewTrophies = _save.Data.arenaTrophies,
                LeagueChanged = LeagueIndex != leagueBefore,
                Rewards = rewards
            };
        }

        // ---------------------------------------------------------------- weekly league rewards

        public bool WeeklyRewardAvailable
        {
            get { RolloverWeek(); return _save.Data.arenaUnclaimedLeague >= 0; }
        }

        public LeagueDef UnclaimedLeague => _save.Data.arenaUnclaimedLeague >= 0 ? _cfg.leagues[_save.Data.arenaUnclaimedLeague] : null;

        public List<GrantedItem> ClaimWeeklyReward()
        {
            RolloverWeek();
            int idx = _save.Data.arenaUnclaimedLeague;
            if (idx < 0) return null;
            _save.Data.arenaUnclaimedLeague = -1;
            _save.MarkDirty();
            var items = _granter.Grant(_cfg.leagues[idx].weeklyRewards, "arena_league_" + _cfg.leagues[idx].id);
            EventBus.Publish(new ArenaChangedEvent());
            return items;
        }

        /// <summary>On a new local week: bank last week's best league as claimable, and start tracking the new week.</summary>
        public void RolloverWeek()
        {
            int week = _daily.WeekKey;
            var d = _save.Data;
            if (d.arenaWeekKey == week) return;

            if (d.arenaWeekKey != 0 && (d.arenaWins + d.arenaLosses) > 0)
            {
                int peakLeague = _cfg.LeagueIndexFor(Math.Max(d.arenaWeekPeakTrophies, d.arenaTrophies));
                d.arenaUnclaimedLeague = Math.Max(d.arenaUnclaimedLeague, peakLeague);
            }
            d.arenaWeekKey = week;
            d.arenaWeekPeakTrophies = d.arenaTrophies;
            _save.MarkDirty();
        }
    }
}
