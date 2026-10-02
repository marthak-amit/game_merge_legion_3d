using System;
using MergeLegion.Data;

namespace MergeLegion.Economy
{
    public struct LevelReward
    {
        public long Coins;
        public long Gems;
        public int Keys;
        public int BattlePassXp;
        public int Stars;
    }

    /// <summary>Pure reward formulas (all numbers from RewardConfig). Granting is done by the caller via CurrencyService.</summary>
    public static class RewardService
    {
        public static long WinCoins(RewardConfig cfg, int level, int stars, float coinBonus = 0f)
        {
            double baseCoins = cfg.levelCoinBase * Math.Pow(cfg.levelCoinGrowth, Math.Max(0, level - 1));
            double starMult = 1.0 + cfg.starBonusPct * Math.Max(0, stars - 1);
            return Math.Max(1L, (long)Math.Round(baseCoins * starMult * (1.0 + coinBonus)));
        }

        public static long LoseCoins(RewardConfig cfg, int level, float coinBonus = 0f)
        {
            return Math.Max(1L, (long)Math.Round(WinCoins(cfg, level, 1, coinBonus) * cfg.loseConsolationPct));
        }

        public static LevelReward ForWin(RewardConfig cfg, int level, int stars, bool isBoss, float coinBonus = 0f)
        {
            return new LevelReward
            {
                Coins = WinCoins(cfg, level, stars, coinBonus),
                Gems = isBoss ? cfg.bossBonusGems : 0,
                Keys = cfg.keyProgressPerWin,
                BattlePassXp = isBoss ? cfg.battlePassXpPerBossWin : cfg.battlePassXpPerWin,
                Stars = stars
            };
        }

        public static LevelReward ForLoss(RewardConfig cfg, int level, float coinBonus = 0f)
        {
            return new LevelReward { Coins = LoseCoins(cfg, level, coinBonus) };
        }

        /// <summary>Rewarded-ad multiplier applies to the coin part only.</summary>
        public static LevelReward WithAdMultiplier(RewardConfig cfg, LevelReward r)
        {
            r.Coins *= Math.Max(1, cfg.adMultiplier);
            return r;
        }
    }
}
