# Attack visual review — released 23 October 2026

The reviewed full build is `Builds/AttackVisuals20261023`; `Play Battle Lab.cmd`
now selects it. It includes the existing 1,000 roster model files and all 47 ZA /
22 BDSP attack presentations. No new moves or species were added in this repair.

The reported missing travelling cores, rectangular cards, misplaced camera
layers, oversized spray/backdrop layers and corrupted mesh tint are repaired.
Current contact sheets and selected full timelines are in
`evidence/attack-completion/final-review`; dense travel originals are in
`accepted-travel`. Source shaders were reconstructed from embedded program data,
not replaced with untextured substitute particles.

Verification: all69 capture/binding/model-layer checks passed; changes following
that run were rechecked on the affected effects. Dense 60Hz Flamethrower and Dragon
Pulse runs show the travelling cores and pass one-hit/expiry checks. Opposing
Flamethrowers produce one clash, no damage and no remaining tails. Final shader
checks changed19,845 refraction pixels with zero outside-footprint, zero-strength
or foreground leakage; cutout, culling, soft intersection, primitive masks and
packed mesh colour tests pass. Dig passes with normal full-roster loading,
including underground input, emergence reach, immunity and cleanup. Final build
has zero errors. Release/log identifiers are in `attack-visuals-release.json`.

Visual acceptance is for these reconstructed battle presentations and reported
rendering failures, not a claim of pixel-identical source-game rendering. Original
cinematic camera tracks, transparent-on-transparent refraction, native deformation
and two unidentified Iron Tail material curves remain fidelity limits. Seed Bomb's
remaining dark lighting-ramp shading is authored by its OneMinusAlpha material;
the false blue/purple mesh-colour transport is repaired. No tests establish VR
headset comfort. The prior rejected experiments below are retained as history.

# Earlier checkpoint — superseded by the reviewed release below

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

## Continued repair after Bruno requested completion (23 October)

The generic EFT combiner was not sufficient for the staged ZA assets. Their BNSH
shader sections contain native fragment assembly. The export now records the
program identity and CSDP constants; VioletParticle reconstructs the shared
shape/colour/erosion program and the two Hydro Pump/Psybeam beam programs. Shape,
colour and erosion maps are sampled separately. An initially reversed shape/noise
product was caught during review and corrected before acceptance.

Sparse ZA texture slots were incorrectly promoted into slot zero, where different
channel selectors exposed rectangular cards. Missing slots now stay neutral;
Bullet Punch's contact card no longer renders as a square. Native Fresnel and
BDSP distortion modes/lifetime curves are restored. Close Combat's base layer is
restored with a smaller, translucent presentation envelope for the battle camera.

BDSP mesh diagnosis separated three faults: exported UNorm8 vertex colours were
0..255 instead of 0..1; Unity's CPU particle mesh path interpreted Float32 mesh
COLOR bytes as packed colour; secondary mesh UVs were absent. Primitive masks now
have dedicated streams, mesh colours use Color32, and secondary UVs have a separate
stream. The baked Seed Bomb colour changed from (0,0,.051,.247) to the expected
linear brown (.173,.106,.106,1). A targeted regression checks this transport.
Texture-sheet animation is restricted to UV0 so it cannot overwrite these streams.

BDSP MaterialController stores initial UV offsets in _UvScroll0 and motion in
_UvScroll1, with rotation x=initial, y=random range, z=motion. The old exporter
used offsets as speeds and rotation randomness as speed. The conversion now
separates those values and uses the particle clock for material motion. Reference:
https://github.com/TeamLumi/opendpr/blob/main/Assets/Scripts/Effect/MaterialController.cs
The source was used to verify parameter meaning; its implementation was not copied.

During review, a direct vendor BDSP export accidentally staged its 69-move default
set. That run is rejected as acceptance evidence. The selected 47 ZA / 22 BDSP
pipeline was restored, with a runtime count guard and required model-layer checks.
Full current-build rendered acceptance results follow below; these source repairs
alone do not close the visual gate.

Bruno playtest feedback after the AttackVisuals20261023 release: he played a subset
of attacks and reports they feel substantially better. This is positive manual
feedback for the tested subset, not verification of every species/move pairing.
He asks which fidelity gaps remain and whether to expand the move set. Distinguish
resolved staged asset bindings from the two unmapped Iron Tail material curves,
renderer parity limits, shared physical/special body clips and source-engine scope
(ZA/BDSP effects; other model families currently reuse ZA effects).
