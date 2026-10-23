# Hoenn move batch — reviewed build

Bruno authorized six to eight additional Hoenn attacks after positive playtest feedback on AttackVisuals20261023. Selected six: Blaze Kick (Blaziken), Muddy Water (Swampert), Ice Beam (Walrein), Dragon Claw (Salamence), Rock Slide (Aggron), Earthquake (Flygon). Existing newest-model species selection and the 1,000-model roster remain unchanged. The move catalog grows from 47 to 53; 53 ZA and 28 BDSP native presentations are staged.

The six moves are appended to pooled legal learnsets. Eligible species: Blaze Kick 21, Muddy Water 98, Ice Beam 271, Dragon Claw 76, Rock Slide 358, Earthquake 282. Setup → Loadout has six tryout buttons, each equipping its Hoenn representative with the new move in slot 1. Other Pokémon can select eligible additions through their loadouts.

## Repairs and behavior

- Ice Beam participates in finite-segment ranged clashes. Projectiles retain their swept collision test. Damage is compared after STAB, target typing and defense; a winner needs at least 1.5× the opposing damage and continues with the difference. Ground fields and melee effects remain outside projectile clashes.
- Muddy Water follows native speed rather than the carrier’s misleading .05-second base lifetime. Its upside-down mesh crest is fitted above the floor, travels, splashes and expires. BDSP’s stage-wide waves are fitted to the arena, with their native trough baseline and feathered finite crest boundary.
- Rock Slide restores the missing secondary native timeline: 24 falling rocks, impact layers and dust. Native timing and speed are retained; deterministic bounded spread replaces the original carrier shake.
- Ice Beam restores the authored blue palette and its native custom flow program. Beam span and impact sizes are fitted to current fighters. Mirrored BDSP hit-atlas coordinates remain inside their clamped textures.
- Blaze Kick and Dragon Claw approach before hitting, use native body cues and contact effects, and have restrained visual extents. BDSP Dragon Claw’s previously invisible mirrored slash layers are restored.
- Earthquake damages nearby enemies individually, respects Flying immunity and leaves lab allies unharmed. BDSP’s rigid single-bone weights, submesh materials, textures and 39 animation tracks are retained. The 38 fractured-ground renderers remain; the cinematic black enclosure and competing flat base are intentionally omitted from the open arena. The source tremor curve is amplified at arena scale. ZA’s ground rings are oriented and sized to the arena.
- All six use the existing hit feedback: damage, recoil, contact flash, one-shot impact audio and a brief hit pause. Blocking/reset disposes effects and prevents late damage.

## Review history

The first functional PASS did not pass visual review. Rejected captures showed an orange Ice Beam, oversized kick/claw layers, a missing wave crest, tiled wave/floor intersections, and a black Earthquake dome. Subsequent reviews exposed the inward-facing cinematic enclosure, clamped negative UVs, and floor movement too small at the fitted scale. Those were repaired in exporters/runtime; earlier evidence is preserved locally under first-pass, second-pass and third-pass. Curated final review images and check reports are tracked separately.

The native ground regression tests baked vertex movement, rather than accepting the presence of animation tracks. Its first run failed the movement threshold and exposed edit-mode temporary-mesh disposal warnings; both are addressed in the final candidate.

The final Ice Beam review also caught a vertical BDSP frost patch: its flat XZ mesh inherited a cinematic root rotation of −90° around X. Resetting that attachment rotation and authoring offset restores the horizontal ground patch. The final focused player run and rendered capture confirm contact and expiry.

## Fidelity boundaries

These are native-source effects adapted to direct control, not a claim of pixel-identical original-engine rendering. Original cinematic camera tracks, transparent-on-transparent refraction, some native deformation programs and the pre-existing two Iron Tail material curves remain outside this batch. Body cues select the available native physical/special clips; not every species has a bespoke animation for each move. PLA/SV/Sword bodies continue using the shared ZA effect library.

Audio uses the existing documented Emerald family substitutes (Ember, Bubble, Hyper Beam, Cut, Rock Throw and Strength); it is not original ZA/BDSP audio. Status/ability simulation is outside this addition. Full species×move×camera combinations and the subjective feel still require Bruno’s playtesting.

## Validation

Released as Builds/HoennMoves20261023; Play Battle Lab.cmd now opens this build. Unity build succeeded with zero errors. All 15 focused tests pass, including finite beam clashes, damage thresholds, native texture/audio bindings and baked ground movement. The full normal-roster player review passes all six representative users, all 12 native presentations, equal and mixed ranged clashes, three-enemy Earthquake, Flying immunity and reset cleanup. Final Ice Beam attachment-only repair was rebuilt and checked in a separate successful normal-roster player run; its rendered frost patch is horizontal and expires. Curated visual timelines and reports are in Docs/evidence/hoenn-moves/final-review. The full build contains 1,000 GLBs and a catalog identical to LocalRoster. Bruno’s playtest of this new batch remains pending. The final export audit finds 4,355 texture bindings, zero missing textures, zero unsupported slots and zero unhandled particle timeline commands. This audit establishes staged asset coverage, not renderer parity.

## Source-version selection

Both ZA and BDSP effects for these six moves are retained separately. A BDSP body uses its BDSP move when available; other bodies use ZA. No cross-source layers are merged. Shared damage, hit feedback and clash rules do not make the two visual versions identical. The newest-model-per-species preference controls roster models, not replacement of all effects with the latest version.
