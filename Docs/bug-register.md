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

LAB-03 investigation: `ew0396_bullet` emitter `allowhead_02` has only a slot-2
texture (`cpt_3_blur0202s`). The importer only considered slots 0/1, producing an
unmasked cyan primitive. Slot-2 fallback and UV rotation/scroll are now connected.
The elongated plate-XZ primitive also lacked its plane-basis rotation, leaving its
long axis upright. Source repair and focused mask/basis checks pass; the new
standalone visual check is pending. This supersedes the earlier vague size-only
hypothesis; no global scale reduction or extra covering geometry was used.

## LAB-04 — expanded real-time roster, moves and sound

22 October request: Bruno wants active real-time battles regardless of the body
model's original game's rules, more attacks, familiar Hoenn Pokémon, and sound.
The first expansion stages Treecko/Torchic/Mudkip/Ralts (ZA), Poochyena (SV), and
Breloom (BDSP), retaining their actual source labels. Fifteen bodies across nine
species, six move choices each, four equipped slots, 47 distinct ZA effect rows
and 22 BDSP sequences. Audio uses original Emerald move recordings and eight
cries; missing exact newer-move sounds use documented type-family substitutes.
Lucario has no staged Emerald cry. No new lore/map/progression work.

Repair locations: roster/seed data, dynamic catalog/source selection and loadout
UI in BattleLabApp, LabAudio/Importer and export scripts. Recurrence: all model
sources can move and fight in real time; slot four and both unequipped choices can
be selected and used; attacks emit their audio cue, recover, and reset cleanly.
Actual movement/hitboxes, secondaries, authentic modern audio and native state
machines remain approximations, distinct from original body/particle assets.

LAB-03 visual follow-up: offscreen wind-up/contact renders for Arceus and ZA
Lucario in evidence/realtime-roster no longer show the towering cyan primitive.
The source repair is built and lifecycle verified. Continuous travel, exact
native fidelity and Bruno's visual acceptance remain open; stills cannot close
those checks. His launched review build includes this repair.

LAB-04 verification: all 90 source/move presentations pass in the separate
verification player: exactly one resolved hit, sound-cue dispatch, live native
particles, recovery to idle and restored movement. All fifteen bodies pass
movement/idle/dash and reset cleanup. Audio samples are verified non-silent;
speaker output/mix quality remains for manual listening. See verification.md.

## LAB-05 — remaining converted effect rendering artifacts

Observed during 22 October expanded-roster screenshot review, in both the
RealtimeRoster gameplay sources and its separate verification build. Evidence:
`evidence/realtime-roster/BDSP-BRELOOM-MACH_PUNCH-impact.png` shows dark rectangular
streaks and striped cards around contact; `Arceus-GENGAR-SHADOW_BALL-impact.png`
shows a visible rectangular effect edge. These cases passed lifecycle checks
and are explicitly not visually accepted.

Known source facts: BDSP Mach Punch contains speedline_black emitters using
Zero/SrcColor multiplicative blending, while the converted shared shader computes
alpha independently of its colour output. Whether missing alpha-to-white blending,
texture mask channels or another exported shader parameter causes the observed
rectangles remains a hypothesis. Shadow Ball requires a separate mask/material
inspection; no root cause established. Repair locations: Native/BdspFx.cs,
Native/VioletFx.cs, VioletParticle.shader and their source export metadata.

Status: open, captured and disclosed during Bruno's playtest. Recurrence check:
repeat wind-up, contact and recovery at several camera angles; effect shapes
should not expose opaque rectangle borders. Preserve the original mask and
blend behavior rather than hide cards with new covering geometry or blanket
scale changes. Do not close based on live-particle counts or build success.

## LAB-06 — Treecko colour mismatch

Reported by Bruno against RealtimeRoster (dead156), 22 October. Screenshot
ZA-TREECKO-LEAF_BLADE.png also shows a brown/red head region. Original ZA
body_b material declares BaseColorLayer1 red with alpha=0; the exporter applied
RGB without layer opacity. Repair: MaterialBaker opacity factor and Treecko
re-export from the original ZA pack, with refreshed hash. Recurrence: inspect
idle, movement and every attack before/after rim effects; no spurious disabled
red layer. Source repaired; new render verification pending.

## LAB-03 reopened — Aura Sphere remains after hit

