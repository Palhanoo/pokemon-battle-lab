using System;
using System.Collections.Generic;
using UnityEngine;
using J = BattleLab.Native.VioletFx.J;

namespace BattleLab.Native
{
    // Rebuilds Brilliant Diamond effect prefabs (exported by Tools~/hoenngen/bdspfx.py) as ParticleSystems.
    // BDSP is a Unity game, so every module value is the serialized one and maps 1:1 onto the API; its
    // colour combiner (colour0/colour1 from custom data, texture channel expressions) is what the
    // VioletParticle shader already does, so effects reuse that material.
    public static class BdspFx
    {
        // One prefab as a stopped GameObject tree. texture(name) supplies the exported PNGs; baseMaterial uses
        // Hoenn/VioletParticle and is cloned per system.
        public static GameObject Build(string prefabJson, Func<string, Texture2D> texture, Material baseMaterial, Transform parent = null)
        {
            var js = J.Obj(MiniJson.Parse(prefabJson));
            var meshes = new List<Mesh>();
            foreach (var m in J.List(js, "meshes")) meshes.Add(BuildMesh(J.Obj(m)));
            var nodes = J.List(js, "nodes");
            var gos = new GameObject[nodes.Count];
            var systems = new ParticleSystem[nodes.Count];
            GameObject root = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = J.Obj(nodes[i]);
                var go = gos[i] = new GameObject(J.Str(n, "name"));
                int p = (int)J.F(n, "parent");
                if (p >= 0) go.transform.SetParent(gos[p].transform, false);
                else if (root == null) { root = go; if (parent) go.transform.SetParent(parent, false); }
                else go.transform.SetParent(root.transform, false);
                go.transform.localPosition = J.Vec(n, "pos");
                var q = J.Floats(n["rot"]);
                go.transform.localRotation = new Quaternion(q[0], q[1], q[2], q[3]);
                go.transform.localScale = J.Vec(n, "scale");
                if (n.ContainsKey("ps"))
                {
                    systems[i] = go.AddComponent<ParticleSystem>();
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = J.Obj(nodes[i]);
                if (systems[i] == null) continue;
                Configure(systems[i], J.Obj(n["ps"]), systems);
                var r = gos[i].GetComponent<ParticleSystemRenderer>();
                if (n.TryGetValue("r", out var ro) && ro is Dictionary<string, object> rd) Render(r, rd, texture, baseMaterial, meshes);
                else r.enabled = false;
            }
            for (int i = 0; i < nodes.Count; i++)
                if (!J.B(J.Obj(nodes[i]), "active")) gos[i].SetActive(false);
            return root ? root : new GameObject("empty bdsp fx");
        }

        public static void Play(GameObject root)
        {
            foreach (var ps in VioletFx.Roots(root)) ps.Play(true);
        }

