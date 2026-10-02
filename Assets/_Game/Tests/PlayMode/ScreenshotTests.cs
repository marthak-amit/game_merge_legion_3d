using System.Collections;
using System.IO;
using MergeLegion.Core;
using MergeLegion.Tutorial;
using MergeLegion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergeLegion.Tests
{
    /// <summary>Captures store-style screenshots into Screenshots/ (CI uploads them as an artifact). Not a correctness test.</summary>
    public class ScreenshotTests
    {
        private static string Dir
        {
            get { string d = Path.GetFullPath("Screenshots"); Directory.CreateDirectory(d); return d; }
        }

        private static IEnumerator Shot(string name)
        {
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
            yield return null;
            yield return null;
        }

        [SetUp]
        public void SetUp() { ServiceLocator.Clear(); EventBus.ClearAll(); }

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureScreens()
        {
            SceneManager.LoadScene(SceneNames.Boot);
            float timeout = 15f;
            while (SceneManager.GetActiveScene().name != SceneNames.Main && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSeconds(1f);
            ServiceLocator.Get<TutorialService>().SkipAll();
            yield return Shot("01_home");

            var ui = UIManager.Instance;
            foreach (var id in new[] { ScreenId.Army, ScreenId.Commanders, ScreenId.Shop, ScreenId.Missions, ScreenId.BattlePass, ScreenId.Arena, ScreenId.Settings })
            {
                ui.OpenTab(id);
                yield return Shot("02_" + id.ToString().ToLowerInvariant());
            }

            GameBootstrap.EnsureServices();
            SceneManager.LoadScene(SceneNames.Battle);
            timeout = 10f;
            while (Battle.BattleSceneRoot.Director == null && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
            yield return Shot("03_battle_prepare");
            if (Battle.BattleSceneRoot.Director != null)
            {
                Battle.BattleSceneRoot.Director.StartFight();
                yield return new WaitForSeconds(2f);
                yield return Shot("04_battle_fight");
            }
            Assert.Pass();
        }
    }
}
