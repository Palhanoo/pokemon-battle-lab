# Battle lab feedback and regressions

## LAB-01 — Arceus Pikachu sometimes appears stuck

Reported by Bruno on 22 October 2026 against initial lab commit 5453b54 / original
Builds/PokemonBattleLab.exe. Evidence: user report in chat, no capture supplied.
The core is visible, but animation continuity is not accepted.

Confirmed source defect: the old player selected a single generic attack/special
clip and held its final frame. Arceus Pikachu's special is rangeattack01_start;
its matching loop/end were never scheduled. Its damage01_start also had no
independent recovery timer. These defects plausibly explain the report; the exact
moment Bruno saw is not known.

Repair: LabActor.React/Play, AttackPresentation.SplitPlan and the native timeline
adapter schedule recovery, and direct input owns movement after release. Body
bounds cache only model renderers, excluding attached effect overlays. Reset
cancels both active presentations and remaining effect tails.

Recurrence check: play all three attacks with Arceus Pikachu, receive a hit, pause
mid-action, reset mid-action, then move immediately after recovery. Repeat with
all nine bodies. Focused regression and standalone automated checks are included;
results will be recorded in verification.md. Status: source repaired, 20/20 focused checks and 27 standalone attack cases passed; Bruno's subjective acceptance remains open.

## LAB-02 — trainer commands and diagnostic spheres do not test Pokémon feel

Reported by Bruno on 22 October 2026. He explicitly confirms direct Pokémon
control. Evidence: user feedback that body/attack animations and feeling like the
Pokémon are central to this experiment.

Known cause: initial ZA loop controlled a trainer marker, then moved a follower;
attack effects were generic spheres. The initial build did not include native
attack particle timelines despite containing native body clips.

Repair: default ZA direct-control view; camera-relative WASD, hold RMB camera,
Shift walking, Space dash, 1–3 attacks. Eight distinct exported ZA move effects and
eight corresponding BDSP effects are now staged, selected by body source. Native
body cues/particle clocks run together. Arceus bodies use ZA effect/timing data,
explicitly labeled in the UI. Traditional rhythms remain in Setup.

Recurrence check: verify real input moves the Pokémon itself; all 27 loadout/source
combinations show live particles, release their body state, accept movement after
recovery, and clean up on reset. Practice target is default, with optional AI and
finite HP. Status: source implemented, built and automated lifecycle verified, visual issues remain. This is
an experimental direct-control loop, not a recreation of three original engines.

## LAB-03 — Aura Sphere travelling effect expands into a large cyan shape

Reported by Bruno on 22 October during the direct-control review. Evidence:
evidence/direct-control/ZA-LUCARIO-AURA_SPHERE-impact.png shows a tall cyan shape
extending beyond the frame. The effect lifecycle passes; visual fidelity does
not. Cause is still under investigation: inspect emitter mesh/billboard/scale
conversion and projectile placement before changing global sizes. Repair location:
Native/VioletFx.cs, VioletMove.cs and selected Aura Sphere export. Recurrence:
watch charge, release, travel and contact at short and long ranges; the sphere
and trail must remain readable without a giant vertical shape.

Bruno also requests more attacks, available Hoenn species, native sound effects,
and a real-time battle for every model source. Body animation source and battle
rhythm remain independent. His preference for mixed generations is established;
no lore or map work is requested.
