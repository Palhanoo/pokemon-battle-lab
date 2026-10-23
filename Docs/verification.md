# Verification — 22 October 2026

## Initial lab (5453b54)

- Nine distinct native GLB exports verified against the SHA256 manifest:
  73,729,808 bytes total. No source models were relabeled as another game.
- Unity imported all nine models with 591 clips: Arceus 194, Z-A 314, BDSP 83.
  Required idle, attack, special, damage and faint bindings validated.
- Eight focused EditMode checks pass (8/8, zero failures). Evidence:
  `evidence/editmode-tests.xml`. The initial 7/8 result exposed an incorrect
  action-order test fixture: its enemy deadline was too early to permit the
  asserted third Agile action. The fixture was corrected; no rules were changed
  to satisfy that assertion.
- Windows development build succeeded: one scene, zero build errors,
  419,487,117 bytes. First player build took 117.146 seconds; the final
  incremental player build took 17.534 seconds (Unity build-report time,
  excluding editor startup/import). See `evidence/build-summary.json`.
- Final standalone smoke run completed all three fights through a winner,
  reset each mode during an attack, waited for late impacts and verified no
  pending action or delayed damage. Final process exit 0; `LAB SMOKE PASSED`;
  no exception in the final player log. Summary: `evidence/smoke.txt`.
- Initial player inspection caught small camera framing and text trails outside
  a partial camera viewport. Fixed with closer battle framing, a common display
  scale for each species in the viewer, and a full-frame background camera.
- Initial smoke results were invalidated by live input changing a scenario,
  and the gallery loop exposed an out-of-range index during shutdown. The final
  smoke ignores live keyboard/mouse input, asserts mode identity, and uses a
  bounded gallery index. Initial log retained under `Logs/initial-smoke/`.
- Inspected final screenshots of the three model-source comparisons (Pikachu,
  Gengar, Lucario) and the BDSP-style battle. All nine models are visible with
  readable source labels; earlier UI trails are absent. Arceus and Z-A battle
  screenshots are saved too. Still images do not verify complete animation
  continuity, native shader fidelity or subjective battle feel.

## Still open

Bruno has not accepted the combat feel. Headset/VR is not implemented or tested
in this desktop lab. Full original-game damage, movement, AI, native move effects,
root motion and state machines are not reproduced; see README and the design
plan. In particular, the Arceus queue is experimental and the Z-A projectile/
area presentation is diagnostic. The rendered source models also use converted
materials under shared Unity lighting, not the original games' renderers.

HoennVR received only the feedback/decision notes (commit 1db47c7). Its existing
ProjectSettings edits remain untouched, and its game executable and saves were
not opened or rewritten. This local lab has no remote configured.

## Direct-control animation review — follow-up

Bruno reported Arceus Pikachu sticking and confirmed that movement and attack
animations are the priority. See bug-register.md LAB-01/02. Native presentation
helpers, eight ZA effects and eight original BDSP sequences are now staged.
Arceus uses its own body clips with ZA effects/timing; this is labeled in the UI.

- Focused final EditMode checks: **20/20 passed**, including the original eight
  rule tests, nine model/loadout plans, PLA split special, ZA split rush, and
  non-null exported effect references. Native imported clips remain 591.
- First standalone attempt failed because its movement check waited on wall time
  during initial rendering. The test now measures movement over simulated game
  time; startup cannot consume the test's movement interval. Initial failure log
  retained as Logs/direct-control-player-initial.log.
- The next standalone run passed **27 attacks over all nine bodies**, with live
  particle counts, one hit each, pause/resume, body recovery and movement after
  each attack. All nine also passed direct movement/idle/dash checks and reset
  cleanup; PLA Pikachu's split damage reaction returned to idle.
- That run completed finite battles in BDSP, Arceus and ZA rhythms and verified
  reset during attacks without delayed damage. Process exit 0, no exception.
- Inspected source-specific screenshots: native effects are visible, including
  BDSP Thunderbolt and ZA Aura Sphere. Gengar's charge effect is large in the
  initial framing; the camera now adapts to body height. Effect UV scrolling now
  uses the presentation clock so pausing also freezes the texture motion. Final
  rebuilt-player evidence will be recorded below.
- Desktop input inspection through the Computer Use skill could not initialize:
  node_repl reported sandboxCwd is not a local file URI: file:///mnt/d/www/HoennVR.
  Automated gameplay drives the same movement/action methods with controlled
  input. Actual keyboard/mouse handling and subjective feel are not claimed as
  manually verified. No headset verification.