        public static void Stop(GameObject root, bool fade)
        {
            foreach (var ps in VioletFx.Roots(root))
                ps.Stop(true, fade ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        static void Configure(ParticleSystem ps, Dictionary<string, object> d, ParticleSystem[] systems)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.05f, J.F(d, "duration"));
            main.loop = J.B(d, "loop");
            main.prewarm = J.B(d, "prewarm");
            main.startDelay = Curve(d["delay"]);
            main.simulationSpeed = J.F(d, "speed") > 0 ? J.F(d, "speed") : 1f;
            main.simulationSpace = J.F(d, "space") >= 1 ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // effects are sized to the Pokémon from above
            var m = J.Obj(d["main"]);
            main.startLifetime = Curve(m["life"]);
            main.startSpeed = Curve(m["speed"]);
            main.startColor = Gradient(m["color"]);
            main.startSize3D = J.B(m, "size3d");
            if (main.startSize3D) { main.startSizeX = Curve(m["size"]); main.startSizeY = Curve(m["size_y"]); main.startSizeZ = Curve(m["size_z"]); }
            else main.startSize = Curve(m["size"]);
            main.startRotation3D = J.B(m, "rot3d");
            if (main.startRotation3D) { main.startRotationX = Curve(m["rot_x"]); main.startRotationY = Curve(m["rot_y"]); main.startRotationZ = Curve(m["rot"]); }
            else main.startRotation = Curve(m["rot"]);
            main.flipRotation = J.F(m, "flip_rot");
            main.maxParticles = Mathf.Clamp((int)J.F(m, "max"), 1, 4000);
            main.gravityModifier = Curve(m["gravity"]);

            var em = ps.emission;
            if (d.TryGetValue("emission", out var eo) && eo is Dictionary<string, object> e)
            {
                em.enabled = true;
                em.rateOverTime = Curve(e["rate"]);
                em.rateOverDistance = Curve(e["rate_dist"]);
                var bursts = new List<ParticleSystem.Burst>();
                foreach (var bo in J.List(e, "bursts"))
                {
                    var b = J.Obj(bo);
                    bursts.Add(new ParticleSystem.Burst(J.F(b, "t"), Curve(b["count"]), (int)J.F(b, "cycles"), Mathf.Max(0.0001f, J.F(b, "interval"))) { probability = J.F(b, "p") });
                }
                em.SetBursts(bursts.ToArray());
            }
            else em.enabled = false;

            var shape = ps.shape;
            if (d.TryGetValue("shape", out var so) && so is Dictionary<string, object> s) Shape(shape, s);
            else shape.enabled = false;

            if (Module(d, "size_life", out var sl))
            {
                var m2 = ps.sizeOverLifetime; m2.enabled = true;
                m2.separateAxes = J.B(sl, "sep");
                if (m2.separateAxes) { m2.x = Curve(sl["x"]); m2.y = Curve(sl["y"]); m2.z = Curve(sl["z"]); }
                else m2.size = Curve(sl["x"]);
            }
            if (Module(d, "rot_life", out var rl))
            {
                var m2 = ps.rotationOverLifetime; m2.enabled = true;
                m2.separateAxes = J.B(rl, "sep");
                if (m2.separateAxes) { m2.x = Curve(rl["x"]); m2.y = Curve(rl["y"]); }
                m2.z = Curve(rl["z"]);
            }
            if (d.TryGetValue("color_life", out var cl) && cl != null) { var m2 = ps.colorOverLifetime; m2.enabled = true; m2.color = Gradient(cl); }
            if (Module(d, "uv", out var uv))
            {
                var m2 = ps.textureSheetAnimation; m2.enabled = true; m2.uvChannelMask=UnityEngine.Rendering.UVChannelFlags.UV0;
                m2.mode = ParticleSystemAnimationMode.Grid;
                var tiles = J.Floats(uv["tiles"]);
                m2.numTilesX = Mathf.Max(1, (int)tiles[0]); m2.numTilesY = Mathf.Max(1, (int)tiles[1]);
                m2.animation = J.F(uv, "anim") >= 1 ? ParticleSystemAnimationType.SingleRow : ParticleSystemAnimationType.WholeSheet;
                m2.rowMode = (ParticleSystemAnimationRowMode)(int)J.F(uv, "row_mode");
                m2.rowIndex = (int)J.F(uv, "row");
                m2.cycleCount = Mathf.Max(1, (int)J.F(uv, "cycles"));
                m2.frameOverTime = Curve(uv["frame"]);
                m2.startFrame = Curve(uv["start"]);
                m2.timeMode = (ParticleSystemAnimationTimeMode)(int)J.F(uv, "time_mode");
                m2.fps = J.F(uv, "fps");
                var flip = J.Floats(uv["flip"]); m2.flipU = flip[0]; m2.flipV = flip[1];
            }
            if (Module(d, "vel", out var v))
            {
                var m2 = ps.velocityOverLifetime; m2.enabled = true;
                m2.x = Curve(v["x"]); m2.y = Curve(v["y"]); m2.z = Curve(v["z"]);
                m2.orbitalX = MatchingCurve(Curve(v["orbitalX"])); m2.orbitalY = MatchingCurve(Curve(v["orbitalY"])); m2.orbitalZ = MatchingCurve(Curve(v["orbitalZ"]));
                m2.orbitalOffsetX = MatchingCurve(Curve(v["orbitalOffsetX"])); m2.orbitalOffsetY = MatchingCurve(Curve(v["orbitalOffsetY"])); m2.orbitalOffsetZ = MatchingCurve(Curve(v["orbitalOffsetZ"]));
                m2.radial = Curve(v["radial"]); m2.speedModifier = Curve(v["speedModifier"]);
                m2.space = J.B(v, "world") ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            }
            if (Module(d, "inherit", out var iv))
            {
                var m2 = ps.inheritVelocity; m2.enabled = true;
                m2.mode = (ParticleSystemInheritVelocityMode)(int)J.F(iv, "mode"); m2.curve = Curve(iv["curve"]);
            }
            if (Module(d, "force", out var f))
            {
                var m2 = ps.forceOverLifetime; m2.enabled = true;
                m2.x = Curve(f["x"]); m2.y = Curve(f["y"]); m2.z = Curve(f["z"]);
                m2.space = J.B(f, "world") ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                m2.randomized = J.B(f, "random");
            }
            if (Module(d, "limit", out var l))
            {
                var m2 = ps.limitVelocityOverLifetime; m2.enabled = true;
                m2.separateAxes = J.B(l, "sep");
                if (m2.separateAxes) { m2.limitX = Curve(l["x"]); m2.limitY = Curve(l["y"]); m2.limitZ = Curve(l["z"]); }
                else m2.limit = Curve(l["mag"]);
                m2.space = J.B(l, "world") ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                m2.dampen = J.F(l, "dampen"); m2.drag = Curve(l["drag"]);
                m2.multiplyDragByParticleSize = J.B(l, "drag_size"); m2.multiplyDragByParticleVelocity = J.B(l, "drag_vel");
            }
            if (Module(d, "noise", out var no))
            {
                var m2 = ps.noise; m2.enabled = true;
                m2.separateAxes = J.B(no, "sep");
                if (m2.separateAxes) { m2.strengthX = Curve(no["strength"]); m2.strengthY = Curve(no["strength_y"]); m2.strengthZ = Curve(no["strength_z"]); }
                else m2.strength = Curve(no["strength"]);
                m2.frequency = J.F(no, "frequency"); m2.damping = J.B(no, "damping");
                m2.octaveCount = Mathf.Clamp((int)J.F(no, "octaves"), 1, 4);
                m2.octaveMultiplier = J.F(no, "octave_mul"); m2.octaveScale = J.F(no, "octave_scale");
                m2.scrollSpeed = Curve(no["scroll"]); m2.positionAmount = Curve(no["pos"]);
                m2.rotationAmount = Curve(no["rot"]); m2.sizeAmount = Curve(no["size"]);
                m2.quality = (ParticleSystemNoiseQuality)(int)J.F(no, "quality");
            }
            if (Module(d, "size_speed", out var ss))
            {
                var m2 = ps.sizeBySpeed; m2.enabled = true;
                m2.separateAxes = J.B(ss, "sep");
                if (m2.separateAxes) { m2.x = Curve(ss["x"]); m2.y = Curve(ss["y"]); m2.z = Curve(ss["z"]); }
                else m2.size = Curve(ss["x"]);
                var r = J.Floats(ss["range"]); m2.range = new Vector2(r[0], r[1]);
            }
            if (Module(d, "rot_speed", out var rs))
            {
                var m2 = ps.rotationBySpeed; m2.enabled = true;
                m2.separateAxes = J.B(rs, "sep");
                if (m2.separateAxes) { m2.x = Curve(rs["x"]); m2.y = Curve(rs["y"]); }
                m2.z = Curve(rs["z"]);
                var r = J.Floats(rs["range"]); m2.range = new Vector2(r[0], r[1]);
            }
            var subs = J.List(d, "subs");
            if (subs.Count > 0)
            {
                var m2 = ps.subEmitters; m2.enabled = true;
                foreach (var so2 in subs)
                {
                    var sd = J.Obj(so2);
                    int node = (int)J.F(sd, "node");
                    if (node < 0 || node >= systems.Length || systems[node] == null || systems[node] == ps) continue;
                    m2.AddSubEmitter(systems[node], (ParticleSystemSubEmitterType)(int)J.F(sd, "type"),
                                     (ParticleSystemSubEmitterProperties)(int)J.F(sd, "props"), J.F(sd, "p"));
                }
            }
            CustomColours(ps, d.TryGetValue("custom", out var co) ? co as Dictionary<string, object> : null);
        }

