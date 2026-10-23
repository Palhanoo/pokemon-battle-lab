using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleLab.Native
{
    // Rebuilds Scarlet/Violet battle effects (exported by Tools~/hoenngen/movefx.py) as ParticleSystems.
    // An emitter set (one .ptcl) becomes a root with one ParticleSystem per emitter; child emitters become
    // birth sub-emitters. Violet's space is right-handed like glTF, so x is mirrored as glTFast does.
    public sealed class VioletInfiniteEmitter : MonoBehaviour {}

    public static class VioletFx
    {
        public const float Fps = 30f;
        static Mesh quad;

        // One emitter set as a stopped GameObject tree. texture(name) supplies the exported PNGs;
        // baseMaterial must use Hoenn/VioletParticle and is cloned per emitter.
        public static GameObject Build(string setJson, Func<string, Texture2D> texture, Material baseMaterial, Transform parent = null)
        {
            var js = J.Obj(MiniJson.Parse(setJson));
            var meshes = new List<Mesh>();
            foreach (var m in J.List(js, "meshes")) meshes.Add(BuildMesh(J.Obj(m)));
            var root = new GameObject("violet fx");
            if (parent) root.transform.SetParent(parent, false);
            foreach (var s in J.List(js, "sets"))
            {
                var set = J.Obj(s);
                var setGo = new GameObject(J.Str(set, "name"));
                setGo.transform.SetParent(root.transform, false);
                foreach (var e in J.List(set, "emitters"))
                    BuildEmitter(J.Obj(e), setGo.transform, texture, baseMaterial, meshes, null);
            }
            return root;
        }

        public static void Play(GameObject root)
        {
            foreach (var ps in Roots(root)) { var em = ps.emission; em.enabled = true; ps.Play(true); }
        }

        // Fade: stop emitting and let particles finish; otherwise clear them now.
        public static void Stop(GameObject root, bool fade)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var em = ps.emission; em.enabled = false;
                // Native infinite cores live until their timeline Stop. They cannot age out
                // like finite sparks: the 600-second simulation placeholder is not a tail.
                if (!fade || ps.GetComponent<VioletInfiniteEmitter>()) ps.Clear(false);
            }
        }

        // Systems that are not sub-emitters (those are driven by their parent).
        public static List<ParticleSystem> Roots(GameObject root)
        {
            var list = new List<ParticleSystem>();
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                if (!ps.transform.parent || !ps.transform.parent.GetComponent<ParticleSystem>()) list.Add(ps);
            return list;
        }

        static ParticleSystem BuildEmitter(Dictionary<string, object> e, Transform parent, Func<string, Texture2D> texture,
                                           Material baseMaterial, List<Mesh> meshes, Dictionary<string, object> parentEmitter)
        {
            var go = new GameObject(J.Str(e, "name"));
            go.transform.SetParent(parent, false);
            var tr = J.Obj(e["transform"]);
            go.transform.localPosition = Mirror(J.Vec(tr, "trans"));
            go.transform.localRotation = MirrorRot(J.Vec(tr, "rotate"));
            go.transform.localScale = J.Vec(tr, "scale");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            string follow = J.Str(e, "follow");
            main.simulationSpace = follow == "none" ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = 2000;

            float life = J.F(e, "life"), lifeRnd = J.F(e, "life_random");
            bool infinite = J.B(e, "infinite");
            if(infinite)go.AddComponent<VioletInfiniteEmitter>();
            main.startLifetime = infinite ? new ParticleSystem.MinMaxCurve(600f) : new ParticleSystem.MinMaxCurve(Mathf.Max(0.02f, life * (1 - lifeRnd)), Mathf.Max(0.02f, life));
            Emission(ps, J.Obj(e["emit"]), parentEmitter, e);
            Shape(ps, J.Obj(e["shape"]));
            float avgLife = infinite ? 2f : life * (1 - lifeRnd * 0.5f);
            Motion(ps, e, avgLife);
            Size(ps, J.Obj(e["scale"]));
            Rotation(ps, J.Obj(e["rotation"]), e);
            Colour(ps, J.Obj(e["color"]));

            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(baseMaterial) { name = go.name };
            mat.SetFloat("_ParticleUvClock",1);
            Blend(mat, J.Str(e, "blend"));
            mat.SetFloat("_ColorMode", J.F(J.Obj(e["color"]), "mode"));
            // ZA compatibility mapping; exact native combiner parity remains under review.
            int alphaMode=(int)J.F(J.Obj(e["color"]), "alpha_mode");
            mat.SetFloat("_AlphaMode",alphaMode==1?6:alphaMode==2?1:alphaMode==3?5:alphaMode);
            if(e.TryGetValue("combiner",out var combiner)) NativeCombiner(mat,J.Obj(combiner));
            Textures(ps, mat, J.List(e, "textures"), texture, avgLife);
            if(e.TryGetValue("surface",out var surface))
            {
                var d=J.Obj(surface);Surface(mat,d);
                if(J.B(d,"refraction"))
                {
                    // Distortion uses the original parameter envelope, not colour1 blue.
                    var cd=ps.customData;cd.SetVector(ParticleSystemCustomData.Custom2,2,new ParticleSystem.MinMaxCurve(1,KeyCurve(J.List(d,"params"),1,1)));
                }
            }
            if(e.TryGetValue("native_shader",out var shaderData))
            {
                var native=J.Obj(shaderData);
                if(J.F(native,"flow")>0)
                {
                    var values=J.Floats(native["values"]);
                    if(values.Length>=20)
                    {
                        mat.SetFloat("_NativeFlow",J.F(native,"flow"));mat.SetFloat("_FlowColor",J.F(native,"flow_color"));
                        mat.SetVector("_FlowShape",new Vector4(values[12],values[13],values[14],values[15]));
                        if(J.F(native,"flow")==6)mat.SetVector("_FlowShape",new Vector4(values[0],values[1],0,0));
                        mat.SetVector("_FlowEdge",new Vector4(values[16],values[17],values[18],values[19]));
                    }
                }
            }
            r.sharedMaterial = mat;
            Render(r, e, meshes);
            if(e.TryGetValue("render_state",out var state))RenderState(mat,J.Obj(state));
            if (e.ContainsKey("draw") && !J.B(e, "draw")) r.enabled = false; // only carries child emitters
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.Custom2XYZW, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.UV3,
                ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.InvStartLifetime, ParticleSystemVertexStream.StableRandomXYZW,
            });

            foreach (var c in J.List(e, "children"))
            {
                var child = BuildEmitter(J.Obj(c), go.transform, texture, baseMaterial, meshes, e);
                var subs = ps.subEmitters; subs.enabled = true;
                subs.AddSubEmitter(child, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
            }
            return ps;
        }

        public static void RenderState(Material mat,Dictionary<string,object> state)
        {
            int side=(int)J.F(state,"display_side");mat.SetFloat("_Cull",side==1?2:side==2?1:0);
            mat.SetFloat("_ZTest",J.B(state,"depth_test")?J.F(state,"depth_func")+1:8);
            mat.SetFloat("_AlphaTest",J.F(state,"alpha_test"));mat.SetFloat("_AlphaCompare",J.F(state,"alpha_func"));
            mat.SetFloat("_AlphaThreshold",J.F(state,"alpha_threshold"));
            if(mat.GetFloat("_Refraction")>.5f)return;
            mat.SetFloat("_ZWrite",J.F(state,"depth_mask"));
            if(!J.B(state,"blend_enable"))
            {
                mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.SetFloat("_OpaqueParticle",1);
                mat.renderQueue=2450; // native masked depth surface, before refraction/transparent layers
            }
        }

        public static void Surface(Material mat,Dictionary<string,object> data)
        {
            var range=J.Floats(data["soft_range"]);
            mat.SetVector("_SoftParticle",new Vector4(J.F(data,"soft")>0?1:0,range[0],Mathf.Max(.0001f,range[1]-range[0]),0));
            if(J.F(data,"fresnel")>0 && data.ContainsKey("fresnel_range"))
            {var rim=J.Floats(data["fresnel_range"]);mat.SetVector("_Fresnel",new Vector4(1,rim[0],rim[1],0));}
            if(!J.B(data,"refraction"))return;
            mat.SetFloat("_Refraction",1);mat.SetFloat("_DistortionByDistance",J.F(data,"distance"));
            mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);
            // The opaque scene copy precedes particles: refract before ordinary effect layers.
            mat.renderQueue=2990;
        }

        public static void NativeCombiner(Material mat,Dictionary<string,object> data)
        {
            mat.SetFloat("_NativeCombiner",1);
            foreach(var pair in new[]{("color_channels","_ColorChannels"),("alpha_channels","_AlphaChannels"),("color_blends","_ColorBlends"),("alpha_blends","_AlphaBlends")})
            {
                var v=J.Floats(data[pair.Item1]);mat.SetVector(pair.Item2,new Vector4(v[0],v[1],v[2],v.Length>3?v[3]:0));
            }
        }

        static void Emission(ParticleSystem ps, Dictionary<string, object> em, Dictionary<string, object> parentEmitter, Dictionary<string, object> e)
        {
            var main = ps.main;
            var emission = ps.emission;
            float start = J.F(em, "start"), dur = J.F(em, "duration"), rate = J.F(em, "rate"), interval = J.F(em, "interval");
            bool oneTime = J.B(em, "one_time");
            // A child emitter starts with each parent particle; Violet delays it by a share of the parent's life.
            if (parentEmitter != null && em.ContainsKey("timing")) start += J.F(em, "timing") / 100f * J.F(parentEmitter, "life");
            float frame = 1f / Fps;
            emission.rateOverTime = 0;
            if (oneTime)
            {
                main.loop = false;
                main.duration = Mathf.Max(start + dur + frame, 0.05f);
                if (interval <= frame + 1e-4f && dur > frame + 1e-4f)
                {
                    // every frame: a stepped rate over the system's duration
                    float d = main.duration, a = start / d, b = Mathf.Min(1f, (start + dur) / d);
                    var c = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(Mathf.Max(0, a - 1e-4f), 0, 0, 0),
                                               new Keyframe(a, 1, 0, 0), new Keyframe(b, 1, 0, 0), new Keyframe(Mathf.Min(1, b + 1e-4f), 0, 0, 0));
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate * Fps, c);
                }
                else
                {
                    int cycles = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(dur, frame) / Mathf.Max(interval, frame) - 1e-3f));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(start, (short)Mathf.Max(1, Mathf.RoundToInt(rate)), (short)Mathf.Max(1, Mathf.RoundToInt(rate)), cycles, Mathf.Max(interval, frame)) });
                }
            }
            else
            {
                main.loop = true;
                main.startDelay = start;
                if (interval <= frame + 1e-4f) { main.duration = 1f; emission.rateOverTime = rate * Fps; }
                else
                {
                    main.duration = interval;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, Mathf.RoundToInt(rate))) });
                }
            }
        }

        static void Shape(ParticleSystem ps, Dictionary<string, object> s)
        {
            var sh = ps.shape;
            sh.enabled = true;
            var rad = J.Vec(s, "radius");
            float r = Mathf.Max(Mathf.Max(rad.x, rad.z), 1e-4f);
            string type = J.Str(s, "type");
            float arc = J.F(s, "sweep_longitude") * Mathf.Rad2Deg;
            sh.scale = Vector3.Scale(J.Vec(s, "form_scale"), new Vector3(rad.x / r, rad.y / r, rad.z / r));
            switch (type)
            {
                case "circle": case "circle_div": case "circle_fill":
                    sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = r;
                    sh.radiusThickness = type == "circle_fill" ? 1 : 0;
                    sh.arc = arc > 0 ? Mathf.Min(arc, 360) : 360;
                    sh.rotation = new Vector3(-90, 0, 0); // Violet circles lie in XZ
                    sh.scale = new Vector3(sh.scale.x, sh.scale.z, sh.scale.y);
                    break;
                case "sphere": case "sphere_div": case "sphere_div64": case "sphere_fill":
                    float lat = J.F(s, "sweep_latitude");
                    sh.shapeType = lat > 0 && lat < 2f ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Sphere;
                    if (sh.shapeType == ParticleSystemShapeType.Hemisphere) sh.rotation = new Vector3(-90, 0, 0);
                    sh.radius = r; sh.radiusThickness = type == "sphere_fill" ? 1 : 0;
                    sh.arc = arc > 0 ? Mathf.Min(arc, 360) : 360;
                    break;
                case "cylinder": case "cylinder_fill":
                    sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 0; sh.radius = r;
                    sh.length = Mathf.Max(rad.y * 2, 0.001f); sh.radiusThickness = type == "cylinder_fill" ? 1 : 0;
                    sh.rotation = new Vector3(-90, 0, 0); sh.position = new Vector3(0, -rad.y, 0);
                    sh.shapeType = ParticleSystemShapeType.ConeVolume;
                    sh.scale = Vector3.one;
                    break;
                case "box": case "box_fill":
                    sh.shapeType = ParticleSystemShapeType.Box; sh.scale = rad * 2;
                    sh.boxThickness = type == "box_fill" ? Vector3.zero : Vector3.zero;
                    break;
                case "line": case "line_div":
                    sh.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                    sh.radius = Mathf.Max(J.F(s, "line_length") * 0.5f, 1e-4f);
                    sh.position = new Vector3(-J.F(s, "line_center"), 0, 0); sh.scale = Vector3.one;
                    break;
                case "rectangle":
                    sh.shapeType = ParticleSystemShapeType.Rectangle; sh.rotation = new Vector3(-90, 0, 0);
                    sh.scale = new Vector3(rad.x * 2, rad.z * 2, 1);
                    break;
                default: // point: random directions from the centre
                    sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1e-4f; sh.radiusThickness = 0; sh.scale = Vector3.one;
                    break;
            }
        }

        static void Motion(ParticleSystem ps, Dictionary<string, object> e, float avgLife)
        {
            var main = ps.main;
            var v = J.Obj(e["velocity"]);
            float all = J.F(v, "all_dir"), rnd = J.F(v, "random");
            main.startSpeed = new ParticleSystem.MinMaxCurve(all * (1 - rnd), all);
            float drag = J.F(e, "drag");

            // Designated direction (+ cone spread and random wobble) as a per-particle constant velocity
            // that decays with air resistance like Violet's integrated velocity does.
            Vector3 dir = Mirror(J.Vec(v, "dir"));
            if (dir.sqrMagnitude > 1e-6f) dir.Normalize();
            float speed = J.F(v, "dir_speed"), spread = J.F(v, "spread_deg") * Mathf.Deg2Rad, xz = J.F(v, "xz_random");
            Vector3 axisRnd = J.Vec(v, "axis_random");
            Vector3 baseV = dir * speed * (1 - rnd * 0.5f);
            Vector3 jitter = new Vector3(Mathf.Abs(axisRnd.x) + xz, Mathf.Abs(axisRnd.y), Mathf.Abs(axisRnd.z) + xz)
                             + Vector3.one * (speed * Mathf.Sin(Mathf.Min(spread, Mathf.PI / 2)));
            if (baseV.sqrMagnitude > 1e-8f || jitter.sqrMagnitude > 1e-8f)
            {
                var vol = ps.velocityOverLifetime;
                vol.enabled = true;
                vol.space = ParticleSystemSimulationSpace.Local;
                vol.x = new ParticleSystem.MinMaxCurve(baseV.x - jitter.x, baseV.x + jitter.x);
                vol.y = new ParticleSystem.MinMaxCurve(baseV.y - jitter.y, baseV.y + jitter.y);
                vol.z = new ParticleSystem.MinMaxCurve(baseV.z - jitter.z, baseV.z + jitter.z);
                if (drag > 0.01f)
                {
                    var k = new Keyframe[9];
                    for (int i = 0; i < k.Length; i++) { float t = i / 8f; k[i] = new Keyframe(t, Mathf.Exp(-drag * t * avgLife)); }
                    vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(k));
                }
            }
            if (drag > 0.01f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true; lim.limit = 1000f; lim.drag = drag;
                lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;
            }
            Vector3 g = Mirror(J.Vec(e, "gravity"));
            if (g.sqrMagnitude > 1e-6f)
            {
                var f = ps.forceOverLifetime;
                f.enabled = true;
                f.space = J.B(e, "world_gravity") ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                f.x = g.x; f.y = g.y; f.z = g.z;
            }
        }

        static void Size(ParticleSystem ps, Dictionary<string, object> s)
        {
            var main = ps.main;
            var b = J.Vec(s, "base");
            float bx = b.x > 1e-5f ? b.x : Mathf.Max(b.y, 1e-3f), rnd = J.F(s, "random");
            main.startSize3D=true;
            main.startSizeX=new ParticleSystem.MinMaxCurve(bx*(1-rnd),bx);
            main.startSizeY=new ParticleSystem.MinMaxCurve(b.y*(1-rnd),b.y);
            main.startSizeZ=new ParticleSystem.MinMaxCurve(b.z*(1-rnd),b.z);
            var keys = J.List(s, "keys");
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.separateAxes = true;
            sol.x = new ParticleSystem.MinMaxCurve(1f, KeyCurve(keys, 1, 1f));
            sol.y = new ParticleSystem.MinMaxCurve(1f, KeyCurve(keys, 2, 1f));
            sol.z = new ParticleSystem.MinMaxCurve(1f, KeyCurve(keys, 3, 1f));
        }

        static void Rotation(ParticleSystem ps, Dictionary<string, object> r, Dictionary<string, object> e)
        {
            var main = ps.main;
            Vector3 init = J.Vec(r, "init"), rnd = J.Vec(r, "init_random"), add = J.Vec(r, "per_s"), addRnd = J.Vec(r, "per_s_random");
            bool mesh = !(e["mesh"] is null) || J.Str(e, "billboard") == "plate_xy";
            var rot = ps.rotationOverLifetime;
            if (mesh)
            {
                main.startRotation3D = true;
                float basis=J.Str(e,"billboard")=="plate_xz"?-Mathf.PI*.5f:0;
                main.startRotationX = new ParticleSystem.MinMaxCurve(init.x+basis, init.x + rnd.x+basis);
                main.startRotationY = new ParticleSystem.MinMaxCurve(-init.y - rnd.y, -init.y);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(-init.z - rnd.z, -init.z);
                if (add.sqrMagnitude + addRnd.sqrMagnitude > 1e-8f)
                {
                    rot.enabled = true; rot.separateAxes = true;
                    rot.x = new ParticleSystem.MinMaxCurve(add.x - addRnd.x, add.x + addRnd.x);
                    rot.y = new ParticleSystem.MinMaxCurve(-add.y - addRnd.y, -add.y + addRnd.y);
                    rot.z = new ParticleSystem.MinMaxCurve(-add.z - addRnd.z, -add.z + addRnd.z);
                }
            }
            else
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(init.z, init.z + rnd.z);
                if (Mathf.Abs(add.z) + Mathf.Abs(addRnd.z) > 1e-5f)
                {
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(add.z - addRnd.z, add.z + addRnd.z);
                }
            }
        }

        // Colour0/alpha0 -> Custom1, colour1/alpha1 -> Custom2, as curves over normalised life (HDR).
        static void Colour(ParticleSystem ps, Dictionary<string, object> c)
        {
            float scale = J.F(c, "scale");
            if (scale <= 0) scale = 1;
            var tint0 = J.Col(c, "tint0");
            var tint1 = J.Col(c, "tint1");
            var cd = ps.customData;
            cd.enabled = true;
            Channel(ps, cd, ParticleSystemCustomData.Custom1, J.Obj(c["c0"]), J.Obj(c["a0"]), tint0, scale, true);
            Channel(ps, cd, ParticleSystemCustomData.Custom2, J.Obj(c["c1"]), J.Obj(c["a1"]), tint1, scale, false);
        }

        static void Channel(ParticleSystem ps, ParticleSystem.CustomDataModule cd, ParticleSystemCustomData stream,
                            Dictionary<string, object> col, Dictionary<string, object> alpha, Color tint, float scale, bool first)
        {
            cd.SetMode(stream, ParticleSystemCustomDataMode.Vector);
            cd.SetVectorComponentCount(stream, 4);
            float[] mul = { tint.r * scale, tint.g * scale, tint.b * scale };
            string ct = J.Str(col, "type");
            if (ct == "random" && first)
            {
                // a random pick among the keys: tint the whole particle through the vertex colour
                var keys = J.List(col, "keys");
                var g = new Gradient();
                var gk = new List<GradientColorKey>();
                for (int i = 0; i < keys.Count && i < 8; i++)
                {
                    var k = J.Floats(keys[i]);
                    gk.Add(new GradientColorKey(new Color(k[1], k[2], k[3]), keys.Count > 1 ? i / (keys.Count - 1f) : 0));
                }
                if (gk.Count == 0) gk.Add(new GradientColorKey(Color.white, 0));
                g.SetKeys(gk.ToArray(), new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
                for (int i = 0; i < 3; i++) cd.SetVector(stream, i, new ParticleSystem.MinMaxCurve(mul[i]));
            }
            else
            {
                for (int i = 0; i < 3; i++) cd.SetVector(stream, i, Anim(col, i, mul[i]));
            }
            cd.SetVector(stream, 3, Anim(alpha, 0, tint.a));
        }

        static ParticleSystem.MinMaxCurve Anim(Dictionary<string, object> a, int comp, float mul)
        {
            var value = a["value"];
            float v = value is List<object> l ? (float)(double)l[comp] : (float)(double)value;
            if (J.Str(a, "type") != "keys") return new ParticleSystem.MinMaxCurve(v * mul);
            var keys = J.List(a, "keys");
            if (keys.Count == 0) return new ParticleSystem.MinMaxCurve(v * mul);
            return new ParticleSystem.MinMaxCurve(mul, KeyCurve(keys, comp + 1, 1f));
        }

        // Piecewise-linear curve through [t, x, y, z] keys, holding the end values.
        static AnimationCurve KeyCurve(List<object> keys, int comp, float scale)
        {
            if (keys.Count == 0) return AnimationCurve.Constant(0, 1, scale);
            var pts = new List<Vector2>();
            foreach (var k in keys)
            {
                var f = J.Floats(k);
                float t = Mathf.Clamp01(f[0]);
                if (pts.Count > 0 && t <= pts[pts.Count - 1].x) t = pts[pts.Count - 1].x + 1e-4f;
                pts.Add(new Vector2(t, f[comp] * scale));
            }
            if (pts[0].x > 0) pts.Insert(0, new Vector2(0, pts[0].y));
            if (pts[pts.Count - 1].x < 1) pts.Add(new Vector2(1, pts[pts.Count - 1].y));
            var curve = new AnimationCurve();
            for (int i = 0; i < pts.Count; i++)
            {
                float inT = i > 0 ? (pts[i].y - pts[i - 1].y) / Mathf.Max(1e-4f, pts[i].x - pts[i - 1].x) : 0;
                float outT = i < pts.Count - 1 ? (pts[i + 1].y - pts[i].y) / Mathf.Max(1e-4f, pts[i + 1].x - pts[i].x) : 0;
                curve.AddKey(new Keyframe(pts[i].x, pts[i].y, inT, outT));
            }
            return curve;
        }

        static void Textures(ParticleSystem ps, Material mat, List<object> textures, Func<string, Texture2D> texture, float avgLife)
        {
            Dictionary<string, object> t0 = null, t1 = null, t2 = null;
            foreach (var o in textures)
            {
                var t = J.Obj(o);
                int slot = (int)J.F(t, "slot");
                if (slot == 0) t0 = t; else if (slot == 1 && t1 == null) t1 = t; else if(slot==2 && t2==null)t2=t;
            }
            // Keep sparse native slots in place. Promoting slot 1 to slot 0
            // applies the wrong channel selector (often ONE), exposing whole cards.
            if(t0==null)t0=new Dictionary<string,object>{
                {"name",""},{"div",new List<object>{1d,1d}},
                {"uv_scale",new List<object>{1d,1d}},{"scroll",new List<object>{0d,0d}},
                {"scroll_per_s",new List<object>{0d,0d}}};
            var tex = string.IsNullOrEmpty(J.Str(t0,"name"))?null:texture(J.Str(t0, "name"));
            if (tex) { Wrap(mat,"Main", t0); mat.SetTexture("_MainTex", tex); mat.SetFloat("_MainGrey", Grey(tex) ? 1 : 0); }
            var div = J.Floats(t0["div"]);
            int nx = Mathf.Max(1, (int)div[0]), ny = Mathf.Max(1, (int)div[1]);
            mat.SetVector("_MainSheet",new Vector4(nx,ny,0,0));
            if (nx * ny > 1) SheetAnimation(ps, t0, nx, ny, avgLife);
            UvTransform(mat, "_MainTex", t0, nx * ny > 1);
            var mainScroll=J.Floats(t0["scroll_per_s"]);
            mat.SetVector("_MainScroll",new Vector4(mainScroll[0],mainScroll[1],0,0));
            mat.SetFloat("_MainRotation",J.F(t0,"uv_rotate"));
            if (t1 != null && t1 != t0)
            {
                var sub = texture(J.Str(t1, "name"));
                if (sub)
                {
                    Wrap(mat,"Sub", t1);
                    mat.SetTexture("_SubTex", sub);
                    mat.SetFloat("_SubGrey", Grey(sub) ? 1 : 0);
                    mat.SetFloat("_UseSub", 1);
                    UvTransform(mat, "_SubTex", t1, false);
                    mat.SetFloat("_SubRotation",J.F(t1,"uv_rotate"));
                    var sp = J.Floats(t1["scroll_per_s"]);
                    mat.SetVector("_SubScroll", new Vector4(sp[0], sp[1], 0, 0));
                }
            }
            if(t2!=null && t2!=t0)
            {
                var third=texture(J.Str(t2,"name"));
                if(third)
                {
                    Wrap(mat,"Third",t2);mat.SetTexture("_ThirdTex",third);mat.SetFloat("_ThirdGrey",Grey(third)?1:0);mat.SetFloat("_UseThird",1);
                    UvTransform(mat,"_ThirdTex",t2,false);mat.SetFloat("_ThirdRotation",J.F(t2,"uv_rotate"));
                    var scroll=J.Floats(t2["scroll_per_s"]);mat.SetVector("_ThirdScroll",new Vector4(scroll[0],scroll[1],0,0));
                }
            }
            mat.SetFloat("_MainSphere",J.B(t0,"sphere_map")?1:0);
            if(t1!=null&&t1!=t0)mat.SetFloat("_SubSphere",J.B(t1,"sphere_map")?1:0);
            if(t2!=null&&t2!=t0)mat.SetFloat("_ThirdSphere",J.B(t2,"sphere_map")?1:0);
            TextureMotion(mat,"Main",t0);
            if(t1!=null&&t1!=t0)TextureMotion(mat,"Sub",t1);
            if(t2!=null&&t2!=t0)TextureMotion(mat,"Third",t2);
        }
        static void TextureMotion(Material material,string prefix,Dictionary<string,object> texture)
        {
            var random=texture.ContainsKey("scroll_random")?J.Floats(texture["scroll_random"]):new[]{0f,0f};
            material.SetVector("_"+prefix+"RandomScroll",new Vector4(random[0],random[1],0,0));
            var rate=texture.ContainsKey("uv_scale_per_s")?J.Floats(texture["uv_scale_per_s"]):new[]{0f,0f};
            material.SetVector("_"+prefix+"Motion",new Vector4(rate[0],rate[1],J.F(texture,"uv_rotate_per_s"),0));
        }

        // Violet's v runs top-down and its masks are often one quadrant drawn 2x2 with mirror wrap:
        // scale about the quad's top-left so the mirrored copies meet in the middle.
        static void UvTransform(Material mat, string prop, Dictionary<string, object> t, bool sheet)
        {
            var rep = t.ContainsKey("repeat") ? J.Floats(t["repeat"]) : new[] { 1f, 1f };
            var sc = J.Floats(t["uv_scale"]);
            var off = J.Floats(t["scroll"]);
            float sx = rep[0] * (sheet ? 1 : sc[0]), sy = rep[1] * (sheet ? 1 : sc[1]);
            mat.SetTextureScale(prop, new Vector2(sx, sy));
            mat.SetTextureOffset(prop, new Vector2(off[0], 1 - sy - off[1]));
        }

        // Greyscale masks are imported single channel (red only).
        static bool Grey(Texture2D t) => t.format == TextureFormat.BC4 || t.format == TextureFormat.R8 || t.format == TextureFormat.R16;

        static void Wrap(Material material,string prefix, Dictionary<string, object> t)
        {
            var w = J.Floats(t["wrap"]);
            material.SetVector("_"+prefix+"Wrap",new Vector4((int)WrapOf((int)w[0]),(int)WrapOf((int)w[1]),0,0));
        }

        static TextureWrapMode WrapOf(int w) => w switch
        {
            0 => TextureWrapMode.Mirror,
            1 => TextureWrapMode.Repeat,
            3 => TextureWrapMode.MirrorOnce,
            _ => TextureWrapMode.Clamp,
        };

        static void SheetAnimation(ParticleSystem ps, Dictionary<string, object> t, int nx, int ny, float avgLife)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.uvChannelMask=UnityEngine.Rendering.UVChannelFlags.UV0;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = nx; tsa.numTilesY = ny;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            float n = nx * ny;
            var frames = J.Floats(t["frames"]);
            string pattern = J.Str(t, "pattern");
            float ft = Mathf.Max(J.F(t, "frame_time"), 1f / Fps);
            Func<float, float> f = i => (i + 0.5f) / n;
            if (frames.Length == 0 || pattern == "none")
            {
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(f(frames.Length > 0 ? frames[0] : 0));
                return;
            }
            if (pattern == "random")
            {
                float lo = frames[0], hi = frames[0];
                foreach (var x in frames) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0);
                tsa.startFrame = new ParticleSystem.MinMaxCurve(lo / n, (hi + 0.99f) / n);
                return;
            }
            var curve = new AnimationCurve();
            if (pattern == "fit_life")
            {
                for (int i = 0; i < frames.Length; i++) curve.AddKey(new Keyframe(i / (float)frames.Length, f(frames[i]), 0, 0) { weightedMode = WeightedMode.None });
            }
            else
            {
                // clamp / loop run at a fixed frame period, laid out over the average life
                float step = ft / Mathf.Max(avgLife, 1e-3f);
                int count = 0;
                for (float x = 0; x < 1f && count < 256; x += step, count++)
                {
                    int i = pattern == "loop" ? count % frames.Length : Mathf.Min(count, frames.Length - 1);
                    curve.AddKey(new Keyframe(x, f(frames[i]), 0, 0));
                }
            }
            for (int i = 0; i < curve.length; i++) AnimationUtilityStep(curve, i);
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        // Stepped keys: hold each frame until the next key.
        static void AnimationUtilityStep(AnimationCurve c, int i)
        {
            var k = c[i];
            k.inTangent = float.PositiveInfinity; k.outTangent = float.PositiveInfinity;
            c.MoveKey(i, k);
        }

        static void Render(ParticleSystemRenderer r, Dictionary<string, object> e, List<Mesh> meshes)
        {
            string bb = J.Str(e, "billboard");
            Mesh mesh = e["mesh"] is double d && (int)d < meshes.Count ? meshes[(int)d] : null;
            r.alignment = ParticleSystemRenderSpace.View;
            if (mesh)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = bb == "velocity" ? YToZ(mesh) : mesh;
                r.alignment = bb == "velocity" ? ParticleSystemRenderSpace.Velocity : ParticleSystemRenderSpace.Local;
            }
            else switch (bb)
            {
                case "velocity":
                    r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1f; r.velocityScale = 0f;
                    break;
                case "y_billboard": r.renderMode = ParticleSystemRenderMode.VerticalBillboard; break;
                case "plate_xz": r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; break;
                case "plate_xy":
                    r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = Quad(); r.alignment = ParticleSystemRenderSpace.Local;
                    break;
                default: r.renderMode = ParticleSystemRenderMode.Billboard; break;
            }
            r.sharedMaterial.SetFloat("_PrimitiveStream",mesh?1:0);
            r.sharedMaterial.SetFloat("_MeshParticles", r.renderMode == ParticleSystemRenderMode.Mesh ? 1 : 0);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enableGPUInstancing = false;
            r.sortMode = ParticleSystemSortMode.None;
        }

        static void Blend(Material m, string blend)
        {
            switch (blend)
            {
                case "add": m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One); break;
                case "mul": m.SetFloat("_SrcBlend", (float)BlendMode.DstColor); m.SetFloat("_DstBlend", (float)BlendMode.Zero); break;
                case "screen": m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcColor); break;
                default: m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); break;
            }
            m.renderQueue = blend == "add" ? 3010 : 3000;
        }

        static Mesh BuildMesh(Dictionary<string, object> m)
        {
            var colours=J.List(m,"col");var pos = J.List(m, "pos"); var nrm = J.List(m, "nrm"); var uv = J.List(m, "uv");
            var idx = J.List(m, "idx");
            var v = new Vector3[pos.Count]; var n = new Vector3[pos.Count]; var u = new Vector2[pos.Count]; var c = new Color[pos.Count];
            for (int i = 0; i < pos.Count; i++)
            {
                var p = J.Floats(pos[i]); v[i] = new Vector3(-p[0], p[1], p[2]);
                var q = J.Floats(nrm[i]); n[i] = new Vector3(-q[0], q[1], q[2]);
                var w = J.Floats(uv[i]); u[i] = new Vector2(w[0], 1 - w[1]);
                var col=i<colours.Count?J.Floats(colours[i]):new[]{1f,1f,1f,1f};c[i]=new Color(col[0],col[1],col[2],col[3]); // consumed through native primitive-channel selectors
            }
            var tris = new int[idx.Count];
            for (int i = 0; i + 2 < idx.Count; i += 3) // mirrored, so flip the winding
            {
                tris[i] = (int)(double)idx[i]; tris[i + 1] = (int)(double)idx[i + 2]; tris[i + 2] = (int)(double)idx[i + 1];
            }
            var mesh = new Mesh { name = "violet primitive" };
            mesh.vertices = v; mesh.normals = n; mesh.uv = u; mesh.triangles = tris;
            // Unity's particle COLOR stream is particle tint, not a stable primitive-color transport.
            // Reserve UV2/UV3 for the four native primitive channels; keep that data independent.
            var rg=new List<Vector2>();var ba=new List<Vector2>();var white=new Color32[c.Length];
            foreach(var value in c){rg.Add(new Vector2(value.r,value.g));ba.Add(new Vector2(value.b,value.a));}
            for(int i=0;i<white.Length;i++)white[i]=Color.white;
            // Mesh particle COLOR requires packed UNorm8; Float32 colours bake as raw bytes on this Unity path.
            mesh.colors32=white;mesh.SetUVs(1,rg);mesh.SetUVs(2,ba);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Violet aligns a velocity-facing primitive's +Y with the motion; Unity aligns +Z.
        public static Mesh YToZ(Mesh src)
        {
            var m = UnityEngine.Object.Instantiate(src);
            var v = m.vertices; var n = m.normals;
            for (int i = 0; i < v.Length; i++) { v[i] = new Vector3(v[i].x, -v[i].z, v[i].y); n[i] = new Vector3(n[i].x, -n[i].z, n[i].y); }
            m.vertices = v; m.normals = n; m.RecalculateBounds();
            return m;
        }

        static Mesh Quad()
        {
            if (quad) return quad;
            quad = new Mesh { name = "violet quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return quad;
        }

        public static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        // Euler radians (XYZ order) in Violet space -> Unity rotation under the x mirror.
        public static Quaternion MirrorRot(Vector3 r) =>
            Quaternion.AngleAxis(-r.z * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis(-r.y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(r.x * Mathf.Rad2Deg, Vector3.right);

        // MiniJson accessors.
        public static class J
        {
            public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
            public static List<object> List(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var o) && o is List<object> l ? l : new List<object>();
            public static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var o) && o is string s ? s : "";
            public static float F(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var o) ? o is double x ? (float)x : o is bool b && b ? 1f : 0f : 0f;
            public static bool B(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var o) && (o is bool b ? b : o is double x && x != 0);
            public static float[] Floats(object o)
            {
                var l = (List<object>)o; var f = new float[l.Count];
                for (int i = 0; i < l.Count; i++) f[i] = l[i] is double x ? (float)x : l[i] is bool b && b ? 1f : 0f;
                return f;
            }
            public static Vector3 Vec(Dictionary<string, object> d, string k)
            {
                if (!d.TryGetValue(k, out var o) || !(o is List<object>)) return Vector3.zero;
                var f = Floats(o);
                return new Vector3(f.Length > 0 ? f[0] : 0, f.Length > 1 ? f[1] : 0, f.Length > 2 ? f[2] : 0);
            }
            public static Color Col(Dictionary<string, object> d, string k)
            {
                var f = Floats(d[k]);
                return new Color(f[0], f[1], f[2], f.Length > 3 ? f[3] : 1);
            }
        }
    }
}
