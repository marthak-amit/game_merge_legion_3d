using System;
using MergeLegion.Data;

namespace MergeLegion.Levels
{
    /// <summary>
    /// Power metric used by the level designer, the procedural generator and balance tests.
    /// Unit strength = sqrt(hp * dps) so it scales ~1.9x per unit level; army power is the sum of unit strengths.
    /// </summary>
    public static class LevelPower
    {
        public static float UnitStrength(UnitLevelData l) => (float)Math.Sqrt(l.hp * l.damage * l.attackSpeed);

        public static float UnitStrength(GameDatabase db, int line, int level) =>
            UnitStrength(db.GetLine((UnitLineId)line).GetLevel(level));

        public static float ArmyPower(GameDatabase db, System.Collections.Generic.IEnumerable<EnemySpawnDef> units, float hpMult = 1f, float dmgMult = 1f)
        {
            float sum = 0f;
            foreach (var u in units) sum += UnitStrength(db, u.line, u.level);
            return sum * (float)Math.Sqrt(hpMult * dmgMult);
        }
    }
}
