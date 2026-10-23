# Native attack shader detail pass

Scope: existing 47 ZA and 22 BDSP presentations, no move expansion.

## Confirmed data omissions

The original movefx exporter forced draw=false for combiner.shader_type=1 even
when the native info.is_particle_draw flag was set. Hydration now restores that
flag, retaining 16 true carriers as invisible and re-enabling 16 distortion
emitters. All 16 have native parameter curves. Shader_type=1 is an observed
convention in these game assets, not a universal EFT2 enum name.

ZA surface metadata retains shader kind, apply_alpha, distance attenuation,
soft-edge range, Fresnel and near/far fade enums/ranges, and parameter keys.
195 emitters have nonzero soft-particle flags. BDSP exporter retains its explicit
SOFTPARTICLESENABLED keyword and near/far distances (188 staged renderers).
Native sampler textures are already imported as linear data.

## URP implementation and limits

Refraction samples the opaque scene using signed RG from the native distortion
map. The original normalized-life parameter curve drives displacement; source
alpha and the secondary mask still control the footprint. Full-resolution opaque
and depth textures are requested both in the pipeline and on the battle camera.
Refractors draw before other transparent effects so the opaque copy does not
replace already-rendered attack layers. Foreground-depth rejection prevents
sampling closer surfaces into the distortion. Zero strength is exactly transparent.

The 0.015 screen displacement factor and silhouette softening are explicitly
calibrated compatibility choices, not a decoded original engine framebuffer
formula. Transparent particles cannot themselves be refracted by the opaque-only
scene copy; overlapping refractors are not sequential full-scene compositors.
The raw apply_alpha, Fresnel and near/far enums are retained for investigation,
but are not claimed faithfully translated. Native vertex deformation, two Iron
Tail model-material curves and three Dig timeline commands remain incomplete.

Soft particles fade using camera-depth separation and the source range, only on
flagged materials. Sphere masks use interpolated normals normalized per fragment,
reducing faceting on coarse meshes. Artificial directional darkening is removed
from emissive mesh particles; reconstructed lit ordinary models retain it.

## Reproduction

With Unity closed: run Tools/enrich_effect_data.py for ZA. The normal
Tools/export_attack_effects.py pipeline includes this and the updated BDSP material
exporter. Audit with Tools/audit_effects.py. Build into a new isolated output with
Tools/unity.sh build -lab-build-dir Builds/PokedexShaderDetails.
Run that player's -lab-effect-audit -lab-shader-details -lab-fast-load flags.
The focused branch writes Docs/evidence/shader-details independently of the
previous visual-layer review. It includes checker/occluder pixel assertions and
native attack on/off refraction pairs, twelve timed frames, contact captures and
native material bindings. Pixel evidence establishes this URP implementation,
not matching native-game footage; Bruno's visual acceptance remains open.

Parser cross-check reference: https://github.com/KillzXGaming/EffectLibrary
(EFT2 emitter fields; this library does not establish the original shader math).

## Native surface state follow-up

Focused captures exposed a separate source omission: every native mesh had been
forced to transparent/double-sided rendering. Hydration now preserves depth test,
depth write, alpha comparison/threshold, blending enable and display face.
914 staged ZA emitter records use unblended, depth-writing masked surfaces; 242
select one face. Psybeam's core and core_ura explicitly select opposite faces.
Opaque masked surfaces render before refraction, without alpha premultiplication.
157 mesh-emitter references have nonwhite primitive channels: retain those data
and feed the authored primitive color/alpha selectors and operations. This
restores Dragon Pulse vertex-alpha fades without applying invented mesh lighting.
Native beam cores are intentionally solid surfaces in the source; original
Fresnel/deformation is still needed for closer full-game matching.

The first extended run found a test-only null-material assumption on BDSP carrier
emitters, fixed with a material guard. A later HLSL reserved-word error was caught
by the pixel check. Build packaging now also rejects nonzero reported errors even
if Unity labels the overall build Succeeded. Final verification recorded below.


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
