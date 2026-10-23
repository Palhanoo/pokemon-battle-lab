using System;
using System.Collections.Generic;
using UnityEngine;
using J = BattleLab.Native.VioletFx.J;

namespace BattleLab.Native
{
    // A move effect playing on a timeline between two battlers (Violet or Brilliant Diamond).
    public interface IMoveFx
    {
        float Now { get; }
        float StartTime { get; }
        float HitTime { get; }
        float LastEmit { get; }
        float EndTime { get; }
        bool ManualSimulate { set; }
        void Step(float dt);
        GameObject gameObject { get; }
    }

    // Plays an exported Brilliant Diamond move sequence: each group creates a particle prefab (or a stand-in
    // for an effect model) and the sequence's commands place, tween, scale, turn and attach it relative to
    // the battlers' effect locators. Sequence distances are centimetres at BDSP's battle size; effects are
    // scaled to the Pokémon they sit on.
    public class BdspMove : MonoBehaviour, IMoveFx
    {
        const float RefHeight = 1.1f; // a BDSP battler's on-screen height the effects were sized for

        class Group
        {
            public int grp;
            public bool particle;
            public string file;
            public GameObject fx;public BdspModel model;public float born;public bool flip;
            public Transform tr;
            public List<Dictionary<string, object>> cmds = new();
            public VioletMove.Battler anchor;     // the battler the group was placed on (sets its size)
            public Group followGroup; public Vector3 followOffset;
            public VioletMove.Battler followPoke; public int followNode;
            public bool alive, playing;
            public float baseScale = 1f;
            public Vector3 scale = Vector3.one;
            public Vector3 tweenFrom; public Quaternion rotFrom; public Vector3 scaleFrom;
        }

        readonly List<Group> groups = new();
        public VioletMove.Battler attacker, defender;
        public float time, startTime, endTime, lastEmit, hitTime = -1, attackTime = -1;
        public bool manualSimulate;
        static Dictionary<int, string> locators;

        public float Now => time;
        public float StartTime => startTime;
        public float HitTime => hitTime;
        public float LastEmit => lastEmit;
        public float EndTime => endTime;
        public bool ManualSimulate { set => manualSimulate = value; }

        public static BdspMove Create(string moveJson, Func<string, string> prefabJson, Func<string, Texture2D> texture,
                                      Material baseMaterial, Dictionary<int, string> locatorNames, VioletMove.Battler atk, VioletMove.Battler def)
        {
            locators = locatorNames;
            var go = new GameObject("bdsp move");
            var mv = go.AddComponent<BdspMove>();
            mv.attacker = atk; mv.defender = def;
            var js = J.Obj(MiniJson.Parse(moveJson));
            float first = float.MaxValue, last = 0;
            foreach (var go2 in J.List(js, "groups"))
            {
                var gd = J.Obj(go2);
                var g = new Group { grp = (int)J.F(gd, "grp"), particle = J.Str(gd, "kind") == "particle" };
                foreach (var c in J.List(gd, "cmds")) g.cmds.Add(J.Obj(c));
                var holder = new GameObject(J.Str(gd, "name"));
                holder.transform.SetParent(go.transform, false);
                g.tr = holder.transform;
                foreach (var c in g.cmds)
                {
                    string cmd = J.Str(c, "cmd");
                    if (cmd != "ParticleCreate" && cmd != "ModelCreate") continue;
                    float s = J.F(c, "start"), e = J.F(c, "end");
                    {
                        g.file = J.Str(J.Obj(c["args"]), "file");
                        var json = prefabJson(g.file);
                        if (json == null) continue;
                        g.fx = cmd=="ModelCreate"?BdspModel.Build(json,texture,baseMaterial,holder.transform):BdspFx.Build(json, texture, baseMaterial, holder.transform);
                        g.model=g.fx.GetComponent<BdspModel>();g.born=s;
                        first = Mathf.Min(first, s); mv.lastEmit = Mathf.Max(mv.lastEmit, s);
                    }
                    last = Mathf.Max(last, e);
                }
                holder.SetActive(false);
                mv.groups.Add(g);
            }
            mv.attackTime = js.TryGetValue("attack", out var a) && a is double ad ? (float)ad : -1;
            mv.hitTime = js.TryGetValue("hit", out var h) && h is double hd ? (float)hd : -1;
            mv.startTime = first == float.MaxValue ? 0 : Mathf.Max(0, first - 0.1f);
            if (mv.hitTime < mv.startTime) mv.hitTime = first == float.MaxValue ? mv.startTime : mv.lastEmit;
            foreach(var g in mv.groups)if(g.model)g.model.FitContact(Mathf.Max(0,mv.hitTime-g.born));
            mv.endTime = Mathf.Max(last, mv.startTime) + 1.5f;
            mv.time = mv.startTime;
            return mv;
        }

