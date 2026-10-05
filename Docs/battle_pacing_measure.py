"""Portable deterministic proxy, not a Unity test or full live-species simulation.
Run: python Docs/battle_pacing_measure.py
Fixtures mirror BattlePacingTests neutral synthetic equal-stat matrix.
"""
import re
import statistics
from pathlib import Path

constants = Path("Assets/Scripts/Core/GameConstants.cs").read_text(encoding="utf-8-sig")
def value(name):
    return float(re.search(r"\b" + name + r" = ([0-9.]+)f?;", constants).group(1))

def measure(multiplier):
    rounds = []
    for rarity in range(5):
        for level in (5, 15, 30, 50):
            for power in (18, 30, 45):
                hp = 49 + rarity * 18 + level * value("HpPerLevel")
                attack = 19 + rarity * 9 + level * 2
                defense = 14 + rarity * 7 + level
                ratio = min(value("MaxAtkDefRatio"), max(value("MinAtkDefRatio"), attack / defense))
                player = enemy = hp
                count = 0
                while min(player, enemy) > 0 and count < 30:
                    count += 1
                    damage = power + level * value("LevelDamageScale") if count % 2 else round(attack * .7)
                    enemy -= max(1, round(max(1, round(damage * multiplier)) * ratio))
                    if enemy <= 0:
                        break
                    damage = (power if count % 2 else 10) + level * value("LevelDamageScale")
                    player -= max(1, round(max(1, round(damage * multiplier)) * ratio))
                rounds.append(count)
    return rounds

for label, multiplier in (("baseline", 1), ("wild pacing", value("WildDamageMultiplier"))):
    rounds = measure(multiplier)
    print(label, "n=", len(rounds), "median=", statistics.median(rounds), "min=", min(rounds), "max=", max(rounds))
    print({n: rounds.count(n) for n in sorted(set(rounds))})
assert 4 <= statistics.median(measure(value("WildDamageMultiplier"))) <= 6