Limits: native renderer parity, Arceus move effects, complete BDSP motion-ID
variants, locomotion/root motion, authentic audio and moving projectile hitboxes
remain incomplete. Large/solid-looking particle meshes can differ from the
original shaders. The test establishes body/effect/control lifecycle, not native
game fidelity or that Bruno will enjoy the result.

Final direct-control package: one scene, zero build errors, 443,011,297 bytes.
Final standalone again passed all 27 presentations and three complete fights;
exit 0 and LAB SMOKE PASSED. Fifty-four wind-up/impact captures are retained under
evidence/direct-control. Aura Sphere's impact capture shows an oversized cyan
travelling element; Bruno independently reports this as the next defect. It is
not accepted as visually correct despite passing lifecycle checks.

## Expanded real-time roster — 22 October follow-up

- Fifteen actual-source models, nine species, 1,011 imported clips. Six choices
  per species, four equipped; 47 distinct ZA effect rows and 22 BDSP sequences.
- **29/29 focused EditMode tests passed**, including all model/move animation
  plans, roster/audio coverage, non-silent samples and Aura Sphere mask/basis.
  Evidence: `evidence/realtime-roster/editmode-tests.xml`. One earlier fixture
  still expected nine models and every species in all three sources; it was
  updated to check all fifteen actual variants against shared species data.
- SHA256 audit passed for all 15 model, 783 effect and 27 audio manifest records.
- User review build: one scene, zero errors, 640,861,857 bytes. Launched for Bruno
  from Builds/RealtimeRoster; remains untouched while he tests. Verification
  player is separately packaged (640,862,517 bytes) with the same gameplay and
  a focus-independent camera-to-texture capture harness. It uses `-batchmode
  -lab-smoke`, with graphics enabled, so these are rendered images, not null-
  device simulation. See `evidence/realtime-roster/build-summary.json`.
- **90/90 standalone model/move cases passed**: one hit, live particles, sound
  cue dispatch, idle recovery, movement after each attack. All fifteen bodies
  also passed movement/idle/dash, pause and reset cleanup. PLA Pikachu's split
  damage reaction recovered independently. Full summary:
  `evidence/realtime-roster/presentation-smoke.txt`.
- Wind-up/contact captures are in `evidence/realtime-roster`. Inspected Aura
  Sphere (ZA/Arceus Lucario), Breloom Mach Punch, Poochyena Crunch, Mudkip Water
  Pulse, Torchic Flamethrower and Gengar Shadow Ball. The giant upright Aura
  primitive is absent in the new stills. Mach Punch has dark/striped rectangular
  artifacts and Shadow Ball exposes effect-card edges (LAB-05, open). No visual
  acceptance claim for the complete set; stills do not establish travel quality.
- Sound verification proves clip data and cue dispatch, not speaker playback or
  a good mix. Samples are Emerald recordings with documented family substitutes
  for newer moves, not authentic ZA/SV audio. Eight cries; Lucario is missing.

Bruno is testing real keyboard/mouse control and battle feel independently.
Automated gameplay uses the same controller methods with injected input; no
manual input or headset verification is claimed. Original-engine root motion,
projectile collision, secondary move behavior and material parity remain open.

Final standalone result: **exit 0, LAB SMOKE PASSED**, with no exception in
Logs/realtime-roster-smoke.log. Finite BDSP, Arceus and ZA fights each reached a
winner; each mode also passed reset-during-attack and the wait for delayed damage.
Evidence: `evidence/realtime-roster/smoke.txt`. Both the 90-case presentation
matrix and all three full battle/reset scenarios are complete. These results do
not close LAB-03 visual acceptance or LAB-05 rendering defects.


## Preferred Pokédex, teams and renderer audit — 22 October

The local catalog now selects exactly one preferred body per species, with
ZA > SV > Arceus > BDSP > Sword/Shield priority. There are 1,000 species and
52,807 clips: ZA 364, SV 478, Arceus 37, BDSP 61, Sword 60. The 25 unavailable
species and seven explicit role aliases are recorded in pokedex-validation.json.
All Hoenn species are sourced. Six move choices per species reuse 47 staged moves;
test-only assignments are marked. This is not a complete move/ability simulation.

- 40/40 focused Unity EditMode tests pass. Standalone team checks pass 3v3,
  ally/target switching, automatic approach and cancellation, faint handoff,
  victory/reset and projectile clashes. Equal projectiles cancel; 20 versus 9
  leaves 11 damage. Evidence: evidence/team-battle/team-smoke.txt.
