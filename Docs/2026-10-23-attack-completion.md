# Attack completion review — withheld candidate

The public launcher still selects PokedexShaderDetails. AttackCompletionCandidate
is a private diagnostic build, not a completed build for Bruno. No new attacks or
species were added. The visual completion gate remains OPEN.

## Implemented repairs

- Nidoking uses BDSP Sludge Bomb. Preserve its native alpha/depth state and the
  material controller's UV rotation. Its stretched splash uses a 1x8 sheet on a
  horizontal atlas with a 1.57-radian UV rotation; losing that rotation produced
  the reported striped rectangular splashes. Timed captures confirm distinct
  splash shapes after restoration.
- Preserve BDSP per-texture scroll, rotation/speed, scale speed and sphere flags.
  Preserve native primitive channel metadata, including OneMinusR. The attempted
  separate BDSP primitive-channel transport failed visual review and was removed;
  the prior mesh path is retained pending a proper mesh-tint repair.
- Direct Dig accepts camera-relative input during its native hidden interval,
  leaves a soil trail, and emerges at the actual position. Incoming ordinary
  attacks miss buried targets; Dig only lands within emergence reach. Restore
  body grounding/visibility and clear the trail on cancel, faint and reset.
- Restore motion-lived carrier volleys using the local native bullet table and
  attack hold, instead of the carrier's .05-second nominal lifetime. Flamethrower
  emits 11 children at .1 seconds; Dragon Pulse emits 3 at .2 seconds. The same
  source omission affected Water Pulse, Leaf Storm and Disarming Voice. Muzzle
  attachments stay at their native attacker locator. Visual children do not add
  gameplay hits. Repair script is idempotent and part of the export pipeline.
- Preserve independent un-atlased coordinates for ZA secondary texture layers;
  main atlas animation no longer compresses their noise into one frame. Restore
  per-particle UV clocks and source random scroll ranges. Random phase selection
  is a compatibility reconstruction, not the original engine RNG.

## Verification and unresolved visuals

Logs/attack-completion-dig.log records LAB DIG CHECKS PASSED, process exit 0:
direct input, visible soil, pause, incoming immunity, distant miss, nearby hit,
grounded/visible emergence, cancel, faint, AI movement and reset cleanup.

The asset audit resolves all 3,600 texture references and all staged model and
particle references; no unhandled exported particle timeline commands remain.
This says nothing about original-game shader parity or the two unknown Iron Tail
material-property curves. Native Fresnel/distance fade, deformation, BDSP
specialized distortion and transparent-on-transparent refraction remain open.

Dense 60 Hz simulations and captures at .1-second intervals confirm that both
reported travelling cores exist geometrically and emit particles, but the normal
materials suppress too much of their visible coverage. Magenta geometry and
no-secondary-alpha captures are DIAGNOSTICS ONLY. Disabling the secondary masks
reveals the cores; that workaround was not applied to production. Whole-move
contact snapshots had missed this short travel interval and cannot approve it.

The ZA alpha-enum adapter is a compatibility interpretation. BDSP's original
shader exposes formula keywords, but those do not establish ZA's shader math.
Experiments changing erosion direction and UV SRT order did not resolve the visual
failure and were removed. Native frame, texture, channel and mask data remain
intact for further investigation. Do not describe Flamethrower or Dragon Pulse as
visually fixed, and do not release this candidate based on capture/lifecycle PASS.

The first added clash fixture used unequal Pokémon; a repeat with identical
Pokémon also failed (two hits, zero clashes), proving a genuine tracking issue.
Collision selection now excludes carriers/expired tails and explicitly selects
the leading active child rather than relying on dependency-sorted object order.
The focused rerun must verify one clash, zero hits and no remaining tails.

The leading-child rerun passes (Logs/attack-completion-travel.log, exit 0):
one hit per attack, one equal-power Flamethrower clash, zero clash damage,
all action/tail objects expire. Dense screenshots and path traces are preserved
in evidence/attack-completion/travel. The failure was object-order selection;
excluding non-travelling objects alone was not sufficient.

The 69-presentation capture run completed with bindings/particle checks passing
(Logs/attack-completion-final.log, exit 0). Visual review rejects additional
backdrop/coverage cases: ZA Bullet Punch/Mach Punch/Quick Attack expose card
borders; Close Combat's re-enabled backdrop still becomes a large filled dome;
BDSP Close Combat has a rectangular distortion layer. Close Combat restoration
was rejected and its previous omitted-backdrop workaround retained, explicitly
not an all-layers repair. BDSP mesh-tint regression (neon/black seeds and splashes)
was caught; attempted tint preservation also failed visual review. That mesh
transport experiment was rejected and the prior mesh rendering restored.

Pixel checks pass in Logs/attack-completion-shaders.log (exit 0): 19,845 changed
refraction pixels, zero outside/zero-strength/foreground leakage, plus soft
intersection, native cutout/culling and actual particle primitive-channel checks.
These tests do not establish visual acceptance of the 69 attacks.

The actual BDSP mesh-tint rerun still showed false blue/black patches, so it did
not pass visual acceptance. Both mesh-transport changes were reverted; native UV
and alpha-state repairs remain. Rejected images are in rejected-mesh/. Sludge
atlas stripe repair is distinct from the remaining mesh-core appearance issue.
Pixel evidence is copied into attack-completion/shader-checks; earlier tracked
shader-details evidence restored so its prior-build record remains intact.

Final checkpoint build succeeds with zero errors. The eight affected presentation
captures pass runtime/binding checks in Logs/attack-completion-checkpoint.log,
exit 0. Visual review confirms Sludge's striped atlas cards are gone, the neon
mesh experiment is removed, and Close Combat's prior dome workaround is retained.
Seed Bomb's dark meshes and the remaining coverage/distortion issues are still
unresolved. Exporting motion streams twice produces byte-identical move data.
No candidate was handed off and Play Battle Lab.cmd remains unchanged.
