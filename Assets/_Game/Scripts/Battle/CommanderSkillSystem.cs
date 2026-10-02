using System;
using MergeLegion.Data;
using MergeLegion.Meta;

namespace MergeLegion.Battle
{
    /// <summary>Runs the equipped commander's active skill during a battle (section 1.3).</summary>
    public sealed class CommanderSkillSystem
    {
        private readonly SkillSpec _spec;
        private float _damageBase;

        public SkillType Type => _spec.Type;
        public float Cooldown => _spec.Cooldown;
        public float Remaining { get; private set; }
        public bool Ready => _spec.Type != SkillType.None && Remaining <= 0f;
        public float ReadyFraction => _spec.Cooldown <= 0f ? 1f : 1f - Math.Max(0f, Remaining) / _spec.Cooldown;
        public int Casts { get; private set; }

        public CommanderSkillSystem(SkillSpec spec, float startCooldownFraction = 0.4f)
        {
            _spec = spec;
            Remaining = spec.Cooldown * startCooldownFraction;
        }

        /// <summary>Skill damage scales with the player's army: base = mean unit damage at battle start.</summary>
        public void Begin(BattleSim sim)
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Team != Team.Player) continue;
                sum += u.Damage; n++;
            }
            _damageBase = n > 0 ? sum / n : 10f;
        }

        public void Update(float dt)
        {
            if (Remaining > 0f) Remaining -= dt;
        }

        public bool TryCast(BattleSim sim)
        {
            if (!Ready || sim.Outcome != BattleOutcome.Running) return false;
            switch (_spec.Type)
            {
                case SkillType.Meteor: CastMeteor(sim); break;
                case SkillType.HealWave: sim.HealTeam(Team.Player, _spec.Power); Announce(sim, 0f, 0f); break;
                case SkillType.Rally: sim.Buff(Team.Player, true, 1f + _spec.Power, _spec.Duration); Announce(sim, 0f, 0f); break;
                case SkillType.ShieldWall: sim.Buff(Team.Player, false, Math.Max(0.05f, 1f - _spec.Power), _spec.Duration); Announce(sim, 0f, 0f); break;
                case SkillType.LightningChain: CastLightning(sim); break;
                case SkillType.Summon: sim.SpawnReinforcements(_spec.Count); Announce(sim, 0f, 0f); break;
                default: return false;
            }
            Remaining = _spec.Cooldown;
            Casts++;
            return true;
        }

        private void Announce(BattleSim sim, float x, float y)
        {
            sim.Events.Add(new SimEvent { Type = SimEventType.SkillCast, A = (int)_spec.Type, B = -1, Team = (int)Team.Player, Value = _spec.Radius, X = x, Y = y });
        }

        private void CastMeteor(BattleSim sim)
        {
            // Strike the densest enemy cluster.
            Vec2 best = new Vec2(0f, 0f);
            int bestCount = -1;
            float rsq = _spec.Radius * _spec.Radius;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var a = sim.Units[i];
                if (!a.Alive || a.Team != Team.Enemy) continue;
                int count = 0;
                for (int j = 0; j < sim.Units.Count; j++)
                {
                    var b = sim.Units[j];
                    if (b.Alive && b.Team == Team.Enemy && Vec2.SqrDistance(a.Pos, b.Pos) <= rsq) count++;
                }
                if (count > bestCount) { bestCount = count; best = a.Pos; }
            }
            if (bestCount < 0) return;
            sim.QueueAoe(best, _spec.Radius, _damageBase * _spec.Power, Team.Enemy, 0.7f, -(int)SkillType.Meteor, false);
        }

        private void CastLightning(BattleSim sim)
        {
            // Start on the healthiest enemy, then hop to the nearest unvisited enemy in radius.
            SimUnit current = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Alive && u.Team == Team.Enemy && (current == null || u.Hp > current.Hp)) current = u;
            }
            if (current == null) return;

            float damage = _damageBase * _spec.Power;
            float rsq = _spec.Radius * _spec.Radius;
            int hops = Math.Max(1, _spec.Count);
            int[] visited = new int[hops];
            for (int hop = 0; hop < hops && current != null; hop++)
            {
                visited[hop] = current.Id;
                sim.Events.Add(new SimEvent { Type = SimEventType.SkillBolt, A = current.Id, B = hop, Team = (int)Team.Player, Value = damage, X = current.Pos.x, Y = current.Pos.y });
                sim.DealDamage(current, damage, -(int)SkillType.LightningChain);
                damage *= 0.85f;

                SimUnit next = null;
                float bestSq = float.MaxValue;
                for (int i = 0; i < sim.Units.Count; i++)
                {
                    var u = sim.Units[i];
                    if (!u.Alive || u.Team != Team.Enemy) continue;
                    bool seen = false;
                    for (int v = 0; v <= hop; v++) if (visited[v] == u.Id) { seen = true; break; }
                    if (seen) continue;
                    float d = Vec2.SqrDistance(current.Pos, u.Pos);
                    if (d <= rsq && d < bestSq) { bestSq = d; next = u; }
                }
                current = next;
            }
        }
    }
}