        void OnDestroy()
        {
            foreach (var r in GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r.sharedMaterial) Gone(r.sharedMaterial);
                if (r.mesh && r.mesh.name.StartsWith("bdsp mesh")) Gone(r.mesh);
            }
        }

        static void Gone(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void Update()
        {
            if (manualSimulate) return;
            Step(Time.deltaTime);
            if (time > endTime) Destroy(gameObject);
        }

        public void Step(float dt)
        {
            if(!attacker?.root || !defender?.root)
            {
                enabled=false;gameObject.SetActive(false);
                if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
                return;
            }
            float prev = time;
            time += dt;
            foreach (var g in groups)
            {
                foreach (var c in g.cmds) Apply(g, c, prev, time);
                Follow(g);
                if(g.alive&&g.model)g.model.Sample(time-g.born);
                if (manualSimulate && g.playing && g.fx)
                    foreach (var ps in VioletFx.Roots(g.fx)) ps.Simulate(dt, true, false, false);
            }
        }

        void Apply(Group g, Dictionary<string, object> c, float prev, float now)
        {
            float s = J.F(c, "start"), e = J.F(c, "end");
            if (now < s || prev > Mathf.Max(e, s + 1e-3f)) return;
            bool begins = prev <= s && now >= s;
            float span = e - s;
            bool tween = span > 1.5f / 30f;
            float k = tween ? Mathf.Clamp01((now - s) / span) : 1f;
            var p = J.Obj(c["args"]);
            switch (J.Str(c, "cmd"))
            {
                case "ParticleCreate":
                case "ModelCreate":
                    if (begins && !g.alive)
                    {
                        g.alive = true;g.born=s;
                        g.tr.gameObject.SetActive(true);
                        if (g.fx)
                        {
                            if (manualSimulate) foreach (var ps in VioletFx.Roots(g.fx)) ps.Simulate(0, true, true, false);
                            else BdspFx.Play(g.fx);
                            g.playing = true;
                        }
                    }
                    if (now >= e && prev < e && g.alive && span > 0) End(g);
                    break;
                case "ModelDelete":
                case "ParticleStop":
                    if (begins) End(g);
                    break;
                case "ParticleMoveRelativePoke":
                case "ModelMoveRelativePoke":
                {
                    var b = Poke((int)J.F(p, "trg"));
                    if (b == null) break;
                    if (begins) { g.anchor ??= b; g.flip=J.B(p,"isFlip");ApplyScale(g); g.followPoke = null; g.followGroup = null; }
                    var offset=Offset(b,p,"pos",J.B(p,"isRotPos"));
                    if(g.model)offset=Vector3.ClampMagnitude(offset,b.height*.25f);
                    var to = Node(b, (int)J.F(p, "node")) + offset;
                    float rate = p.ContainsKey("rate") ? J.F(p, "rate") / 100f : 1f;
                    if (begins) g.tweenFrom = g.tr.position;
                    g.tr.position = tween ? Vector3.LerpUnclamped(g.tweenFrom, to, Ease((int)J.F(p, "move"), k) * rate) : to;
                    if (J.B(p, "isRot") && begins) g.tr.rotation = b.root.rotation;
                    break;
                }
                case "ParticleMovePosition":
                case "ModelMovePosition":
                {
                    var off = Offset(g.anchor ?? attacker, p, "pos", true);
                    if (begins) g.tweenFrom = g.tr.position;
                    var to = J.B(p, "relative") ? g.tweenFrom + off : Middle() + off;
                    g.tr.position = tween ? Vector3.LerpUnclamped(g.tweenFrom, to, Ease((int)J.F(p, "move"), k)) : to;
                    break;
                }
                case "ParticleMoveSpecialPos":
                case "ModelMoveSpecialPos":
                {
                    g.anchor ??= attacker;
                    if (begins) { ApplyScale(g); g.tweenFrom = g.tr.position; }
                    var to = Middle() + Offset(attacker, p, "ofs", true);
                    g.tr.position = tween ? Vector3.LerpUnclamped(g.tweenFrom, to, Ease((int)J.F(p, "move"), k)) : to;
                    if (J.B(p, "isRot") && begins) g.tr.rotation = attacker.root.rotation;
                    break;
                }
                case "ParticleScale":
                {
                    var v = J.Vec(p, "scale");
                    if (begins) g.scaleFrom = g.scale;
                    var to = J.B(p, "relative") ? Vector3.Scale(g.scaleFrom, v) : v;
                    g.scale = tween ? Vector3.Lerp(g.scaleFrom, to, Ease((int)J.F(p, "move"), k)) : to;
                    ApplyScale(g);
                    break;
                }
                case "ParticleRotate":
                case "ParticleRotateOffset":
                {
                    var q = Quaternion.Euler(J.Vec(p, "scale"));
                    if (begins) g.rotFrom = g.tr.rotation;
                    var to = J.B(p, "relative") || J.Str(c, "cmd") == "ParticleRotateOffset" ? g.rotFrom * q : (g.anchor ?? attacker).root.rotation * q;
                    g.tr.rotation = tween ? Quaternion.SlerpUnclamped(g.rotFrom, to, Ease((int)J.F(p, "move"), k)) : to;
                    break;
                }
                case "ParticleRotatePoke":
                {
                    var b = Poke((int)J.F(p, "dirPoke"));
                    if (b == null) break;
                    var dir = Node(b, (int)J.F(p, "node")) - g.tr.position;
                    if (J.F(p, "vertical") < 0.5f) dir.y = 0;
                    if (dir.sqrMagnitude > 1e-6f) g.tr.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, J.F(p, "ofs"), 0);
                    break;
                }
                case "ParticleFollowPoke":
                    if (begins)
                    {
                        g.followPoke = J.B(p, "isEnable") ? Poke((int)J.F(p, p.ContainsKey("trg") ? "trg" : "pos")) : null;
                        g.followNode = (int)J.F(p, "node");
                        g.followOffset = J.Vec(p, "posOfs") * 0.01f;
                        if (g.followPoke != null) { g.anchor ??= g.followPoke; ApplyScale(g); }
                    }
                    break;
                case "ParticleFollowModel":
                case "DprParticleFollowModel":
                case "DprModelFollowModel":
                case "ParticleMoveRelativeModel":
                case "DprParticleMoveRelativeModel":
                    if (begins)
                    {
                        var other = groups.Find(x => x.grp == (int)J.F(p, "grpNo") && x != g);
                        if (other == null) break;
                        g.anchor ??= other.anchor;
                        ApplyScale(g);
                        if (J.Str(c, "cmd").Contains("Follow")) { g.followGroup = other; g.followOffset = J.Vec(p, "pos") * 0.01f; }
                        else g.tr.position = other.tr.position;
                    }
                    break;
            }
        }