        // Unity requires matching modes on all three orbital axes. Keep authored
        // curves and random ranges exactly; represent constants as constant curves.
        static ParticleSystem.MinMaxCurve MatchingCurve(ParticleSystem.MinMaxCurve c)
        {
            if(c.mode==ParticleSystemCurveMode.TwoCurves)return c;
            if(c.mode==ParticleSystemCurveMode.Curve)return new ParticleSystem.MinMaxCurve(c.curveMultiplier,c.curve,c.curve);
            float lo=c.mode==ParticleSystemCurveMode.TwoConstants?c.constantMin:c.constant;
            float hi=c.mode==ParticleSystemCurveMode.TwoConstants?c.constantMax:c.constant;
            return new ParticleSystem.MinMaxCurve(1,AnimationCurve.Constant(0,1,lo),AnimationCurve.Constant(0,1,hi));
        }
        static bool Module(Dictionary<string, object> d, string key, out Dictionary<string, object> m)
        {
            m = d.TryGetValue(key, out var o) ? o as Dictionary<string, object> : null;
            return m != null;
        }

        static void Shape(ParticleSystem.ShapeModule shape, Dictionary<string, object> s)
        {
            shape.enabled = true;
            int type = (int)J.F(s, "type");
            // mesh-based shapes need meshes the prefabs don't carry: a small sphere stands in
            shape.shapeType = type == 6 || type == 13 || type == 14 || type == 19 || type == 20 ? ParticleSystemShapeType.Sphere : (ParticleSystemShapeType)type;
            shape.angle = J.F(s, "angle");
            shape.length = J.F(s, "length");
            shape.boxThickness = J.Vec(s, "box_thickness");
            shape.radiusThickness = J.F(s, "radius_thickness");
            shape.donutRadius = J.F(s, "donut");
            shape.position = J.Vec(s, "pos");
            shape.rotation = J.Vec(s, "rot");
            shape.scale = J.Vec(s, "scale");
            shape.radius = Mathf.Max(0.0001f, J.F(s, "radius"));
            shape.radiusMode = (ParticleSystemShapeMultiModeValue)(int)J.F(s, "radius_mode");
            shape.radiusSpread = J.F(s, "radius_spread");
            shape.arc = J.F(s, "arc");
            shape.arcMode = (ParticleSystemShapeMultiModeValue)(int)J.F(s, "arc_mode");
            shape.arcSpread = J.F(s, "arc_spread");
            shape.arcSpeed = Curve(s["arc_speed"]);
            shape.randomDirectionAmount = J.F(s, "random_dir");
            shape.sphericalDirectionAmount = J.F(s, "spherical_dir");
            shape.randomPositionAmount = J.F(s, "random_pos");
            shape.alignToDirection = J.B(s, "align");
        }

