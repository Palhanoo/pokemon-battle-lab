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
