using System.Collections;
using System.IO;
using UnityEngine;
using BlockBloom.Logic;

namespace BlockBloom
{
    /// <summary>CI / marketing helper: only active when the player is launched with "-bbshots &lt;folder&gt;". Walks the screens, plays the bot, saves PNGs.</summary>
    public sealed class AutoScreenshot : MonoBehaviour
    {
        private string _dir;
        private static volatile int _frames;
        private static volatile string _stage = "init";

        public static bool TryAttach(GameObject host)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-bbshots");
            if (i < 0 || i + 1 >= args.Length) return false;
            var d = Save.Data;
            d.tutorialDone = true; d.lastLoginClaim = Save.Today; d.unlockedLevel = Mathf.Max(d.unlockedLevel, 9);
            d.coins = 1240; d.classicBest = 4820;
            for (int k = 0; k < 8; k++) d.stars[k] = k % 3 == 0 ? 3 : (k % 3 == 1 ? 2 : 1);
            d.boosterBomb = 3; d.boosterUndo = 2; d.boosterShuffle = 1;
            d.chestProgress = 9; d.streak = 2;
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
            Debug.Log("[Shots] started dir=" + _dir);
            Directory.CreateDirectory(_dir);
            yield return new WaitForSeconds(3.2f);
            yield return Shot("01_home");

            LevelUi.Open(9);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("02_level_goals");
            App.I.Back();
            yield return new WaitForSeconds(0.4f);

            App.I.StartLevel(9);
            yield return new WaitForSeconds(2.2f);
            yield return Shot("03_game_start");
            var g = App.I.Current as GameScreen;
            int guard = 0;
            int lastLines = 0;
            bool shotClear = false;
            while (g != null && !g.Ended && guard++ < 60)
            {
                if (!g.AutoMove()) break;
                if (g.S.Keeper.TotalLines > lastLines && !shotClear)
                {
                    lastLines = g.S.Keeper.TotalLines;
                    yield return new WaitForSeconds(0.22f);
                    yield return Shot("04_line_clear_fx");
                    shotClear = true;
                }
                yield return new WaitForSeconds(0.55f);
                if (g.S.MovesMade == 6) yield return Shot("05_mid_game");
            }
            yield return new WaitForSeconds(2.2f);
            yield return Shot("06_result");

            App.I.ShowMap();
            yield return new WaitForSeconds(1.4f);
            yield return Shot("07_map");

            App.I.StartClassic();
            yield return new WaitForSeconds(2f);
            g = App.I.Current as GameScreen;
            for (int n = 0; n < 9 && g != null; n++) { g.AutoMove(); yield return new WaitForSeconds(0.5f); }
            yield return new WaitForSeconds(0.8f);
            yield return Shot("08_classic");

            App.I.ShowHome();
            yield return new WaitForSeconds(1.8f);
            Popups.Shop(); yield return new WaitForSeconds(0.9f); yield return Shot("09_shop"); App.I.Back(); yield return new WaitForSeconds(0.5f);
            Popups.DailyReward(null); yield return new WaitForSeconds(0.9f); yield return Shot("10_daily_reward"); App.I.Back(); yield return new WaitForSeconds(0.5f);
            Popups.Spin(null); yield return new WaitForSeconds(0.9f); yield return Shot("11_lucky_spin"); App.I.Back(); yield return new WaitForSeconds(0.5f);
            Popups.Themes(null); yield return new WaitForSeconds(0.9f); yield return Shot("12_themes"); App.I.Back(); yield return new WaitForSeconds(0.5f);
            Popups.Settings(null); yield return new WaitForSeconds(0.9f); yield return Shot("13_settings"); App.I.Back(); yield return new WaitForSeconds(0.5f);
            Save.Data.theme = 1; Save.Data.themeOwned[1] = true; App.I.ApplyTheme();
            App.I.ShowHome();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("14_home_sunset");
            Debug.Log("[Shots] done");
            Application.Quit();
        }

        private IEnumerator Shot(string name)
        {
            _stage = name;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
                Debug.Log("[Shots] saved " + name);
            }
            else Debug.Log("[Shots] capture failed " + name);
        }
    }
}