        // Colour0/alpha0 -> Custom1, colour1/alpha1 -> Custom2 (what VioletParticle reads), as HDR curves
        // over normalised life. Systems without them draw in their texture colour.
        static void CustomColours(ParticleSystem ps, Dictionary<string, object> custom)
        {
            var cd = ps.customData;
            cd.enabled = true;
            for (int i = 0; i < 2; i++)
            {
                var stream = i == 0 ? ParticleSystemCustomData.Custom1 : ParticleSystemCustomData.Custom2;
                cd.SetMode(stream, ParticleSystemCustomDataMode.Vector);
                cd.SetVectorComponentCount(stream, 4);
                object g = null;
                if (custom != null) custom.TryGetValue("c" + i, out g);
                var fallback = i == 0 ? Color.white : new Color(0, 0, 0, 1);
                for (int c = 0; c < 4; c++) cd.SetVector(stream, c, g != null ? GradientChannel(J.Obj(g), c) : new ParticleSystem.MinMaxCurve(fallback[c]));
            }
        }

        static ParticleSystem.MinMaxCurve GradientChannel(Dictionary<string, object> g, int c)
        {
            int s = (int)J.F(g, "s");
            switch (s)
            {
                case 0: return new ParticleSystem.MinMaxCurve(J.Floats(g["max"])[c]);
                case 2: return new ParticleSystem.MinMaxCurve(J.Floats(g["min"])[c], J.Floats(g["max"])[c]);
                case 3: return new ParticleSystem.MinMaxCurve(1f, ChannelCurve(J.Obj(g["gmin"]), c), ChannelCurve(J.Obj(g["gmax"]), c));
                default: return new ParticleSystem.MinMaxCurve(1f, ChannelCurve(J.Obj(g["gmax"]), c));
            }
        }

