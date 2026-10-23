# Pokémon Battle Lab

An isolated desktop Unity battle experiment for Bruno: **1,000 species**, one
preferred model per species, direct Pokémon control, and 1v1 or 3v3 battles.
No HoennVR maps, story, runtime assemblies or gameplay saves are loaded.

Double-click **Play Battle Lab.cmd** to open `Builds/PokedexEffectRepair`.
The previous `Builds/RealtimeRoster` player remains available through
**Play Previous Roster.cmd**. Historical source-comparison models stay in the
verification assets; the playable Pokédex shows one body per species.

## Controls and testing

- WASD moves the Pokémon; Shift walks; Space dashes; hold right mouse to look;
  mouse wheel zooms. 1–4 use the equipped attacks.
- Short-range commands approach automatically. WASD cancels approach without
  spending cooldown, or exits recovery shortly after contact.
- Q switches living allies. E changes the enemy target. Other Pokémon use AI.
- Tab opens setup. Click any team slot to search by name or Pokédex number.
  Choose 1v1/3v3 and click move slots to cycle six choices. `*` marks a test-only
  assignment absent from the pooled historical learnsets.
- R resets; Esc pauses; F1 opens the body animation viewer. F2 opens live attack
  layers: pause an attack, then toggle layers to isolate a texture or emitter.
- Enable Practice and disable AI for repeatable attacks. Sound volume is below
  the move slots. The clash drill uses your current Pokémon and matching equipped move slots; any opposing projectiles can collide. Resetting or changing species exits the drill.

Opposing projectiles can clash along their travel paths. Compare damage after
STAB, target type and defenses: a move at least 50% stronger continues with the
**difference**; otherwise both cancel. 150 vs 100 leaves 50; 149 vs 100 cancels both.
This is a lab experiment, not an original-game rule.
Successful damaging hits now trigger a compact contact burst, visual recoil, a
contact sound and a brief desktop hit-stop. Close Combat’s unsupported flat backdrop is omitted and broad glow
layers are reduced for readability; attack ranges are unchanged.

## Preferred roster

| Source preference | Selected species |
|---|---:|
| Legends: Z-A |364|
| Scarlet/Violet |478|
| Legends: Arceus |37|
| Brilliant Diamond/Shining Pearl |61|
| Sword/Shield |60|

The highest available, validated source wins. Native resource/personal tables map
model IDs to species; filenames alone are unreliable. Only current-team models
are streamed into the roster cache. See `Docs/pokedex-ready.json` for each body,
source, hash, clip roles and move choices; `Docs/pokedex-validation.json` lists
25 unavailable species and seven species with explicit clip-role fallbacks.

The roster includes every sourced Hoenn species. Six choices per species reuse
**47 existing test moves**, with four equipped. This is not every Pokémon move,
all alternate forms, or a complete learnset/ability/status simulation. Some bodies
reuse a native special clip for physical attacks; four use the damage pose when
no exported faint clip exists. These are marked in the provenance, not presented
as authentic missing animations.

The renderer now binds third texture masks and UV motion, clears stopped infinite
Aura Sphere cores, and handles multiplicative transparency. The audit found
additional native gaps: BDSP Brick Break/Iron Tail/Lick model layers are missing;
118 native lighting lookups are unsupported. Fire, water and psychic appearance
still need manual review. Passing lifecycle checks does not establish fidelity.

## What the labels mean

**These are comparison prototypes, not three complete original game engines.**
Models and clips are native exports from the named local sources. The battle
controllers are new, small reconstructions. No HoennVR battle controller was copied.

| Part | Implemented | Explicit limit |
|---|---|---|
| Models | 1,000 species, one preferred body each, 52,807 clips | Source animations do not imply native state-machine or shader parity |
| BDSP style | simultaneous round choices, priority, Speed, PP, accuracy, types | Shared modern stats/damage baseline; no abilities, items, status or full BDSP rules |
| Arceus style | independent action queue, preview, Agile/Strong power/delay tradeoffs, extra PP cost | Delay/power formulas are experimental; not decoded Arceus formulas, move legality or native damage |
| Z-A rhythm / direct control | camera-relative Pokémon movement, walk/run/dash, native cooldowns, imported body cues and effect clocks | Experimental movement, dash and range-at-hit collision; no full native hitboxes, homing, root motion, Mega or boss state machine |
| Attack presentation | 47 native ZA move effects, 22 BDSP sequences; split start/loop/end body cues and reaction recovery | Arceus/SV/Sword bodies use ZA effects/timing; BDSP falls back to ZA when a native sequence is not staged; converted renderers and incomplete native state/motion variants |
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
/home/bruno/pokemon/tools/venv/bin/python Tools/pokedex.py --export --workers 1
python3 Tools/validate_roster.py
```

For a separate verification package, use `bash Tools/unity.sh build -lab-build-dir
Builds/PokedexVerificationNew`. Never rebuild over a player Bruno is using.

The large roster is exported into `LocalRoster`, outside Unity Assets. The build
copies it into StreamingAssets and keeps unchanged files on incremental rebuilds.
`pokedex.py` normalizes duplicate timestamps and audits/rebakes native colour layers automatically. Exported models,
textures and clips stay local and ignored by Git.

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

Latest effect review: shared sphere-mask/UV/blend repairs; duplicate animation-name
selection and posed grounding; model-sized spawn/approach spacing; Hydro Pump beam
fit; cooldown flanking for AI; short Flying SFX; visible collision ring/audio.
There are47 distinct test moves (four equipped from a small candidate list per
species), not complete species learnsets. The69 staged presentations cover47 ZA
and22 BDSP variants. Other model sources currently share those effect libraries.
See Docs/effect-layer-audit.json for remaining missing BDSP models and unsupported
refraction/deformation, and Docs/evidence/effect-repair for captures/checks.
