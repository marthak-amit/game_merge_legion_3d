using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Data
{
    /// <summary>Turns the balance CSVs into ScriptableObjects. Shared by the Editor importer and the runtime fallback.</summary>
    public static class BalanceBuilder
    {
        public static List<UnitLineData> BuildLines(string linesCsv, string unitsCsv)
        {
            var result = new List<UnitLineData>();
            var lines = CsvTable.Parse(linesCsv);
            var units = CsvTable.Parse(unitsCsv);

            for (int r = 0; r < lines.RowCount; r++)
            {
                var line = ScriptableObject.CreateInstance<UnitLineData>();
                string name = lines.Str(r, "line");
                line.name = name;
                line.id = (UnitLineId)Enum.Parse(typeof(UnitLineId), name, true);
                line.nameKey = lines.Str(r, "name_key");
                line.roleKey = lines.Str(r, "role_key");
                line.unlockLevel = lines.Int(r, "unlock_level", 1);
                line.baseCost = lines.Int(r, "base_cost", 50);
                line.costGrowth = lines.Float(r, "cost_growth", 1.15f);
                line.isRanged = lines.Bool(r, "ranged");
                line.isFlying = lines.Bool(r, "flying");
                line.isTaunt = lines.Bool(r, "taunt");
                line.projectileSpeed = lines.Float(r, "projectile_speed", 12f);

                for (int u = 0; u < units.RowCount; u++)
                {
                    if (!string.Equals(units.Str(u, "line"), name, StringComparison.OrdinalIgnoreCase)) continue;
                    line.levels.Add(new UnitLevelData
                    {
                        level = units.Int(u, "level"),
                        hp = units.Float(u, "hp"),
                        damage = units.Float(u, "damage"),
                        attackSpeed = units.Float(u, "attack_speed", 1f),
                        range = units.Float(u, "range", 1.4f),
                        moveSpeed = units.Float(u, "move_speed", 3f),
                        scale = units.Float(u, "scale", 1f),
                        tint = ParseColor(units.Str(u, "tint"))
                    });
                }
                line.levels.Sort((a, b) => a.level.CompareTo(b.level));
                result.Add(line);
            }
            return result;
        }

        public static List<CommanderData> BuildCommanders(string csv)
        {
            var result = new List<CommanderData>();
            var t = CsvTable.Parse(csv);
            for (int r = 0; r < t.RowCount; r++)
            {
                var c = ScriptableObject.CreateInstance<CommanderData>();
                c.id = t.Str(r, "id");
                c.name = c.id;
                c.nameKey = t.Str(r, "name_key");
                c.passiveKey = t.Str(r, "passive_key");
                c.skillKey = t.Str(r, "skill_key");
                c.passiveType = (PassiveType)Enum.Parse(typeof(PassiveType), t.Str(r, "passive_type", "None"), true);
                c.passiveValue = t.Float(r, "passive_value");
                c.skillType = (SkillType)Enum.Parse(typeof(SkillType), t.Str(r, "skill_type", "None"), true);
                c.cooldown = t.Float(r, "cooldown", 25f);
                c.power = t.Float(r, "power", 1f);
                c.radius = t.Float(r, "radius", 3f);
                c.duration = t.Float(r, "duration", 6f);
                c.count = t.Int(r, "count", 1);
                c.unlockShards = t.Int(r, "unlock_shards");
                c.shardsPerLevel = t.Int(r, "shards_per_level", 10);
                c.maxLevel = t.Int(r, "max_level", 10);
                c.levelScale = t.Float(r, "level_scale", 0.1f);
                c.tint = ParseColor(t.Str(r, "tint"));
                result.Add(c);
            }
            return result;
        }

        public static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7) return Color.white;
            float r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255f;
            float g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255f;
            float b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255f;
            return new Color(r, g, b, 1f);
        }
    }
}
