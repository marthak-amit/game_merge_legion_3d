using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;

namespace MergeLegion.Battle
{
    /// <summary>
    /// Deterministic, Unity-free battle simulation: fixed-step steering AI (no NavMesh), projectiles, boss patterns,
    /// area effects and buffs. The same setup and seed always produce the same result.
    /// Tick() performs no allocations once the internal lists have warmed up.
    /// </summary>
    public sealed class BattleSim
    {
        private const float CellSize = 2f;
        private const int GridW = 20, GridH = 24;
        private const float GridOriginX = -20f, GridOriginY = -24f;

        private readonly BattleConfig _cfg;
        private readonly DeterministicRng _rng;
        private readonly List<SimProjectile> _projectiles = new List<SimProjectile>(128);
        private readonly Stack<SimProjectile> _projectilePool = new Stack<SimProjectile>(128);
        private readonly List<PendingAoe> _aoes = new List<PendingAoe>(16);
        private readonly Stack<PendingAoe> _aoePool = new Stack<PendingAoe>(16);
        private readonly List<int>[] _buckets = new List<int>[GridW * GridH];
        private int _nextProjectileId = 1;
        private int _alive0, _alive1;
        private float _initialPlayerHp;
        private int _unitsLost, _enemiesKilled;

        public readonly List<SimUnit> Units = new List<SimUnit>(256);
        public readonly List<SimEvent> Events = new List<SimEvent>(512);

        public float Time { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public float MinX = -9f, MaxX = 9f, MinY = -16f, MaxY = 16f;
        public UnitSpec Reinforcement;
        public float PlayerSpawnZ = -3f;

        public IReadOnlyList<SimProjectile> Projectiles => _projectiles;
        public int AliveCount(Team team) => team == Team.Player ? _alive0 : _alive1;
        public DeterministicRng Rng => _rng;

        public BattleSim(BattleConfig cfg, int seed)
        {
            _cfg = cfg;
            _rng = new DeterministicRng(seed);
            for (int i = 0; i < _buckets.Length; i++) _buckets[i] = new List<int>(8);
        }

        // ------------------------------------------------------------------ setup

        public SimUnit AddUnit(UnitSpec s)
        {
            var u = new SimUnit
            {
                Id = Units.Count + 1,
                Team = s.Team,
                Line = s.Line,
                Level = s.Level,
                Alive = true,
                IsBoss = s.IsBoss,
                IsRanged = s.IsRanged,
                IsFlying = s.IsFlying,
                IsTaunt = s.IsTaunt,
                Hp = s.Hp,
                MaxHp = s.Hp,
                Damage = s.Damage,
                AttackInterval = s.AttackSpeed > 0f ? 1f / s.AttackSpeed : 1f,
                Range = s.Range,
                MoveSpeed = s.MoveSpeed * (s.IsFlying ? _cfg.flyingSpeedMultiplier : 1f),
                Scale = s.Scale,
                Radius = 0.38f * s.Scale,
                ProjectileSpeed = s.ProjectileSpeed,
                Pos = s.Pos,
                Prev = s.Pos,
                // small deterministic stagger so a whole army does not strike on the same tick
                Cooldown = ((Units.Count % 7) * 0.07f),
                RetargetTimer = 0f
            };
            Units.Add(u);
            if (u.Team == Team.Player) { _alive0++; _initialPlayerHp += u.MaxHp; }
            else _alive1++;
            Emit(SimEventType.Spawn, u.Id, 0, (int)u.Team, 0f, u.Pos.x, u.Pos.y);
            return u;
        }

        public void SetBoss(SimUnit unit, BossPattern pattern)
        {
            unit.Boss = pattern;
            pattern.SlamTimer = pattern.SlamInterval * 0.6f;
            pattern.SummonTimer = pattern.SummonInterval * 0.8f;
        }

        public SimUnit GetUnit(int id) => id > 0 && id <= Units.Count ? Units[id - 1] : null;

        public void Drain() => Events.Clear();

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            if (Outcome != BattleOutcome.Running) return;
            Time += dt;

            UpdatePending(dt);
            int count = Units.Count; // units summoned this tick act next tick
            for (int i = 0; i < count; i++)
            {
                var u = Units[i];
                if (!u.Alive) continue;
                u.Prev = u.Pos;
                if (u.Boss != null) UpdateBoss(u, dt);
                UpdateUnit(u, dt);
            }
            Separate();
            UpdateProjectiles(dt);
            CheckOutcome();
        }

