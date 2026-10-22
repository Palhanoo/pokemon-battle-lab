# Pokémon Battle Lab

An isolated desktop Unity experiment for Bruno. One flat arena, three Pokémon,
three native model sources, and three small combat-loop comparisons. Created
22 October 2026 after the Rayquaza battle-feel reset.

Double-click **Play Battle Lab.cmd**. No HoennVR save, map, story, or runtime
assemblies are loaded. The application uses its own BrunoLabs/Pokemon Battle Lab
identity and does not write gameplay saves.

## Try this first

1. Open **Model comparison**: Pikachu, Gengar, and Lucario can each be shown with
   their Arceus, Z-A, and BDSP models together. The viewer applies one shared display scale per species; battles keep native size.
   Compare idle, movement, attack,
   ranged attack, damage, and faint clips. Change playback speed to inspect them.
2. Return to battle. Select a rule style and choose the same matchup. The model
   source can be changed separately for each side; every change resets the fight.
3. Compare BDSP-style rounds, an Arceus-inspired action queue (Normal/Agile/Strong),
   and Z-A-inspired real-time move commands. Press **1–3** or click a move.
4. **R** resets. **Esc** pauses. **F1** switches the model viewer. In real time,
   **WASD** moves the trainer marker; the partner follows between commands.
   **Space** dashes the trainer marker. This prototype does not simulate trainer
   damage, invulnerability, or the original trainer animation controller.
5. Disable opponent commands to inspect attacks on a passive target. The passive target
   does not consume turns in the action-order experiment.

## What the labels mean

**These are comparison prototypes, not three complete original game engines.**
Models and clips are native exports from the named local sources. The battle
controllers are new, small reconstructions. No HoennVR battle controller was copied.

| Part | Implemented | Explicit limit |
|---|---|---|
| Models | 3 species × 3 sources, 9 distinct exported assets | Source animations do not imply native state-machine or shader parity |
| BDSP style | simultaneous round choices, priority, Speed, PP, accuracy, types | Shared modern stats/damage baseline; no abilities, items, status, switching or full BDSP rules |
| Arceus style | independent action queue, preview, Agile/Strong power/delay tradeoffs, extra PP cost | Delay/power formulas are experimental; not decoded Arceus formulas, move legality or native damage |
| Z-A style | concurrent actions, individual native cooldowns, imported power/windup/recovery/range, approach and spatial misses | Prototype movement and staging; no original timeline interpreter, homing, native area semantics, trainer damage, Mega, boss AI, status or party switching |
| Effects/audio | visible diagnostic projectile/impact and synthesized hit tick | Not the original games' move VFX/audio |
| Presentation | native clip playback, short crossfade, held one-shots, grounding at posed rest | No reconstructed root-motion/state/locator timeline controller |
| Review | pause, reset with seed 42, speed/zoom controls, event log | Desktop only; VR and headset comfort untested |

The same species stats and deterministic damage baseline are held across modes
so model comparisons do not secretly change matchups. Z-A uses its imported
power and resistance multiplier. The Arceus style factors (.75/1/1.25 power,
.65/1/1.5 action delay) are deliberately visible lab assumptions.

Real-time projectile visuals currently use a fixed release aim with a simplified
sphere/segment contact check. Moves such as Thunderbolt are represented by this
diagnostic path even when the imported source shape is Area. Do not use this
build to judge faithful move choreography. That is the next research gate.

## Rebuild

Unity **6000.6.3f1**, URP **17.6.0**, glTFast **6.20.0**. Open this folder in Unity,
then use **Battle Lab / Set up isolated lab** or **Build Windows review**.
From WSL: `bash Tools/unity.sh setup`, `tests`, or `build`.

Native exports are kept locally and ignored by Git, along with Library and
Builds. The source repository stays small. To reproduce them, use the existing
Pokémon Python environment with numpy, Pillow, UnityPy and ooz:

```
/home/bruno/pokemon/tools/venv/bin/python Tools/export_models.py
```

Source paths are in `Tools/vendor/arceusx/sources.py`; BDSP also supports
`BDSP_ROMFS`. Export only while this project's Unity editor/import/build is
stopped. `Docs/model-manifest.json` records the source and SHA256 of each model;
`Docs/data-provenance.json` separates native data from prototype assumptions.

The offline exporter snapshot contains only model-conversion helpers. The lab
has no runtime dependency on the original checkout. This is a **local Git repo**;
no remote repository has been created or published.

See `Docs/design-plan.md` for the staged design/research plan and `Docs/verification.md`
for actual checks. A build or smoke test does not establish enjoyable combat or
original-game parity.
