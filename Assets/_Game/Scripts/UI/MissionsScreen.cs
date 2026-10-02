using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeLegion.UI
{
    /// <summary>Daily / weekly missions and long-term achievements (section 2.5).</summary>
    public sealed class MissionsScreen : MenuScreen
    {
        private int _tab;
        private RectTransform _list;
        private TMP_Text _resetLabel;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private int _signature;

        protected override void BuildContent(RectTransform c)
        {
            UIKit.Tabs(c, new[] { Loc.Get("missions.daily"), Loc.Get("missions.weekly"), Loc.Get("missions.achievements") }, 0,
                i => { _tab = i; _signature = 0; Rebuild(); }, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1040, 90));
            _resetLabel = UIKit.Label(c, "", 32, UIKit.TextDim);
            UIKit.Place(_resetLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(1000, 50));
            RowKit.ScrollArea(c, 14, 160, out _list);
        }

        protected override void OnShown()
        {
            _signature = 0;
            base.OnShown();
        }

        protected override void Refresh()
        {
            var missions = ServiceLocator.Get<MissionService>();
            missions.Refresh();
            var daily = ServiceLocator.Get<DailyService>();
            _resetLabel.text = _tab == 0 ? Loc.Format("missions.resets", ItemDisplay.Duration(daily.UntilReset())) : "";

            // rebuild only when progress/claim state changes (keeps scrolling smooth)
            int sig = _tab * 100003;
            foreach (var m in missions.Missions(_tab)) sig = sig * 31 + m.progress * 7 + (m.claimed ? 3 : 0) + m.id.GetHashCode();
            foreach (var a in missions.Achievements()) sig = sig * 31 + a.Progress + a.ClaimedTiers * 1000;
            sig = sig * 31 + (missions.BonusAvailable(_tab) ? 1 : 0);
            if (sig == _signature) return;
            _signature = sig;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            var missions = ServiceLocator.Get<MissionService>();

            if (_tab == 2)
            {
                foreach (var a in missions.Achievements()) AchievementRow(a);
                return;
            }

            foreach (var m in missions.Missions(_tab)) MissionRow(m);
            BonusRow(_tab);
        }

        private void MissionRow(MissionState m)
        {
            var missions = ServiceLocator.Get<MissionService>();
            var t = missions.Template(m.id);
            var row = UIKit.ListRow(_list, 190, m.claimed ? new Color(0.14f, 0.2f, 0.16f) : UIKit.Panel);
            _spawned.Add(row.gameObject);

            RowKit.Left(row, Loc.Get(t.titleKey), 38, Color.white, 24, 14, 640, 60, FontStyles.Bold);
            RowKit.Left(row, ItemDisplay.Describe(t.reward), 30, UIKit.Gold, 24, 72, 600, 46);
            RowKit.Bar(row, UIKit.Good, 24, 134, 560, 26, out var fill);
            fill.fillAmount = Mathf.Clamp01((float)m.progress / t.target);
            RowKit.Left(row, m.progress + " / " + t.target, 26, Color.white, 600, 128, 160, 40);

            bool done = missions.IsComplete(m);
            if (m.claimed) RowKit.RightButton(row, Loc.Get("common.claimed"), UIKit.Disabled, null, new Vector2(260, 100), 32).interactable = false;
            else if (done)
            {
                string id = m.id;
                RowKit.RightButton(row, Loc.Get("common.claim"), UIKit.Good, () =>
                {
                    var items = missions.Claim(id);
                    if (items != null) { Popups.Reward(Loc.Get(t.titleKey), items); Sfx.Play(SfxId.Reward); }
                    _signature = 0;
                    Refresh();
                }, new Vector2(260, 100), 38);
            }
            else if (_tab == 0)
            {
                string id = m.id;
                var ads = ServiceLocator.Get<AdsManager>();
                var b = RowKit.RightButton(row, Loc.Get("missions.swap") + " [AD]", UIKit.Blue, () =>
                {
                    ads.ShowRewarded(AdPlacements.MissionReroll, ok =>
                    {
                        if (ok) missions.Reroll(id); else Toast.Show(Loc.Get("result.ad_unavailable"));
                        _signature = 0;
                        Refresh();
                    });
                }, new Vector2(260, 100), 30);
                b.interactable = ads.CanShowRewarded(AdPlacements.MissionReroll) == AdBlockReason.None;
            }
        }

        private void BonusRow(int period)
        {
            var missions = ServiceLocator.Get<MissionService>();
            var row = UIKit.ListRow(_list, 150, new Color(0.25f, 0.2f, 0.4f));
            _spawned.Add(row.gameObject);
            RowKit.Left(row, Loc.Get(period == 0 ? "missions.bonus_daily" : "missions.bonus_weekly"), 36, UIKit.Gold, 24, 20, 650, 60, FontStyles.Bold);
            RowKit.Left(row, Loc.Get("missions.bonus_hint"), 28, UIKit.TextDim, 24, 80, 650, 50);
            var b = RowKit.RightButton(row, Loc.Get("common.claim"), UIKit.Good, () =>
            {
                var items = missions.ClaimBonus(period);
                if (items != null) Popups.Reward(Loc.Get("missions.bonus_title"), items);
                _signature = 0;
                Refresh();
            }, new Vector2(260, 100), 38);
            b.interactable = missions.BonusAvailable(period);
        }

        private void AchievementRow(AchievementView a)
        {
            var missions = ServiceLocator.Get<MissionService>();
            var row = UIKit.ListRow(_list, 190);
            _spawned.Add(row.gameObject);
            RowKit.Left(row, Loc.Get(a.Template.titleKey), 36, Color.white, 24, 12, 700, 60, FontStyles.Bold);
            if (a.NextTarget == 0)
            {
                RowKit.Left(row, Loc.Get("missions.all_done"), 32, UIKit.Good, 24, 80, 600, 50);
                return;
            }
            var tier = a.Template.tiers[a.ClaimedTiers];
            RowKit.Left(row, Loc.Format("missions.tier", a.ClaimedTiers + 1, a.Template.tiers.Count) + "   " + ItemDisplay.Describe(tier.reward), 28, UIKit.Gold, 24, 72, 700, 46);
            RowKit.Bar(row, UIKit.Blue, 24, 134, 560, 26, out var fill);
            fill.fillAmount = Mathf.Clamp01((float)a.Progress / a.NextTarget);
            RowKit.Left(row, Mathf.Min(a.Progress, a.NextTarget) + " / " + a.NextTarget, 26, Color.white, 600, 128, 200, 40);
            var b = RowKit.RightButton(row, Loc.Get("common.claim"), UIKit.Good, () =>
            {
                var items = missions.ClaimAchievement(a.Template.id);
                if (items != null) Popups.Reward(Loc.Get(a.Template.titleKey), items);
                _signature = 0;
                Refresh();
            }, new Vector2(240, 100), 38);
            b.interactable = a.CanClaim;
        }
    }
}