- Historical presentation checks pass 90 attacks across 15 source bodies, plus
  three finite style battles and reset checks. Streamed presentation checks pass
  another 90 attacks across 15 representative preferred models, spanning all five
  sources. They check damage, effect/audio cues and return to movement/idle.
  Evidence: evidence/team-battle and evidence/stream-combat/combat-smoke.txt.
- The union of standalone roster checks covers all 1,000 species: actual loading,
  finite bounds, role sampling, real transform changes and bounded model caching.
  Exact runs are recorded in evidence/pokedex/coverage.json. The initial sweep
  was deliberately stopped after 444 successful cases once other ranges covered
  the remaining IDs. The middle range initially rejected Ferroseed's valid
  one-key idle; the corrected fixture accepts static poses and still requires
  motion across action roles. The resumed range passed. This is aggregate
  coverage, not a claim that one uninterrupted 1,000-case run passed.
- Duplicate/nonincreasing source timestamps were normalized on 28,065 tracks in
  65 models, keeping the last authored value at each time and preserving cubic
  tangents. Two Python normalization tests pass. Runtime animation retains native
  legacy curves and samples them manually; clip-name-only checks were replaced
  after they failed to expose the original non-moving-bones problem.
- Three Python material tests pass: overlapping layers, disabled opacity and
  single tint. Treecko's remaining brown head was caused by multiplying successive
  mask tints. Later layers now blend against original albedo with their opacity.
  ZA/SV/Arceus materials were audited/rebaked; BDSP/Sword use independent pipelines.
  Every rebaked model proves unchanged geometry, skin and animation buffer bytes,
  so the subsequent material-only update preserves the runtime animation evidence.
- The full staged attack audit covers 3,500 texture references. It explicitly
  fails on missing BDSP ModelCreate layers for Brick Break, Iron Tail and Lick;
  118 native lighting lookups remain unsupported. These are open defects, not
  passed fidelity checks. F2 layer inspection and Esc pause assist manual review.

Automated gameplay uses the same controller methods with injected input and a
real graphics device. It does not establish keyboard/mouse feel, speaker playback,
headset comfort or original-engine visual parity. Fire/water/psychic fidelity,
large-species spacing and Bruno's playtest acceptance remain open. The previous
user builds and all main-game saves remain untouched.


Final package: **Builds/PokedexBattleReady**, one scene, zero build errors,
640,917,885 player-build bytes plus 8,440,906,028 bytes of streamed model payloads.
All 1,000 preferred models were staged and the packaged catalog matches source.
Of 879 bodies using the shared native material converter, 830 were rebaked with
unchanged geometry/skin/animation proofs; the others needed no overlap correction.
The 121 BDSP/Sword bodies use independent pipelines. Final-player Treecko check
passed with exit 0; its rendered capture visibly has a green head. Before/after:
evidence/materials/treecko-before.png and treecko-after.png. This closes the
specific brown-head source repair, with broader material fidelity still reviewed
by Bruno. See evidence/pokedex/final-build.json for package and spot-check data.

Final post-rebake static validation passed: 1,000 unique preferred-source species,
52,807 clips, all model SHA256s, embedded resource bounds, roles, stats and move
bindings. Seven explicitly recorded role aliases remain. The validation runs
against the corrected final catalog rather than the earlier pre-rebake exports.


## Quick playtest repairs — 22 October follow-up

Bruno requests targeted checks only. No full roster or 90-case suite rerun.
The focused standalone run passes sustained idle/run sampling after many loops
for Arcanine, Iron Jugulis and Charizard; loading an uncached drill matchup;
reset clearing drill/stale text; and Thunderbolt, Thunder, Dragon Pulse and
Fire Spin presentation lifecycles. Fire Spin expires within1.3s after contact.
Log: Logs/focused-review-fixes.log, exit0, LAB REVIEW FIXES PASSED.
Evidence: evidence/review-fixes. The two rebaked models' hashes match metadata;
Iron Jugulis eye materials now have emissive textures. These checks do not prove
all shader layers match the games. Thunder textures/post details and unspecified
underground effects remain open, beyond the confirmed Dragon Pulse carrier fix.
Review package: Builds/PokedexReviewFixes. Prior running review remains untouched.

Frontal capture review: Charizard's eye highlight is corrected. Iron Jugulis
required native LED colours in its lit eye texture too; emissive-only export did
not display in the player. This is an explicit compatibility fallback, not full
native glow/shader parity. Updated model data is copied only into the stopped
PokedexReviewFixes package, preserving the user's previous running player.

