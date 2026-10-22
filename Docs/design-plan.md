# Battle feel reset — 22 October 2026

Bruno reports that battles do not feel good and the accumulated gameplay/story
parts do not yet connect. Pause expansion and test combat separately. BDSP was
explicitly confirmed as the third reference beside Legends: Arceus and Z-A.

## Diagnosis and working hypothesis

HoennVR's current runtime layers `BattleRealtime` on the existing `Battle`
turn/rules engine, while `RealtimeBattle`, partner/field actors, animation plans
and native effect interpreters share presentation responsibility. The latest
review notes still list root motion, posture/state transitions, conditional boss
control flow and visual parity as incomplete. This is evidence of coupling and
unresolved fidelity gaps, not proof of which one feels worst to Bruno.

My hypothesis: a clear command → intent → anticipation → contact → reaction →
recovery sequence will improve the experience more than adding species, map
content, boss phases or further isolated timing patches. The trainer's role
must also be coherent: directing a partner, making tactical turn choices, or
managing positioning under pressure. Mixing those promises without clear
feedback makes commands feel unreliable.

## Experiment design

Use Pikachu (small contact/ranged), Lucario (humanoid melee/ranged), and Gengar
(broad body and distinctive stance). Export all three from all three local
sources. Preserve their native clip sets and record provenance. Model source
and rules are independent controls. Keep the matchup, lighting, camera direction,
seed, stats and test arena constant when comparing one variable.

The current small implementation is a **rhythm sketch**. It cannot establish
which original game has the best complete battle feel because original VFX,
full movement/state machines and rules are not reproduced. The viewer can
already expose model/clip differences independently of the combat sketch.

## Sequence and acceptance gates

1. **Reference and inventory.** Native model availability and clip inventory;
   original-game reference for idle, melee, ranged, hit, recovery and faint.
   Mark each behavior as observed, decoded, inferred, experimental or missing.
   Do not infer full behavior from a model name or numeric parameter.
2. **Small comparison lab (this repo).** One scene, no save/story, nine models,
   separate model/rule controls, reset/pause, simple readable lifecycle.
   Check imports, distinct sources, command completion, damage, reset and renders.
3. **One faithful vertical comparison per source.** Pick one contact move and
   one projectile/area move. Compare anticipation, body travel, facing, release
   socket, contact, reaction and recovery against the actual game. Add native
   effect playback and data-driven animation state resolution here. Only after
   this gate should we judge animation fidelity or copy choreography to Rayquaza.
4. **Choose the desired player experience with Bruno.** Score command response,
   readability, physical contact, tactical choice, control and enjoyment 1–5;
   record one specific frustrating moment per trial. Select a direction or a
   precisely described hybrid. Do not declare a winner from automated tests.
5. **Tune the chosen loop.** Input buffer/queue feedback, range intent, cancel
   policy, partner spacing, recovery, enemy readability, camera and audio.
   Change one parameter at a time. Add switch/defend only once single-matchup
   combat is accepted. VR camera and comfort are a separate headset gate.
6. **Integrate through a narrow interface.** Commands in; action/impact/recovery
   events out. One movement owner and one damage owner per action. Use explicit
   cancellation on reset, faint, swap and exit. Keep the lab as the regression
   harness. Reconnect story teaching only after the battle loop is approved.

## What not to add yet

No town, terrain generator, progression, inventory, quests, capture loop, large
roster, Mega bosses, online features or automated claims of 'ready'. The current
lab intentionally exposes its approximations instead of importing the same
interwoven controllers into a smaller folder.

## Five-minute manual session

Compare the same species in the viewer; watch an attack at 1×, then .5×. Run
Pikachu versus Lucario once in each mode, with the same model sources. Repeat
one favorite mode with each native model source. Use Gengar to expose type
immunities and different body sizes. Reset between trials. Report: 'I pressed X,
expected Y, but saw Z.' Record build/commit and whether AI was enabled.

For Z-A, test a ranged miss while moving and two differently cooling moves.
For Arceus, compare preview and actual next actions after Normal/Agile/Strong.
For BDSP, Quick Attack should precede a faster enemy's ordinary move. In every
mode, reset mid-action and verify no lingering projectile or later HP change.

## Sources

- [Arceus official gameplay](https://legends.arceus.pokemon.com/en-gb/gameplay/):
  seamless encounters, action ordering, Agile/Strong tradeoffs.
- [Z-A official gameplay](https://legends.pokemon.com/en-us/gameplay): real-time
  trainer/partner movement and differences in reach, area and activation time.
- [BDSP official battling guide](https://diamondpearl.pokemon.com/en-us/trainersguide/fundamentals/battling/):
  turn selection, Speed ordering, switching/items and their turn cost.
- HoennVR `Docs~/notes/2026-10-22-battle-volley-review.md`,
  `2026-10-22-native-presentation.md`, `2026-10-20-za-battles-spec.md`, bug register.

## 22 October follow-up — control and animations first

Bruno confirmed that he wants to control the Pokémon directly. This chooses the
next experiment's player role: movement, attacks and recovery from the Pokémon's
perspective. The default is now that direct-control study, with native ZA and
BDSP effects and split body animations. The older comparison modes remain
available. Arceus body/effect parity and complete original engines remain open.
See 2026-10-22-direct-control.md and bug-register.md. First acceptance question:
does moving, stopping, attacking and regaining movement feel connected?