Bruno reports the effect remains onscreen after contact in dead156. Known cause:
infinite native cores use a 600-second placeholder lifetime; Particle/Stop only
disabled emission so existing cores stayed until disposal of the whole effect.
Repair: explicit infinite-emitter marker and clearing those cores on native Stop;
finite sparks still age naturally. Check charge disappears at launch, projectile
core at contact, finite tails finish, pause/reset remain coherent. New test pending.

## LAB-07 — short-range commands reject instead of approaching; combat feels stuck

Bruno requests automatic approach before an out-of-range attack, plus simultaneous
allied/enemy teams. The direct controller explicitly rejected distant commands
and skipped the existing AI approach phase. Repair: all attacks can approach;
manual input cancels approach without spending cooldown, movement/dash can leave
post-hit recovery after .18s; cooldown starts on actual wind-up. New 3v3 mode
uses living team/target selection and individual action/cooldown state, Q ally /
E target. Recurrence: distant melee, moving target, cancelling approach, switching
while another ally attacks, faint/retarget, team victory and six-actor reset.

## LAB-08 — fire, water and psychic effects look flat or mismatched

Bruno reports Flamethrower/fire, water, then psychic effects in dead156. Evidence:
prior fire/water captures and user report. Confirmed gap: third non-sphere texture
slot silently discarded when slot0/1 exists; many fire/water/psychic emitters also
have animated UV scale/rotation. HDR colours above 1 clipped without tone mapping.
Repair: third mask binding, per-layer UV motion, neutral HDR tone mapping, and
multiplicative alpha-to-white output. Complete native combiners/refraction remain
unproven. Audit every staged ZA and BDSP move, every layer binding, body/source,
start/travel/impact/cleanup. Arceus/SV reuse ZA effects and must be labeled.
Bruno asks to focus entirely on fight quality before returning to the main game.

## LAB-09 — ranged attack clashes experiment

Bruno requests physical intersection of opposing ranged attacks, cancellation
unless one is at least 50% stronger, and only the winning difference reaching
the target. Implement post-STAB/type/defense damage comparison: 150 vs100 leaves
50;149 vs100 cancels both. Swept simultaneous projectile paths prevent tunneling;
only enemy projectiles clash, once per pair. Visual burst, cancelled future
impact effects/damage, surviving damage budget, and a fire/water drill. Check
threshold, immunity, different crossing times, allies, chained clashes and reset.
This is an explicitly experimental combat rule, not a native game mechanic.

## LAB-10 — one newest usable model per species; large roster streaming

Bruno requests the maximum locally available roster, one body per species,
prefer the latest game: ZA > SV > Arceus > BDSP > Sword. No additional maps/story.
Inventory joins actual resource catalogs/personal tables to National Dex IDs;
model filenames are NOT National Dex identifiers (Froakie is model0700/dex656).
Initial full conversion recovered 1,000 species;25 have no sourced model in the
checked local game files. Catalog/provenance: pokedex-inventory.json and pokedex-ready.json.
Six choices use the existing47 test moves; pooled historical learnsets and
supplemental test-only slots are explicitly recorded. This is not a full move,
ability, status or form implementation. Missing native attack/faint/locomotion
roles can alias another real clip; roleAliases records every such fallback.

Known new loading defect, found by standalone testing: runtime Mecanim curve
construction rejected SetCurve; simply changing a populated legacy clip's flag
also did not animate bones. Repair: retain legacy curves and manually sample
Animation states on the battle clock, with blending; editor-imported models keep
PlayableGraph playback. First15-model 'pass' was invalid because it only checked
clip names. Strengthened test checks real transform changes and runtime errors.
Source tracks with duplicate timestamps violate glTF; normalization keeps the
last authored value per time, retaining interpolation and cubic tangents.
Locations: PokedexRoster, BattleLabPokedex, LabActor, Tools/pokedex.py,
normalize_roster.py and validate_roster.py. Recurrence: load every selected body,
check actual motion, bounds, textures and roles; reset/swap while effects run;
ensure only current-team models stay resident. Verify large species camera and
spacing manually. A source export or catalog entry alone is not visual acceptance.

## LAB-11 — BDSP model layers absent from three attack reconstructions