        private void UpdateUnit(SimUnit u, float dt)
        {
            SimUnit target = u.TargetId > 0 ? Units[u.TargetId - 1] : null;
            u.RetargetTimer -= dt;
            if (target == null || !target.Alive || u.RetargetTimer <= 0f)
            {
                target = FindTarget(u);
                u.TargetId = target != null ? target.Id : 0;
                u.RetargetTimer = _cfg.retargetInterval + (u.Id % 5) * 0.03f;
            }

            float haste = HasteOf(u);
            if (u.Cooldown > 0f) u.Cooldown -= dt * haste;
            if (target == null) return;

            float dx = target.Pos.x - u.Pos.x, dy = target.Pos.y - u.Pos.y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float reach = u.Range + target.Radius + u.Radius * 0.5f;

            if (dist <= reach)
            {
                if (u.Cooldown <= 0f)
                {
                    Attack(u, target);
                    u.Cooldown += u.AttackInterval;
                }
                return;
            }

            float step = Math.Min(u.MoveSpeed * haste * dt, dist - reach + 0.02f);
            if (step <= 0f || dist < 1e-5f) return;
            u.Pos.x += dx / dist * step;
            u.Pos.y += dy / dist * step;
            Clamp(u);
        }

        private float HasteOf(SimUnit u) => u.HasteUntil > Time ? u.HasteMult : 1f;

        private void Clamp(SimUnit u)
        {
            if (u.Pos.x < MinX) u.Pos.x = MinX; else if (u.Pos.x > MaxX) u.Pos.x = MaxX;
            if (u.Pos.y < MinY) u.Pos.y = MinY; else if (u.Pos.y > MaxY) u.Pos.y = MaxY;
        }

        // ------------------------------------------------------------------ targeting

        private SimUnit FindTarget(SimUnit u)
        {
            SimUnit best = null;
            float bestScore = float.MaxValue;
            float tauntSq = _cfg.tauntRange * _cfg.tauntRange;

            if (u.IsFlying && !u.IsBoss)
            {
                // Flyers dive past the frontline: backline (ranged) first, then anything non-tank, then nearest.
                for (int pass = 0; pass < 3 && best == null; pass++)
                {
                    for (int i = 0; i < Units.Count; i++)
                    {
                        var e = Units[i];
                        if (!e.Alive || e.Team == u.Team) continue;
                        if (pass == 0 && !e.IsRanged) continue;
                        if (pass == 1 && e.IsTaunt) continue;
                        float d = Vec2.SqrDistance(u.Pos, e.Pos);
                        if (d < bestScore) { bestScore = d; best = e; }
                    }
                    bestScore = float.MaxValue;
                }
                return best;
            }

            SimUnit taunt = null;
            float tauntBest = float.MaxValue;
            for (int i = 0; i < Units.Count; i++)
            {
                var e = Units[i];
                if (!e.Alive || e.Team == u.Team) continue;
                float d = Vec2.SqrDistance(u.Pos, e.Pos);
                if (d < bestScore) { bestScore = d; best = e; }
                if (e.IsTaunt && d < tauntSq && d < tauntBest) { tauntBest = d; taunt = e; }
            }
            return taunt ?? best;
        }

        // ------------------------------------------------------------------ combat

