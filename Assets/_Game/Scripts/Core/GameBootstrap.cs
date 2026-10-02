using System.Collections;
using MergeLegion.Save;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Core
{
    /// <summary>
    /// Lives in the Boot scene. Installs services, warms up SDKs (mocks in the Editor), then loads Main.
    /// Persists across scenes and hosts the long-lived runners.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap _instance;

        [SerializeField] private string firstScene = SceneNames.Main;

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

            var loader = gameObject.AddComponent<SceneLoader>();
            ServiceLocator.Register(loader);
            gameObject.AddComponent<SaveRunner>().Init(save, ServiceLocator.Get<IRemoteConfigService>());
            gameObject.AddComponent<AppLifecycle>();
        }

        private IEnumerator Start()
        {
            if (_instance != this) yield break;

            var config = ServiceLocator.Get<IRemoteConfigService>();
            float timeout = config.GetFloat(RemoteKeys.BootTimeoutSeconds, 4f);

            int pending = 0;
            void Begin() => pending++;
            void End() => pending--;

            Begin(); ServiceLocator.Get<IRemoteConfigService>().Fetch(_ => End());
            Begin(); ServiceLocator.Get<IAuthService>().SignInAnonymously(_ => End());
            Begin(); ServiceLocator.Get<IAdsService>().Initialize(End);
            Begin(); ServiceLocator.Get<IIAPService>().Initialize(System.Array.Empty<string>(), End);
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
            GetComponent<AppLifecycle>().BeginSession();

            EventBus.Publish(new BootCompletedEvent());
            ServiceLocator.Get<SceneLoader>().Load(firstScene);
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