Found by the all-attack reference audit,22 October. Existing particle-binding
checks pass, but ModelCreate references for Brick Break's hand/tail, Iron Tail's
whip and Lick's tongue have no staged model layer. Original bundles exist under
BDSP Effects/effmodel. The existing exporter only reconstructs particle prefabs;
its ModelCreate holder stays empty. Known cause, not a texture-mask hypothesis.
Repair locations: Tools/vendor/bdspfx.py, Native/BdspMove and a future native model
layer loader. Status: open; do not mark those effects visually complete.
Concrete recurrence: audit ModelCreate and ParticleCreate separately; compare
frames containing hand/tail/whip/tongue geometry and verify animated deformation,
attachment, expiry and reset. Full audit: effect-layer-audit.json (3,500 texture
references,118 native lighting lookups unsupported by the reconstructed shader).

LAB-03/07/09 verification update:40/40 EditMode checks pass. Isolated standalone
team test passes approach/cancel, ally/target switching, faint handoff, a finite
3v3 fight (21 hits), reset/no-late-damage, equal projectiles cancelling and20vs9
resolving11 residual damage. All90 prior body/move presentations pass lifecycle,
audio-cue and return-to-control checks, plus all three battle modes finish/reset.
Evidence: evidence/team-battle. Fire/water/psychic native shader fidelity and
Bruno's combat-feel acceptance remain open despite those checks.

LAB-06 material follow-up: the first opacity repair did not fix Treecko's head.
The streamed Leaf Blade capture still showed brown skin. Native body_a mask is
[R=1,G=1,B=0,A=0] over the skin; red layer1 belongs to the mouth and green layer2
must replace it over skin. Multiplying accumulated tint produced brown. Shared
MaterialBaker now blends each layer against original albedo, respecting opacity.
The native mouth colour is retained; no species-specific tint is introduced.
Tools/rebake_materials.py audits ZA/SV/Arceus bodies and rebakes overlapping masks,
proving every geometry/skin/animation buffer byte is unchanged. Independent BDSP
and Sword converters do not use this mask implementation. Three material tests
cover overlapping layers, disabled opacity and a single tint. Visual comparison
and final hash/resource audit are required before closing this repair.

LAB-10 verification correction: Ferroseed's native idle has eight channels with
one key each, a valid static pose. The sweep's positive-duration assertion was
incorrect; accept that pose while still requiring actual transform changes
across its action roles. Earlier successful per-species results remain valid;
resume597–749 after fixing the fixture. Counts and exact test ranges are recorded
in the final verification report, including the deliberately stopped overlapping
first sweep. Do not infer an uninterrupted1,000-case test pass from those runs.


LAB-06 final material verification: shared material audit completed for879 bodies;
830 rebakes preserve all geometry/skin/animation buffers. The new isolated
PokedexBattleReady player loads and animates Treecko successfully (exit0), and
its rendered head is visibly green. Retained before/after images:
evidence/materials/treecko-before.png and treecko-after.png. Mark the brown-head
cause source-repaired, built and visually spot-checked; broader material/native
shader fidelity and Bruno's real-input acceptance remain open.
LAB-10 final coverage: all1,000 species have successful per-species runtime checks
across the documented overlapping/resumed runs. evidence/pokedex/coverage.json
records exact ranges and the corrected Ferroseed fixture. Both90-case attack
suites pass. Final package stages all1,000 preferred bodies;25 remain unavailable.

## LAB-12 — streamed clash drill exception and stale matchup labels

Bruno reports NullReferenceException clicking the fire/water drill in
PokedexBattleReady, then incorrect repeated Dark Pulse/Water Gun labels.
Logs/bruno-pokedex-review.log identifies StartClashDrill line99: ResetBattle
returns while streamed imports are pending, leaving fighters null. Drill state
also survives species changes and old clash expiry survives clock resets.
Repair: await loading before positioning, use current species/matching equipped
slots, visible stop toggle, clear drill/message on ordinary resets. Clashes remain
type-independent for opposing projectiles, using actual post-STAB/type damage.
Recurrence: uncached species -> drill -> change matchup/reset -> no stale label,
no unsolicited opponent attack. Focused streamed smoke covers async setup/reset.

## LAB-13 — eyes, held locomotion, camera and lingering/absent effect layers