        private void Attack(SimUnit u, SimUnit target)
        {
            Emit(SimEventType.Attack, u.Id, target.Id, (int)u.Team, 0f, u.Pos.x, u.Pos.y);
            if (u.IsRanged && u.ProjectileSpeed > 0f)
            {
                var p = _projectilePool.Count > 0 ? _projectilePool.Pop() : new SimProjectile();
                p.Id = _nextProjectileId++;
                p.Team = (int)u.Team;
                p.TargetId = target.Id;
                p.SourceId = u.Id;
                p.Pos = u.Pos;
                p.Speed = u.ProjectileSpeed;
                p.Damage = u.Damage;
                p.Line = u.Line;
                _projectiles.Add(p);
                Emit(SimEventType.ProjectileLaunch, p.Id, target.Id, p.Team, 0f, u.Pos.x, u.Pos.y);
            }
            else DealDamage(target, u.Damage, u.Id);
        }

        public void DealDamage(SimUnit target, float amount, int sourceId)
        {
            if (!target.Alive || amount <= 0f) return;
            if (target.ShieldUntil > Time) amount *= target.ShieldMult;
            target.Hp -= amount;
            Emit(SimEventType.Hit, target.Id, sourceId, (int)target.Team, amount, target.Pos.x, target.Pos.y);
            if (target.Hp <= 0f) Kill(target, sourceId);
        }

        private void Kill(SimUnit u, int killerId)
        {
            u.Alive = false;
            u.Hp = 0f;
            if (u.Team == Team.Player) { _alive0--; _unitsLost++; }
            else { _alive1--; _enemiesKilled++; }
            Emit(SimEventType.Death, u.Id, killerId, (int)u.Team, 0f, u.Pos.x, u.Pos.y);
        }

        public void Heal(SimUnit u, float amount)
        {
            if (!u.Alive) return;
            float before = u.Hp;
            u.Hp = Math.Min(u.MaxHp, u.Hp + amount);
            if (u.Hp > before) Emit(SimEventType.Heal, u.Id, 0, (int)u.Team, u.Hp - before, u.Pos.x, u.Pos.y);
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                var target = Units[p.TargetId - 1];
                if (!target.Alive) { RemoveProjectile(i); continue; }

                float dx = target.Pos.x - p.Pos.x, dy = target.Pos.y - p.Pos.y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = p.Speed * dt;
                if (dist <= step + target.Radius)
                {
                    DealDamage(target, p.Damage, p.SourceId);
                    RemoveProjectile(i);
                }
                else
                {
                    p.Pos.x += dx / dist * step;
                    p.Pos.y += dy / dist * step;
                }
            }
        }

        private void RemoveProjectile(int index)
        {
            var p = _projectiles[index];
            Emit(SimEventType.ProjectileEnd, p.Id, p.TargetId, p.Team, 0f, p.Pos.x, p.Pos.y);
            int last = _projectiles.Count - 1;
            _projectiles[index] = _projectiles[last];
            _projectiles.RemoveAt(last);
            _projectilePool.Push(p);
        }

        // ------------------------------------------------------------------ area effects

        /// <summary>Damage everything of <paramref name="victims"/> within radius after <paramref name="delay"/> seconds.</summary>
        public void QueueAoe(Vec2 pos, float radius, float damage, Team victims, float delay, int sourceId, bool slam)
        {
            var a = _aoePool.Count > 0 ? _aoePool.Pop() : new PendingAoe();
            a.TimeLeft = delay; a.Pos = pos; a.Radius = radius; a.Damage = damage;
            a.Victims = victims; a.SourceId = sourceId; a.Slam = slam;
            _aoes.Add(a);
            if (delay > 0f) Emit(SimEventType.Telegraph, sourceId, 0, (int)victims, radius, pos.x, pos.y);
        }

