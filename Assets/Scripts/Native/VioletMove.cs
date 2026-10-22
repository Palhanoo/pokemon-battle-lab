using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleLab.Native
{
    // Plays an exported move timeline: effect objects attach to the attacker/defender, tween between
    // them and start/stop their emitter sets on Violet's frames. Effect locators (eff_front01 etc.) are
    // approximated from each battler's body centre, radius, height, facing and head bone.
    public class VioletMove : MonoBehaviour, IMoveFx
    {
        public class Battler
        {
            public Transform root, head;
            public float height = 1f, radius = 0.3f, scale = 1f; // height/radius in world metres; scale = size setting
            public Func<Vector3> center;
            public Func<string, Transform> bone; // real effect locator, if the model has it
            public Vector3 Forward => root.forward;
            public Vector3 Center => center != null ? center() : root.position + Vector3.up * (height * 0.5f);
        }

        class Obj
        {
            public string name;
            public GameObject fx;
            public List<Dictionary<string, object>> clips = new List<Dictionary<string, object>>();
            public bool playing;
        }

        readonly List<Obj> objs = new List<Obj>();
        readonly List<Dictionary<string,object>> actorClips=new();
        ZaActorPresentation actorPresentation;
        bool actorLease;
        readonly object actorKey=new();
        public bool ExternalActorClock;
        public void SampleActor(float elapsed){if(actorPresentation && actorLease)actorPresentation.Sample(actorKey,actorClips,elapsed);}
        public Battler attacker, defender;
        public float time, startTime, endTime, lastEmit;
        public float hitTime = -1, attackTime = -1; // timeline seconds: damage trigger on the target, attacker motion
        float effectScale = 1f;
        public bool manualSimulate; // edit mode: advance particles with Simulate instead of the player loop

        public float Now => time;
        public float StartTime => startTime;
        public float HitTime => hitTime;
        public float LastEmit => lastEmit;
        public float EndTime => endTime;
        public bool ManualSimulate { set => manualSimulate = value; }

        public static VioletMove Create(string moveJson, Func<string, string> setJson, Func<string, Texture2D> texture,
                                        Material baseMaterial, Battler atk, Battler def, Func<string, bool> keep = null, int shotCount = -1)
        {
            var go = new GameObject("violet move");
            var mv = go.AddComponent<VioletMove>();
            mv.attacker = atk; mv.defender = def;
            mv.effectScale = Mathf.Sqrt(Mathf.Max(0.05f, atk.scale) * Mathf.Max(0.05f, def.scale));
            var js = VioletFx.J.Obj(MiniJson.Parse(moveJson));
            SetVolleyCount(js,shotCount);
            foreach (var o in VioletFx.J.List(js, "other_clips"))
            {
                var cd = VioletFx.J.Obj(o);
                mv.actorClips.Add(cd);
                string cmd = VioletFx.J.Str(cd, "cmd");
                float st = VioletFx.J.F(cd, "start");
                var p = cd.TryGetValue("params", out var po) ? VioletFx.J.Obj(po) : null;
                if (cmd == "Animation/SetTrigger" && p != null && VioletFx.J.Str(p, "Trigger") == "damage_trigger" && (mv.hitTime < 0 || st < mv.hitTime)) mv.hitTime = st;
                if (cmd == "Title/Battle/AttackMotion" && (mv.attackTime < 0 || st < mv.attackTime)) mv.attackTime = st;
            }
            float first = float.MaxValue, last = 0;
            foreach (var o in VioletFx.J.List(js, "objects"))
            {
                var od = VioletFx.J.Obj(o);
                var obj = new Obj { name = VioletFx.J.Str(od, "name") };
                if (keep != null && !keep(obj.name)) continue;
                var set = setJson(VioletFx.J.Str(od, "set"));
                if (set == null) continue;
                obj.fx = VioletFx.Build(set, texture, baseMaterial, go.transform);
                obj.fx.name = obj.name;
                obj.fx.transform.localScale = Vector3.one * mv.effectScale;
                foreach (var c in VioletFx.J.List(od, "clips"))
                {
                    var cd = VioletFx.J.Obj(c);
                    obj.clips.Add(cd);
                    string cmd = VioletFx.J.Str(cd, "cmd");
                    if (cmd == "Particle/Emit") { first = Mathf.Min(first, VioletFx.J.F(cd, "start")); mv.lastEmit = Mathf.Max(mv.lastEmit, VioletFx.J.F(cd, "start")); }
                    if (cmd == "Particle/Stop" || cmd.StartsWith("GameObject/")) last = Mathf.Max(last, VioletFx.J.F(cd, "end"));
                }
                // size adjustments before scale clips, placement before emission on the same frame
                obj.clips.Sort((a, b) => Order(a).CompareTo(Order(b)));
                mv.objs.Add(obj);
            }
            mv.startTime = first == float.MaxValue ? 0 : Mathf.Max(0, first - 0.1f);
            if (mv.hitTime < 0) mv.hitTime = first == float.MaxValue ? mv.startTime : mv.lastEmit;
            mv.endTime = Mathf.Max(last, mv.startTime) + 1.5f;
            mv.time = mv.startTime;
            if(mv.actorClips.Exists(c=>VioletFx.J.Str(c,"cmd") is "BlendColor/RimBlend" or "BlendColor/ResetRim" or "Title/Character/EnableInvisible"))
            {
                mv.actorPresentation=atk.root.GetComponent<ZaActorPresentation>();
                if(!mv.actorPresentation)mv.actorPresentation=atk.root.gameObject.AddComponent<ZaActorPresentation>();
                mv.actorLease=true;
            }
            return mv;
        }

        // The same decoded carrier serves normal and Plus volleys. Extend or
        // trim its particle emissions before constructing any particle systems.
        public static void SetVolleyCount(Dictionary<string,object> js,int count)
        {
            int original=(int)VioletFx.J.F(js,"volleyCount");
            if(original<1 || count<1 || count==original)return;
            string prefix=VioletFx.J.Str(js,"volleyPrefix");
            float interval=VioletFx.J.F(js,"volleyInterval");
            var objects=VioletFx.J.List(js,"objects");var templates=new List<Dictionary<string,object>>();
            int Index(Dictionary<string,object> o)
            {
                string n=VioletFx.J.Str(o,"name"),stem=n.StartsWith("bullet")?"bullet":n.StartsWith("impact")?"impact":null;
                if(stem==null || !n.StartsWith(stem+prefix))return -1;
                int start=stem.Length+prefix.Length,end=start;while(end<n.Length && char.IsDigit(n[end]))end++;
                return int.TryParse(n.Substring(start,end-start),out int i)?i:-1;
            }
            for(int i=objects.Count-1;i>=0;i--)
            {
                var o=VioletFx.J.Obj(objects[i]);int index=Index(o);
                if(index==original-1)templates.Add(o);
                if(index>=count)objects.RemoveAt(i);
            }
            for(int i=original;i<count;i++)foreach(var template in templates)
            {
                var o=new Dictionary<string,object>(template);string old=VioletFx.J.Str(o,"name");
                string name=old.Replace(prefix+(original-1),prefix+i);o["name"]=name;
                var clips=new List<object>();float delta=(i-original+1)*interval;
                foreach(var item in VioletFx.J.List(template,"clips"))
                {
                    var c=new Dictionary<string,object>(VioletFx.J.Obj(item));
                    c["start"]=VioletFx.J.F(c,"start")+delta;c["end"]=VioletFx.J.F(c,"end")+delta;c["object"]=name;clips.Add(c);
                }
                o["clips"]=clips;objects.Add(o);
            }
        }
        static int Order(Dictionary<string, object> c)
        {
            string cmd = VioletFx.J.Str(c, "cmd");
            int pri = cmd == "Title/Battle/AdjustScaleTarget" ? 0 : cmd.StartsWith("Particle/") ? 2 : 1;
            return Mathf.RoundToInt(VioletFx.J.F(c, "start") * VioletFx.Fps) * 4 + pri;
        }

        // Per-play materials and primitive meshes go with the effect.
        void OnDisable(){ReleaseActor();}
        void ReleaseActor(){if(actorPresentation && actorLease)actorPresentation.Release(actorKey);actorLease=false;}
        void OnDestroy()
        {
            ReleaseActor();
            foreach (var r in GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r.sharedMaterial) Gone(r.sharedMaterial);
                if (r.mesh && r.mesh.name.StartsWith("violet primitive")) Gone(r.mesh);
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
            if(!ExternalActorClock)SampleActor(time);
            foreach (var o in objs)
            {
                foreach (var c in o.clips) Apply(o, c, prev, time);
                if (manualSimulate && o.playing)
                    foreach (var ps in VioletFx.Roots(o.fx)) ps.Simulate(dt, true, false, false);
            }
        }

        void Apply(Obj o, Dictionary<string, object> c, float prev, float now)
        {
            float s = VioletFx.J.F(c, "start"), e = VioletFx.J.F(c, "end");
            if (now < s || prev > Mathf.Max(e, s + 1e-3f)) return;
            bool begins = prev <= s && now >= s;
            float span = e - s;
            float k = span > 1.5f / VioletFx.Fps ? Mathf.Clamp01((now - s) / span) : 1f;
            var p = VioletFx.J.Obj(c["params"]);
            var tr = o.fx.transform;
            switch (VioletFx.J.Str(c, "cmd"))
            {
                case "Particle/Emit":
                    if (begins)
                    {
                        if (manualSimulate)
                            foreach (var ps in VioletFx.Roots(o.fx)) { var em = ps.emission; em.enabled = true; ps.Simulate(0, true, true, false); }
                        else VioletFx.Play(o.fx);
                        o.playing = true;
                    }
                    break;
                case "Particle/Stop":
                    if (begins) VioletFx.Stop(o.fx, !VioletFx.J.B(p, "Kill"));
                    break;
                case "GameObject/AttachTransform":
                {
                    var target=Side(VioletFx.J.Str(p,"Target"));
                    if(target==null)break;
                    string node=VioletFx.J.Str(p,"Node");
                    tr.position=Node(target,node);
                    if(VioletFx.J.B(p,"InherSRT"))
                    {
                        var bone=target.bone?.Invoke(node);
                        tr.rotation=bone?bone.rotation:target.root.rotation;
                    }
                    break;
                }
                case "GameObject/SetTranslation":
                {
                    var target = Side(VioletFx.J.Str(p, "Target"));
                    if (target == null) break;
                    var to = Node(target, VioletFx.J.Str(p, "Node")) + target.root.rotation * VioletFx.Mirror(VioletFx.J.Vec(p, "Translation"));
                    bool tween = span > 1.5f / VioletFx.Fps;
                    if (begins && tween) tweenFrom[o] = tr.position;
                    tr.position = tween && tweenFrom.TryGetValue(o, out var from)
                        ? Vector3.LerpUnclamped(from, to, Ease((int)VioletFx.J.F(p, "EasingType"), k)) : to;
                    break;
                }
                case "GameObject/SetRotationtoTarget":
                {
                    var target = Side(VioletFx.J.Str(p, "Target"));
                    if (target == null) break;
                    var dir = Node(target, VioletFx.J.Str(p, "Node")) - tr.position;
                    if (VioletFx.J.F(p, "AxisType") >= 1) dir.y = 0;
                    if (dir.sqrMagnitude > 1e-6f)
                        tr.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-VioletFx.J.F(p, "OffsetRotateX"), -VioletFx.J.F(p, "OffsetRotateY"), 0);
                    break;
                }
                case "GameObject/SetRotationByTarget":
                {
                    var target = Side(VioletFx.J.Str(p, "Target"));
                    if (target != null) tr.rotation = Quaternion.LookRotation(target.Forward) * Quaternion.Euler(VioletFx.J.Vec(p, "Offset"));
                    break;
                }
                case "GameObject/SetRotation":
                {
                    var r = VioletFx.J.Vec(p, "Rotation");
                    var q = Quaternion.Euler(r.x, -r.y, -r.z);
                    tr.rotation = VioletFx.J.B(p, "isRelative") ? tr.rotation * q : q;
                    break;
                }
                case "GameObject/SetScale":
                {
                    var to = VioletFx.J.Vec(p, "Scale") * (adjust.TryGetValue(o, out var adj) ? adj : effectScale);
                    if (begins) scaleFrom[o] = tr.localScale;
                    tr.localScale = span > 1.5f / VioletFx.Fps && scaleFrom.TryGetValue(o, out var from) ? Vector3.Lerp(from, to, k) : to;
                    break;
                }
                case "Title/Battle/AdjustScaleTarget":
                {
                    var target = Side(VioletFx.J.Str(p, "Target"));
                    // Violet sizes these to the target Pokémon; heights here already include the size setting
                    if (target != null && begins) adjust[o] = Mathf.Clamp(target.height, 0.4f, 2.5f);
                    break;
                }
            }
        }

        readonly Dictionary<Obj, Vector3> tweenFrom = new Dictionary<Obj, Vector3>();
        readonly Dictionary<Obj, Vector3> scaleFrom = new Dictionary<Obj, Vector3>();
        readonly Dictionary<Obj, float> adjust = new Dictionary<Obj, float>();

        Battler Side(string s) => s == "ATK" ? attacker : s == "DEF" ? defender : null;

        // Violet's effect locators, approximated.
        public static Vector3 Node(Battler b, string node)
        {
            float h = Mathf.Max(b.height, 0.2f), r = Mathf.Max(b.radius, 0.15f);
            var bone = b.bone?.Invoke(node);
            if (bone) return bone.position;
            var c = b.Center;
            var f = b.Forward;
            switch (node)
            {
                case "": case "origin": return b.root.position;
                case "eff_center01": return c;
                case "eff_front01": return c + f * (r * 0.9f);
                case "eff_rangeattack01": case "eff_rangeattack02": case "eff_attack01": case "eff_mouth01":
                    return b.head ? b.head.position + f * (r * 0.4f) : c + Vector3.up * (0.2f * h) + f * (r * 0.9f);
                case "eff_headcenter01": case "eff_eye01": case "eff_eye02":
                    return b.head ? b.head.position : c + Vector3.up * (0.3f * h);
                case "eff_overhead01": return b.root.position + Vector3.up * (h * 1.05f);
                default: return c;
            }
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
