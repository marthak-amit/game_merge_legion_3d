using System;
using MergeLegion.Core;
using MergeLegion.Save;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class SaveServiceTests
    {
        private InMemorySaveStorage _storage;
        private FakeTimeService _time;

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
            _storage = new InMemorySaveStorage();
            _time = new FakeTimeService();
        }

        private SaveService Create() => new SaveService(_storage, _time);

        [Test]
        public void Load_WithNoSave_StartsNewGameAndStampsCreationTime()
        {
            var svc = Create();
            Assert.AreEqual(LoadOutcome.NewGame, svc.Load());
            Assert.AreEqual(_time.UtcNow.Ticks, svc.Data.createdUtcTicks);
            Assert.IsTrue(svc.IsDirty);
        }

        [Test]
        public void SaveThenLoad_RestoresData()
        {
            var a = Create();
            a.Load();
            a.Data.coins = 500;
            string id = a.Data.playerId;
            a.Save();

            var b = Create();
            Assert.AreEqual(LoadOutcome.Loaded, b.Load());
            Assert.AreEqual(500, b.Data.coins);
            Assert.AreEqual(id, b.Data.playerId);
            Assert.IsFalse(b.IsDirty);
        }

        [Test]
        public void Save_StampsTimeAndPublishesEvent()
        {
            var svc = Create();
            svc.Load();
            int events = 0;
            EventBus.Subscribe<SaveCompletedEvent>(_ => events++);
            _time.Advance(TimeSpan.FromHours(3));

            svc.Save();

            Assert.AreEqual(_time.UtcNow.Ticks, svc.Data.lastSavedUtcTicks);
            Assert.AreEqual(1, events);
        }

        [Test]
        public void Flush_OnlyWritesWhenDirty()
        {
            var svc = Create();
            svc.Load();
            svc.Save();
            int events = 0;
            EventBus.Subscribe<SaveCompletedEvent>(_ => events++);

            svc.Flush();
            Assert.AreEqual(0, events);

            svc.MarkDirty();
            svc.Flush();
            Assert.AreEqual(1, events);
            Assert.IsFalse(svc.IsDirty);
        }

        [Test]
        public void Load_CorruptMain_RecoversFromBackup()
        {
            var a = Create();
            a.Load();
            a.Data.coins = 111;
            a.Save();            // backup slot is empty after the first write
            a.Data.coins = 222;
            a.Save();            // main = 222, backup = 111
            _storage.CorruptMain("{{garbage");

            var b = Create();
            Assert.AreEqual(LoadOutcome.RecoveredFromBackup, b.Load());
            Assert.AreEqual(111, b.Data.coins);
        }

        [Test]
        public void Load_AllCorrupt_StartsNewGame()
        {
            _storage.Write("junk");
            var svc = Create();
            Assert.AreEqual(LoadOutcome.NewGame, svc.Load());
        }

        [Test]
        public void Load_TamperedSave_ReportsTamperedAndKeepsFlag()
        {
            var a = Create();
            a.Load();
            a.Data.coins = 10;
            a.Save();
            string edited = new System.Collections.Generic.List<string>(_storage.ReadAll())[0]
                .Replace("\\\"coins\\\":10", "\\\"coins\\\":9999");
            _storage.CorruptMain(edited);

            var b = Create();
            Assert.AreEqual(LoadOutcome.Tampered, b.Load());
            Assert.IsTrue(b.Data.tamperDetected);

            b.Save();
            var c = Create();
            c.Load();
            Assert.IsTrue(c.Data.tamperDetected, "tamper flag must survive re-saving");
        }

        [Test]
        public void Load_FutureVersion_DoesNotOverwriteWithNewGame()
        {
            string future = new SaveCodec(null, SaveCodec.CurrentVersion + 1).Encode(new SaveData { coins = 5 });
            _storage.Write(future);
            Assert.AreEqual(LoadOutcome.FutureVersion, Create().Load());
        }

        [Test]
        public void DeleteAll_ClearsStorageAndResetsData()
        {
            var svc = Create();
            svc.Load();
            svc.Data.coins = 99;
            svc.Save();
            string oldId = svc.Data.playerId;

            svc.DeleteAll();

            Assert.AreEqual(0, svc.Data.coins);
            Assert.AreNotEqual(oldId, svc.Data.playerId);
            Assert.AreEqual(0, _storage.ReadAll().Count);
        }

        [Test]
        public void Replace_PersistsNewData()
        {
            var svc = Create();
            svc.Load();
            svc.Replace(new SaveData { highestCampaignLevel = 30 });

            var other = Create();
            other.Load();
            Assert.AreEqual(30, other.Data.highestCampaignLevel);
        }
    }

    public class SaveMergerTests
    {
        private static SaveData Progress(int level, int wave = 0, long ticks = 0) =>
            new SaveData { highestCampaignLevel = level, endlessBestWave = wave, lastSavedUtcTicks = ticks };

        [Test]
        public void NoCloud_UsesLocal()
        {
            var d = SaveMerger.Resolve(Progress(5), null);
            Assert.AreEqual(MergeChoice.UseLocal, d.Choice);
            Assert.IsFalse(d.PromptRequired);
        }

        [Test]
        public void FreshLocal_AdoptsCloudSilently()
        {
            var d = SaveMerger.Resolve(Progress(0), Progress(40));
            Assert.AreEqual(MergeChoice.UseCloud, d.Choice);
            Assert.IsFalse(d.PromptRequired);
        }

        [Test]
        public void BothHaveProgress_HigherWinsWithPrompt()
        {
            var cloudWins = SaveMerger.Resolve(Progress(10), Progress(25));
            Assert.AreEqual(MergeChoice.UseCloud, cloudWins.Choice);
            Assert.IsTrue(cloudWins.PromptRequired);

            var localWins = SaveMerger.Resolve(Progress(30), Progress(25));
            Assert.AreEqual(MergeChoice.UseLocal, localWins.Choice);
            Assert.IsTrue(localWins.PromptRequired);
        }

        [Test]
        public void EndlessWave_BreaksTieOnCampaignLevel()
        {
            var d = SaveMerger.Resolve(Progress(200, 5), Progress(200, 40));
            Assert.AreEqual(MergeChoice.UseCloud, d.Choice);
        }

        [Test]
        public void EqualProgress_NewerSaveWinsWithoutPrompt()
        {
            var d = SaveMerger.Resolve(Progress(10, 0, 100), Progress(10, 0, 200));
            Assert.AreEqual(MergeChoice.UseCloud, d.Choice);
            Assert.IsFalse(d.PromptRequired);
        }
    }
}