Bruno reports Charizard eye artifacts, Iron Jugulis missing eyes/idle, Arcanine
held paws during movement, poor POV, missing thunder/Dragon Pulse detail,
underground attacks and long-lived Fire Spin in PokedexBattleReady.
Confirmed material causes: fallback highlight replaced `_alb` in a filename
already ending `_msk`, sampling Charizard's white base as an all-white highlight;
Iron Jugulis eyes require emissive layers absent from MaterialBaker. Correct
explicit highlight binding and export masked emissive colour. Only those two
models rebaked in this quick repair; geometry/animation bytes preserved.
Animation hypothesis: clamped legacy state disabled after transitions or skinned
bounds culling. Reassert active loop sampling each frame and update skin offscreen;
verify actual late-loop transform changes for both reported species. Earlier
all-roster smoke only proved changes across roles, not sustained idle/run loops.
Camera now backs off for large bodies and aims ahead with a lower initial pitch.
Confirmed missing Dragon Pulse layer attachment: BulletWeakHandle wasn't resolved,
so its muzzle stayed at world origin. Resolve after the projectile advances.
This is a specific source repair, not proof every underground effect is fixed.
Fire Spin's native field ends at13s; lab has single-hit rules, so its presentation
now stops .8s after contact and expires .5s later. Native field behavior and
thunder/fire/water shader/post-effect parity remain open. Focused recurrence:
uncached drill, sustained idle/run, four named effect lifecycles and eye captures.
Bruno requests a quick repair and explicitly no full-suite rerun.

LAB-13 render follow-up: Charizard's corrected eye is visible in the frontal
player capture. Iron Jugulis emissive texture export alone still rendered dark;
runtime shader-variant coverage is a hypothesis, not proven. Preserve the same
native LED mask/colours in a lit eye fallback as well as emissive data. No geometry
or bone edits. Final capture and focused check are repeated only for this repair.

Final visual result: Iron Jugulis eyes remain dark even with the lit-texture
fallback; that attempted fallback was removed. Emissive layer export is retained
as a confirmed missing source feature, but the visible eye defect is OPEN.
Atlas UV/material addressing or overlapping geometry requires further diagnosis;
shader-variant stripping is not established. Do not claim Iron Jugulis eyes fixed.
Charizard's eye repair is visually confirmed. Sustained animation checks pass.

## LAB-14 — run pose survives stopping; obscuring effects and weak contact

Bruno reopens Arcanine's raised paw after stopping in PokedexReviewFixes: attack
or damage resets it. Prior sustained-loop checks missed the transition defect.
Source evidence: run01_loop keys shoulder and spine transforms absent from
battlewait01_loop (15 additional channels). A legacy sparse clip retains those
channels from the previous pose. Restore imported rest transforms before sampling
the active blend. Recurrence compares every local bone after actual run->stop to
a fresh idle actor at the same time, rather than checking clip names or motion.

Bruno reports Close Combat's large circle hiding both bodies, weak/no contact
sensation, and oversized/incomplete Ember, Brick Break and Dragon Pulse layers.
Close Combat exports a3-unit base circle growing1.5x plus large wind/trail/warp
layers; reduce those explicit backdrop layers' size/opacity. Reduce broad glow
layers in shared presentation; keep damage range and other attack shapes intact.
These are lab readability choices, not a claim of native shader parity.
At confirmed nonzero damage only, add compact native contact burst, short visual
recoil (including during another action), a contact sound, and45ms desktop
hit-stop. Misses, immunities and blocked attacks must not trigger contact cues.
Source: LabActor, AttackPresentation, VioletParticle, BattleLabApp/Clashes.
Focused recurrence: Arcanine transition, the four reported moves' actual damage
and single contact cue, burst expiry and miss/reset. Original BDSP Brick Break
model-layer gap and native texture/compositor differences remain open.

LAB-14 visual follow-up: the smaller/transparent Close Combat base still draws a
large flat yellow disk in the post-contact capture. Omit that specific decorative
base renderer in the lab profile; retain its strikes, sparks, wind and trails.
This removes an unsupported flat backdrop rather than presenting it as a faithful
native layer. Final focused pose/contact run passed before this profile-only
adjustment; the final build is checked again for the four reported attacks.

## LAB-15 — shared effect layers, Hydro Pump, floating Pikachu, AI and Flying sound

