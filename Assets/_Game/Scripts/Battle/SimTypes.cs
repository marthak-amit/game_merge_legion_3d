using System.Collections.Generic;

namespace MergeLegion.Battle
{
    public enum Team { Player = 0, Enemy = 1 }

    public enum BattleOutcome { Running, Win, Lose }

    public enum SimEventType
    {
        Spawn,
        Attack,
        ProjectileLaunch,
        ProjectileEnd,
        Hit,
        Heal,
        Death,
        SkillCast,
        SkillBolt,
        Telegraph,
        Slam,
        BossEnrage,
        Revive,
        Outcome
    }

    /// <summary>One thing that happened during a tick; the presentation layer turns these into VFX, numbers and sounds.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int A;        // primary unit/projectile id
        public int B;        // secondary id (killer, target)
        public int Team;     // team of the unit the event is about
        public float Value;  // damage / heal amount / radius
        public float X, Y;   // world position (x, z)
    }

    public struct UnitSpec
    {
        public Team Team;
        public int Line;
        public int Level;
        public float Hp;
        public float Damage;
        public float AttackSpeed;
        public float Range;
        public float MoveSpeed;
        public float Scale;
        public float ProjectileSpeed;
        public bool IsRanged;
        public bool IsFlying;
        public bool IsTaunt;
        public bool IsBoss;
        public Vec2 Pos;
    }

    public sealed class SimUnit
    {
        public int Id;
        public Team Team;
        public int Line;
        public int Level;
        public bool Alive;
        public bool IsBoss, IsRanged, IsFlying, IsTaunt;
        public float Hp, MaxHp, Damage, AttackInterval, Range, MoveSpeed, Radius, Scale, ProjectileSpeed;
        public Vec2 Pos, Prev;
        public float Cooldown;
        public int TargetId;
        public float RetargetTimer;
        public float HasteUntil, HasteMult = 1f;
        public float ShieldUntil, ShieldMult = 1f;
        public BossPattern Boss;

        public float HpFraction => MaxHp > 0f ? Hp / MaxHp : 0f;
    }

    public sealed class BossPattern
    {
        public float SlamInterval = 6f;
        public float SlamRadius = 3.2f;
        public float SlamDamageMult = 2.5f;     // x boss damage
        public float SlamTelegraphSeconds = 0.9f;
        public float SummonInterval = 10f;
        public int SummonCount = 2;
        public UnitSpec Minion;
        public float EnrageHpPct = 0.3f;
        public float EnrageMult = 1.5f;

        public float SlamTimer;
        public float SummonTimer;
        public bool Enraged;
    }

    public sealed class SimProjectile
    {
        public int Id;
        public int Team;
        public int TargetId;
        public int SourceId;
        public Vec2 Pos;
        public float Speed;
        public float Damage;
        public int Line;
    }

    public sealed class PendingAoe
    {
        public float TimeLeft;
        public Vec2 Pos;
        public float Radius;
        public float Damage;
        public Team Victims;
        public int SourceId;
        public bool Slam;
    }

    public struct BattleResult
    {
        public BattleOutcome Outcome;
        public int Stars;
        public float SurvivingHpPct;
        public float Duration;
        public int EnemiesKilled;
        public int UnitsLost;
    }
}
