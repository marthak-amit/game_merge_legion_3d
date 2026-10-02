using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Meta.Arena;
using MergeLegion.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Leaderboards (section 2.10): highest campaign level, endless wave, arena trophies.</summary>
    public sealed class LeaderboardScreen : MenuScreen
    {
        public static int Preselect = 0;

        private static readonly string[] Boards = { LeaderboardIds.CampaignLevel, LeaderboardIds.EndlessWave, LeaderboardIds.ArenaTrophies };
        private int _tab;
        private RectTransform _list;
        private TMP_Text _me;
        private Button[] _tabs;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _loading;

        protected override string TitleKey => "board.title";
        protected override float RefreshSeconds => 30f;

        protected override void BuildContent(RectTransform c)
        {
            _tabs = UIKit.Tabs(c, new[] { Loc.Get("board.campaign"), Loc.Get("board.endless"), Loc.Get("board.arena") }, 0,
                i => { _tab = i; Load(); }, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1040, 90));
            _me = UIKit.Label(c, "", 38, UIKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_me.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -106), new Vector2(1000, 60));
            RowKit.ScrollArea(c, 10, 176, out _list);
        }

        protected override void OnShown()
        {
            _tab = Preselect;
            Preselect = 0;
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].GetComponent<Image>().color = i == _tab ? UIKit.Accent : UIKit.PanelLight;
            base.OnShown();
        }

        protected override void Refresh() => Load();

        private void Load()
        {
            if (_loading) return;
            _loading = true;
            var boards = ServiceLocator.Get<ILeaderboardService>();
            string id = Boards[_tab];
            boards.GetPlayerEntry(id, me =>
                _me.text = me != null ? Loc.Format("board.you", me.Rank, me.Score) : Loc.Get("board.unranked"));
            boards.GetTop(id, 20, entries =>
            {
                _loading = false;
                foreach (var go in _spawned) if (go != null) Destroy(go);
                _spawned.Clear();
                var auth = ServiceLocator.Get<IAuthService>();
                foreach (var e in entries)
                {
                    bool mine = e.PlayerId == auth.PlayerId;
                    var row = UIKit.ListRow(_list, 100, mine ? new Color(0.25f, 0.35f, 0.2f) : UIKit.Panel);
                    _spawned.Add(row.gameObject);
                    RowKit.Left(row, "#" + e.Rank, 38, e.Rank <= 3 ? UIKit.Gold : Color.white, 24, 18, 140, 64, FontStyles.Bold);
                    RowKit.Left(row, mine ? e.DisplayName + " (" + Loc.Get("board.you_tag") + ")" : e.DisplayName, 36, Color.white, 170, 18, 560, 64);
                    RowKit.Right(row, e.Score.ToString("N0"), 40, UIKit.Accent, 24, 16, 260, 68, FontStyles.Bold);
                }
            });
        }
    }
}