        void End(Group g)
        {
            if (!g.alive) return;
            g.alive = false;
            if(g.model){g.playing=false;g.tr.gameObject.SetActive(false);}
            else if (g.fx) BdspFx.Stop(g.fx, true); // let live particles finish
            else g.tr.gameObject.SetActive(false);
        }

        void Follow(Group g)
        {
            if (g.followGroup != null) g.tr.position = g.followGroup.tr.position + g.followGroup.tr.rotation * (g.followOffset * Size(g.anchor));
            else if (g.followPoke != null) g.tr.position = Node(g.followPoke, g.followNode) + g.followPoke.root.rotation * (g.followOffset * Size(g.followPoke));
        }

        void ApplyScale(Group g) => g.tr.localScale = Vector3.Scale(g.scale,new Vector3(g.flip?-1:1,1,1)) * Size(g.anchor ?? attacker);

        // Effects were made for BDSP's battle size; a small Pokémon gets a smaller burst.
        static float Size(VioletMove.Battler b) => b == null ? 1f : Mathf.Clamp(b.height / RefHeight, 0.45f, 1.8f) * Mathf.Max(0.3f, b.scale);

        Vector3 Offset(VioletMove.Battler b, Dictionary<string, object> p, string key, bool rotate)
        {
            var v = J.Vec(p, key) * 0.01f * Size(b);
            return rotate ? b.root.rotation * v : v;
        }

        VioletMove.Battler Poke(int trg) => trg == 0 ? attacker : trg == 1 ? defender : null;

        Vector3 Middle() => (attacker.root.position + defender.root.position) * 0.5f;

        // A sequence node is an index into the Pokémon's effect locators; models without them use
        // the same approximations as the Violet effects.
        static Vector3 Node(VioletMove.Battler b, int node)
        {
            if (locators != null && locators.TryGetValue(node, out var name))
            {
                var bone = b.bone?.Invoke(name);
                if (bone) return bone.position;
            }
            return VioletMove.Node(b, node switch
            {
                0 or 25 or 26 or 27 or 28 => "origin",
                5 or 41 => "eff_headcenter01",
                7 or 51 => "eff_mouth01",
                >= 8 and <= 12 => "eff_attack01",
                17 or 18 => "eff_eye01",
                6 or 33 or >= 35 and <= 38 => "eff_front01",
                57 => "eff_overhead01",
                58 or 59 => "eff_rangeattack01",
                _ => "eff_center01",
            });
        }

        static float Ease(int type, float t) => type switch
        {
            1 => t * t,
            2 => 1 - (1 - t) * (1 - t),
            3 => t * t * (3 - 2 * t),
            _ => t,
        };
    }
}
