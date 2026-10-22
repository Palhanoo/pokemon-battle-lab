# Verification — 22 October 2026

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
