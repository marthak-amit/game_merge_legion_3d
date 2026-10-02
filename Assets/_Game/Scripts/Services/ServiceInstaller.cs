using System.Collections.Generic;
using System.IO;
using MergeLegion.Core;
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
        }
    }
}
