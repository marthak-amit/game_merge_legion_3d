using System;
using System.Collections.Generic;
using System.IO;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Meta.Arena;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services.Mock;
using UnityEngine;

namespace MergeLegion.Services
{
    /// <summary>
    /// Single place that decides which implementation backs each service interface.
    /// Real SDK implementations are selected here behind scripting define symbols (Phase 9);
    /// with no symbols defined everything runs on mocks, so the game always works in the Editor.
    /// </summary>
    public static class ServiceInstaller
    {
        public const string DefaultsResource = "RemoteConfigDefaults";

        public static SaveService InstallCore(ISaveStorage saveStorage = null, ITimeService time = null, string remoteDefaultsJson = null)
        {
            time = time ?? new SystemTimeService();
            ServiceLocator.Register(time);

            var save = new SaveService(saveStorage ?? new FileSaveStorage(Path.Combine(Application.persistentDataPath, "save.json")), time);
            save.Load();
            ServiceLocator.Register(save);

            if (remoteDefaultsJson == null)
            {
                var asset = Resources.Load<TextAsset>(DefaultsResource);
                remoteDefaultsJson = asset != null ? asset.text : null;
            }
            var remote = PlatformServiceOverrides.RemoteConfig != null
                ? PlatformServiceOverrides.RemoteConfig(remoteDefaultsJson)
                : new MockRemoteConfigService(remoteDefaultsJson);
            ServiceLocator.Register(remote);
            return save;
        }

        /// <summary>SKU list for the store catalog (filled in Phase 6 from the shop data).</summary>
        public static IReadOnlyList<string> IapSkus() =>
            ServiceLocator.TryGet<IapManager>(out var iap) ? iap.Skus() : new List<string>();

        /// <summary>Game-level services that depend on save, config and platform services.</summary>
        public static void InstallGameplay(SaveService save)
        {
            var remote = ServiceLocator.Get<IRemoteConfigService>();
            var analytics = ServiceLocator.Get<IAnalyticsService>();
            var config = GameConfig.Load(remote);
            ServiceLocator.Register(config);

            var currency = new CurrencyService(save, analytics);
            ServiceLocator.Register(currency);
            if (!save.Data.HasFlag(SaveFlags.StarterCoinsGiven))
            {
                save.Data.SetFlag(SaveFlags.StarterCoinsGiven);
                currency.Add(CurrencyType.Coins, config.grid.startCoins, "starter");
            }

            var db = GameDatabase.Instance;
            var research = new ResearchService(save, currency, config.research);
            ServiceLocator.Register(research);
            ServiceLocator.Register<IResearchProvider>(research);

            var army = new ArmyService(new GridModel(config.grid.cols, config.grid.rows), save, currency, config, db,
                new DeterministicRng(Environment.TickCount), analytics, research);
            army.Load();
            ServiceLocator.Register(army);

            var time = ServiceLocator.Get<ITimeService>();
            var daily = new DailyService(save, time);
            ServiceLocator.Register(daily);

            var ads = new AdsManager(ServiceLocator.Get<IAdsService>(), config, daily, save, time, analytics,
                ServiceLocator.Get<IAttributionService>());
            ads.RefreshEntitlements();
            ServiceLocator.Register(ads);

            var campaign = new CampaignService(save, analytics);
            ServiceLocator.Register(campaign);
            ServiceLocator.Register(new LevelRepository(db, config.endless));
            ServiceLocator.Register(new CommanderService(save, currency, db));

            var meta = MetaConfig.Load(remote);
            ServiceLocator.Register(meta);
            var rng = new DeterministicRng(Environment.TickCount * 31 + 7);
            var granter = new RewardGranter(save, currency, config, db, time, rng, () => campaign.CurrentLevel);
            granter.EntitlementsChanged = ads.RefreshEntitlements;
            ServiceLocator.Register(granter);

            ServiceLocator.Register(new CastleService(save, meta.castle, config, time, currency, () => ads.IsVip));
            ServiceLocator.Register(new ChestService(save, meta, currency, granter, time, rng, analytics, ads));
            ServiceLocator.Register(new MissionService(save, meta.missions, daily, granter, analytics, () => campaign.CurrentLevel));
            ServiceLocator.Register(new LoginService(save, meta.login, daily, granter, time));
            ServiceLocator.Register(new SpinService(meta.spin, daily, granter, ads, rng));

            // ---- monetization
            var shop = MonetizationConfig.Load(remote);
            ServiceLocator.Register(shop);
            var commanders = ServiceLocator.Get<CommanderService>();

            var bp = new BattlePassService(save, shop.battlePass, time, granter, analytics);
            var piggy = new PiggyService(save, shop.piggy, currency);
            var vip = new VipService(save, shop.vip, time, daily, granter);
            var offers = new OfferService(save, shop, time);
            ServiceLocator.Register(bp);
            ServiceLocator.Register(piggy);
            ServiceLocator.Register(vip);
            ServiceLocator.Register(offers);
            ServiceLocator.Register(new WeekendEventService(save, shop.weekendEvent, remote, time, granter, db, analytics, () => campaign.CurrentLevel));

            // ---- online features (mock backends by default)
            var arenaCfg = ArenaConfig.Load(remote);
            ServiceLocator.Register(arenaCfg);
            ServiceLocator.Register(PlatformServiceOverrides.ArenaBackend != null
                ? PlatformServiceOverrides.ArenaBackend(db, arenaCfg)
                : (IArenaBackend)new MockArenaBackend(db, arenaCfg));
            ServiceLocator.Register(new ArenaService(save, arenaCfg, ServiceLocator.Get<IArenaBackend>(), army, research,
                commanders, db, granter, daily, time, analytics, ServiceLocator.Get<IAuthService>(), ServiceLocator.Get<ILeaderboardService>()));
            var sync = new CloudSyncService(save, ServiceLocator.Get<ICloudSaveService>(), ServiceLocator.Get<IAuthService>(), time, analytics);
            ServiceLocator.Register(sync);
            ServiceLocator.Register(new LeaderboardReporter(save, ServiceLocator.Get<ILeaderboardService>()));

            // ---- onboarding + notifications
            ServiceLocator.Register(new Tutorial.TutorialService(save, Tutorial.TutorialConfig.Load(), () => campaign.CurrentLevel, currency, analytics));
            ServiceLocator.Register(new PushScheduler(ServiceLocator.Get<IPushService>(), ServiceLocator.Get<CastleService>(),
                ServiceLocator.Get<ChestService>(), ServiceLocator.Get<LoginService>(), ServiceLocator.Get<WeekendEventService>(),
                time, UI.Loc.Get));

            var iapService = ServiceLocator.Get<IIAPService>();
            if (iapService is MockIAPService mockIap)
            {
                mockIap.PriceLookup = sku => shop.Product(sku) != null ? (decimal)shop.Product(sku).priceUsd : 0.99m;
                mockIap.KindLookup = sku => shop.Product(sku) != null ? shop.Product(sku).kind : ProductKind.Consumable;
            }
            var iap = new IapManager(iapService, shop, granter, save, analytics, ServiceLocator.Get<IAttributionService>(),
                ServiceLocator.Get<IReceiptValidator>(), ads);
            iap.RegisterSpecial(shop.battlePass.premiumSku, def => bp.UnlockPremium());
            iap.RegisterSpecial(shop.vip.sku, def => vip.Activate());
            iap.RegisterSpecial(shop.piggy.sku, def => piggy.Break());
            foreach (var p in shop.products)
            {
                if (string.IsNullOrEmpty(p.commanderId)) continue;
                string cid = p.commanderId;
                iap.RegisterSpecial(p.sku, def => commanders.GrantUnlock(cid));
            }
            iap.RegisterSpecial("no_ads", def => { /* RemoveAds reward flips the flag */ });
            ServiceLocator.Register(iap);

        }

