using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Levels;
using MergeLegion.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Home hub. Phase 2: title + battle entry. Later phases add castle, chests, missions and the bottom nav.</summary>
    public sealed class HomeScreen : UIScreen
    {
        private TMP_Text _levelLabel;
        private TMP_Text _versionLabel;
        private Button _battleButton;

        protected override void Build()
        {
            var bg = UIKit.PanelImage(transform, UIKit.Bg, "Background");
            bg.sprite = null;
            UIKit.Stretch(bg.rectTransform);
            var safe = UIKit.SafeArea(transform);

            var title = UIKit.Label(safe, Loc.Get("game.title"), 110, UIKit.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(980, 180));

            _levelLabel = UIKit.Label(safe, "", 56, Color.white);
            UIKit.Place(_levelLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(900, 90));

            _battleButton = UIKit.Btn(safe, Loc.Get("home.battle"), UIKit.Accent, OnBattle, new Vector2(640, 200), 72);
            UIKit.Place((RectTransform)_battleButton.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 200));

            _versionLabel = UIKit.Label(safe, "", 30, UIKit.TextDim);
            UIKit.Place(_versionLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(400, 50));
        }

        protected override void OnShown()
        {
            _versionLabel.text = Loc.Format("home.version", Application.version);
            int level = ServiceLocator.TryGet<SaveService>(out var save) ? save.Data.highestCampaignLevel + 1 : 1;
            if (ServiceLocator.TryGet<LevelRepository>(out var repo))
            {
                var def = repo.Get(level);
                _levelLabel.text = def.endless
                    ? Loc.Format("home.endless", def.index)
                    : Loc.Format("home.chapter", def.chapter, Loc.Get(ThemeLibrary.Get(def.theme).nameKey)) + "\n" + Loc.Format("home.level", level);
            }
            else _levelLabel.text = Loc.Format("home.level", level);
        }

        private void OnBattle()
        {
            if (ServiceLocator.TryGet<SceneLoader>(out var loader)) loader.Load(SceneNames.Battle);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Battle);
        }
    }
}
