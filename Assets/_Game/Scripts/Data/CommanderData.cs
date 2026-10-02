using UnityEngine;

namespace MergeLegion.Data
{
    public enum PassiveType { None, RangedDamage, MeleeDamage, TankHp, FlyingDamage, AllHp, AllDamage, CoinBonus }

    public enum SkillType { None, Meteor, HealWave, Rally, ShieldWall, LightningChain, Summon }

    [CreateAssetMenu(menuName = "Merge Legion/Commander", fileName = "Commander")]
    public sealed class CommanderData : ScriptableObject
    {
        public string id;
        public string nameKey;
        public string passiveKey;
        public string skillKey;
        public PassiveType passiveType;
        public float passiveValue;       // 0.10 = +10%
        public SkillType skillType;
        public float cooldown = 25f;
        public float power = 1f;
        public float radius = 3f;
        public float duration = 6f;
        public int count = 1;
        public int unlockShards;         // 0 = owned from the start
        public int shardsPerLevel = 10;  // cost to reach level L+1 = L * shardsPerLevel
        public int maxLevel = 10;
        public float levelScale = 0.1f;  // +10% to passive and skill power per level
        public Color tint = Color.white;
    }
}
