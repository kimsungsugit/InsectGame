# Battle pacing validation — 2026-09-18

## Implemented behavior

- Normal attack presentation: 1.8 seconds; skill: 2.5 seconds at 1x. Arena callback reveals HP and damage numbers at impact. 2x affects presentation delta only.
- Resolved rounds retain the player-action HP snapshot separately from the final enemy reply. Enemy healing and player healing therefore reveal on their own action, not prematurely.
- An enemy reply is presented before pending defeat/swap, including lethal replies. End-of-action faint presentation gets 0.7 presentation seconds before the result/swap screen.
- Damage labels follow the actual target model screen position. Controller hit/faint/text effects are deferred while the battle screen owns presentation; messages drain at impact.
- Ordinary wild direct-hit damage uses multiplier 0.7 before the existing defense ratio. Duel/guardian/raid, damage over time, save stats and HP growth remain unchanged.

## Executed checks

`python Docs/battle_pacing_measure.py` passed. This is a portable formula proxy, not Unity execution or a live-species balance certification.

60 neutral, equal-stat synthetic fixtures: five rarity stat bands × four levels (5/15/30/50) × three skill powers (18/30/45). Skill cooldown 2, player basic fallback, enemy existing null-skill fallback, no IV/outfit/item bonuses or STAB. Deterministic; no random draws.

| Version | Median rounds | Min–max | Round-count distribution |
|---|---:|---:|---|
| Baseline | 3 | 1–4 | 1:1, 2:19, 3:38, 4:2 |
| Wild pacing 0.7 | 4 | 2–5 | 2:1, 3:24, 4:33, 5:2 |

The median meets the 4–6 target in this deliberately neutral fixture matrix. This does not imply every matchup lasts 4–6 rounds. Real species with STAB, status effects, IVs and equipped skills still need runtime distribution measurement.

`python .claude/scripts/ui_layout_lint.py`: PASS, 231 files, 0 violations at execution time.

## Added Unity regression coverage

`BattlePacingTests`: 60-fixture controller matrix; unchanged max HP/duel and guardian damage; player-action snapshots before reply; lethal player hit without invented reply; stunned enemy without invented attack; 1x/2x damage and cooldown equivalence.

Unity test execution and rendered validation are **not claimed**. Root verification found Unity editor license failure (exit 198). Run the project's PlayMode test runner, not EditMode (which discovers zero tests), once the license is available. Check fresh XML and nonzero test count.

## Outstanding runtime acceptance

Fatal enemy hit → faint → swap/result; poison KO; heal/buff/miss text and HP timing; stun/escape; 16:9/4:3/mobile target labels; repeated battles; 1x/2x switching; reduced motion/flashes; Android frame-time comparison. UI and video validation require a real Game view or standalone capture including IMGUI.

## Adversarial review follow-up

Separated the after-enemy-action snapshot from final round state. Poison KO now appears as end-of-round HP loss rather than being counted as the enemy's direct hit. Healing/buffs no longer play the UI hit sound when they deal no damage. Player stun and failed escape do not launch a player attack model animation. Invalid skills retain the current round and snapshots.

Disabled UI ignores delayed impact callbacks, clears stale input, and restores deferred-presentation ownership on enable. Interrupted action completion uses its retained snapshot; an arena cleaned up while hidden is reconstructed from encounter origins. An interrupted faint restarts before the result transition.

Added controller/UI phase regression cases for poison KO snapshots, player stun, invalid skills, deterministic failed escape, healing snapshots and disable/enable resume. Added recovery (30% heal, cooldown 3) and defense (35%, duration 3, cooldown 3) rotations against an equal-stat damaging opponent, with damage fallback and a 20-round stall bound. These NUnit cases are source additions, not claimed executed Unity results.
