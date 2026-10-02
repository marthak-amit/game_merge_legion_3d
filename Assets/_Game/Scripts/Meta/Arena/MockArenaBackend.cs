using System;
using System.Collections.Generic;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Levels;

namespace MergeLegion.Meta.Arena
{
    /// <summary>
    /// Offline stand-in for the arena server: serves generated bot armies scaled to the player's power plus any
    /// snapshots uploaded by other player ids (so uploading twice under different ids behaves like a real pool).
    /// </summary>
    public sealed class MockArenaBackend : IArenaBackend
    {
        private static readonly string[] Names =
        {
            "IronFist", "Mira", "Thorn", "Kestrel", "Dax", "Orla", "Brannock", "Yuki", "Volt", "Sable", "Rook", "Nova",
            "Haldor", "Pip", "Ember", "Zed", "Lyra", "Grim", "Tess", "Bolt", "Juno", "Rex", "Willow", "Onyx"
        };

        private readonly GameDatabase _db;
        private readonly ArenaConfig _cfg;
        private readonly Dictionary<string, ArenaSnapshot> _uploaded = new Dictionary<string, ArenaSnapshot>();
        private int _nonce;

        public MockArenaBackend(GameDatabase db, ArenaConfig cfg)
        {
            _db = db;
            _cfg = cfg;
        }

        public int UploadCount => _uploaded.Count;

        public void Upload(ArenaSnapshot snapshot, Action<bool> onDone)
        {
            _uploaded[snapshot.playerId] = snapshot;
            onDone?.Invoke(true);
        }

        public void FindOpponents(ArenaSnapshot me, int count, Action<List<ArenaSnapshot>> onDone)
        {
            var result = new List<ArenaSnapshot>();
            float[] ratios = { _cfg.botPowerEasy, _cfg.botPowerEven, _cfg.botPowerHard };

            // real snapshots first (any uploaded by someone else, nearest trophies)
            var others = new List<ArenaSnapshot>();
            foreach (var kv in _uploaded) if (kv.Key != me.playerId) others.Add(kv.Value);
            others.Sort((a, b) => Math.Abs(a.trophies - me.trophies).CompareTo(Math.Abs(b.trophies - me.trophies)));
            for (int i = 0; i < others.Count && result.Count < count / 2; i++) result.Add(others[i]);

            _nonce++;
            int slot = 0;
            while (result.Count < count)
            {
                float ratio = ratios[Math.Min(slot, ratios.Length - 1)];
                result.Add(MakeBot(me, ratio, (me.trophies / 25) * 31 + slot * 7 + _nonce));
                slot++;
            }
            // present weakest to strongest
            result.Sort((a, b) => a.power.CompareTo(b.power));
            onDone?.Invoke(result);
        }

        public ArenaSnapshot MakeBot(ArenaSnapshot me, float powerRatio, int seed)
        {
            var rng = new DeterministicRng(seed);
            float target = Math.Max(40f, me.power * powerRatio);
            var level = new LevelDefinition { enemyCols = 5 };
            int lines = me.units.Count > 0 ? HighestLine(me) + 1 : 2;
            ProceduralLevelGenerator.FillFormation(level, _db, target, rng, Math.Max(2, lines), 15);

            var bot = new ArenaSnapshot
            {
                playerId = "bot_" + seed,
                name = Names[rng.NextInt(Names.Length)] + (10 + rng.NextInt(89)),
                trophies = Math.Max(0, me.trophies + (int)((powerRatio - 1f) * 120f) + rng.Range(-25, 26)),
                isBot = true,
                hpBonus = 0f,
                dmgBonus = 0f,
                commander = _db.commanders.Count > 0 ? _db.commanders[rng.NextInt(_db.commanders.Count)].id : ""
            };
            Arrange(level, bot);
            bot.power = LevelPower.ArmyPower(_db, level.enemies);
            return bot;
        }

        private static int HighestLine(ArenaSnapshot me)
        {
            int m = 0;
            foreach (var u in me.units) m = Math.Max(m, u.line);
            return m;
        }

        /// <summary>Lays units out on the 5x3 grid: tanks/melee in front, ranged/flyers behind.</summary>
        public static void Arrange(LevelDefinition level, ArenaSnapshot into)
        {
            var units = new List<EnemySpawnDef>(level.enemies);
            units.Sort((a, b) =>
            {
                int pa = Priority(a.line), pb = Priority(b.line);
                return pa != pb ? pa.CompareTo(pb) : b.level.CompareTo(a.level);
            });
            for (int i = 0; i < units.Count && i < 15; i++)
                into.units.Add(new ArenaUnit { line = units[i].line, level = units[i].level, col = i % 5, row = i / 5 });
        }

        private static int Priority(int line)
        {
            switch (line)
            {
                case 2: return 0;   // tank
                case 0: return 1;   // melee
                case 3: return 2;   // flying
                default: return 3;  // ranged
            }
        }
    }
}
