using System.Collections;
using MergeLegion.Core;
using MergeLegion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MergeLegion.Tests
{
    /// <summary>Requires the generated scenes (Tools > Merge Legion > Generate Scenes) in Build Settings.</summary>
    public class BootFlowTests
    {
        [UnityTest]
        public IEnumerator Boot_LoadsMainAndShowsHome()
        {
            SceneManager.LoadScene(SceneNames.Boot);

            float timeout = 10f;
            while (SceneManager.GetActiveScene().name != SceneNames.Main && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(SceneNames.Main, SceneManager.GetActiveScene().name, "Boot did not reach Main in time");

            yield return null; // let UIManager.Start push Home

            var ui = Object.FindFirstObjectByType<UIManager>();
            Assert.IsNotNull(ui);
            Assert.AreEqual(ScreenId.Home, ui.Current);
            Assert.IsTrue(ServiceLocator.Has<Services.IAdsService>(), "mock services should be registered");
        }

        [TearDown]
        public void TearDown()
        {
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot != null) Object.Destroy(boot.gameObject);
        }
    }
}
