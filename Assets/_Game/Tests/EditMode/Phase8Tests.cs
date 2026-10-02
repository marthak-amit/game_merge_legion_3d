using System;
using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Battle;
using MergeLegion.Core;
using MergeLegion.Economy;
using MergeLegion.Grid;
using MergeLegion.Levels;
using MergeLegion.Meta;
using MergeLegion.Monetization;
using MergeLegion.Save;
using MergeLegion.Services;
using MergeLegion.Services.Mock;
using MergeLegion.Tutorial;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class TutorialTests : MetaFixture
    {
        private TutorialService _tut;
        private TutorialConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            _cfg = TutorialConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/tutorial").text);
            Level = 1;
            Save.Data.highestCampaignLevel = 0;
            _tut = new TutorialService(Save, _cfg, () => Save.Data.highestCampaignLevel + 1, Currency, Analytics);
        }

        [TearDown]
        public void TearDown() => _tut.Dispose();

        private void Win(int level)
        {
            Save.Data.highestCampaignLevel = level;
            EventBus.Publish(new LevelCompletedEvent(level, true, 3, false, false, 20f));
        }

        [Test]
        public void FirstLaunch_NeedsIntroBattle_AndIsActive()
        {
            Assert.IsTrue(_tut.IsActive);
            Assert.IsTrue(_tut.NeedsIntroBattle);
            Assert.IsFalse(Save.Data.HasFlag(SaveFlags.TutorialDone));
        }

        [Test]
        public void FirstThirtySeconds_BuyBuyMergeFight_InOrder()
        {
            Assert.AreEqual("buy1", _tut.Current("battle", "prepare").id);
            Assert.AreEqual("buy_Melee", _tut.Current("battle", "prepare").target);

            EventBus.Publish(new UnitBoughtEvent(0, 50));
            Assert.AreEqual("buy2", _tut.Current("battle", "prepare").id);

            EventBus.Publish(new UnitBoughtEvent(0, 58));
            var merge = _tut.Current("battle", "prepare");
            Assert.AreEqual("merge", merge.id);
            Assert.IsFalse(merge.dim, "dragging needs the world input to stay free");

            EventBus.Publish(new UnitMergedEvent(0, 1, 0, 2));
            Assert.AreEqual("fight", _tut.Current("battle", "prepare").id);

            EventBus.Publish(new BattlePhaseEvent(BattlePhase.Fighting));
            Assert.IsNull(_tut.Current("battle", "fight"), "next step waits for level 2");
            Assert.IsTrue(_tut.IsDone("fight"));
        }

        [Test]
        public void WrongEventsDoNotAdvanceTheCurrentStep()
        {
            EventBus.Publish(new UnitMergedEvent(0, 1, 0, 2));
            EventBus.Publish(new BattlePhaseEvent(BattlePhase.Fighting));
            Assert.AreEqual("buy1", _tut.Current("battle", "prepare").id);
        }

        [Test]
        public void WrongSceneOrPhase_ShowsNothing()
        {
            Assert.IsNull(_tut.Current("home", ""));
            Assert.IsNull(_tut.Current("battle", "fight"));
        }

        [Test]
        public void SkillStep_AppearsOnLevelTwoDuringFight()
        {
            foreach (var id in new[] { "buy1", "buy2", "merge", "fight" }) Complete(id);
            Win(1);
            Assert.IsNull(_tut.Current("battle", "prepare"));
            var skill = _tut.Current("battle", "fight");
            Assert.AreEqual("skill", skill.id);
            EventBus.Publish(new SkillUsedEvent());
            Assert.IsTrue(_tut.IsDone("skill"));
        }

        private void Complete(string id)
        {
            switch (id)
            {
                case "buy1": case "buy2": EventBus.Publish(new UnitBoughtEvent(0, 1)); break;
                case "merge": EventBus.Publish(new UnitMergedEvent(0, 1, 0, 2)); break;
                case "fight": EventBus.Publish(new BattlePhaseEvent(BattlePhase.Fighting)); break;
            }
        }

        [Test]
        public void GuidedUnlocks_ResearchChestCommanderCastleMissions()
        {
            foreach (var id in new[] { "buy1", "buy2", "merge", "fight" }) Complete(id);
            Save.Data.highestCampaignLevel = 2;
            EventBus.Publish(new SkillUsedEvent());
            Save.Data.highestCampaignLevel = 3;                 // about to play level 4... skill window over, research available
            Assert.AreEqual("research_open", _tut.Current("home", "").id);

            EventBus.Publish(new ScreenChangedEvent(UI.ScreenId.Shop));
            Assert.IsFalse(_tut.IsDone("research_open"), "other screens don't count");
            EventBus.Publish(new ScreenChangedEvent(UI.ScreenId.Army));
            var buy = _tut.Current("home", "");
            Assert.AreEqual("research_buy", buy.id);
            Assert.AreEqual(400, Currency.Get(CurrencyType.Coins), "gifted so the player can afford the upgrade");
            EventBus.Publish(new ResearchChangedEvent(Data.UnitLineId.Melee, ResearchStat.Hp));

            Save.Data.highestCampaignLevel = 4;
            Assert.AreEqual("chest", _tut.Current("home", "").id);
            EventBus.Publish(new ChestOpenedEvent("wooden"));

            Save.Data.highestCampaignLevel = 5;
            Assert.AreEqual("commander_open", _tut.Current("home", "").id);
            Assert.AreEqual(20, Currency.GetShards("selene"));
            EventBus.Publish(new ScreenChangedEvent(UI.ScreenId.Commanders));
            Assert.AreEqual("commander_unlock", _tut.Current("home", "").id);
            EventBus.Publish(new CommanderChangedEvent());

            Save.Data.highestCampaignLevel = 6;
            Assert.AreEqual("castle", _tut.Current("home", "").id);
            _tut.Notify(TutorialTrigger.TargetTapped);

            Save.Data.highestCampaignLevel = 7;
            Assert.AreEqual("missions", _tut.Current("home", "").id);
            EventBus.Publish(new ScreenChangedEvent(UI.ScreenId.Missions));

            Assert.IsNull(_tut.Current("home", ""));
            Assert.IsFalse(_tut.IsActive);
            Assert.IsTrue(Save.Data.HasFlag(SaveFlags.TutorialDone));
        }

        [Test]
        public void MissedWindow_DoesNotBlockTheRest()
        {
            foreach (var id in new[] { "buy1", "buy2", "merge", "fight" }) Complete(id);
            Save.Data.highestCampaignLevel = 7;      // jumped past the research window (3..6) via cloud save etc.
            var step = _tut.Current("home", "");
            Assert.AreNotEqual("research_open", step == null ? "" : step.id);
        }

        [Test]
        public void ReturningPlayer_AtHighLevel_SkipsTutorial()
        {
            _tut.Dispose();
            Save.Data.highestCampaignLevel = 30;
            _tut = new TutorialService(Save, _cfg, () => Save.Data.highestCampaignLevel + 1, Currency, Analytics);
            Assert.IsFalse(_tut.IsActive);
            Assert.IsNull(_tut.Current("battle", "prepare"));
        }

        [Test]
        public void NoInterstitialsWhileTutorialRuns_ThenTheyUnlock()
        {
            Save.Data.highestCampaignLevel = 8;
            Assert.IsTrue(_tut.IsActive);
            Assert.IsFalse(Ads.CanShowInterstitial(10), "never during the tutorial");
            _tut.SkipAll();
            Assert.IsTrue(Ads.CanShowInterstitial(10));
        }

        [Test]
        public void StepCompletion_LogsAnalytics()
        {
            EventBus.Publish(new UnitBoughtEvent(0, 50));
            Assert.IsTrue(Analytics.Events.Exists(e => e.StartsWith("tutorial_step") && e.Contains("buy1")));
        }

        [Test]
        public void ShippedTutorial_ReferencesRealTargetsAndText()
        {
            UI.Loc.Reset();
            UI.Loc.Load("en");
            foreach (var s in _cfg.steps)
            {
                Assert.AreNotEqual("#" + s.textKey, UI.Loc.Get(s.textKey), s.id);
                Assert.IsFalse(string.IsNullOrEmpty(s.target), s.id);
            }
            UI.Loc.Reset();
        }
    }

    public class PushSchedulerTests : MetaFixture
    {
        [Test]
        public void SchedulesCastleChestDailyAndEvent()
        {
            Time.UtcNow = new DateTime(2026, 3, 4, 20, 0, 0, DateTimeKind.Local).ToUniversalTime(); // Wednesday evening
            var push = new MockPushService();
            var castle = new CastleService(Save, Meta.castle, Config, Time, Currency, () => false);
            var chests = new ChestService(Save, Meta, Currency, Granter, Time, new DeterministicRng(1), Analytics, Ads);
            var login = new LoginService(Save, Meta.login, Daily, Granter, Time);
            var shop = MonetizationConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/monetization_config").text);
            var ev = new WeekendEventService(Save, shop.weekendEvent, null, Time, Granter, Db, Analytics, () => Level);
            chests.OpenFree("wooden");
            login.Claim();

            var sched = new PushScheduler(push, castle, chests, login, ev, Time, k => k);
            sched.Reschedule();

            Assert.IsTrue(push.Scheduled.ContainsKey(PushIds.CastleFull));
            Assert.AreEqual(Time.UtcNow.AddHours(8), push.Scheduled[PushIds.CastleFull]);
            Assert.AreEqual(Time.UtcNow.AddHours(4), push.Scheduled[PushIds.ChestReady]);
            Assert.AreEqual(new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Local).ToUniversalTime(), push.Scheduled[PushIds.DailyReward]);
            Assert.IsTrue(push.Scheduled.ContainsKey(PushIds.EventStart));
            Assert.IsTrue(push.Scheduled[PushIds.EventStart] > Time.UtcNow);
        }

        [Test]
        public void Reschedule_ReplacesPreviousSchedule_AndSkipsReadyThings()
        {
            var push = new MockPushService();
            var castle = new CastleService(Save, Meta.castle, Config, Time, Currency, () => false);
            var chests = new ChestService(Save, Meta, Currency, Granter, Time, new DeterministicRng(1), Analytics, Ads);
            var login = new LoginService(Save, Meta.login, Daily, Granter, Time);
            var shop = MonetizationConfig.FromJson(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Config/monetization_config").text);
            var ev = new WeekendEventService(Save, shop.weekendEvent, null, Time, Granter, Db, Analytics, () => Level);
            var sched = new PushScheduler(push, castle, chests, login, ev, Time, k => k);

            Time.Advance(TimeSpan.FromHours(10));   // castle full, chest ready
            sched.Reschedule();
            Assert.IsFalse(push.Scheduled.ContainsKey(PushIds.CastleFull), "already full: nothing to remind about");
            Assert.IsFalse(push.Scheduled.ContainsKey(PushIds.ChestReady), "already ready");
            Assert.IsTrue(push.Scheduled.ContainsKey(PushIds.DailyReward));

            int before = push.Scheduled.Count;
            sched.Reschedule();
            Assert.AreEqual(before, push.Scheduled.Count, "no duplicates");
            sched.CancelAll();
            Assert.AreEqual(0, push.Scheduled.Count);
        }
    }

    public class ProceduralAudioTests
    {
        [Test]
        public void EverySfx_RendersAudibleBoundedSamples()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var data = ProceduralAudio.Render(id);
                Assert.Greater(data.Length, ProceduralAudio.Rate / 40, id.ToString());
                Assert.Less(data.Length, ProceduralAudio.Rate * 2, id + " should be short");
                float peak = 0f;
                foreach (var s in data)
                {
                    Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), id.ToString());
                    peak = Math.Max(peak, Math.Abs(s));
                }
                Assert.Greater(peak, 0.05f, id + " is audible");
                Assert.LessOrEqual(peak, 1.0001f, id + " does not clip");
            }
        }

        [Test]
        public void MusicLoops_AreSeamlessLengthAndBounded()
        {
            for (int variant = 0; variant < 2; variant++)
            {
                var data = ProceduralAudio.MusicLoop(variant);
                int bpm = variant == 0 ? 92 : 132;
                int expected = (int)(8 * 4 * (60.0 / bpm) * ProceduralAudio.Rate);
                Assert.AreEqual(expected, data.Length, 2);
                float peak = 0f;
                foreach (var s in data) { Assert.IsFalse(float.IsNaN(s)); peak = Math.Max(peak, Math.Abs(s)); }
                Assert.Greater(peak, 0.1f);
                Assert.LessOrEqual(peak, 1.0001f);
                Assert.Less(Math.Abs(data[0]), 0.5f, "starts near zero crossing region to avoid a click");
            }
        }

        [Test]
        public void Synthesis_IsDeterministic()
        {
            var a = ProceduralAudio.Render(SfxId.Hit);
            var b = ProceduralAudio.Render(SfxId.Hit);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i += 97) Assert.AreEqual(a[i], b[i]);
        }
    }
}