Reported by Bruno against PokedexHitFeel: all attack families need a layer audit;
Pikachu floats; Hydro Pump is malformed; clashes lack a clear collision animation;
uncontrolled fighters wait in place; Flying attack sounds repeat excessively.
Confirmed shared gaps: all118 sphere-map references were skipped; texture wrap
mutated shared texture assets; screen blending ignored alpha; authored zero UV
scale was replaced with1. Bind all three slots with per-slot view-normal projection
where requested, per-material wrap, and opacity-aware screen blending. Native
combiner/refraction parity remains incomplete: audit now also lists omitted
renderers and unhandled timeline commands instead of calling file existence enough.
Hydro Pump has unit meshes along +Y; aim/stretch its beam between muzzle and target
and select its normal variant rather than drawing the conditional extra variant.
This is a lab beam fit, not reconstruction of native deformation shaders.
Pikachu grounding used conservative renderer bounds: use actual posed skinned
vertices once at spawn. Preserve animation offsets thereafter, with no framewise
foot snapping. Flying SFX source is11.2167s of repeated Gust: derive a faded0.65s
clip for Peck/Aerial Ace. Source recording preserved. Add short expanding clash
ring + impact sound at collision; uncontrolled fighters flank during cooldowns,
while attack approach/commitment and player ownership stay unchanged.
Recurrence: all47 ZA +22 BDSP presentations and material bindings, Pikachu baked
feet, Hydro beam axis/length, two sound durations, cooldown movement ownership,
actual projectile collision + flash expiry. Source repair pending build/visual
review. Five BDSP model references remain absent, not counted as repaired.

LAB-15 additional feedback: Bruno supplies large-body overlap screenshot (saved
under Docs/evidence/effect-repair/bruno-large-body-overlap.png). Confirmed source
cause:1.1m clearance cap, .6m target reach cap, fixed .85m Quick Attack stop, and
fixed team spawn separation. Use uncapped directional posed-body footprints for
allied/enemy separation and range, separate all spawn pairs, and stop approach at
body surfaces. Recurrence: Giratina/Latias spawn and actual melee approach/contact.
Roster currently exposes47 distinct test attacks with four equipped from each
species' small candidate list; it is not a complete species learnset. No expansion
of move coverage is claimed by this rendering repair.

LAB-15 grounding follow-up: first capture still shows Pikachu balanced on its tail
although lowest-vertex ground check passes. Its GLB includes duplicate named
category/menu clips after land clips. ModelEntry picks first, but imported Legacy
Animation's name lookup retains another clip. Rebuild its library in first-source
order to match role resolution. This is a shared duplicate-name repair, not a
species-specific vertical offset. Capture must show the intended foot-supported
pose before claiming success. First renderer smoke also exposed an overstrict test:
untextured emitters legitimately use shader-default white. Allow these, while
catalog lookups now fail explicitly if a requested texture is absent.

LAB-15 full-library sweep caught a BDSP runtime error: mixed orbital-velocity curve
modes rejected by Unity. Convert all three orbital/offset axes to TwoCurves while
preserving original ranges/keyframes. Second run reached all69 effects, actual
clash and large-body hit, but is FAILED because of those logged renderer errors.
Hydro screenshot rejected the initial axis fit: native90-degree particle rotation
already changes the mesh axis. Normalize mesh/size/longitudinal rotation together
to +Z before fitting beam length, so roll cannot swing the beam off its target.
Pikachu's new capture is foot-supported after duplicate-name fix; large-body
spawn/approach capture shows separation. These two visual checks pass independently.

LAB-15 release verification: all69 presentation checks pass, but process shutdown
then logs NullReferenceException from AttackPresentation.Alive: an IMoveFx interface
bypasses Unity's destroyed-object null semantics. Check the backing Component
before accessing gameObject. Focused final lifecycle run exercises externally
destroyed effects and process shutdown; do not treat exit0 alone as error-free.

LAB-15 outcome: isolated Builds/PokedexEffectRepair compiles with0 errors. The69
presentation sweep passes all assertions; final focused lifecycle rerun after the
Alive guard passes and exits0 with no exceptions/particle errors in the complete
shutdown log (Logs/effect-repair-lifecycle.log). No full1,000-body suite rerun.
Evidence: Docs/evidence/effect-repair/focused-checks.txt and lifecycle/focused-checks.txt;
69 contact-frame captures plus Pikachu, clash and Giratina/Latias captures.
Visual inspection confirms Pikachu feet, large-body separation, Hydro beam axis
and visible clash ring. It also confirms remaining visual limitations: flat fire/
Dragon Pulse surfaces and oversized Hydro splashes; sphere binding is not native
shader reconstruction. Five BDSP model references,32 omitted renderer records and
Dig's three unsupported position/raycast commands remain explicitly audited.
Other games' bodies still use shared ZA/BDSP effects, not their own native engines.
Launcher updated; prior player/build preserved. Subjective feel/audio acceptance
and full source-game visual parity remain open for Bruno's playtest.
