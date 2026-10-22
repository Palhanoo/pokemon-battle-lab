# Pokémon Battle Lab

An isolated desktop Unity experiment for Bruno. One flat arena, nine Pokémon,
15 native bodies from four sources, and three small combat-loop comparisons. Created
22 October 2026 after the Rayquaza battle-feel reset.

Double-click **Play Battle Lab.cmd**. No HoennVR save, map, story, or runtime
assemblies are loaded. The application uses its own BrunoLabs/Pokemon Battle Lab
identity and does not write gameplay saves.

## Try this first

1. Open **Play Battle Lab.cmd**: the expanded real-time review is selected.
2. **WASD** moves your Pokémon. **Shift** walks, **hold right mouse** rotates the
   camera, **mouse wheel** zooms, **Space** dashes between attacks.
3. **1–4** attacks the single opponent. Get close for melee attacks. Body movement
   is committed until recovery; effects can finish trailing after control returns.
4. **Tab** opens Setup: choose Pokémon and model source. Click an equipped move
   to cycle through six choices, keeping four equipped. Battles start with active
   AI and finite HP. For animation study, enable Practice and disable opponent
   commands. Sound volume is below the move slots.
5. **R** resets, **Esc** pauses, **F1** opens the model viewer. The
   earlier BDSP/Arceus rule comparisons are in Setup.

Original player: **Play Original Lab.cmd**. New player:
`Builds/RealtimeRoster/PokemonBattleLab.exe`. **Play Animation Study.cmd** keeps
the earlier direct-control build. All use in-memory fights, no saves.

## Current roster

| Pokémon | Available bodies | Six move choices |
|---|---|---|
| Pikachu | Arceus, ZA, BDSP | Quick Attack, Thunderbolt, Iron Tail, Thunder, Spark, Discharge |
| Gengar | Arceus, ZA, BDSP | Shadow Ball, Dark Pulse, Sludge Bomb, Lick, Psychic, Thunderbolt |
| Lucario | Arceus, ZA, BDSP | Quick Attack, Aura Sphere, Metal Claw, Dragon Pulse, Bullet Punch, Close Combat |
| Treecko | ZA | Quick Attack, Leaf Blade, Energy Ball, Leaf Storm, Absorb, Giga Drain |
| Torchic | ZA | Slash, Ember, Flamethrower, Peck, Fire Spin, Aerial Ace |
| Mudkip | ZA | Tackle, Water Gun, Water Pulse, Mud Shot, Rock Throw, Hydro Pump |
| Ralts | ZA | Confusion, Psybeam, Psychic, Disarming Voice, Draining Kiss, Shadow Ball |
| Poochyena | SV | Tackle, Bite, Crunch, Dig, Take Down, Play Rough |
| Breloom | BDSP | Mach Punch, Brick Break, Seed Bomb, Giga Drain, Rock Tomb, Drain Punch |

These are experimental test loadouts, not a claim of original-game move legality.

## What the labels mean

**These are comparison prototypes, not three complete original game engines.**
Models and clips are native exports from the named local sources. The battle
controllers are new, small reconstructions. No HoennVR battle controller was copied.

| Part | Implemented | Explicit limit |
|---|---|---|
| Models | 9 species, 15 distinct assets, 1,011 clips | Source animations do not imply native state-machine or shader parity |
| BDSP style | simultaneous round choices, priority, Speed, PP, accuracy, types | Shared modern stats/damage baseline; no abilities, items, status, switching or full BDSP rules |
| Arceus style | independent action queue, preview, Agile/Strong power/delay tradeoffs, extra PP cost | Delay/power formulas are experimental; not decoded Arceus formulas, move legality or native damage |
| Z-A rhythm / direct control | camera-relative Pokémon movement, walk/run/dash, native cooldowns, imported body cues and effect clocks | Experimental movement, dash and range-at-hit collision; no full native hitboxes, homing, root motion, Mega or boss state machine |
| Attack presentation | 47 native ZA move effects, 22 BDSP sequences; split start/loop/end body cues and reaction recovery | Arceus/SV bodies use ZA effects/timing; converted renderers and incomplete native state/motion variants |
| Sound | Emerald recordings mapped to all 47 moves; eight species cries; volume/pause/reset | Newer moves use documented type-family substitutes; no Lucario cry or authentic modern audio |
| Review | pause, reset with seed 42, speed/zoom controls, event log | Desktop only; VR and headset comfort untested |

The same species stats and deterministic damage baseline are held across modes
so model comparisons do not secretly change matchups. Z-A uses its imported
power and resistance multiplier. The Arceus style factors (.75/1/1.25 power,
.65/1/1.5 action delay) are deliberately visible lab assumptions.

Attacks play imported native particle sequences, with damage resolved once at the
presentation's hit marker. Real-time contact is a simplified range check and a
lab dash window; visual projectile paths are not authoritative hitboxes. Drain,
status, multi-hit and other secondary move behavior are not implemented. Native
motion/state-machine and shader parity remain open research gates.

## Rebuild

Unity **6000.6.3f1**, URP **17.6.0**, glTFast **6.20.0**. Open this folder in Unity,
then use **Battle Lab / Set up isolated lab** or **Build Windows review**.
From WSL: `bash Tools/unity.sh setup`, `tests`, or `build`.

Native exports are kept locally and ignored by Git, along with Library and
Builds. The source repository stays small. To reproduce them, use the existing
Pokémon Python environment with numpy, Pillow, UnityPy and ooz:

```
/home/bruno/pokemon/tools/venv/bin/python Tools/export_models.py
/home/bruno/pokemon/tools/venv/bin/python Tools/export_attack_effects.py --source ../HoennVR
python3 Tools/export_audio.py
```

For a separate verification package, use `bash Tools/unity.sh build -lab-build-dir
Builds/RealtimeRosterVerification`. Never rebuild over a player Bruno is using.

Source paths are in `Tools/vendor/arceusx/sources.py`; BDSP also supports
`BDSP_ROMFS`. Export only while this project's Unity editor/import/build is
stopped. `Docs/model-manifest.json` records the source and SHA256 of each model;
`Docs/data-provenance.json` separates native data from prototype assumptions.

The offline model exporter snapshot and isolated native presentation helpers are included.
`Docs/native-fx-manifest.json` records the selected local effect exports.
See `Docs/2026-10-22-direct-control.md` for controls, provenance and remaining limits. The lab
has no runtime dependency on the original checkout. This is a **local Git repo**;
no remote repository has been created or published.

See `Docs/design-plan.md` for the staged design/research plan and `Docs/verification.md`
for actual checks. A build or smoke test does not establish enjoyable combat or
original-game parity.
