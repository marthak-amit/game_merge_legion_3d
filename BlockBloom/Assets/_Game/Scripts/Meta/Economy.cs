using System;
using BlockBloom.Logic;

namespace BlockBloom
{
    /// <summary>Hearts, coins, boosters, rewards - every number that shapes the economy lives here so it is easy to tune.</summary>
    public static class Economy
    {
        public const int MaxHearts = 5;
        public const int HeartRegenMinutes = 20;
        public const int HeartRefillCost = 120;
        public const int ReviveCost = 150;
        public const int ExtraMovesCost = 100;
        public const int ExtraMovesAmount = 5;
        public const int PriceUndo = 80, PriceBomb = 120, PriceShuffle = 100;
        public const int FreeLevelsBeforeAds = 4;
        public const int InterstitialEveryNthEnd = 3;
        public const float InterstitialMinSeconds = 100f;
        public static readonly int[] LoginRewards = { 50, 80, 120, 160, 220, 300, 500 };
        public static readonly int[] SpinPrizes = { 30, 60, 100, 50, 200, 80, 40, 400 };   // coins per wheel sector
        public const int ChestStars = 15;
        public const int ChestCoins = 250;

        // ---------- hearts ----------
        public static void TickHearts()
        {
            var d = Save.Data;
            if (d.hearts >= MaxHearts) { d.hearts = MaxHearts; d.heartNextTicks = 0; return; }
            long now = DateTime.UtcNow.Ticks;
            long step = TimeSpan.FromMinutes(HeartRegenMinutes).Ticks;
            bool changed = false;
            while (d.hearts < MaxHearts && d.heartNextTicks != 0 && now >= d.heartNextTicks)
            {
                d.hearts++; d.heartNextTicks += step; changed = true;
            }
            if (d.hearts >= MaxHearts) d.heartNextTicks = 0;
            else if (d.heartNextTicks == 0) { d.heartNextTicks = now + step; changed = true; }
            if (changed) Save.Commit();
        }

        public static void LoseHeart()
        {
            var d = Save.Data;
            TickHearts();
            if (d.hearts <= 0) return;
            if (d.hearts >= MaxHearts) d.heartNextTicks = DateTime.UtcNow.Ticks + TimeSpan.FromMinutes(HeartRegenMinutes).Ticks;
            d.hearts--;
            Save.Commit();
        }

        public static void AddHearts(int n)
        {
            var d = Save.Data;
            d.hearts = Math.Min(MaxHearts, d.hearts + n);
            if (d.hearts >= MaxHearts) d.heartNextTicks = 0;
            Save.Commit();
        }

        public static TimeSpan UntilNextHeart()
        {
            var d = Save.Data;
            if (d.hearts >= MaxHearts || d.heartNextTicks == 0) return TimeSpan.Zero;
            var t = new TimeSpan(d.heartNextTicks - DateTime.UtcNow.Ticks);
            return t < TimeSpan.Zero ? TimeSpan.Zero : t;
        }

        // ---------- coins ----------
        public static void AddCoins(int n) { Save.Data.coins += n; Save.Commit(); }
        public static bool Spend(int n)
        {
            if (Save.Data.coins < n) return false;
            Save.Data.coins -= n; Save.Commit(); return true;
        }

        // ---------- boosters ----------
        public static int BoosterCount(int kind) { var d = Save.Data; return kind == 0 ? d.boosterUndo : (kind == 1 ? d.boosterBomb : d.boosterShuffle); }
        public static void AddBooster(int kind, int n)
        {
            var d = Save.Data;
            if (kind == 0) d.boosterUndo += n; else if (kind == 1) d.boosterBomb += n; else d.boosterShuffle += n;
            Save.Commit();
        }
        public static bool UseBooster(int kind)
        {
            if (BoosterCount(kind) <= 0) return false;
            AddBooster(kind, -1); return true;
        }
        public static int BoosterPrice(int kind) { return kind == 0 ? PriceUndo : (kind == 1 ? PriceBomb : PriceShuffle); }
        public static string BoosterName(int kind) { return kind == 0 ? "UNDO" : (kind == 1 ? "BOMB" : "SHUFFLE"); }

        // ---------- daily login / spin ----------
        public static bool LoginAvailable { get { return Save.Data.lastLoginClaim != Save.Today; } }
        public static int NextLoginDay
        {
            get
            {
                var d = Save.Data;
                if (d.lastLoginClaim == Save.Today) return d.streak;
                string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
                int s = d.lastLoginClaim == yesterday ? d.streak : 0;
                return s % LoginRewards.Length;
            }
        }
        public static int ClaimLogin()
        {
            var d = Save.Data;
            int day = NextLoginDay;
            int reward = LoginRewards[day];
            d.streak = day + 1;
            d.lastLoginClaim = Save.Today;
            d.coins += reward;
            if (day == LoginRewards.Length - 1) { d.boosterBomb++; d.boosterUndo++; d.boosterShuffle++; }
            Save.Commit();
            return reward;
        }
        public static bool SpinAvailable { get { return Save.Data.lastSpin != Save.Today; } }

        // ---------- daily quests ----------
        public sealed class Quest { public string Text; public int Target; public int Reward; }
        public static readonly Quest[] Quests =
        {
            new Quest { Text = "Clear 25 lines", Target = 25, Reward = 60 },
            new Quest { Text = "Play 3 games", Target = 3, Reward = 50 },
            new Quest { Text = "Make a x3 combo", Target = 1, Reward = 80 },
        };
        public static void EnsureQuestDay()
        {
            var d = Save.Data;
            if (d.questDay == Save.Today) return;
            d.questDay = Save.Today;
            d.questProgress = new int[3]; d.questClaimed = new bool[3];
            Save.Commit();
        }
        public static void QuestAdd(int q, int n)
        {
            EnsureQuestDay();
            var d = Save.Data;
            d.questProgress[q] = Math.Min(Quests[q].Target, d.questProgress[q] + n);
        }

        // ---------- chest ----------
        public static bool AddStars(int n, out bool chestReady)
        {
            var d = Save.Data;
            d.chestProgress += n;
            chestReady = d.chestProgress >= ChestStars;
            return chestReady;
        }

        public static int DailySeed()
        {
            var t = DateTime.Now;
            return t.Year * 10000 + t.Month * 100 + t.Day;
        }

        // ---------- shop catalogue ----------
        public sealed class Product { public string Sku; public string Title; public string Sub; public int Coins; public int Price; public ProductKindEx Kind; }
        public enum ProductKindEx { Coins, RemoveAds, Starter }
        public static readonly Product[] Products =
        {
            new Product { Sku = "bb_starter",   Title = "STARTER PACK", Sub = "Best value, once only", Coins = 800, Price = 49,  Kind = ProductKindEx.Starter },
            new Product { Sku = "bb_remove_ads", Title = "NO ADS",       Sub = "Remove all pop-up ads", Coins = 0,   Price = 199, Kind = ProductKindEx.RemoveAds },
            new Product { Sku = "bb_coins_s",   Title = "HANDFUL",      Sub = "", Coins = 600,  Price = 79,  Kind = ProductKindEx.Coins },
            new Product { Sku = "bb_coins_m",   Title = "POUCH",        Sub = "", Coins = 1800, Price = 199, Kind = ProductKindEx.Coins },
            new Product { Sku = "bb_coins_l",   Title = "CHEST",        Sub = "", Coins = 4200, Price = 399, Kind = ProductKindEx.Coins },
            new Product { Sku = "bb_coins_xl",  Title = "VAULT",        Sub = "", Coins = 10000, Price = 749, Kind = ProductKindEx.Coins },
        };
    }
}
