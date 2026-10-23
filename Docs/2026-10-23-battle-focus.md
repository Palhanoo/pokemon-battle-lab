# Focused battle controls repair

Bruno's playtest rejected mandatory free aim and the camera bounce, reported a Swampert loadout exception, weak impact confirmation, Hydro Pump/Rock Throw problems and insufficient room for Eternatus. Dynamax is removed at his request. Earlier experiment reports remain in the immersive-battle chapter and LAB-22; they do not override these new reports.

## Controls

Focused commands are the default again. WASD moves the controlled Pokémon; 1–4 commands its equipped moves, with automatic approach. E cycles the focused enemy, Q switches allies, Space dashes, Tab opens Setup. A focus label and HP bar appear in duels as well as team battles. Actual damage produces a floating number, a brief HIT confirmation, recoil, contact particles, impact sound and a short hit stop. Immunity is labeled separately.

C cycles Classic / RPG / Shoulder cameras; the same selector is in Setup. Hold RMB to rotate, scroll to zoom. Camera anchors come from the neutral posed model and its world position, never animated walking bounds. Small bodies get a closer starting distance. RPG adds elevation and a limited bias toward the focused enemy. Camera choice is independent of focused/manual attack selection. Manual aiming remains an explicitly experimental Setup option, with T to mark. This iteration does not add parry or basic attacks. Hold-to-steer beams with damage per second are deferred until the view/control foundation is accepted; existing beams still deal one hit per cast.

F raises eligible flying/levitating species while held, using limited energy; release descends. Rotom now participates, with a lower 1.6m rise instead of the 2.8m flying ascent. The water movement lane remains. The floor is expanded from 30×30 to 100×100m, with disjoint water/land surfaces and movement bounds accounting for body footprints.

## Mega Evolution

M begins charge on the original body, then swaps to the selected staged Mega form and plays its native appeal animation. Four original ZA charge/foot/reveal particle sets are exported by Tools/stage_mega_effects.py with their original textures, mesh data and combiner metadata. The lab coordinates the sequence and rebinds the effects to the new body. Movement/attacks wait for the transformation to finish. The existing one-Mega/30-second lab rules remain. This is native particle/body playback with lab timing, not an assertion of exact source-game cinematic or shader parity. Dynamax controls, scaling, HP changes and attack-count mechanics have been removed.

## Verification

All15 focused edit-mode aim/flight/clash tests passed. The normal full-roster player review passes the reported Swampert reset sequence, Hydro Pump/Rock Throw hit/miss/expiry, all six Pikachu/Rotom camera walking checks, Rotom ascent/descent, Mega charge/reveal/revert and post-Mega Hydro Pump, and two Eternatus within the enlarged floor with separated footprints. Native assets: 4,428 bindings,0 missing,0 unhandled particle timeline commands;1,000 base models and96 Mega forms present in the full player.

Rendered review caught additional issues despite those functional passes. Hydro Pump's muzzle/impact scale and attachment are repaired. Rock Throw's exported path was missing its native5m descent at12m/s, causing immediate damage and a floating rock. The restored path now delays damage until contact. Its original shader uses a distinct coverage equation; restoring it makes the solid rock visible instead of clipping it with a lighting gradient. Final Rock Throw checks assert no damage halfway down, a descending collision segment, exactly one hit, deliberate aimed miss and cleanup. Manual hits preserve the native impact animation after stopping projectile bodies. The final rendered captures show the textured falling rock, water jet, Mega animation, six small-species views and the enlarged arena; raw rejected captures remain under first-review.

Hydro Pump and Rock Throw do not have staged BDSP versions; selecting BDSP for them falls back to ZA. Test labels record the requested selector, not a claim of distinct BDSP assets. Exact source-game shader/cinematic parity, broad move/species coverage and Bruno's subjective camera/impact preference remain outside this verification. Hold-to-steer/DPS beams are not implemented in this release.

Build BattleFocus20261023 is isolated from earlier releases and compiles with zero errors. Play Battle Lab.cmd selects it after review. Play Hoenn Checkpoint.cmd continues to preserve the original stable version. Focused report summaries and screenshots are under Docs/evidence/battle-focus.

## Accepted feel and next scope

Bruno's follow-up accepts the current appearance, levitation and battle feel. Keep Classic, RPG and Shoulder available while he compares them. Add a readable flight stamina bar with remaining powered-flight seconds and recovery state; retain the existing three-second reserve and grounded recovery. No new moves or broad movement mechanics are added in this pass. A small species-specific movement pass is the proposed next step before another move batch, using existing attacks to judge the differences. Continuous beams remain deferred.

Flight stamina follow-up completed24 October: the bar shows remaining seconds, amber below0.75s, and ready/airborne/recovery instructions. Existing three-second capacity and0.6s/s grounded recharge are unchanged. Six focused runtime assertions pass, no runtime errors, and ready/low/empty/recovering captures are visually reviewed under evidence/battle-focus/reviewed/flight-*.jpg. FlightStamina20261023 compiled without errors and includes the full1,000 base/96 Mega roster. The launcher selects it; the previous BattleFocus build remains intact.