        private void UpdatePending(float dt)
        {
            for (int i = _aoes.Count - 1; i >= 0; i--)
            {
                var a = _aoes[i];
                a.TimeLeft -= dt;
                if (a.TimeLeft > 0f) continue;

                Emit(a.Slam ? SimEventType.Slam : SimEventType.SkillCast, a.SourceId, 0, (int)a.Victims, a.Radius, a.Pos.x, a.Pos.y);
                float rsq = a.Radius * a.Radius;
                for (int u = 0; u < Units.Count; u++)
                {
                    var unit = Units[u];
                    if (!unit.Alive || unit.Team != a.Victims) continue;
                    if (Vec2.SqrDistance(unit.Pos, a.Pos) <= rsq) DealDamage(unit, a.Damage, a.SourceId);
                }
                int last = _aoes.Count - 1;
                _aoes[i] = _aoes[last];
                _aoes.RemoveAt(last);
                _aoePool.Push(a);
            }
        }

        // ------------------------------------------------------------------ boss

        private void UpdateBoss(SimUnit boss, float dt)
        {
            var p = boss.Boss;
            if (!p.Enraged && boss.HpFraction <= p.EnrageHpPct)
            {
                p.Enraged = true;
                boss.HasteUntil = float.MaxValue;
                boss.HasteMult = p.EnrageMult;
                Emit(SimEventType.BossEnrage, boss.Id, 0, (int)boss.Team, 0f, boss.Pos.x, boss.Pos.y);
            }

            p.SlamTimer -= dt;
            if (p.SlamTimer <= 0f)
            {
                p.SlamTimer = p.SlamInterval;
                var target = boss.TargetId > 0 ? Units[boss.TargetId - 1] : null;
                Vec2 at = target != null && target.Alive ? target.Pos : boss.Pos;
                QueueAoe(at, p.SlamRadius, boss.Damage * p.SlamDamageMult, Team.Player, p.SlamTelegraphSeconds, boss.Id, true);
            }

            if (p.SummonCount > 0 && p.SummonInterval > 0f)
            {
                p.SummonTimer -= dt;
                if (p.SummonTimer <= 0f)
                {
                    p.SummonTimer = p.SummonInterval;
                    for (int i = 0; i < p.SummonCount; i++)
                    {
                        var spec = p.Minion;
                        float side = (i % 2 == 0 ? -1f : 1f) * (1.2f + 0.8f * (i / 2));
                        spec.Pos = new Vec2(boss.Pos.x + side, boss.Pos.y - 1.2f);
                        AddUnit(spec);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ skills support

        public void Buff(Team team, bool haste, float mult, float seconds)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Team != team) continue;
                if (haste) { u.HasteMult = mult; u.HasteUntil = Time + seconds; }
                else { u.ShieldMult = mult; u.ShieldUntil = Time + seconds; }
            }
        }

        public void HealTeam(Team team, float fractionOfMax)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive && u.Team == team) Heal(u, u.MaxHp * fractionOfMax);
            }
        }

        public void SpawnReinforcements(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var spec = Reinforcement;
                spec.Team = Team.Player;
                spec.Pos = new Vec2(_rng.Range(-3.5f, 3.5f), PlayerSpawnZ + _rng.Range(-0.5f, 0.5f));
                AddUnit(spec);
            }
        }

        /// <summary>Brings back fallen units of a team at a fraction of their max HP (rewarded revive).</summary>
        public int Revive(Team team, float hpFraction)
        {
            int revived = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive || u.Team != team) continue;
                u.Alive = true;
                u.Hp = Math.Max(1f, u.MaxHp * hpFraction);
                u.TargetId = 0;
                u.Cooldown = 0.3f;
                if (team == Team.Player) { _alive0++; if (_unitsLost > 0) _unitsLost--; } else { _alive1++; if (_enemiesKilled > 0) _enemiesKilled--; }
                Emit(SimEventType.Revive, u.Id, 0, (int)team, 0f, u.Pos.x, u.Pos.y);
                revived++;
            }
            if (revived > 0) Outcome = BattleOutcome.Running;
            return revived;
        }

        /// <summary>Time-out helper: allows the director to extend a battle after a revive.</summary>
        public void ExtendTime(float seconds) => _timeBonus += seconds;
        private float _timeBonus;

        // ------------------------------------------------------------------ separation

        private void Separate()
        {
            for (int i = 0; i < _buckets.Length; i++) _buckets[i].Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive) continue;
                _buckets[BucketIndex(u.Pos)].Add(i);
            }

            for (int i = 0; i < Units.Count; i++)
            {
                var a = Units[i];
                if (!a.Alive) continue;
                int cx = (int)((a.Pos.x - GridOriginX) / CellSize), cy = (int)((a.Pos.y - GridOriginY) / CellSize);
                for (int ox = -1; ox <= 1; ox++)
                {
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int nx = cx + ox, ny = cy + oy;
                        if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                        var bucket = _buckets[ny * GridW + nx];
                        for (int k = 0; k < bucket.Count; k++)
                        {
                            int j = bucket[k];
                            if (j <= i) continue;
                            Push(a, Units[j]);
                        }
                    }
                }
            }
        }

        private void Push(SimUnit a, SimUnit b)
        {
            if (a.IsFlying != b.IsFlying) return;
            float minDist = (a.Radius + b.Radius) * _cfg.separationRadius * 1.2f;
            float dx = a.Pos.x - b.Pos.x, dy = a.Pos.y - b.Pos.y;
            float sq = dx * dx + dy * dy;
            if (sq >= minDist * minDist) return;

            float dist = (float)Math.Sqrt(sq);
            float nx, ny;
            if (dist < 1e-4f) { nx = (a.Id & 1) == 0 ? 1f : -1f; ny = 0f; dist = 0f; }
            else { nx = dx / dist; ny = dy / dist; }

            float push = (minDist - dist) * 0.35f;
            a.Pos.x += nx * push; a.Pos.y += ny * push;
            b.Pos.x -= nx * push; b.Pos.y -= ny * push;
            Clamp(a); Clamp(b);
        }

        private int BucketIndex(Vec2 p)
        {
            int cx = (int)((p.x - GridOriginX) / CellSize), cy = (int)((p.y - GridOriginY) / CellSize);
            if (cx < 0) cx = 0; else if (cx >= GridW) cx = GridW - 1;
            if (cy < 0) cy = 0; else if (cy >= GridH) cy = GridH - 1;
            return cy * GridW + cx;
        }

        // ------------------------------------------------------------------ outcome

        private void CheckOutcome()
        {
            if (_alive0 <= 0) Finish(BattleOutcome.Lose);
            else if (_alive1 <= 0) Finish(BattleOutcome.Win);
            else if (Time >= _cfg.maxDurationSeconds + _timeBonus) Finish(BattleOutcome.Lose);
        }

        private void Finish(BattleOutcome outcome)
        {
            Outcome = outcome;
            Emit(SimEventType.Outcome, (int)outcome, 0, 0, 0f, 0f, 0f);
        }

        public BattleResult GetResult()
        {
            float hp = 0f;
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Alive && Units[i].Team == Team.Player) hp += Units[i].Hp;
            float pct = _initialPlayerHp > 0f ? Math.Min(1f, hp / _initialPlayerHp) : 0f;

            int stars = 0;
            if (Outcome == BattleOutcome.Win)
                stars = pct >= _cfg.threeStarHpPct ? 3 : pct >= _cfg.twoStarHpPct ? 2 : 1;

            return new BattleResult
            {
                Outcome = Outcome,
                Stars = stars,
                SurvivingHpPct = pct,
                Duration = Time,
                EnemiesKilled = _enemiesKilled,
                UnitsLost = _unitsLost
            };
        }

        private void Emit(SimEventType type, int a, int b, int team, float value, float x, float y)
        {
            Events.Add(new SimEvent { Type = type, A = a, B = b, Team = team, Value = value, X = x, Y = y });
        }
    }
}
