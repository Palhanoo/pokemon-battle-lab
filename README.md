# Pokémon Battle Lab

An isolated desktop Unity battle experiment: 1,000 species with one preferred
model per species, direct Pokémon control, 1v1/3v3, and native ZA/BDSP attack effects.
It loads no HoennVR maps, story or gameplay saves.

The reviewed six-move checkpoint is commit `be0a9d0` on `main`.
Double-click **Play Battle Lab.cmd** for the current reviewed local build.
Builds, exported models, audio and native effect assets remain local and are
ignored by Git. This repository contains source, staging tools and review records;
cloning it alone does not supply the native game assets or compiled player.

## Current focused battle iteration

- WASD moves; hold RMB to rotate the camera; Shift walks; Space dashes.
- 1–4 command attacks on the focused enemy, with automatic approach.
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

There are 53 distinct test moves, four equipped per Pokémon: 53 ZA and 28 BDSP
native presentations. The Hoenn tryout buttons equip Blaze Kick, Muddy Water,
Ice Beam, Dragon Claw, Rock Slide or Earthquake on representative species.
This is a curated move set, not complete learnsets/status/ability simulation.

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
python Tools/stage_forms.py
python Tools/validate_roster.py
```

Do not export into Assets during a Unity import/build. The builder stages
LocalRoster and LocalForms into StreamingAssets; runtime loads only needed bodies.

See `Docs/bug-register.md` for user reports and rejected approaches,
`Docs/2026-10-23-hoenn-moves.md` for the stable checkpoint, and the iteration notes
for current verification. Compilation alone does not establish visual quality
or enjoyable combat. This lab is desktop-only; VR comfort has not been tested.
