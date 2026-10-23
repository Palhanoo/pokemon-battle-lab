# Pokémon Battle Lab

An isolated desktop Unity battle experiment: 1,000 species with one preferred
model per species, direct Pokémon control, independent simultaneous teams, and native ZA/SV/BDSP effects.
It loads no HoennVR maps, story or gameplay saves.

The reviewed six-move checkpoint is commit `be0a9d0` on `main`.
Double-click **Play Battle Lab.cmd** for the current reviewed local build.
Builds, exported models, audio and native effect assets remain local and are
ignored by Git. This repository contains source, staging tools and review records;
cloning it alone does not supply the native game assets or compiled player.

## Current focused battle iteration

- WASD moves; hold RMB to rotate the camera; Shift walks; Space dashes.
- 1–4 command attacks on the focused enemy, with automatic approach.
- Setup independently selects **1–6 allies against 1–12 enemies**, with a
  scrollable roster for each side. All selected members fight simultaneously.
  The BDSP/Arceus turn comparisons remain duels.
- Click an equipped move to search by name or type. Choose from the species pool
  or enable explicitly marked **All lab moves** test overrides. Selecting an
  already-equipped move swaps slots. Four moves remain equipped.
- **C switches Classic / RPG / Shoulder views**; scroll zooms. E changes focus,
  Q changes allies. Camera anchors remain stable while bodies animate.
- Hold F to rise briefly on eligible flying/levitating Pokémon; release descends.
  Rotom has a lower ascent. The flight stamina bar shows the remaining three-second
  reserve, turns amber when low, and displays recovery after landing and releasing F.
- M starts Mega Evolution with native ZA charge/reveal particles and the Mega
  body's appeal animation. **Dynamax has been removed.**
- Tab opens Setup. Esc pauses; R resets. F1 opens the model viewer;
  F2 opens the effect-layer inspector.
- Setup selects **Auto / ZA / BDSP effects independently of the model**, camera,
  Earthquake radius, teams, loadouts and alternate Mega forms.
- Manual aiming is optional and experimental; T marks an enemy and LMB repeats
  the selected slot. Beams still resolve one hit per cast. Hold-to-steer/DPS
  channels are deferred until the camera/control foundation is accepted.
- Floating damage, focus HP bars and HIT confirmation distinguish actual hits.
- The arena is 100×100m. The blue water lane gives grounded Water types a
  1.65× movement bonus.

The catalog contains **487 damage moves**: 249 ZA, 225 SV and 13 BDSP native
primary presentations, plus 29 alternate BDSP variants. The original53 reviewed moves
remain available. Species pools include all available moves recorded in the
pinned pooled learnsets; test overrides are labeled. BDSP selection falls back
to the available ZA/SV presentation when there is no BDSP version.

This is not the complete move dex or complete competitive mechanics. Status,
fixed-damage and missing-presentation moves remain coverage gaps. Secondary
effects, abilities and specialized rules are not fully simulated. Multi-hit
animations currently use one combined damage event. See
`Docs/2026-10-24-attack-coverage.md` and `Docs/attack-coverage.json` for exact scope
and verification; native asset presence alone does not establish visual quality.

Ranged clashes compare damage after STAB, target typing and defenses. At least
1.5× damage wins and continues with the difference; otherwise both cancel.
Ground fields and melee are outside ranged clashes. The native effects are
adapted to the arena; source-renderer parity is not claimed.

The preferred roster order is ZA → Scarlet/Violet → Arceus → BDSP → Sword/Shield.
96 additional standard Mega form models can be staged from existing local ZA
exports; native form stats/types apply. Mega uses original ZA particles/body clips
with lab timing; exact cinematic/shader parity and ability behavior remain outside
this prototype. See [iteration notes](Docs/2026-10-23-battle-focus.md) for scope and limits.

## Build and asset staging

Unity **6000.6.3f1**, URP **17.6.0**, glTFast **6.20.0**.
From WSL: `bash Tools/unity.sh setup`, `tests`, or `build`.
Use a new output directory while a player is running:

```sh
bash Tools/unity.sh build -lab-build-dir Builds/MyReview
```

Staging tools use the configured local source archives/exports and Python with
numpy, Pillow, UnityPy and ooz. Source paths are in
`Tools/vendor/arceusx/sources.py`; BDSP also supports `BDSP_ROMFS`.

```sh
python Tools/export_models.py
python Tools/export_attack_effects.py --source ../HoennVR
python Tools/export_audio.py
python Tools/pokedex.py --export --workers 1
python Tools/stage_hoenn_roster.py
python Tools/stage_hoenn_effects.py
python Tools/export_remaining_sv_attacks.py
python Tools/expand_attack_catalog.py
python Tools/stage_bdsp_legacy_attacks.py
python Tools/expand_attack_catalog.py
python Tools/export_audio.py
python Tools/audit_effects.py
python Tools/stage_forms.py
python Tools/validate_roster.py
```

Do not export into Assets during a Unity import/build. The builder stages
LocalRoster and LocalForms into StreamingAssets; runtime loads only needed bodies.

See `Docs/bug-register.md` for user reports and rejected approaches,
`Docs/2026-10-23-hoenn-moves.md` for the stable checkpoint, and the iteration notes
for current verification. Compilation alone does not establish visual quality
or enjoyable combat. This lab is desktop-only; VR comfort has not been tested.