        public static void InstallPlatformServices(SaveService save, IReadOnlyList<string> iapSkus = null)
        {
            var auth = PlatformServiceOverrides.Auth != null ? PlatformServiceOverrides.Auth(save.Data) : new MockAuthService(save.Data.playerId);
            ServiceLocator.Register(auth);

            if (PlatformServiceOverrides.AnalyticsSinks.Count > 0)
            {
                var sinks = new List<IAnalyticsService>();
                foreach (var f in PlatformServiceOverrides.AnalyticsSinks) sinks.Add(f());
                ServiceLocator.Register<IAnalyticsService>(new CompositeAnalyticsService(sinks));
            }
            else ServiceLocator.Register<IAnalyticsService>(new MockAnalyticsService());

            ServiceLocator.Register(PlatformServiceOverrides.Ads != null ? PlatformServiceOverrides.Ads() : new MockAdsService());
            ServiceLocator.Register(PlatformServiceOverrides.Iap != null ? PlatformServiceOverrides.Iap() : new MockIAPService());
            ServiceLocator.Register(PlatformServiceOverrides.Receipts != null ? PlatformServiceOverrides.Receipts() : new MockReceiptValidator());
            ServiceLocator.Register(PlatformServiceOverrides.CloudSave != null
                ? PlatformServiceOverrides.CloudSave()
                : new MockCloudSaveService(new FileSaveStorage(Path.Combine(Application.persistentDataPath, "cloud_mock.json"))));
            ServiceLocator.Register(PlatformServiceOverrides.Leaderboards != null
                ? PlatformServiceOverrides.Leaderboards(auth)
                : new MockLeaderboardService(auth));
            ServiceLocator.Register(PlatformServiceOverrides.Push != null ? PlatformServiceOverrides.Push() : new MockPushService());
            ServiceLocator.Register(PlatformServiceOverrides.Attribution != null ? PlatformServiceOverrides.Attribution() : new MockAttributionService());
            ServiceLocator.Register(PlatformServiceOverrides.Consent != null ? PlatformServiceOverrides.Consent() : new MockConsentService());
            ServiceLocator.Register<IAudioService>(new NullAudioService());
        }
    }
}