        // One channel of a gradient as a linear curve through its keys (fixed-mode gradients step).
        static AnimationCurve ChannelCurve(Dictionary<string, object> g, int c)
        {
            var keys = J.List(g, c < 3 ? "c" : "a");
            bool fixedMode = J.F(g, "mode") >= 1;
            var pts = new List<Vector2>();
            foreach (var k in keys)
            {
                var f = J.Floats(k);
                pts.Add(new Vector2(f[0], c < 3 ? f[1 + c] : f[1]));
            }
            if (pts.Count == 0) return AnimationCurve.Constant(0, 1, c == 3 ? 1 : 0);
            pts.Sort((a, b) => a.x.CompareTo(b.x));
            var curve = new AnimationCurve();
            if (pts[0].x > 0) curve.AddKey(new Keyframe(0, pts[0].y));
            for (int i = 0; i < pts.Count; i++)
            {
                float t = Mathf.Clamp01(pts[i].x);
                if (fixedMode && i > 0) curve.AddKey(new Keyframe(Mathf.Max(0, t - 1e-4f), pts[i - 1].y));
                curve.AddKey(new Keyframe(t, pts[i].y));
            }
            if (pts[pts.Count - 1].x < 1) curve.AddKey(new Keyframe(1, pts[pts.Count - 1].y));
            for (int i = 0; i < curve.length; i++) Linear(curve, i);
            return curve;
        }

        static void Linear(AnimationCurve curve, int i)
        {
            var k = curve[i];
            float inS = i > 0 ? (k.value - curve[i - 1].value) / Mathf.Max(1e-5f, k.time - curve[i - 1].time) : 0;
            float outS = i < curve.length - 1 ? (curve[i + 1].value - k.value) / Mathf.Max(1e-5f, curve[i + 1].time - k.time) : 0;
            k.inTangent = inS; k.outTangent = outS;
            curve.MoveKey(i, k);
        }

