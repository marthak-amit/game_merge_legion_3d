using System.Collections;
using System.IO;
using MergeLegion.Battle;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Data;
using MergeLegion.Save;
using MergeLegion.Tutorial;
using MergeLegion.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeLegion.Core
{
    /// <summary>
    /// CI/marketing helper, active only when the player is launched with "-mlshots &lt;folder&gt;":
    /// skips the compliance popups, walks the main screens and a battle, saves PNGs and quits.
    /// </summary>
    public sealed class AutoScreenshot : MonoBehaviour
    {
        private string _dir;
        private static volatile int _frames;
        private static volatile string _stage = "init";

        public static bool TryAttach(GameObject host, SaveData save)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-mlshots");
            if (i < 0 || i + 1 >= args.Length) return false;
            save.consent.ageGatePassed = true;
            save.consent.consentAnswered = true;
            save.consent.trackingPromptShown = true; // keep the permissions pre-prompt out of the shots
            var shots = host.AddComponent<AutoScreenshot>();
            shots._dir = args[i + 1];
            var watchdog = new System.Threading.Thread(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    System.Threading.Thread.Sleep(15000);
                    Debug.Log("[Shots] heartbeat t=" + (int)sw.Elapsed.TotalSeconds + "s frames=" + _frames + " stage=" + _stage);
                }
            }) { IsBackground = true };
            watchdog.Start();
            return true;
        }

        private void Update() { _frames++; }

        private IEnumerator Start()
        {
            Debug.Log("[Shots] started, dir=" + _dir + " scene=" + SceneManager.GetActiveScene().name);
            Directory.CreateDirectory(_dir);
            yield return Wait(SceneNames.Battle, 40f);
            Debug.Log("[Shots] scene now " + SceneManager.GetActiveScene().name);
            _stage = "in " + SceneManager.GetActiveScene().name;
            yield return Shot("01_intro_battle");

            ServiceLocator.Get<TutorialService>().SkipAll();
            ServiceLocator.Get<CurrencyService>().Add(CurrencyType.Coins, 4000, "shots");
            SceneManager.LoadScene(SceneNames.Main);
            yield return Wait(SceneNames.Main, 10f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("02_home");

            foreach (var id in new[] { ScreenId.Army, ScreenId.Commanders, ScreenId.Shop, ScreenId.Missions, ScreenId.BattlePass, ScreenId.Arena, ScreenId.Settings })
            {
                if (UIManager.Instance != null) UIManager.Instance.OpenTab(id);
                yield return new WaitForSeconds(0.8f);
                yield return Shot("03_" + id.ToString().ToLowerInvariant());
            }

            SceneManager.LoadScene(SceneNames.Battle);
            yield return Wait(SceneNames.Battle, 10f);
            yield return new WaitForSeconds(1f);
            var army = ServiceLocator.Get<ArmyService>();
            for (int n = 0; n < 4; n++) army.Buy(n % 2 == 0 ? UnitLineId.Melee : UnitLineId.Ranged);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("04_battle_prepare");
            if (BattleSceneRoot.Director != null)
            {
                BattleSceneRoot.Director.StartFight();
                yield return new WaitForSeconds(3f);
                yield return Shot("05_battle_fight");
                yield return new WaitForSeconds(4f);
                yield return Shot("06_battle_later");
            }
            Application.Quit();
        }

        private static IEnumerator Wait(string scene, float timeout)
        {
            while (timeout > 0f && SceneManager.GetActiveScene().name != scene) { timeout -= Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSeconds(1f);
        }

        private const int W = 540, H = 960;
        private RenderTexture _rt;

        /// <summary>Batch mode has no real back buffer, so render the cameras (and the UI, switched to camera space) into a texture.</summary>
        private void PrepareRenderTarget()
        {
            if (_rt == null) _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { name = "ShotsRT" };
            Camera main = Camera.main;
            if (main == null && Camera.allCamerasCount > 0) main = Camera.allCameras[0];
            foreach (var cam in Camera.allCameras) { if (cam.enabled) cam.targetTexture = _rt; }
            if (main == null) return;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = main;
                    canvas.planeDistance = main.nearClipPlane + 0.6f;
                }
                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null) canvas.worldCamera = main;
            }
        }

        private IEnumerator Shot(string name)
        {
            PrepareRenderTarget();
            yield return null;
            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log("[Shots] saved " + name);
            _stage = "after " + name;
            Destroy(tex);
        }
    }
}
