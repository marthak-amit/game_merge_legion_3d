#!/usr/bin/env python3
"""Generates the balance CSVs in Assets/_Game/Resources/Balance. Edit numbers here or edit the CSVs directly."""
import os
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Resources", "Balance")
os.makedirs(OUT, exist_ok=True)
SCALE = 1.9
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
            f.write("%s,%d,%.1f,%.1f,%.2f,%.2f,%.2f,%.2f,%s\n" % (name, lv, hp * m, dmg * m, aspd, rng, ms, 0.7 + 0.08 * (lv - 1), hexc(tint)))
print("balance csv written")
