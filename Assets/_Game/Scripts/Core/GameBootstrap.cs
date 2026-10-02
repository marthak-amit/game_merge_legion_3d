using System.Collections;
using MergeLegion.Save;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Core
{
    /// <summary>
    /// Lives in the Boot scene. Installs services, warms up SDKs (mocks in the Editor), then loads Main.
    /// Persists across scenes and hosts the long-lived runners. <see cref="EnsureServices"/> lets any scene be
    /// played directly in the Editor without going through Boot.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap _instance;

        [SerializeField] private string firstScene = SceneNames.Main;

        /// <summary>Installs everything synchronously if Boot has not run (Editor play-from-any-scene, tests).</summary>
        public static void EnsureServices()
        {
            if (_instance != null || ServiceLocator.Has<SaveService>()) return;
            var go = new GameObject("[GameBootstrapLite]");
            DontDestroyOnLoad(go);
            var boot = go.AddComponent<GameBootstrap>();
            boot.firstScene = null; // Awake already installed the services; Start finishes SDK warm-up without loading a scene
        }

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var save = ServiceInstaller.InstallCore();
            ServiceInstaller.InstallPlatformServices(save);
            ServiceInstaller.InstallGameplay(save);

            var loader = gameObject.AddComponent<SceneLoader>();
            ServiceLocator.Register(loader);
            gameObject.AddComponent<SaveRunner>().Init(save, ServiceLocator.Get<IRemoteConfigService>());
            gameObject.AddComponent<AppLifecycle>();
            gameObject.AddComponent<Audio.AudioManager>();
            ServiceLocator.Register<Audio.IAudioService>(GetComponent<Audio.AudioManager>());
            Tutorial.TutorialOverlay.Create();
            UI.PermissionsFlow.Init();

            var auth = ServiceLocator.Get<IAuthService>();
            if (!string.IsNullOrEmpty(save.Data.displayName)) auth.DisplayName = save.Data.displayName;
            else save.Data.displayName = auth.DisplayName;
        }

        private IEnumerator Start()
        {
            if (_instance != this) yield break;

            var config = ServiceLocator.Get<IRemoteConfigService>();
            float timeout = config.GetFloat(RemoteKeys.BootTimeoutSeconds, 4f);

            int pending = 0;
            void End() => pending--;

            pending++; ServiceLocator.Get<IRemoteConfigService>().Fetch(_ => End());
            pending++; ServiceLocator.Get<IAuthService>().SignInAnonymously(_ => End());
            pending++; ServiceLocator.Get<IAdsService>().Initialize(End);
            pending++; ServiceLocator.Get<IIAPService>().Initialize(ServiceInstaller.IapSkus(), End);
            ServiceLocator.Get<IAnalyticsService>().Initialize();
            ServiceLocator.Get<IAttributionService>().Initialize();

            float waited = 0f;
            while (pending > 0 && waited < timeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (pending > 0) Debug.Log("[Boot] continuing offline after " + timeout + "s timeout");

            var auth = ServiceLocator.Get<IAuthService>();
            ServiceLocator.Get<IAnalyticsService>().SetUserId(auth.PlayerId);

            // compliance first: age gate + consent (full boot only; direct-play of a scene in the Editor skips it)
            if (!string.IsNullOrEmpty(firstScene))
            {
                bool flowDone = false;
                UI.FirstRunFlow.Run(() => flowDone = true);
                while (!flowDone) yield return null;
            }

            // cloud save: adopt a better cloud save (asks the player when both have progress) or upload ours
            bool syncDone = false;
            ServiceLocator.Get<CloudSyncService>().SyncOnBoot(_ => syncDone = true);
            float syncWait = 0f;
            while (!syncDone && syncWait < 30f) // a conflict prompt may be on screen, so allow time
            {
                syncWait += Time.unscaledDeltaTime;
                yield return null;
            }
            GetComponent<AppLifecycle>().BeginSession();

            EventBus.Publish(new BootCompletedEvent());
            if (!string.IsNullOrEmpty(firstScene))
            {
                // brand-new players go straight into the 30-second hook: buy -> merge -> fight -> win
                string scene = ServiceLocator.Get<Tutorial.TutorialService>().NeedsIntroBattle ? SceneNames.Battle : firstScene;
                ServiceLocator.Get<SceneLoader>().Load(scene);
                UI.PermissionsFlow.AskIfDue();
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            EventBus.ClearAll();
            ServiceLocator.Clear();
        }
    }
}
