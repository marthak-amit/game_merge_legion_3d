using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Monetization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Weekend event (section 2.9): own level set, token currency and reward track.</summary>
    public sealed class EventScreen : MenuScreen
    {
        private TMP_Text _timer, _tokens, _level;
        private Button _play;
        private sealed class TrackEntry { public Button Button; public Image Bar; }
        private readonly List<TrackEntry> _track = new List<TrackEntry>();

        protected override string TitleKey => "event.screen_title";

        protected override void BuildContent(RectTransform c)
        {
            var ev = ServiceLocator.Get<WeekendEventService>();
            var name = UIKit.Label(c, Loc.Get(ev.Config.nameKey), 56, UIKit.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1000, 80));
            _timer = UIKit.Label(c, "", 36, UIKit.TextDim);
            UIKit.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -86), new Vector2(1000, 50));
            _tokens = UIKit.Label(c, "", 48, UIKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_tokens.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1000, 70));
            _level = UIKit.Label(c, "", 34, Color.white);
            UIKit.Place(_level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(1000, 50));

            _play = UIKit.Btn(c, Loc.Get("common.play"), UIKit.Good, Play, new Vector2(700, 140), 62);
            UIKit.Place((RectTransform)_play.transform, new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(700, 140));

            RowKit.ScrollArea(c, 12, 440, out var list);
            for (int i = 0; i < ev.Config.track.Count; i++)
            {
                int idx = i;
                var entry = ev.Config.track[i];
                var row = UIKit.ListRow(list, 150);
                RowKit.Left(row, Loc.Format("event.need", entry.tokens), 32, UIKit.Gold, 24, 14, 500, 50, FontStyles.Bold);
                RowKit.Left(row, ItemDisplay.Describe(entry.reward), 38, Color.white, 24, 66, 600, 60);
                RowKit.Bar(row, UIKit.Gold, 24, 124, 560, 16, out var fill);
                var b = RowKit.RightButton(row, Loc.Get("common.claim"), UIKit.Good, () => Claim(idx), new Vector2(260, 100), 38);
                _track.Add(new TrackEntry { Button = b, Bar = fill });
            }
        }

        private void Play()
        {
            var ev = ServiceLocator.Get<WeekendEventService>();
            if (!ev.Available) { Toast.Show(Loc.Get("event.inactive")); return; }
            GameSession.Begin(GameMode.Event);
            HomeScreen.LoadBattle();
        }

        private void Claim(int index)
        {
            var items = ServiceLocator.Get<WeekendEventService>().Claim(index);
            if (items != null) Popups.Reward(Loc.Get("event.screen_title"), items);
            Refresh();
        }

        protected override void Refresh()
        {
            var ev = ServiceLocator.Get<WeekendEventService>();
            ev.EnsureCurrent();
            _timer.text = ev.IsActive ? Loc.Format("event.ends_in", ItemDisplay.Duration(ev.TimeLeft)) : Loc.Format("event.starts_in", ItemDisplay.Duration(ev.TimeUntilStart));
            _tokens.text = Loc.Get(ev.Config.tokenKey) + ": " + ev.Tokens;
            _level.text = Loc.Format("event.level", ev.LevelIndex + 1, ev.Config.levelCount);
            _play.interactable = ev.Available;
            _play.GetComponent<Image>().color = ev.Available ? UIKit.Good : UIKit.Disabled;

            for (int i = 0; i < _track.Count; i++)
            {
                var t = _track[i];
                var entry = ev.Config.track[i];
                bool claimed = ev.IsClaimed(i);
                t.Bar.fillAmount = Mathf.Clamp01((float)ev.Tokens / entry.tokens);
                UIKit.SetButtonText(t.Button, claimed ? Loc.Get("common.claimed") : Loc.Get("common.claim"));
                t.Button.interactable = ev.CanClaim(i);
                t.Button.GetComponent<Image>().color = t.Button.interactable ? UIKit.Good : UIKit.Disabled;
            }
        }
    }
}
