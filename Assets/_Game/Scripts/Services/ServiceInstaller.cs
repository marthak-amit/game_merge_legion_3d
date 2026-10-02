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
            ServiceLocator.Register<IRemoteConfigService>(new MockRemoteConfigService(remoteDefaultsJson));
            return save;
        }

        /// <summary>SKU list for the store catalog (filled in Phase 6 from the shop data).</summary>
        public static IReadOnlyList<string> IapSkus() => new string[0];

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
            var army = new ArmyService(new GridModel(config.grid.cols, config.grid.rows), save, currency, config, db,
                new DeterministicRng(Environment.TickCount), analytics);
            army.Load();
            ServiceLocator.Register(army);

            var time = ServiceLocator.Get<ITimeService>();
            var daily = new DailyService(save, time);
            ServiceLocator.Register(daily);

            var ads = new AdsManager(ServiceLocator.Get<IAdsService>(), config, daily, save, time, analytics,
                ServiceLocator.Get<IAttributionService>());
            ads.RefreshEntitlements();
            ServiceLocator.Register(ads);

            ServiceLocator.Register(new CampaignService(save, analytics));
            ServiceLocator.Register(new LevelRepository(db, config.endless));
            ServiceLocator.Register(new CommanderService(save, currency, db));
        }

        public static void InstallPlatformServices(SaveService save, IReadOnlyList<string> iapSkus = null)
        {
            // Phase 9: swap these for real implementations under #if MERGELEGION_* defines.
            var auth = new MockAuthService(save.Data.playerId);
            ServiceLocator.Register<IAuthService>(auth);
            ServiceLocator.Register<IAnalyticsService>(new MockAnalyticsService());
            ServiceLocator.Register<IAdsService>(new MockAdsService());
            ServiceLocator.Register<IIAPService>(new MockIAPService());
            ServiceLocator.Register<ICloudSaveService>(new MockCloudSaveService(
                new FileSaveStorage(Path.Combine(Application.persistentDataPath, "cloud_mock.json"))));
            ServiceLocator.Register<ILeaderboardService>(new MockLeaderboardService(auth));
            ServiceLocator.Register<IPushService>(new MockPushService());
            ServiceLocator.Register<IAttributionService>(new MockAttributionService());
            ServiceLocator.Register<IConsentService>(new MockConsentService());
            ServiceLocator.Register<IAudioService>(new NullAudioService());
        }
    }
}