        static void Render(ParticleSystemRenderer r, Dictionary<string, object> d, Func<string, Texture2D> texture,
                           Material baseMaterial, List<Mesh> meshes)
        {
            r.enabled = J.B(d, "enabled");
            int mode = (int)J.F(d, "mode");
            r.renderMode = mode == 4 && (!d.ContainsKey("mesh") || J.F(d, "mesh") >= meshes.Count) ? ParticleSystemRenderMode.Billboard : (ParticleSystemRenderMode)mode;
            if (r.renderMode == ParticleSystemRenderMode.Mesh) r.mesh = meshes[(int)J.F(d, "mesh")];
            r.sortMode = (ParticleSystemSortMode)(int)J.F(d, "sort");
            r.minParticleSize = J.F(d, "min_size");
            r.maxParticleSize = Mathf.Max(J.F(d, "max_size"), 1f); // up close in VR a half-screen cap shrinks hits
            r.cameraVelocityScale = J.F(d, "cam_vel");
            r.velocityScale = J.F(d, "vel_scale");
            r.lengthScale = J.F(d, "len_scale");
            r.sortingFudge = J.F(d, "fudge");
            r.normalDirection = J.F(d, "normal_dir");
            r.alignment = (ParticleSystemRenderSpace)(int)J.F(d, "align");
            r.pivot = J.Vec(d, "pivot");
            r.flip = J.Vec(d, "flip");
            r.sortingOrder = (int)J.F(d, "order");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enableGPUInstancing = false;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.Custom2XYZW,
                ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.UV3,
                ParticleSystemVertexStream.AgePercent,ParticleSystemVertexStream.InvStartLifetime,ParticleSystemVertexStream.StableRandomXYZW,ParticleSystemVertexStream.UV4,
            });
            if (!d.TryGetValue("mat", out var mo) || !(mo is Dictionary<string, object> m)) { r.enabled = false; return; }
            var mat = new Material(baseMaterial) { name = r.gameObject.name };
            mat.SetFloat("_ColorMode", J.F(m, "color_mode"));
            mat.SetFloat("_AlphaMode", J.F(m, "alpha_mode"));
            mat.SetFloat("_SrcBlend", J.F(m, "src"));
            mat.SetFloat("_DstBlend", J.F(m, "dst"));
            mat.SetFloat("_Cull", J.F(m, "cull"));
            mat.SetFloat("_MeshParticles", 0);
            mat.SetFloat("_ParticleUvClock",1);
            var sheet=r.GetComponent<ParticleSystem>().textureSheetAnimation;
            mat.SetVector("_MainSheet",new Vector4(sheet.enabled?sheet.numTilesX:1,sheet.enabled?sheet.numTilesY:1,0,0));
            mat.SetFloat("_PrimitiveStream",r.renderMode==ParticleSystemRenderMode.Mesh&&J.B(m,"mesh")?1:0);
            int queue = (int)J.F(d, "queue");
            mat.renderQueue = queue >= 2500 ? queue : 3000;
            Texture(mat, "_MainTex", m, "tex0", texture, "_MainScroll");
            mat.SetFloat("_UseSub", Texture(mat, "_SubTex", m, "tex1", texture, "_SubScroll") ? 1 : 0);
            if(m.TryGetValue("combiner",out var combiner))VioletFx.NativeCombiner(mat,J.Obj(combiner));
            mat.SetFloat("_UseThird",Texture(mat,"_ThirdTex",m,"tex2",texture,"_ThirdScroll")?1:0);
            if(m.TryGetValue("surface",out var surface))
            {
                var sd=J.Obj(surface);VioletFx.Surface(mat,sd);
                if(J.F(sd,"distortion_mode")>0)
                {
                    mat.SetFloat("_BdspDistortion",J.F(sd,"distortion_mode"));
                    var strength=J.Floats(sd["distortion_strength"]);
                    mat.SetVector("_DistortionStrength",new Vector4(strength[0],strength[1],0,0));
                    var points=J.List(sd,"distortion_curve");
                    for(int k=0;k<points.Count;k++){var pt=J.Floats(points[k]);mat.SetVector("_DistortionKey"+k,new Vector4(pt[0],pt[1],0,0));}
                }
            }
            if(m.TryGetValue("render_state",out var state))VioletFx.RenderState(mat,J.Obj(state));
            r.sharedMaterial = mat;
            // the combiner's colour scale and multiply colour act on colour0 / colour1
            float scale = J.F(m, "color_scale");
            var mul = m.ContainsKey("mul") ? J.Floats(m["mul"]) : new[] { 1f, 1f, 1f, 1f };
            if (Mathf.Abs(scale - 1) > 1e-3f || mul[0] < 0.999f || mul[1] < 0.999f || mul[2] < 0.999f || mul[3] < 0.999f)
                ScaleColours(r.GetComponent<ParticleSystem>(), scale > 0 ? scale : 1, mul);
        }

        static void ScaleColours(ParticleSystem ps, float scale, float[] mul)
        {
            var cd = ps.customData;
            for (int i = 0; i < 2; i++)
            {
                var stream = i == 0 ? ParticleSystemCustomData.Custom1 : ParticleSystemCustomData.Custom2;
                for (int c = 0; c < 4; c++)
                {
                    var v = cd.GetVector(stream, c);
                    float k = c < 3 ? scale * (i == 0 ? mul[c] : 1) : (i == 0 ? mul[3] : 1);
                    if (v.mode == ParticleSystemCurveMode.Constant) v.constant *= k;
                    else if (v.mode == ParticleSystemCurveMode.TwoConstants) { v.constantMin *= k; v.constantMax *= k; }
                    else v.curveMultiplier *= k;
                    cd.SetVector(stream, c, v);
                }
            }
        }

        public static bool Texture(Material mat, string prop, Dictionary<string, object> m, string key, Func<string, Texture2D> texture, string scrollProp)
        {
            if (!m.TryGetValue(key, out var to) || !(to is Dictionary<string, object> t)) return false;
            var tex = texture(J.Str(t, "name"));
            if (!tex) return false;
            var wrap = J.Floats(t["wrap"]);
            mat.SetVector(prop.Replace("Tex","Wrap"),new Vector4(wrap[0],wrap[1],0,0));
            mat.SetTexture(prop, tex);
            mat.SetFloat(prop.Replace("Tex","Grey"), Grey(tex) ? 1 : 0);
            var sc = J.Floats(t["scale"]); var off = J.Floats(t["offset"]);
            mat.SetTextureScale(prop, new Vector2(sc[0], sc[1]));
            mat.SetTextureOffset(prop, new Vector2(off[0], off[1]));
            if (scrollProp != null && t.ContainsKey("scroll"))
            {
                var s = J.Floats(t["scroll"]);
                mat.SetVector(scrollProp, new Vector4(s[0], s[1], 0, 0));
            }
            mat.SetFloat(prop.Replace("Tex","UvChannel"),J.F(t,"uv_channel"));
            mat.SetFloat(prop.Replace("Tex","Sphere"),J.B(t,"sphere_map")?1:0);
            mat.SetFloat(prop.Replace("Tex","Rotation"),J.F(t,"rotation"));
            var speed=t.ContainsKey("scale_speed")?J.Floats(t["scale_speed"]):new float[2];
            mat.SetVector(prop.Replace("Tex","Motion"),new Vector4(speed[0],speed[1],J.F(t,"rotation_speed"),0));
            return true;
        }

        static bool Grey(Texture2D t) => t.format == TextureFormat.R8 || t.format == TextureFormat.BC4 || t.format == TextureFormat.Alpha8 || t.format == TextureFormat.R16;

        public static Mesh BuildMesh(Dictionary<string, object> m)
        {
            var mesh = new Mesh { name = "bdsp mesh " + J.Str(m, "name") };
            var v = J.Floats(m["v"]);
            var verts = new Vector3[v.Length / 3];
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
            mesh.vertices = verts;
            if (m.ContainsKey("n"))
            {
                var n = J.Floats(m["n"]);
                var ns = new Vector3[verts.Length];
                for (int i = 0; i < ns.Length && i * 3 + 2 < n.Length; i++) ns[i] = new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]);
                mesh.normals = ns;
            }
            if (m.ContainsKey("uv"))
            {
                var u = J.Floats(m["uv"]);
                var us = new Vector2[verts.Length];
                for (int i = 0; i < us.Length && i * 2 + 1 < u.Length; i++) us[i] = new Vector2(u[i * 2], u[i * 2 + 1]);
                mesh.uv = us;
            }
            // Primitive masks and particle tint are distinct shader inputs. The
            // exporter normalizes UNorm8 colours before packing these UV streams.
            var colors=m.ContainsKey("col")?J.Floats(m["col"]):null;
            var rg=new List<Vector2>();var ba=new List<Vector2>();var white=new Color32[verts.Length];
            for(int i=0;i<verts.Length;i++)
            {
                var c=colors!=null&&i*4+3<colors.Length?new Color(colors[i*4],colors[i*4+1],colors[i*4+2],colors[i*4+3]):Color.white;
                rg.Add(new Vector2(c.r,c.g));ba.Add(new Vector2(c.b,c.a));white[i]=Color.white;
            }
            // Mesh particle COLOR requires packed UNorm8; Float32 colours bake as raw bytes on this Unity path.
            mesh.colors32=white;mesh.SetUVs(1,rg);mesh.SetUVs(2,ba);
            if(m.ContainsKey("uv1"))
            {
                var sourceUv=J.Floats(m["uv1"]);var secondary=new List<Vector2>();
                for(int i=0;i<verts.Length;i++)secondary.Add(new Vector2(sourceUv[i*2],sourceUv[i*2+1]));
                mesh.SetUVs(3,secondary);
            }
            var idx = J.Floats(m["i"]);
            var tris = new int[idx.Length];
            for (int i = 0; i < tris.Length; i++) tris[i] = (int)idx[i];
            mesh.indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.triangles = tris;
            if (!m.ContainsKey("n")) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static ParticleSystem.MinMaxCurve Curve(object o, float mul = 1f)
        {
            if (o is double d) return new ParticleSystem.MinMaxCurve((float)d * mul);
            if (!(o is Dictionary<string, object> c)) return new ParticleSystem.MinMaxCurve(0);
            int s = (int)J.F(c, "s");
            float k = J.F(c, "k") * mul, mn = J.F(c, "m") * mul;
            switch (s)
            {
                case 1: return new ParticleSystem.MinMaxCurve(k, Keys(J.List(c, "max")));
                case 2: return new ParticleSystem.MinMaxCurve(k, Keys(J.List(c, "min")), Keys(J.List(c, "max")));
                case 3: return new ParticleSystem.MinMaxCurve(mn, k);
                default: return new ParticleSystem.MinMaxCurve(k);
            }
        }

        static AnimationCurve Keys(List<object> keys)
        {
            var c = new AnimationCurve();
            foreach (var k in keys)
            {
                var f = J.Floats(k);
                c.AddKey(new Keyframe(f[0], f[1], f[2], f[3]));
            }
            if (c.length == 0) c.AddKey(0, 1);
            return c;
        }

        static ParticleSystem.MinMaxGradient Gradient(object o)
        {
            var g = J.Obj(o);
            switch ((int)J.F(g, "s"))
            {
                case 1: return new ParticleSystem.MinMaxGradient(Grad(J.Obj(g["gmax"])));
                case 2: return new ParticleSystem.MinMaxGradient(J.Col(g, "min"), J.Col(g, "max"));
                case 3: return new ParticleSystem.MinMaxGradient(Grad(J.Obj(g["gmin"])), Grad(J.Obj(g["gmax"])));
                case 4: return new ParticleSystem.MinMaxGradient(Grad(J.Obj(g["gmax"]))) { mode = ParticleSystemGradientMode.RandomColor };
                default: return new ParticleSystem.MinMaxGradient(J.Col(g, "max"));
            }
        }

        static Gradient Grad(Dictionary<string, object> g)
        {
            var ck = new List<GradientColorKey>();
            foreach (var k in J.List(g, "c")) { var f = J.Floats(k); ck.Add(new GradientColorKey(new Color(f[1], f[2], f[3]), f[0])); }
            var ak = new List<GradientAlphaKey>();
            foreach (var k in J.List(g, "a")) { var f = J.Floats(k); ak.Add(new GradientAlphaKey(f[1], f[0])); }
            if (ck.Count == 0) ck.Add(new GradientColorKey(Color.white, 0));
            if (ak.Count == 0) ak.Add(new GradientAlphaKey(1, 0));
            var grad = new Gradient { mode = J.F(g, "mode") >= 1 ? GradientMode.Fixed : GradientMode.Blend };
            grad.SetKeys(ck.ToArray(), ak.ToArray());
            return grad;
        }
    }
}
