using System;
using UnityEngine;

namespace MergeLegion.Data
{
    /// <summary>Stats and presentation for one level of a unit line. Prefabs may be null: a primitive is built instead.</summary>
    [Serializable]
    public sealed class UnitLevelData
    {
        public int level;
        public float hp;
        public float damage;
        public float attackSpeed;   // attacks per second
        public float range;         // world units, edge to edge
        public float moveSpeed;
        public float scale;
        public Color tint = Color.white;
        public GameObject prefab;
        public GameObject attackVfx;
        public GameObject deathVfx;
    }
}
