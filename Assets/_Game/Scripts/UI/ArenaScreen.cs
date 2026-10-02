using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Meta.Arena;
using MergeLegion.Monetization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Async PvP arena (section 2.11): pick an opponent snapshot, earn trophies, climb leagues.</summary>
    public sealed class ArenaScreen : MenuScreen
    {
        private TMP_Text _league, _trophies, _attempts;
        private Image _leagueBar;
        private Button _weekly;
        private RectTransform _list;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private List<ArenaSnapshot> _opponents;
        private bool _loading;
        private int _loadedAttempts = -1;

        protected override string TitleKey => "arena.title";

        protected override void BuildContent(RectTransform c)
        {
            _league = UIKit.Label(c, "", 60, UIKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_league.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1000, 90));
            _trophies = UIKit.Label(c, "", 40, Color.white, TextAlignmentOptions.Center);
            UIKit.Place(_trophies.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(1000, 56));
            var bar = UIKit.ProgressBar(c, UIKit.Gold, new Vector2(800, 28), out _leagueBar);
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(800, 28));
            _attempts = UIKit.Label(c, "", 34, UIKit.TextDim);
            UIKit.Place(_attempts.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1000, 50));

            _weekly = UIKit.Btn(c, Loc.Get("arena.claim_weekly"), UIKit.Accent, ClaimWeekly, new Vector2(700, 100), 40);
            UIKit.Place((RectTransform)_weekly.transform, new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(700, 100));

            var rank = UIKit.Btn(c, Loc.Get("home.board"), UIKit.PanelLight, () => { LeaderboardScreen.Preselect = 2; UIManager.Instance.Push(ScreenId.Leaderboard); }, new Vector2(300, 80), 34);
            UIKit.Place((RectTransform)rank.transform, new Vector2(1f, 1f), new Vector2(-10, -6), new Vector2(240, 80));

            Section(c, Loc.Get("arena.choose"), -380);
            RowKit.ScrollArea(c, 14, 450, out _list);
        }

        protected override void OnShown()
        {
            _opponents = null;
            _loadedAttempts = -1;
            ServiceLocator.Get<ArenaService>().UploadSnapshot();
            base.OnShown();
        }

        private void ClaimWeekly()
        {
            var items = ServiceLocator.Get<ArenaService>().ClaimWeeklyReward();
            if (items != null) Popups.Reward(Loc.Get("arena.weekly_title"), items);
            Refresh();
        }

        protected override void Refresh()
        {
            var arena = ServiceLocator.Get<ArenaService>();
            _league.text = Loc.Get(arena.League.nameKey);
            _trophies.text = Loc.Format("arena.total", arena.Trophies);
            var next = arena.NextLeague;
            if (next != null)
            {
                int lo = arena.League.minTrophies, hi = next.minTrophies;
                _leagueBar.fillAmount = Mathf.Clamp01((float)(arena.Trophies - lo) / Mathf.Max(1, hi - lo));
            }
            else _leagueBar.fillAmount = 1f;

            int attempts = arena.Attempts;
            var until = arena.UntilNextAttempt();
            _attempts.text = Loc.Format("arena.attempts", attempts, arena.Config.maxAttempts) + (until > System.TimeSpan.Zero ? "   (" + ItemDisplay.Duration(until) + ")" : "");
            _weekly.gameObject.SetActive(arena.WeeklyRewardAvailable);

            if (_opponents == null && !_loading) LoadOpponents();
            else if (_opponents != null && attempts != _loadedAttempts) BuildList();
        }

        private void LoadOpponents()
        {
            _loading = true;
            ServiceLocator.Get<ArenaService>().FindOpponents(list =>
            {
                _loading = false;
                _opponents = list;
                _loadedAttempts = -2;
            });
        }

        private void BuildList()
        {
            var arena = ServiceLocator.Get<ArenaService>();
            _loadedAttempts = arena.Attempts;
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            float mine = arena.BuildSnapshot().power;
            foreach (var opp in _opponents)
            {
                var row = UIKit.ListRow(_list, 210);
                _spawned.Add(row.gameObject);
                float ratio = mine > 0 ? opp.power / mine : 1f;
                Color diff = ratio < 0.9f ? UIKit.Good : ratio < 1.12f ? UIKit.Accent : UIKit.Bad;
                RowKit.Dot(row, diff, 20, 40, 90);
                RowKit.Left(row, opp.name, 40, Color.white, 130, 14, 520, 56, FontStyles.Bold);
                RowKit.Left(row, Loc.Format("arena.opp_info", opp.trophies, Mathf.RoundToInt(opp.power)), 30, UIKit.TextDim, 130, 70, 520, 46);
                int gain = ArenaMath.WinGain(arena.Config, arena.Trophies, opp.trophies);
                RowKit.Left(row, "+" + gain + " " + Loc.Get("arena.trophies"), 30, UIKit.Gold, 130, 120, 520, 46);
                var snapshot = opp;
                var fight = RowKit.RightButton(row, Loc.Get("hud.fight"), UIKit.Good, () => Fight(snapshot), new Vector2(280, 110), 44);
                fight.interactable = arena.Attempts > 0;
                fight.GetComponent<Image>().color = fight.interactable ? UIKit.Good : UIKit.Disabled;
            }
        }

        private void Fight(ArenaSnapshot opp)
        {
            var arena = ServiceLocator.Get<ArenaService>();
            if (!arena.TryStartMatch(opp)) { Toast.Show(Loc.Get("arena.no_attempts")); return; }
            GameSession.Begin(GameMode.Arena);
            HomeScreen.LoadBattle();
        }
    }
}
