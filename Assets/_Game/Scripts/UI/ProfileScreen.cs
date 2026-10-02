using MergeLegion.Core;
using MergeLegion.Levels;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using TMPro;
using UnityEngine;

namespace MergeLegion.UI
{
    /// <summary>Profile: name, VIP badge and lifetime stats.</summary>
    public sealed class ProfileScreen : MenuScreen
    {
        private static readonly string[] First = { "Iron", "Storm", "Shadow", "Golden", "Wild", "Silent", "Crimson", "Frost", "Ember", "Royal", "Lucky", "Brave" };
        private static readonly string[] Second = { "Fox", "Wolf", "Knight", "Falcon", "Drake", "Bear", "Raven", "Lion", "Tiger", "Viper", "Hawk", "Golem" };

        private TMP_Text _name, _vip, _stats;
        private TMP_Text _avatar;

        protected override string TitleKey => "profile.title";

        protected override void BuildContent(RectTransform c)
        {
            var avatar = UIKit.Icon(c, UIKit.Blue, new Vector2(220, 220));
            UIKit.Place(avatar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(220, 220));
            _avatar = UIKit.Label(avatar.transform, "", 110, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(_avatar.rectTransform);

            _name = UIKit.Label(c, "", 56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(900, 80));
            _vip = UIKit.Label(c, "", 36, UIKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_vip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -340), new Vector2(900, 56));

            var reroll = UIKit.Btn(c, Loc.Get("profile.random_name"), UIKit.Blue, RandomName, new Vector2(640, 100), 42);
            UIKit.Place((RectTransform)reroll.transform, new Vector2(0.5f, 1f), new Vector2(0, -410), new Vector2(640, 100));

            var card = UIKit.PanelImage(c, UIKit.Panel, "Stats");
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -540), new Vector2(980, 560));
            _stats = UIKit.Label(card.transform, "", 40, Color.white, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(_stats.rectTransform, 30, 20, 30, 20);
        }

        private void RandomName()
        {
            var save = ServiceLocator.Get<SaveService>();
            var rng = new System.Random();
            string name = First[rng.Next(First.Length)] + Second[rng.Next(Second.Length)] + rng.Next(10, 99);
            save.Data.displayName = name;
            ServiceLocator.Get<IAuthService>().DisplayName = name;
            save.MarkDirty();
            Refresh();
        }

        protected override void Refresh()
        {
            var save = ServiceLocator.Get<SaveService>().Data;
            var auth = ServiceLocator.Get<IAuthService>();
            _name.text = auth.DisplayName;
            _avatar.text = auth.DisplayName.Length > 0 ? auth.DisplayName.Substring(0, 1).ToUpperInvariant() : "?";
            var vip = ServiceLocator.Get<VipService>();
            _vip.text = vip.IsActive ? "VIP  *  " + Loc.Format("shop.vip_active", ItemDisplay.Duration(vip.Remaining)) : "";
            var campaign = ServiceLocator.Get<CampaignService>();
            _stats.text =
                Loc.Format("profile.level", save.highestCampaignLevel) + "\n" +
                Loc.Format("profile.stars", campaign.TotalStars()) + "\n" +
                Loc.Format("profile.endless", save.endlessBestWave) + "\n" +
                Loc.Format("profile.wins", save.totalWins, save.totalLosses) + "\n" +
                Loc.Format("profile.arena", save.arenaTrophies, save.arenaWins) + "\n" +
                Loc.Format("profile.playtime", ItemDisplay.Duration(System.TimeSpan.FromSeconds(save.totalPlaytimeSeconds)));
        }
    }
}
