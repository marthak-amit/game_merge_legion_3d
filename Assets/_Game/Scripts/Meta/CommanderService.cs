using System;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Economy;
using MergeLegion.Save;

namespace MergeLegion.Meta
{
    /// <summary>Aggregated passive bonuses of the equipped commander (0.10 = +10%).</summary>
    public struct CommanderBonuses
    {
        public float MeleeDamage, RangedDamage, FlyingDamage, AllDamage;
        public float TankHp, AllHp;
        public float CoinBonus;

        public float DamageMult(UnitLineId line)
        {
            float m = 1f + AllDamage;
            switch (line)
            {
                case UnitLineId.Melee: m += MeleeDamage; break;
                case UnitLineId.Ranged: m += RangedDamage; break;
                case UnitLineId.Flying: m += FlyingDamage; break;
            }
            return m;
        }

        public float HpMult(UnitLineId line) => 1f + AllHp + (line == UnitLineId.Tank ? TankHp : 0f);
    }

    public struct SkillSpec
    {
        public SkillType Type;
        public float Cooldown, Power, Radius, Duration;
        public int Count;
    }

    public readonly struct CommanderChangedEvent { }

    public enum CommanderResult { Ok, NotEnoughShards, MaxLevel, Locked, Unknown }

    public sealed class CommanderService
    {
        private readonly SaveService _save;
        private readonly CurrencyService _currency;
        private readonly GameDatabase _db;

        public CommanderService(SaveService save, CurrencyService currency, GameDatabase db)
        {
            _save = save;
            _currency = currency;
            _db = db;
            EnsureStarter();
        }

        private void EnsureStarter()
        {
            foreach (var c in _db.commanders)
            {
                if (c.unlockShards == 0 && GetLevel(c.id) == 0) IntEntries.Set(_save.Data.commanderLevels, c.id, 1);
            }
            if (string.IsNullOrEmpty(_save.Data.equippedCommander) || GetLevel(_save.Data.equippedCommander) == 0)
            {
                foreach (var c in _db.commanders)
                {
                    if (GetLevel(c.id) <= 0) continue;
                    _save.Data.equippedCommander = c.id;
                    break;
                }
            }
            _save.MarkDirty();
        }

        public string EquippedId => _save.Data.equippedCommander;
        public CommanderData Equipped => _db.GetCommander(EquippedId);

        public int GetLevel(string id) => IntEntries.Get(_save.Data.commanderLevels, id);
        public bool IsUnlocked(string id) => GetLevel(id) > 0;
        public long Shards(string id) => _currency.GetShards(id);

        public float Scale(CommanderData c, int level) => 1f + c.levelScale * Math.Max(0, level - 1);

        public int UpgradeCost(string id)
        {
            var c = _db.GetCommander(id);
            int lv = GetLevel(id);
            return c == null || lv <= 0 ? 0 : lv * c.shardsPerLevel;
        }

        /// <summary>Unlocks a commander outright (purchased bundle), no shards spent.</summary>
        public void GrantUnlock(string id)
        {
            if (_db.GetCommander(id) == null || IsUnlocked(id)) return;
            IntEntries.Set(_save.Data.commanderLevels, id, 1);
            _save.MarkDirty();
            EventBus.Publish(new CommanderChangedEvent());
        }

        public CommanderResult Unlock(string id)
        {
            var c = _db.GetCommander(id);
            if (c == null) return CommanderResult.Unknown;
            if (IsUnlocked(id)) return CommanderResult.Ok;
            if (!_currency.TrySpendShards(id, c.unlockShards, "commander_unlock")) return CommanderResult.NotEnoughShards;
            IntEntries.Set(_save.Data.commanderLevels, id, 1);
            _save.MarkDirty();
            EventBus.Publish(new CommanderChangedEvent());
            return CommanderResult.Ok;
        }

        public CommanderResult Upgrade(string id)
        {
            var c = _db.GetCommander(id);
            if (c == null) return CommanderResult.Unknown;
            int lv = GetLevel(id);
            if (lv <= 0) return CommanderResult.Locked;
            if (lv >= c.maxLevel) return CommanderResult.MaxLevel;
            if (!_currency.TrySpendShards(id, UpgradeCost(id), "commander_upgrade")) return CommanderResult.NotEnoughShards;
            IntEntries.Set(_save.Data.commanderLevels, id, lv + 1);
            _save.MarkDirty();
            EventBus.Publish(new CommanderChangedEvent());
            return CommanderResult.Ok;
        }

        public bool Equip(string id)
        {
            if (!IsUnlocked(id)) return false;
            _save.Data.equippedCommander = id;
            _save.MarkDirty();
            EventBus.Publish(new CommanderChangedEvent());
            return true;
        }

        public CommanderBonuses GetBonuses()
        {
            var b = new CommanderBonuses();
            var c = Equipped;
            if (c == null) return b;
            float v = c.passiveValue * Scale(c, GetLevel(c.id));
            switch (c.passiveType)
            {
                case PassiveType.MeleeDamage: b.MeleeDamage = v; break;
                case PassiveType.RangedDamage: b.RangedDamage = v; break;
                case PassiveType.FlyingDamage: b.FlyingDamage = v; break;
                case PassiveType.AllDamage: b.AllDamage = v; break;
                case PassiveType.TankHp: b.TankHp = v; break;
                case PassiveType.AllHp: b.AllHp = v; break;
                case PassiveType.CoinBonus: b.CoinBonus = v; break;
            }
            return b;
        }

        public SkillSpec GetSkill()
        {
            var c = Equipped;
            if (c == null) return new SkillSpec();
            float s = Scale(c, GetLevel(c.id));
            bool scalesPower = c.skillType != SkillType.Summon && c.skillType != SkillType.LightningChain;
            return new SkillSpec
            {
                Type = c.skillType,
                Cooldown = c.cooldown,
                Power = scalesPower || c.skillType == SkillType.LightningChain ? c.power * s : c.power,
                Radius = c.radius,
                Duration = c.duration,
                Count = c.skillType == SkillType.Summon ? c.count + (int)((s - 1f) * 5f) : c.count
            };
        }
    }
}
