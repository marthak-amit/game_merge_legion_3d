using System.Collections.Generic;
using System.IO;
using MergeLegion.Data;
using UnityEditor;
using UnityEngine;

namespace MergeLegion.Editor
{
    /// <summary>
    /// Balance CSV (Resources/Balance) -> ScriptableObject assets + GameDatabase. Re-importing updates stats
    /// but keeps prefab / VFX references you assigned (the art swap path).
    /// </summary>
    public static class BalanceImporter
    {
        public const string UnitFolder = "Assets/_Game/Data/Units";
        public const string DatabasePath = "Assets/_Game/Resources/GameDatabase.asset";

        [MenuItem("Tools/Merge Legion/Import Balance")]
        public static void Import()
        {
            var linesCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Resources/Balance/lines.csv");
            var unitsCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Resources/Balance/units.csv");
            if (linesCsv == null || unitsCsv == null)
            {
                Debug.LogError("[Balance] lines.csv / units.csv not found in Assets/_Game/Resources/Balance");
                return;
            }

            Directory.CreateDirectory(UnitFolder);
            var built = BalanceBuilder.BuildLines(linesCsv.text, unitsCsv.text);
            var assets = new List<UnitLineData>();

            foreach (var fresh in built)
            {
                string path = $"{UnitFolder}/{fresh.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<UnitLineData>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(fresh, path);
                    assets.Add(fresh);
                    continue;
                }

                CopyStats(fresh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
                assets.Add(existing);
            }

            var commanders = ImportCommanders();

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.lines = assets;
            db.commanders = commanders;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            GameDatabase.SetInstance(null);
            Debug.Log($"[Balance] Imported {assets.Count} unit lines and {commanders.Count} commanders.");
        }

        private static List<CommanderData> ImportCommanders()
        {
            var result = new List<CommanderData>();
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Resources/Balance/commanders.csv");
            if (csv == null) return result;

            const string folder = "Assets/_Game/Data/Commanders";
            Directory.CreateDirectory(folder);
            foreach (var fresh in BalanceBuilder.BuildCommanders(csv.text))
            {
                string path = $"{folder}/{fresh.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<CommanderData>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(fresh, path);
                    result.Add(fresh);
                    continue;
                }
                EditorUtility.CopySerialized(fresh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
                result.Add(existing);
            }
            return result;
        }

        private static void CopyStats(UnitLineData from, UnitLineData to)
        {
            to.id = from.id;
            to.nameKey = from.nameKey;
            to.roleKey = from.roleKey;
            to.unlockLevel = from.unlockLevel;
            to.baseCost = from.baseCost;
            to.costGrowth = from.costGrowth;
            to.isRanged = from.isRanged;
            to.isFlying = from.isFlying;
            to.isTaunt = from.isTaunt;
            to.projectileSpeed = from.projectileSpeed;

            while (to.levels.Count < from.levels.Count) to.levels.Add(new UnitLevelData());
            if (to.levels.Count > from.levels.Count) to.levels.RemoveRange(from.levels.Count, to.levels.Count - from.levels.Count);
            for (int i = 0; i < from.levels.Count; i++)
            {
                var f = from.levels[i];
                var t = to.levels[i];
                t.level = f.level; t.hp = f.hp; t.damage = f.damage; t.attackSpeed = f.attackSpeed;
                t.range = f.range; t.moveSpeed = f.moveSpeed; t.scale = f.scale; t.tint = f.tint;
                // prefab, attackVfx, deathVfx are intentionally left as authored
            }
        }
    }
}
