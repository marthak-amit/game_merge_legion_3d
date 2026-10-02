using MergeLegion.Save;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class SaveCodecTests
    {
        private sealed class AddFlagMigration : ISaveMigration
        {
            public int FromVersion { get; }
            private readonly string _from, _to;
            public AddFlagMigration(int from, string a, string b) { FromVersion = from; _from = a; _to = b; }
            public string Migrate(string json) => json.Replace(_from, _to);
        }

        [Test]
        public void RoundTrip_PreservesData()
        {
            var codec = new SaveCodec();
            var data = new SaveData { coins = 1234, gems = 56, highestCampaignLevel = 42 };
            data.settings.hapticsOn = false;
            data.SetFlag("tutorial_done");
            data.commanderShards.Add(new IntEntry { key = "meteor", value = 9 });

            var result = codec.Decode(codec.Encode(data));

            Assert.AreEqual(DecodeStatus.Ok, result.Status);
            Assert.AreEqual(1234, result.Data.coins);
            Assert.AreEqual(56, result.Data.gems);
            Assert.AreEqual(42, result.Data.highestCampaignLevel);
            Assert.IsFalse(result.Data.settings.hapticsOn);
            Assert.IsTrue(result.Data.HasFlag("tutorial_done"));
            Assert.AreEqual(9, result.Data.commanderShards[0].value);
            Assert.AreEqual(data.playerId, result.Data.playerId);
        }

        [Test]
        public void Decode_EditedPayload_IsTamperedButStillLoads()
        {
            var codec = new SaveCodec();
            string encoded = codec.Encode(new SaveData { coins = 10 });
            // payload is JSON-escaped inside the envelope, so the coins field reads \"coins\":10
            string edited = encoded.Replace("\\\"coins\\\":10", "\\\"coins\\\":999999");
            Assert.AreNotEqual(encoded, edited);

            var result = codec.Decode(edited);

            Assert.AreEqual(DecodeStatus.Tampered, result.Status);
            Assert.IsTrue(result.Data.tamperDetected);
            Assert.AreEqual(999999, result.Data.coins);
        }

        [Test]
        public void Decode_Garbage_IsCorrupt()
        {
            Assert.AreEqual(DecodeStatus.Corrupt, new SaveCodec().Decode("not json at all {{{").Status);
        }

        [Test]
        public void Decode_Empty_IsEmpty()
        {
            Assert.AreEqual(DecodeStatus.Empty, new SaveCodec().Decode("").Status);
            Assert.AreEqual(DecodeStatus.Empty, new SaveCodec().Decode(null).Status);
        }

        [Test]
        public void Decode_NewerVersion_IsRejected()
        {
            string fromFuture = new SaveCodec(null, 5).Encode(new SaveData());
            Assert.AreEqual(DecodeStatus.FutureVersion, new SaveCodec(null, 2).Decode(fromFuture).Status);
        }

        [Test]
        public void Decode_OlderVersion_RunsMigrationChainInOrder()
        {
            // Current is v3, so the chain must be applied 1->2->3 in order.
            string v1 = new SaveCodec(null, 1).Encode(new SaveData { coins = 77 });
            var codec = new SaveCodec(new ISaveMigration[]
            {
                new AddFlagMigration(1, "\\\"coins\\\":77", "\\\"coins\\\":78"),
                new AddFlagMigration(2, "\\\"coins\\\":78", "\\\"coins\\\":79"),
            }, 3);

            var result = codec.Decode(v1);

            // The checksum is verified against the stored (pre-migration) payload, so a legit old save is not "tampered".
            Assert.AreEqual(DecodeStatus.Ok, result.Status);
            Assert.AreEqual(79, result.Data.coins);
        }

        [Test]
        public void Decode_MissingMigration_Fails()
        {
            string v1 = new SaveCodec(null, 1).Encode(new SaveData());
            Assert.AreEqual(DecodeStatus.MigrationFailed, new SaveCodec(null, 2).Decode(v1).Status);
        }
    }
}
