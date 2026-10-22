# Real-time roster review — 22 October 2026

Bruno wants real-time control regardless of the model source, more available
Hoenn species and attacks, a corrected Aura Sphere travelling effect, and sound.
He asks to test every Pokémon and move himself while automated testing continues.

The user review package is Builds/RealtimeRoster/PokemonBattleLab.exe. It was
built successfully (one scene, zero errors, 640,861,857 bytes) and launched for
Bruno. Do not replace this package while he is playing. Automated verification
uses Builds/RealtimeRosterVerification with the same gameplay and an offscreen
capture harness so it can run without requiring window focus. Original builds
and their launchers are retained. No world, save or story changes.

| Pokémon | Available bodies | Six move choices |
|---|---|---|
| Pikachu | Arceus, ZA, BDSP | Quick Attack, Thunderbolt, Iron Tail, Thunder, Spark, Discharge |
| Gengar | Arceus, ZA, BDSP | Shadow Ball, Dark Pulse, Sludge Bomb, Lick, Psychic, Thunderbolt |
| Lucario | Arceus, ZA, BDSP | Quick Attack, Aura Sphere, Metal Claw, Dragon Pulse, Bullet Punch, Close Combat |
| Treecko | ZA | Quick Attack, Leaf Blade, Energy Ball, Leaf Storm, Absorb, Giga Drain |
| Torchic | ZA | Slash, Ember, Flamethrower, Peck, Fire Spin, Aerial Ace |
| Mudkip | ZA | Tackle, Water Gun, Water Pulse, Mud Shot, Rock Throw, Hydro Pump |
| Ralts | ZA | Confusion, Psybeam, Psychic, Disarming Voice, Draining Kiss, Shadow Ball |
| Poochyena | SV | Tackle, Bite, Crunch, Dig, Take Down, Play Rough |
| Breloom | BDSP | Mach Punch, Brick Break, Seed Bomb, Giga Drain, Rock Tomb, Drain Punch |

Four moves are equipped; Setup cycles each slot through six unique choices.
All body sources can use real-time rules. Original comparison rhythms remain.
Battle defaults are now active AI and finite HP. Practice and a passive target
are optional. The model/source selector only offers actual staged exports.

Audio uses existing local Emerald recordings, with documented type-family
substitutes for missing newer moves, eight cries and no Lucario cry. Cue timing
comes from the imported attack timing. This is not modern source-game audio.

Aura Sphere repair: the projectile emitter used a slot-2-only mask which was
ignored, and its plate-XZ primitive lacked the required plane basis. Mask fallback,
UV rotation/scroll and plane orientation are repaired at the importer/renderer.
No blanket scale shrink. Visual acceptance remains pending; see LAB-03.

Checks: 29 focused EditMode cases passed before packaging. The standalone matrix
covers all 90 body/move combinations, movement/recovery, pause, one hit per
attack, live particles, sound-cue dispatch, reset cleanup and complete fights in
all three rhythms. Results are recorded in verification.md after execution.

Native body clips and exported effects are reused in a new runtime. Complete
native state machines, root motion, moving projectile collision, status, drain,
multihit and other secondaries are not reconstructed. These test loadouts do not
assert move legality. Bruno's playtest decides whether control and animation feel
coherent; automated lifecycle tests cannot make that judgment.

Final verification: 90/90 standalone presentations and all three finite battle /
reset scenarios passed; player exit 0. Captures show the giant upright Aura
shape removed, with visual acceptance still open. Additional rectangular effect
artifacts in Breloom Mach Punch and Gengar Shadow Ball are recorded as LAB-05.
