# Immersive battle iteration

Bruno requested publication of the reviewed Hoenn checkpoint, independent effect-source selection, camera aiming, Mega Evolution/Dynamax exploration, and habitat movement. Stable commit be0a9d0 is published as main at https://github.com/Palhanoo/pokemon-battle-lab. No main-game assets or saves are part of this work.

## Controls and behavior

The real-time mode defaults to a third-person shoulder camera. Mouse movement aims, WASD moves, 1–4 fire the selected slot, left mouse repeats the last slot, and right mouse marks/unmarks a nearby aimed enemy. Marked projectiles aim at that enemy before launch; beams remain freely aimed. Melee retains automatic approach. Setup permits returning to classic commands. Tab releases the mouse into Setup; Esc pauses.

Setup has Auto / ZA / BDSP effects, independent of the Pokémon model. BDSP explicitly falls back to ZA when a move lacks its staged BDSP sequence. An in-progress attack keeps the version with which it started.

Aimed ranged moves resolve finite world-space contact against bodies, including elevation. A selected target alone cannot cause damage. Projectiles commit their path on release; a beam follows the aim while active until its one damaging contact. Native effect clocks and existing hit/clash feedback remain in use. This iteration retains one hit per cast rather than damage-per-second channels. Area moves use an aimed location; Earthquake originates at the caster and uses the configured 4–10m radius (default 6m). Its native field fit scales with that radius.

Hold F for a short ascent on flying-capable Pokémon. Flight has three seconds of energy, landing and ground-only recovery; being aloft escapes low trajectories, not attacks aimed upward. The water lane is the blue square at x −10..−3, z 3..10. Grounded Water types move 1.65× faster inside it, reverting on land. Available native fly/swim clips are selected, with battle/walk fallback. This is a habitat test area, not map expansion.

## Transformations

M swaps to the selected staged native Mega model and its source form stats/types. Native body clips are retained. Only one Mega per side is active; each fighter can use Mega once per reset, with a 30-second lab duration or manual return. HP fraction is preserved. Setup cycles alternate Mega forms for species with more than one. The catalog contains 96 standard Mega forms sourced from local ZA exports; Z-Mega is excluded. Availability is not an assertion that all 96 models received visual playtesting in this iteration.

G activates the explicitly labeled Dynamax prototype: bounded body enlargement, larger footprint/hit volume, twice maximum/current HP and up to 20 seconds or three completed attacks. Returning preserves damage as an HP fraction. It retains the equipped moves: authentic Max/G-Max moves, weather, abilities, restrictions and original transformation cinematics are outside this prototype. Mega and Dynamax cannot overlap on the same Pokémon.

Transformations dispose effects attached to the replaced body and cancel actions targeting that body; this is a defensive transition in the lab. Active forms expire on the battle clock, so Setup/pause suspends timers. Reset returns base models, stats, size, HP limits and movement energy.

## Verification

All 16 focused aim/clash/native-ground tests passed. The full normal-roster runtime review passes effect selection on the same Swampert, aimed beam hit/miss, in-place expanded Earthquake, flight energy/landing, water speed, three native Mega swaps, Dynamax HP/expiry and reset cleanup. The follow-up runtime review also passes opposing aimed Ice Beam cancellation, Mega Blaziken using Blaze Kick and Dynamax returning after three completed attacks. Starmie receives the expected water bonus. Builds have zero errors and contain all 1,000 base GLBs plus 96 Mega GLBs.

Rendered review found a Setup control overlap, insufficient shoulder offset for large forms and excessive BDSP field height when radius was enlarged. All three are source-repaired, rebuilt and visually checked. The final focused render passes X/Z-only field scaling and clean Setup/play camera captures. The launcher now opens Builds/ImmersiveBattle20261023; Play Hoenn Checkpoint.cmd retains Builds/HoennMoves20261023. Original rejected frames remain local. The first aimed beam timing failure and repairs are preserved in LAB-22.

Only representative Mega bodies and attack combinations were exercised, not all 96 forms or every species/move combination. Aiming uses body bounds, not per-limb hitboxes. Native renderer/refraction limits from the prior release remain. This is a playable design experiment; Bruno’s feel/balance playtest is still needed.
