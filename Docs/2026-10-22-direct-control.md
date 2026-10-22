# Direct Pokémon control and attack presentation

Bruno confirmed direct Pokémon control as the next battle-lab experiment. The
initial prototype demonstrated rules but its trainer-follow movement and generic
sphere effects did not address the primary question: how the Pokémon feels to
move, attack and recover.

Default: Z-A rhythm, Z-A Pikachu, stationary training target, unlimited practice
HP. WASD moves the Pokémon relative to the camera; Shift walks; holding right
mouse rotates the camera; wheel zooms. Space makes a short committed dash between
attacks. 1–3 attack the single opponent. Attacks require range and commit the body
until recovery. Quick Attack has an experimental short burst at release. Tab
opens Setup, including model source, species, AI, practice HP and the earlier
BDSP/Arceus comparison rhythms. R resets, Escape pauses, F1 opens model comparison.

The small direct-control arena and dash are lab choices. Z-A's native charge,
body cues, speed multipliers and split animation timing are retained for the
selected move presentation, not a new full engine. BDSP uses exported particle
sequences and attack/hit timings with each model's physical/special body clip.
Neither native BDSP motion-ID variants nor native locomotion/root-motion state
machines are fully reconstructed. Arceus uses its own body clips with ZA attack
effects/timing, clearly identified in the HUD. Native Arceus attack effects and
original audio are not imported; the hit tick remains synthetic.

Eight moves: Quick Attack, Thunderbolt, Iron Tail, Shadow Ball, Dark Pulse,
Sludge Bomb, Aura Sphere and Metal Claw. Shared Quick Attack gives 27 loadout tests
across Pikachu/Gengar/Lucario and Arceus/ZA/BDSP models. Presentation comes from the
local source dumps already used by HoennVR. Only self-contained presentation
helpers were copied/adapted into BattleLab.Native; no HoennVR battle/world/save
controller or runtime assembly is referenced. Exported model/effect data remains
local and ignored by Git; the manifest records its exact hashes.

Effects are reconstructed in Unity from native data. Some original renderer,
material, effect-model and state-machine behaviors remain unsupported. Collision
is a simple range check at the native presentation hit marker, with a lab dash
window; projectile visual paths are not authoritative moving hitboxes. This is a
useful animation/control study, not original-game fidelity or final combat design.

Build isolation: the new player lives under Builds/DirectControl; the original
Builds/PokemonBattleLab.exe remains available. Neither HoennVR's current player
nor its original save is modified. Automated checks use the lab's in-memory state.