Final visual result: Iron Jugulis eyes remain dark even with the lit-texture
fallback; that attempted fallback was removed. Emissive layer export is retained
as a confirmed missing source feature, but the visible eye defect is OPEN.
Atlas UV/material addressing or overlapping geometry requires further diagnosis;
shader-variant stripping is not established. Do not claim Iron Jugulis eyes fixed.
Charizard's eye repair is visually confirmed. Sustained animation checks pass.


## Movement transition and contact feel — 22 October follow-up

Focused standalone check: LAB HIT FEEL PASSED, exit0. Arcanine actually moves,
stops without an attack/reaction, and selects idle; every local bone then matches
a freshly spawned idle actor at the same animation time. This covers the sparse
shoulder/spine channels that the earlier sustained-loop check missed.
Close Combat, Ember, Brick Break and Dragon Pulse each resolve exactly one damage
event and one contact-feedback event; contact bursts expire. A miss produces no
feedback and reset clears transient state. No full-roster or full-suite rerun.
Evidence: evidence/hit-feel, Logs/hit-feel-check.log. Review: Builds/PokedexHitFeel.
Native shader/layer fidelity, missing BDSP Brick Break models and subjective
combat feel remain open. The smaller backdrops/glow are lab readability choices.

Post-contact visual inspection caught a remaining flat yellow Close Combat disk
inside the initially reduced backdrop. Its specific `base` renderer is now omitted
from the lab readability profile; strikes/sparks/wind/trails remain. This is a
bounded compatibility choice, not a completed native compositor. The stopped
Arcanine capture and bone comparison establish run-pose cleanup; comfort/feel
still need Bruno's playtest.

Final package rerun: exit0, LAB HIT FEEL PASSED in Logs/hit-feel-final.log.
Post-contact capture visibly confirms the flat yellow disk is absent.

## Visual layer repair — 23 October 2026

LAB-16 final verification: Builds/PokedexVisualLayers compiles with 0 errors.
Eight targeted presentations pass in Logs/visual-layers-final.log, process exit0,
no logged exceptions or particle errors through shutdown. All five BDSP model
instances exist, deform and deactivate at timeline end. No full roster rerun.
Docs/evidence/visual-layers retains contact frames and twelve timed frames per
move plus focused-checks.txt. Visual inspection confirms smaller Hydro spray,
full-length forward Psybeam (including its aura), more textured fire/Dragon Pulse
and the restored models placed around their target. The new material blending is
shared across the existing47 ZA/22 BDSP effects; no attacks were added.
Audit now resolves3,600 texture references and all five model references (four
unique assets). This is not native visual parity: refraction/deformation shaders,
32 disabled/carrier renderer records, Dig positioning commands and two Iron Tail
material-animation curves remain incomplete. BDSP contact pose fitting and beam
spray footprint limits are intentional real-time lab adaptations. Bruno's visual
acceptance remains open. Launcher selects this isolated build; previous player
and build remain intact.


LAB-17 final verification: Builds/PokedexShaderDetails builds with zero errors.
Logs/shader-details-final.log: LAB REFRACTION PIXELS PASSED and LAB SHADER DETAILS
PASSED, exit 0, no logged exceptions/shader errors through shutdown. Ten focused
presentations: seven ZA (Flamethrower, Dragon Pulse, Hydro Pump, Psybeam, Psychic,
Shadow Ball, Thunderbolt), three BDSP (Brick Break, Psychic, Thunderbolt).
Pixel fixture: 19,845 changed pixels with distortion; zero outside its footprint,
zero changes at zero strength, and zero changes inside the foreground occluder.
Soft intersections, alpha cutout, front/back culling and actual particle primitive
mask transport pass. Native RG/BA now travel in explicit UV2/UV3 streams, separate
from Unity's particle COLOR; this restores the cores lost in the rejected run.
Visual review confirms visible Hydro/Psybeam cores with distinct color layers,
retained fire/Dragon Pulse particles and BDSP contact layers. These captures do
not establish full original-game parity: flat-looking flame shapes, native rim
and deformation, transparent-on-transparent refraction, two Iron Tail curves and
three Dig commands remain open. URP displacement/silhouette are calibrated.
Full staged audit: 3,600 texture refs resolved; 16 restored refractors, 16 true
invisible carriers, 195 ZA / 188 BDSP soft-particle entries. Native states restored
on 914 masked and 242 one-sided ZA emitter records. No attacks added or full
roster tests rerun. Evidence: Docs/evidence/shader-details. Bruno's acceptance is
pending; launcher selects this isolated build and prior builds are preserved.
