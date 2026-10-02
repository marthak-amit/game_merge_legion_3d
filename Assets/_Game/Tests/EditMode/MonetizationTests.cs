using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Services.Mock;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public abstract class MonetizationFixture : MetaFixture
    {
        protected MonetizationConfig Shop;
        protected MockIAPService MockIap;
        protected MockReceiptValidator Validator;
        protected IapManager Iap;
        protected BattlePassService Bp;
        protected PiggyService Piggy;
        protected VipService Vip;
        protected OfferService Offers;
        protected CommanderService Commanders;
        protected MockAttributionService Attribution;

        [SetUp]
        public void SetUpMonetization()
        {
            Shop = MonetizationConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/monetization_config").text);
            MockIap = new MockIAPService
            {
                PriceLookup = sku => Shop.Product(sku) != null ? (decimal)Shop.Product(sku).priceUsd : 0.99m,
                KindLookup = sku => Shop.Product(sku) != null ? Shop.Product(sku).kind : ProductKind.Consumable
            };
            Validator = new MockReceiptValidator();
            Attribution = new MockAttributionService();
            Iap = new IapManager(MockIap, Shop, Granter, Save, Analytics, Attribution, Validator, Ads);
            Iap.Products.GetEnumerator();
            MockIap.Initialize(Iap.Skus());

            Bp = new BattlePassService(Save, Shop.battlePass, Time, Granter, Analytics);
            Piggy = new PiggyService(Save, Shop.piggy, Currency);
            Vip = new VipService(Save, Shop.vip, Time, Daily, Granter);
            Offers = new OfferService(Save, Shop, Time);
            Commanders = new CommanderService(Save, Currency, Db);
            Iap.RegisterSpecial(Shop.battlePass.premiumSku, d => Bp.UnlockPremium());
            Iap.RegisterSpecial(Shop.vip.sku, d => Vip.Activate());
            Iap.RegisterSpecial(Shop.piggy.sku, d => Piggy.Break());
            foreach (var p in Shop.products)
            {
                if (string.IsNullOrEmpty(p.commanderId)) continue;
                string cid = p.commanderId;
                Iap.RegisterSpecial(p.sku, d => Commanders.GrantUnlock(cid));
            }
        }

        [TearDown]
        public void TearDownMonetization()
        {
            Bp.Dispose();
            Piggy.Dispose();
            Offers.Dispose();
        }

        protected PurchaseResult Buy(string sku)
        {
            PurchaseResult res = new PurchaseResult();
            Iap.Purchase(sku, r => res = r);
            return res;
        }
    }

    public class ShopCatalogTests : MonetizationFixture
    {
        [Test]
        public void Catalog_ContainsEverySkuFromTheSpec()
        {
            foreach (var sku in new[]
            {
                "no_ads", "starter_pack", "gems_small", "gems_med", "gems_large", "gems_huge", "gems_mega",
                "battle_pass_premium", "vip_weekly", "piggy_bank",
                "commander_bundle_selene", "commander_bundle_voltra", "commander_bundle_nyx"
            })
                Assert.IsNotNull(Shop.Product(sku), sku);
        }

        [Test]
        public void Prices_MatchSpec()
        {
            Assert.AreEqual(3.99f, Shop.Product("no_ads").priceUsd, 0.001);
            Assert.AreEqual(1.99f, Shop.Product("starter_pack").priceUsd, 0.001);
            Assert.AreEqual(0.99f, Shop.Product("gems_small").priceUsd, 0.001);
            Assert.AreEqual(49.99f, Shop.Product("gems_mega").priceUsd, 0.001);
            Assert.AreEqual(4.99f, Shop.Product("battle_pass_premium").priceUsd, 0.001);
            Assert.AreEqual(4.99f, Shop.Product("vip_weekly").priceUsd, 0.001);
            Assert.AreEqual(ProductKind.Subscription, Shop.Product("vip_weekly").kind);
            Assert.AreEqual(2.99f, Shop.Product("piggy_bank").priceUsd, 0.001);
        }

        [Test]
        public void GemPacks_GetBetterValueAtEachTier()
        {
            float prev = 0f;
            foreach (var sku in new[] { "gems_small", "gems_med", "gems_large", "gems_huge", "gems_mega" })
            {
                var p = Shop.Product(sku);
                float gems = p.rewards[0].amount;
                float perDollar = gems / p.priceUsd;
                Assert.Greater(perDollar, prev, sku);
                prev = perDollar;
            }
        }

        [Test]
        public void PriceLabel_UsesStorePriceWhenAvailable()
        {
            Assert.AreEqual("$3.99", Iap.PriceLabel("no_ads"));
            Assert.AreEqual("$49.99", Iap.PriceLabel("gems_mega"));
        }

        [Test]
        public void EveryOfferReferencesARealProduct()
        {
            foreach (var o in Shop.offers) Assert.IsNotNull(Shop.Product(o.sku), o.id);
        }
    }

    public class IapFlowTests : MonetizationFixture
    {
        [Test]
        public void GemPack_GrantsGems_AndLogsAnalyticsAndAttribution()
        {
            var res = Buy("gems_med");
            Assert.IsTrue(res.Success);
            Assert.AreEqual(550, Currency.Get(CurrencyType.Gems));
            Assert.IsTrue(Analytics.Events.Exists(e => e.StartsWith("iap_purchase") && e.Contains("gems_med")));
            Assert.IsFalse(Iap.Owned("gems_med"), "consumables are not tracked as owned");
        }

        [Test]
        public void NoAds_RemovesForcedAds_KeepsRewarded_AndGivesBonusGems()
        {
            Assert.IsTrue(Buy("no_ads").Success);
            Assert.IsTrue(Save.Data.noAds);
            Assert.IsTrue(MockAds.ForcedAdsRemoved);
            Assert.AreEqual(150, Currency.Get(CurrencyType.Gems));
            Assert.IsTrue(Iap.Owned("no_ads"));
            Save.Data.SetFlag(SaveFlags.TutorialDone);
            Assert.IsFalse(Ads.CanShowInterstitial(50));
            bool rewarded = false;
            Ads.ShowRewarded(AdPlacements.Win3x, ok => rewarded = ok);
            Assert.IsTrue(rewarded);
        }

        [Test]
        public void OneTimeProducts_CannotBeBoughtTwice()
        {
            Assert.IsTrue(Buy("starter_pack").Success);
            long gems = Currency.Get(CurrencyType.Gems);
            Assert.AreEqual(PurchaseStatus.AlreadyOwned, Buy("starter_pack").Status);
            Assert.AreEqual(gems, Currency.Get(CurrencyType.Gems));
        }

        [Test]
        public void CancelledOrFailedPurchase_GrantsNothing()
        {
            MockIap.NextStatus = PurchaseStatus.Cancelled;
            Assert.AreEqual(PurchaseStatus.Cancelled, Buy("gems_small").Status);
            MockIap.NextStatus = PurchaseStatus.Failed;
            Assert.AreEqual(PurchaseStatus.Failed, Buy("gems_small").Status);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gems));
        }

        [Test]
        public void InvalidReceipt_BlocksFulfilment()
        {
            Validator.Result = false;
            Assert.AreEqual(PurchaseStatus.Failed, Buy("gems_large").Status);
            Assert.AreEqual(0, Currency.Get(CurrencyType.Gems));
        }

        [Test]
        public void UnknownSku_Fails()
        {
            Assert.AreEqual(PurchaseStatus.Failed, Buy("does_not_exist").Status);
        }

        [Test]
        public void CommanderBundle_UnlocksCommanderAndGrantsShards()
        {
            Assert.IsFalse(Commanders.IsUnlocked("voltra"));
            Assert.IsTrue(Buy("commander_bundle_voltra").Success);
            Assert.IsTrue(Commanders.IsUnlocked("voltra"));
            Assert.AreEqual(60, Currency.GetShards("voltra"));
            Assert.AreEqual(250, Currency.Get(CurrencyType.Gems));
            Assert.AreEqual(3, Currency.Get(CurrencyType.ChestKeys));
        }

        [Test]
        public void Restore_ReappliesNoAds()
        {
            Buy("no_ads");
            Save.Data.noAds = false;
            Ads.RefreshEntitlements();
            bool ok = false; int restored = 0;
            Iap.Restore((o, n) => { ok = o; restored = n; });
            Assert.IsTrue(ok);
            Assert.AreEqual(1, restored);
            Assert.IsTrue(Save.Data.noAds);
        }

        [Test]
        public void Purchase_IsPersistedImmediately()
        {
            var storage = new InMemorySaveStorage();
            var save = new SaveService(storage, Time);
            save.Load();
            var granter = new RewardGranter(save, new CurrencyService(save, Analytics), Config, Db, Time, new DeterministicRng(1), () => 1);
            var iap = new IapManager(MockIap, Shop, granter, save, Analytics, Attribution, Validator,
                new AdsManager(MockAds, Config, new DailyService(save, Time), save, Time, Analytics, Attribution));
            iap.Purchase("gems_small", r => { });
            var reloaded = new SaveService(storage, Time);
            reloaded.Load();
            Assert.AreEqual(100, reloaded.Data.gems);
        }
    }

    public class BattlePassTests : MonetizationFixture
    {
        [Test]
        public void ThirtyTiers_WithBothTracks()
        {
            Assert.AreEqual(30, Bp.TierCount);
            for (int t = 1; t <= 30; t++)
            {
                Assert.IsNotNull(Bp.TierDef(t).free, "free " + t);
                Assert.IsNotNull(Bp.TierDef(t).premium, "premium " + t);
            }
        }

        [Test]
        public void Xp_Progresses_Tiers_AndMaxesOut()
        {
            Assert.AreEqual(0, Bp.Tier);
            Currency.Add(CurrencyType.BattlePassXp, 99, "t");
            Assert.AreEqual(0, Bp.Tier);
            Currency.Add(CurrencyType.BattlePassXp, 1, "t");
            Assert.AreEqual(1, Bp.Tier);
            Assert.AreEqual(100 + 110, Bp.XpToReach(2));
            Currency.Add(CurrencyType.BattlePassXp, 1000000, "t");
            Assert.AreEqual(30, Bp.Tier);
            Bp.TierProgress(out long have, out long need);
            Assert.AreEqual(0, need);
        }

        [Test]
        public void TierUp_LogsAnalytics()
        {
            Currency.Add(CurrencyType.BattlePassXp, 100, "t");
            Assert.IsTrue(Analytics.Events.Exists(e => e.StartsWith("battle_pass_tier")));
        }

        [Test]
        public void FreeTrack_ClaimOnce_PremiumLockedUntilPurchase()
        {
            Currency.Add(CurrencyType.BattlePassXp, 100, "t");
            Assert.IsTrue(Bp.CanClaimFree(1));
            Assert.IsFalse(Bp.CanClaimPremium(1));
            Assert.IsNotNull(Bp.ClaimFree(1));
            Assert.IsNull(Bp.ClaimFree(1));
            Assert.IsNull(Bp.ClaimPremium(1));
            Assert.IsFalse(Bp.CanClaimFree(2), "tier not reached");
        }

        [Test]
        public void PremiumPurchase_UnlocksRetroactiveClaims()
        {
            Currency.Add(CurrencyType.BattlePassXp, 2000, "t");
            int tier = Bp.Tier;
            Assert.Greater(tier, 3);
            Assert.IsTrue(Buy("battle_pass_premium").Success);
            Assert.IsTrue(Bp.Premium);
            var all = Bp.ClaimAll();
            Assert.Greater(all.Count, 0);
            for (int t = 1; t <= tier; t++) { Assert.IsTrue(Bp.IsFreeClaimed(t)); Assert.IsTrue(Bp.IsPremiumClaimed(t)); }
            Assert.IsFalse(Bp.HasClaimable());
        }

        [Test]
        public void NewSeason_ResetsProgressClaimsAndPremium()
        {
            Currency.Add(CurrencyType.BattlePassXp, 500, "t");
            Bp.UnlockPremium();
            Bp.ClaimFree(1);
            int season = Bp.SeasonId;
            Time.Advance(TimeSpan.FromDays(31));
            Assert.AreEqual(season + 1, Bp.SeasonId);
            Assert.AreEqual(0, Bp.Xp);
            Assert.AreEqual(0, Bp.Tier);
            Assert.IsFalse(Bp.Premium);
            Assert.IsFalse(Bp.IsFreeClaimed(1));
        }

        [Test]
        public void SeasonLength_IsThirtyDays()
        {
            Assert.LessOrEqual(Bp.TimeLeft.TotalDays, 30.0);
            Assert.Greater(Bp.TimeLeft.TotalSeconds, 0);
        }
    }

    public class PiggyVipTests : MonetizationFixture
    {
        [Test]
        public void Piggy_FillsFromPlay_CapsOut_AndPaysOutOnBreak()
        {
            for (int i = 0; i < 10; i++) EventBus.Publish(new LevelCompletedEvent(i + 1, true, 3, false, false, 30f));
            EventBus.Publish(new LevelCompletedEvent(11, false, 0, false, false, 30f));
            Assert.AreEqual(10 * 3 + 1, Piggy.Accumulated);
            Assert.IsFalse(Piggy.CanBreak);

            for (int i = 0; i < 1000; i++) EventBus.Publish(new LevelCompletedEvent(1, true, 3, false, false, 30f));
            Assert.AreEqual(1500, Piggy.Accumulated);
            Assert.IsTrue(Piggy.IsFull);

            Assert.IsTrue(Buy("piggy_bank").Success);
            Assert.AreEqual(1500, Currency.Get(CurrencyType.Gems));
            Assert.AreEqual(0, Piggy.Accumulated);
        }

        [Test]
        public void Vip_Subscription_ActivatesPerks()
        {
            Assert.IsFalse(Vip.IsActive);
            Assert.IsTrue(Buy("vip_weekly").Success);
            Assert.IsTrue(Vip.IsActive);
            Assert.IsTrue(Ads.IsVip);
            Assert.AreEqual(7, Vip.Remaining.TotalDays, 0.01);

            Assert.IsTrue(Vip.DailyGemsAvailable);
            Assert.IsTrue(Vip.ClaimDaily());
            Assert.AreEqual(50, Currency.Get(CurrencyType.Gems));
            Assert.IsFalse(Vip.ClaimDaily());
            Time.Advance(TimeSpan.FromDays(1));
            Assert.IsTrue(Vip.ClaimDaily());

            Time.Advance(TimeSpan.FromDays(8));
            Assert.IsFalse(Vip.IsActive);
            Assert.IsFalse(Vip.ClaimDaily());
        }

        [Test]
        public void Vip_Renewal_ExtendsInsteadOfResetting()
        {
            Buy("vip_weekly");
            Time.Advance(TimeSpan.FromDays(3));
            Vip.Activate();
            Assert.AreEqual(11, Vip.Remaining.TotalDays, 0.01);
        }
    }

    public class OfferTriggerTests : MonetizationFixture
    {
        private void Lose(int level) { Save.Data.loseStreak++; EventBus.Publish(new LevelCompletedEvent(level, false, 0, false, false, 20f)); }
        private void Win(int level) { Save.Data.loseStreak = 0; EventBus.Publish(new LevelCompletedEvent(level, true, 3, false, false, 20f)); }

        [Test]
        public void StarterPack_AppearsAfterLevel5_For48Hours_Once()
        {
            Win(3);
            Assert.IsFalse(Offers.IsActive("starter"));
            Win(4);
            Assert.IsTrue(Offers.IsActive("starter"));
            var a = Offers.Active().Find(o => o.Def.id == "starter");
            Assert.AreEqual(48, a.Remaining.TotalHours, 0.01);

            Time.Advance(TimeSpan.FromHours(49));
            Assert.IsFalse(Offers.IsActive("starter"), "expired");
            Win(10);
            Assert.IsFalse(Offers.IsActive("starter"), "max one activation");
        }

        [Test]
        public void StarterPack_NotOfferedAgainOnceBought()
        {
            Win(4);
            Assert.IsTrue(Buy("starter_pack").Success);
            Assert.IsFalse(Offers.IsActive("starter"));
        }

        [Test]
        public void LoseStreak_TriggersComebackOffer_WithCooldown()
        {
            Lose(7); Lose(7);
            Assert.IsFalse(Offers.IsActive("comeback"));
            Lose(7);
            Assert.IsTrue(Offers.IsActive("comeback"));

            Time.Advance(TimeSpan.FromHours(7));
            Assert.IsFalse(Offers.IsActive("comeback"), "6h timer");
            Lose(7);
            Assert.IsFalse(Offers.IsActive("comeback"), "24h cooldown");
            Time.Advance(TimeSpan.FromHours(25));
            Lose(7);
            Assert.IsTrue(Offers.IsActive("comeback"));
        }

        [Test]
        public void WinningResetsLoseStreakTrigger()
        {
            Lose(7); Lose(7); Win(7); Lose(8);
            Assert.IsFalse(Offers.IsActive("comeback"));
        }

        [Test]
        public void ChapterClear_TriggersOnEveryTwentiethLevel()
        {
            Win(19);
            Assert.IsFalse(Offers.IsActive("chapter"));
            Win(20);
            Assert.IsTrue(Offers.IsActive("chapter"));
        }

        [Test]
        public void ReturningPlayer_AfterThreeDaysAway()
        {
            Offers.OnSessionStart(0);
            Time.Advance(TimeSpan.FromHours(10));
            Offers.OnSessionStart(0);
            Assert.IsFalse(Offers.IsActive("welcome_back"));
            Time.Advance(TimeSpan.FromHours(80));
            Offers.OnSessionStart(0);
            Assert.IsTrue(Offers.IsActive("welcome_back"));
        }

        [Test]
        public void Presentable_ReturnsEachOfferOnlyOnce()
        {
            Win(4);
            Assert.IsTrue(Offers.TryTakePresentable(out var offer));
            Assert.AreEqual("starter", offer.Def.id);
            Assert.IsFalse(Offers.TryTakePresentable(out _));
        }

        [Test]
        public void Purchasing_DismissesTheOffer()
        {
            Lose(5); Lose(5); Lose(5);
            Assert.IsTrue(Offers.IsActive("comeback"));
            Offers.MarkPurchased("offer_comeback");
            Assert.IsFalse(Offers.IsActive("comeback"));
        }
    }

    public class WeekendEventTests : MonetizationFixture
    {
        private WeekendEventService NewEvent(IRemoteConfigService remote = null) =>
            new WeekendEventService(Save, Shop.weekendEvent, remote, Time, Granter, Db, Analytics, () => Level);

        private void SetTime(int year, int month, int day, int hour)
        {
            Time.UtcNow = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Local).ToUniversalTime();
        }

        [Test]
        public void RecurringWeekend_ActiveOnSaturdayAndSunday_NotMidweek()
        {
            var ev = NewEvent();
            SetTime(2026, 3, 4, 12);  // Wednesday
            Assert.IsFalse(ev.IsActive);
            Assert.Greater(ev.TimeUntilStart.TotalHours, 48);
            SetTime(2026, 3, 7, 10);  // Saturday
            Assert.IsTrue(ev.IsActive);
            SetTime(2026, 3, 8, 23);  // Sunday night
            Assert.IsTrue(ev.IsActive);
            SetTime(2026, 3, 9, 1);   // Monday
            Assert.IsFalse(ev.IsActive);
        }

        [Test]
        public void RemoteConfig_CanDefineAnyWindowAndTrack_WithoutAppUpdate()
        {
            var rc = new MockRemoteConfigService();
            string json = "{\"id\":\"halloween\",\"nameKey\":\"event.weekend\",\"startUtc\":\"2026-10-30T00:00:00Z\",\"endUtc\":\"2026-11-02T00:00:00Z\",\"levelCount\":10,\"tokensPerStar\":3,\"minPlayerLevel\":1,\"track\":[{\"tokens\":3,\"reward\":{\"type\":1,\"amount\":50}}]}";
            rc.SetOverride(RemoteKeys.EventConfig, json);
            var ev = NewEvent(rc);

            Assert.AreEqual("halloween", ev.Config.id);
            Time.UtcNow = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(ev.IsActive);
            Time.UtcNow = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(ev.IsActive);
            Assert.AreEqual(1, ev.Config.track.Count);
        }

        [Test]
        public void BadRemoteJson_FallsBackToBundledEvent()
        {
            var rc = new MockRemoteConfigService();
            rc.SetOverride(RemoteKeys.EventConfig, "{broken");
            Assert.AreEqual("weekend", NewEvent(rc).Config.id);
        }

        [Test]
        public void Wins_EarnTokens_AdvanceLevels_AndUnlockTrackRewards()
        {
            SetTime(2026, 3, 7, 10);
            var ev = NewEvent();
            Assert.AreEqual(0, ev.Tokens);
            Assert.IsFalse(ev.CanClaim(0));

            long t1 = ev.OnWin(3);
            Assert.AreEqual(1 + 2 * 3, t1);
            ev.OnWin(3);
            Assert.AreEqual(2, ev.LevelIndex);
            Assert.IsTrue(ev.CanClaim(0));
            Assert.IsNotNull(ev.Claim(0));
            Assert.IsNull(ev.Claim(0));
            Assert.IsTrue(ev.CanClaim(1), "14 tokens passes the 12-token step");
            Assert.IsFalse(ev.CanClaim(2));
        }

        [Test]
        public void EventState_ResetsForTheNextWeekend()
        {
            SetTime(2026, 3, 7, 10);
            var ev = NewEvent();
            ev.OnWin(3);
            Assert.Greater(ev.Tokens, 0);
            SetTime(2026, 3, 14, 10);
            Assert.AreEqual(0, ev.Tokens);
            Assert.AreEqual(0, ev.LevelIndex);
        }

        [Test]
        public void EventLevels_AreDeterministic_ScaleWithPlayer_AndHaveBosses()
        {
            SetTime(2026, 3, 7, 10);
            var ev = NewEvent();
            var a = ev.NextLevel();
            var b = ev.NextLevel();
            Assert.AreEqual(a.enemies.Count, b.enemies.Count);
            Assert.Greater(a.enemies.Count, 0);

            Level = 60;
            Assert.Greater(ev.NextLevel().power, a.power * 3);

            for (int i = 0; i < 9; i++) ev.OnWin(1);
            Assert.IsTrue(ev.NextLevel().isBoss, "every 10th event level");
        }

        [Test]
        public void Locked_UntilPlayerReachesMinLevel()
        {
            SetTime(2026, 3, 7, 10);
            Save.Data.highestCampaignLevel = 1;
            var ev = NewEvent();
            Assert.IsTrue(ev.IsActive);
            Assert.IsFalse(ev.Available);
            Save.Data.highestCampaignLevel = 10;
            Assert.IsTrue(ev.Available);
        }
    }
}
