// EXCLUDE_FROM_HARNESS
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Meta;
using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Names, colours and number formatting for rewards, used by every reward-showing UI.</summary>
    public static class ItemDisplay
    {
        public static Color ColorOf(RewardType type)
        {
            switch (type)
            {
                case RewardType.Coins: case RewardType.CoinsWins: return UIKit.Gold;
                case RewardType.Gems: return new Color(0.35f, 0.75f, 1f);
                case RewardType.ChestKeys: return UIKit.Purple;
                case RewardType.BattlePassXp: return UIKit.Good;
                case RewardType.Shards: return new Color(0.95f, 0.5f, 0.85f);
                case RewardType.Chest: return UIKit.Accent;
                default: return Color.white;
            }
        }

        public static string NameOf(GrantedItem item)
        {
            switch (item.Type)
            {
                case RewardType.Coins: return Loc.Get("item.coins");
                case RewardType.Gems: return Loc.Get("item.gems");
                case RewardType.ChestKeys: return Loc.Get("item.keys");
                case RewardType.BattlePassXp: return Loc.Get("item.bp_xp");
                case RewardType.VipDays: return Loc.Get("item.vip_days");
                case RewardType.RemoveAds: return Loc.Get("item.no_ads");
                case RewardType.Shards:
                    var c = ServiceLocator.TryGet<CommanderService>(out _) ? GameDatabase.Instance.GetCommander(item.Target) : null;
                    return Loc.Format("item.shards", c != null ? Loc.Get(c.nameKey) : item.Target);
                default: return item.Type.ToString();
            }
        }

        public static string NameOf(Reward r)
        {
            switch (r.type)
            {
                case RewardType.CoinsWins: return Loc.Get("item.coins");
                case RewardType.Chest: return Loc.Get(ChestKey(r.target));
                default: return NameOf(new GrantedItem(r.type, r.amount, r.target));
            }
        }

        public static string Describe(Reward r)
        {
            switch (r.type)
            {
                case RewardType.Chest: return Loc.Get(ChestKey(r.target));
                case RewardType.RemoveAds: return Loc.Get("item.no_ads");
                case RewardType.CoinsWins: return "+" + Format(ApproxCoins(r.amount)) + " " + Loc.Get("item.coins");
                case RewardType.Shards: return "+" + r.amount + " " + NameOf(r);
                default: return "+" + Format(r.amount) + " " + NameOf(r);
            }
        }

        private static long ApproxCoins(int wins)
        {
            if (ServiceLocator.TryGet<RewardGranter>(out var g)) return g.CoinsForWins(wins);
            return wins * 100L;
        }

        public static string ChestKey(string chestId) => "chest." + chestId;

        public static string Format(long n)
        {
            if (n >= 1000000000L) return (n / 1000000000.0).ToString("0.##") + "B";
            if (n >= 1000000L) return (n / 1000000.0).ToString("0.##") + "M";
            if (n >= 100000L) return (n / 1000.0).ToString("0.#") + "K";
            return n.ToString("N0");
        }

        public static string Duration(System.TimeSpan t)
        {
            if (t.TotalHours >= 24) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
            return $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
