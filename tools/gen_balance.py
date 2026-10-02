#!/usr/bin/env python3
"""Generates the balance CSVs in Assets/_Game/Resources/Balance. Edit numbers here or edit the CSVs directly."""
import os
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Resources", "Balance")
os.makedirs(OUT, exist_ok=True)
SCALE = 1.9
HP_MULT = 3.0   # global fight-length lever: higher = longer, more watchable battles
LEVELS = 8
lines = [
    # id, name_key, role_key, unlock, base_cost, growth, ranged, flying, taunt, proj_speed
    ("Melee", "line.melee", "role.frontline", 1, 50, 1.15, 0, 0, 0, 0),
    ("Ranged", "line.ranged", "role.backline", 1, 70, 1.15, 1, 0, 0, 14),
    ("Tank", "line.tank", "role.tank", 8, 90, 1.15, 0, 0, 1, 0),
    ("Flying", "line.flying", "role.flyer", 20, 110, 1.15, 0, 1, 0, 0),
]
# hp, dmg, attack_speed, range, move_speed, base rgb
base = {
    "Melee": (120, 14, 1.0, 1.4, 3.4, (0.85, 0.25, 0.20)),
    "Ranged": (70, 16, 0.9, 7.0, 2.6, (0.25, 0.70, 0.30)),
    "Tank": (320, 7, 0.7, 1.4, 2.6, (0.30, 0.45, 0.90)),
    "Flying": (85, 13, 1.1, 1.6, 4.2, (0.70, 0.35, 0.85)),
}
def hexc(c):
    return "#%02X%02X%02X" % tuple(int(max(0, min(1, x)) * 255) for x in c)
with open(os.path.join(OUT, "lines.csv"), "w") as f:
    f.write("line,name_key,role_key,unlock_level,base_cost,cost_growth,ranged,flying,taunt,projectile_speed\n")
    for l in lines:
        f.write(",".join(str(x) for x in l) + "\n")
with open(os.path.join(OUT, "units.csv"), "w") as f:
    f.write("line,level,hp,damage,attack_speed,range,move_speed,scale,tint\n")
    for name, (hp, dmg, aspd, rng, ms, col) in base.items():
        for lv in range(1, LEVELS + 1):
            m = SCALE ** (lv - 1)
            t = (lv - 1) / (LEVELS - 1) * 0.55
            tint = tuple(c + (1 - c) * t for c in col)
            f.write("%s,%d,%.1f,%.1f,%.2f,%.2f,%.2f,%.2f,%s\n" % (name, lv, hp * m * HP_MULT, dmg * m, aspd, rng, ms, 0.7 + 0.08 * (lv - 1), hexc(tint)))
print("balance csv written")

commanders = [
    # id, name_key, passive_key, skill_key, passive_type, passive_value, skill_type, cooldown, power, radius, duration, count, unlock_shards, shards_per_level, max_level, level_scale, tint
    ("ignis", "cmd.ignis", "passive.ranged_damage", "skill.meteor", "RangedDamage", 0.10, "Meteor", 25, 3.0, 3.4, 0, 1, 0, 10, 10, 0.10, "#FF6A2B"),
    ("selene", "cmd.selene", "passive.all_hp", "skill.heal_wave", "AllHp", 0.08, "HealWave", 30, 0.35, 0, 0, 1, 20, 10, 10, 0.10, "#6FE3A8"),
    ("kaz", "cmd.kaz", "passive.melee_damage", "skill.rally", "MeleeDamage", 0.12, "Rally", 28, 0.5, 0, 8, 1, 20, 10, 10, 0.10, "#F2C230"),
    ("bastion", "cmd.bastion", "passive.tank_hp", "skill.shield_wall", "TankHp", 0.15, "ShieldWall", 35, 0.5, 0, 7, 1, 40, 12, 10, 0.10, "#5B8DEF"),
    ("voltra", "cmd.voltra", "passive.all_damage", "skill.lightning", "AllDamage", 0.06, "LightningChain", 22, 2.0, 4.0, 0, 6, 60, 14, 10, 0.10, "#9B6BFF"),
    ("nyx", "cmd.nyx", "passive.coin_bonus", "skill.summon", "CoinBonus", 0.10, "Summon", 40, 1.0, 0, 0, 4, 80, 16, 10, 0.10, "#B04BD6"),
]
with open(os.path.join(OUT, "commanders.csv"), "w") as f:
    f.write("id,name_key,passive_key,skill_key,passive_type,passive_value,skill_type,cooldown,power,radius,duration,count,unlock_shards,shards_per_level,max_level,level_scale,tint\n")
    for c in commanders:
        f.write(",".join(str(x) for x in c) + "\n")
