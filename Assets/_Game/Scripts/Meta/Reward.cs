using System;
using System.Collections.Generic;

namespace MergeLegion.Meta
{
    public enum RewardType
    {
        Coins,
        Gems,
        ChestKeys,
        BattlePassXp,
        Shards,      // target = commander id or "random"
        CoinsWins,   // amount x the coins of one 1-star win at the current level: scales with progress
        Chest,       // target = chest id, opened on grant
        VipDays,
        RemoveAds
    }

    /// <summary>A reward as authored in config JSON.</summary>
    [Serializable]
    public sealed class Reward
    {
        public RewardType type;
        public int amount;
        public string target;

        public Reward() { }
        public Reward(RewardType type, int amount, string target = null) { this.type = type; this.amount = amount; this.target = target; }
    }

    /// <summary>A reward after resolution (what the player actually received), for reward popups.</summary>
    public struct GrantedItem
    {
        public RewardType Type;
        public long Amount;
        public string Target;

        public GrantedItem(RewardType type, long amount, string target = null) { Type = type; Amount = amount; Target = target; }
    }

    [Serializable]
    public sealed class RewardList
    {
        public List<Reward> items = new List<Reward>();
    }
}
