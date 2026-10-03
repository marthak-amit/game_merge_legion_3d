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

        public static bool TryAttach(GameObject host, SaveData save)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-mlshots");
            if (i < 0 || i + 1 >= args.Length) return false;
            save.consent.ageGatePassed = true;
            save.consent.consentAnswered = true;
            var shots = host.AddComponent<AutoScreenshot>();
            shots._dir = args[i + 1];
            return true;
        }

        private IEnumerator Start()
        {
            Debug.Log("[Shots] started, dir=" + _dir + " scene=" + SceneManager.GetActiveScene().name);
            Directory.CreateDirectory(_dir);
            yield return Wait(SceneNames.Battle, 40f);
            Debug.Log("[Shots] scene now " + SceneManager.GetActiveScene().name);
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

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log("[Shots] saved " + name);
            Destroy(tex);
        }
    }
}
