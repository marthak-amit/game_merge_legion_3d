using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Data
{
    [CreateAssetMenu(menuName = "Merge Legion/Unit Line", fileName = "UnitLine")]
    public sealed class UnitLineData : ScriptableObject
    {
        public UnitLineId id;
        public string nameKey;
        public string roleKey;
        public int unlockLevel = 1;
        public int baseCost = 50;
        public float costGrowth = 1.15f;
        public bool isRanged;
        public bool isFlying;
        public bool isTaunt;
        public float projectileSpeed = 12f;
        public List<UnitLevelData> levels = new List<UnitLevelData>();

        public int MaxLevel => levels.Count;

        public UnitLevelData GetLevel(int level)
        {
            int i = Mathf.Clamp(level, 1, levels.Count) - 1;
            return levels[i];
        }
    }
}
