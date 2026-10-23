using System.Linq;
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
            public string name,set;
            public GameObject fx;
            public List<Dictionary<string, object>> clips = new List<Dictionary<string, object>>();
            public bool playing,contactStopped;public Vector3 previousPosition;
        }

        readonly List<Obj> objs = new List<Obj>();
        readonly List<Dictionary<string,object>> actorClips=new();
        ZaActorPresentation actorPresentation;
        bool actorLease;
        readonly object actorKey=new();
        public bool ExternalActorClock;
        public bool SteeredDig;
        public bool HiddenAt(float at)=>ZaActorPresentation.Evaluate(actorClips,at).hidden;
        public Vector2 HideWindow
        {
            get {var c=actorClips.Find(x=>VioletFx.J.Str(x,"cmd")=="Title/Character/EnableInvisible");return c==null?Vector2.zero:new Vector2(VioletFx.J.F(c,"start"),VioletFx.J.F(c,"end"));}
        }
        public bool FitUnitBeam;bool beamPrepared;readonly HashSet<Mesh> beamSourceMeshes=new();
        public void SampleActor(float elapsed){if(actorPresentation && actorLease)actorPresentation.Sample(actorKey,actorClips,elapsed);}
        public Battler attacker, defender;
        public float time, startTime, endTime, lastEmit;float previousTime;
        public float hitTime = -1, attackTime = -1; // timeline seconds: damage trigger on the target, attacker motion
        float effectScale = 1f;
        public bool manualSimulate; // edit mode: advance particles with Simulate instead of the player loop

        public string LabMove;
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
            float waveSpeed=VioletFx.J.F(js,"labGroundWaveSpeed");
            if(waveSpeed>0)
            {
                float distance=Vector3.Distance(atk.root.position,def.root.position);
                float oldHit=0;
                foreach(var value in VioletFx.J.List(js,"other_clips"))
                {var c=VioletFx.J.Obj(value);if(VioletFx.J.Str(c,"cmd")=="Animation/SetTrigger")oldHit=VioletFx.J.F(c,"start");}
                float shift=Mathf.Max(.15f,distance/waveSpeed)-4/waveSpeed;
                foreach(var value in VioletFx.J.List(js,"objects"))foreach(var item in VioletFx.J.List(VioletFx.J.Obj(value),"clips"))
                {
                    var c=VioletFx.J.Obj(item);
                    foreach(var key in new[]{"start","end"})if(VioletFx.J.F(c,key)>=oldHit-.001f)c[key]=(double)(VioletFx.J.F(c,key)+shift);
                }
                foreach(var value in VioletFx.J.List(js,"other_clips"))
                {var c=VioletFx.J.Obj(value);if(VioletFx.J.Str(c,"cmd")=="Animation/SetTrigger"){c["start"]=(double)(oldHit+shift);c["end"]=(double)(oldHit+shift+1f/30);}}
            }
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
            // SV authored optional target copies and non-particle transform anchors.
            // This presentation has one selected defender; never stack unused target copies at origin.
            var definitions=VioletFx.J.List(js,"objects").Select(VioletFx.J.Obj).ToArray();
            var placements=definitions.SelectMany(d=>VioletFx.J.List(d,"clips")).Select(VioletFx.J.Obj)
                .Where(c=>VioletFx.J.Str(c,"cmd") is "GameObject/SetTranslation" or "GameObject/AttachTransform")
                .Select(c=>VioletFx.J.Str(VioletFx.J.Obj(c["params"]),"Target")).ToArray();
            string selectedAlias=placements.Contains("DEF")?null:placements.Where(n=>n.StartsWith("DEF")&&n.Length>3).OrderBy(n=>n).FirstOrDefault();
            var existingNames=definitions.Select(d=>VioletFx.J.Str(d,"name")).ToHashSet();
            foreach(var group in mv.actorClips.GroupBy(c=>VioletFx.J.Str(c,"object")))
            {
                if(!placements.Contains(group.Key)||existingNames.Contains(group.Key)||group.Key=="ATK"||group.Key.StartsWith("DEF")||string.IsNullOrEmpty(group.Key))continue;
                var anchor=new GameObject(group.Key);anchor.transform.SetParent(go.transform,false);
                mv.objs.Add(new Obj{name=group.Key,set="",fx=anchor,clips=group.ToList()});
            }
            float first = float.MaxValue, last = 0;
            foreach (var o in VioletFx.J.List(js, "objects"))
            {
                var od = VioletFx.J.Obj(o);
                var numbered=VioletFx.J.List(od,"clips").Select(VioletFx.J.Obj)
                    .Where(c=>VioletFx.J.Str(c,"cmd") is "GameObject/SetTranslation" or "GameObject/AttachTransform")
                    .Select(c=>VioletFx.J.Str(VioletFx.J.Obj(c["params"]),"Target")).Where(n=>n.StartsWith("DEF")&&n.Length>3).ToArray();
                if(numbered.Length>0&&!numbered.Contains(selectedAlias))continue;
                var obj = new Obj { name = VioletFx.J.Str(od, "name"),set=VioletFx.J.Str(od,"set") };
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
                    if (cmd == "Particle/Emit" || cmd == "Particle/EmitLoop") { first = Mathf.Min(first, VioletFx.J.F(cd, "start")); mv.lastEmit = Mathf.Max(mv.lastEmit, VioletFx.J.F(cd, "start")); }
                    if (cmd == "Particle/Stop" || cmd.StartsWith("GameObject/")) last = Mathf.Max(last, VioletFx.J.F(cd, "end"));
                }
                // size adjustments before scale clips, placement before emission on the same frame
                obj.clips.Sort((a, b) => Order(a).CompareTo(Order(b)));
                mv.objs.Add(obj);
            }
            mv.objs.Sort((a,b)=>a.clips.Exists(c=>VioletFx.J.Str(VioletFx.J.Obj(c["params"]),"Target")=="BulletWeakHandle").CompareTo(b.clips.Exists(c=>VioletFx.J.Str(VioletFx.J.Obj(c["params"]),"Target")=="BulletWeakHandle")));
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
            foreach(var mesh in beamSourceMeshes)if(mesh)Gone(mesh);
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
            if(FitUnitBeam&&!beamPrepared)
            {
                beamPrepared=true;
                foreach(var o in objs)if(o.set.Contains("_beam"))foreach(var ps in o.fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();if(!renderer.mesh)continue;
                    var sourceBounds=renderer.mesh.bounds;
                    beamSourceMeshes.Add(renderer.mesh);renderer.mesh=VioletFx.YToZ(renderer.mesh);
                    if(sourceBounds.min.y<-.01f)
                    {
                        var vertices=renderer.mesh.vertices;float length=1.1f/(sourceBounds.size.y*Mathf.Max(.001f,ps.main.startSizeY.constantMax));
                        for(int i=0;i<vertices.Length;i++)vertices[i].z=(vertices[i].z-sourceBounds.min.y)*length;
                        renderer.mesh.vertices=vertices;renderer.mesh.RecalculateBounds();
                    }
                    var main=ps.main;var baseY=main.startSizeY;main.startSizeY=main.startSizeZ;main.startSizeZ=baseY;main.startRotationX=0;main.startRotationZ=main.startRotationY;main.startRotationY=0;
                    var rotation=ps.rotationOverLifetime;rotation.z=rotation.y;rotation.x=new ParticleSystem.MinMaxCurve(0,0);rotation.y=new ParticleSystem.MinMaxCurve(0,0);
                    var size=ps.sizeOverLifetime;var oldY=size.y;size.y=size.z;size.z=oldY;
                }
            }
            float prev = time;previousTime=prev;
            time += dt;
            if(!ExternalActorClock)SampleActor(time);
            foreach (var o in objs)
            {
                if(o.contactStopped)continue;
                o.previousPosition=o.fx.transform.position;
                foreach (var c in o.clips) Apply(o, c, prev, time);
                if(FitUnitBeam&&!o.set.Contains("_beam")&&o.name!="ready")
                {
                    // Native spray assemblies are authored for the cinematic camera. Keep their
                    // whole footprint (particle size, spawn radius and travel) proportional in the lab.
                    var body=o.name.Contains("impact")||o.set.Contains("_end")?defender:attacker;
                    o.fx.transform.localScale=Vector3.one*Mathf.Clamp(body.height*.45f,.18f,.65f);
                }
                if(FitUnitBeam&&o.set.Contains("_beam"))
                {
                    var from=Node(attacker,"eff_rangeattack01");var to=defender.Center;
                    var direction=to-from;var tr=o.fx.transform;
                    tr.position=from;
                    if(direction.sqrMagnitude>.0001f)tr.rotation=Quaternion.LookRotation(direction.normalized);
                    tr.localScale=new Vector3(Mathf.Clamp(attacker.height,.4f,.8f),Mathf.Clamp(attacker.height,.4f,.8f),direction.magnitude/1.1f);
                }
                if(FitUnitBeam&&o.name.StartsWith("bullet")&&o.set.Contains("_end"))
                {
                    o.fx.transform.position=defender.Center;
                    var direction=defender.Center-Node(attacker,"eff_rangeattack01");
                    if(direction.sqrMagnitude>.0001f)o.fx.transform.rotation=Quaternion.LookRotation(direction);
                    o.fx.transform.localScale=Vector3.one*Mathf.Clamp(defender.height*.45f,.18f,.65f);
                }
                if(LabMove=="HYDRO_PUMP"&&!o.set.Contains("_beam")&&o.name!="ready")
                {
                    bool impact=o.name.Contains("impact")||o.set.Contains("_end");
                    float fit=impact?Mathf.Clamp(defender.height*.16f,.075f,.16f):Mathf.Clamp(attacker.height*.12f,.08f,.2f);
                    o.fx.transform.localScale=Vector3.one*fit;
                    // BulletWeakHandle is the beam carrier at the mouth; its impact
                    // assembly belongs at the endpoint, not around the caster/camera.
                    if(impact)o.fx.transform.position=defender.Center;
                }
                if(LabMove=="MEGA_EVOLUTION")
                    o.fx.transform.localScale=Vector3.one*Mathf.Clamp(attacker.height*.2f,.2f,.6f);
                if((LabMove=="BLAZE_KICK"||LabMove=="DRAGON_CLAW")&&o.name=="bullet1")
                {o.fx.transform.position=defender.Center;var facing=defender.Center-attacker.Center;if(facing.sqrMagnitude>.001f)o.fx.transform.rotation=Quaternion.LookRotation(facing);}
                if((LabMove=="MUDDY_WATER"||LabMove=="SURF")&&o.name.StartsWith("bullet"))
                {var f=defender.root.position-attacker.root.position;f.y=0;if(f.sqrMagnitude>.001f)o.fx.transform.rotation=Quaternion.LookRotation(f);}
                if(LabMove=="EARTHQUAKE"&&o.name=="bullet0")
                {o.fx.transform.position=attacker.root.position;o.fx.transform.rotation=Quaternion.LookRotation(attacker.Forward,Vector3.up);}
                if(LabMove=="ICE_BEAM")
                {
                    if(o.name=="ew0058_impact"){o.fx.transform.position=defender.Center;o.fx.transform.localScale=Vector3.one*Mathf.Clamp(defender.height*.4f,.15f,.4f);}
                    if(o.name=="ew0058_charge")o.fx.transform.localScale=Vector3.one*.3f;
                    if(time>3.3f&&(o.name=="ew0058_impact"||o.name=="ew0058_charge"))VioletFx.Stop(o.fx,false);
                }
                if(SteeredDig&&(o.name=="ew0091_attack_out"||o.name=="ew0091_sign"||o.name=="impact0_0"))
                {o.fx.transform.position=attacker.root.position;o.fx.transform.localScale=Vector3.one*Mathf.Clamp(attacker.height*.45f,.3f,1.2f);}
                if (manualSimulate && o.playing)
                    foreach (var ps in VioletFx.Roots(o.fx)) ps.Simulate(dt, true, false, false);
            }
        }

        public void StopProjectileBodies()
        {
            foreach(var o in objs)if(o.name.StartsWith("bullet"))
            {o.contactStopped=true;o.playing=false;VioletFx.Stop(o.fx,false);}
        }
        public bool ProjectileSegment(out Vector3 previous,out Vector3 current)
        {
            previous=current=Vector3.zero;if(FitUnitBeam)return false;
            Obj selected=null;float firstEmit=float.MaxValue;
            foreach(var o in objs)
            {
                if(!o.name.StartsWith("bullet")||!o.playing)continue;
                // Carriers, stationary muzzles and expired tails are not a flight path.
                var travel=o.clips.Find(c=>VioletFx.J.Str(c,"cmd")=="GameObject/SetTranslation"
                    &&VioletFx.J.Str(VioletFx.J.Obj(c["params"]),"Target")=="DEF"
                    &&VioletFx.J.F(c,"end")-VioletFx.J.F(c,"start")>1.5f/VioletFx.Fps);
                if(travel==null||previousTime>VioletFx.J.F(travel,"end"))continue;
                float emit=float.MaxValue;foreach(var clip in o.clips)if(VioletFx.J.Str(clip,"cmd")=="Particle/Emit")emit=Mathf.Min(emit,VioletFx.J.F(clip,"start"));
                // Object dependency sorting is not shot order. Keep the leading active
                // projectile instead of jumping back to each newly emitted child.
                if(emit<firstEmit){firstEmit=emit;selected=o;}
            }
            if(selected==null)return false;
            current=selected.fx.transform.position;previous=previousTime<firstEmit?current:selected.previousPosition;return true;
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
                case "Particle/EmitLoop":
                case "Particle/Emit":
                    if (begins)
                    {
                        if (manualSimulate)
                            foreach (var ps in VioletFx.Roots(o.fx)) { var em = ps.emission; em.enabled = true; ps.Simulate(0, true, true, false); }
                        else VioletFx.Play(o.fx);
                        o.playing = true;
                    }
                    if(VioletFx.J.Str(c,"cmd")=="Particle/EmitLoop"&&now>=e)VioletFx.Stop(o.fx,!VioletFx.J.B(p,"Kill"));
                    break;
                case "Particle/Stop":
                    if (begins) VioletFx.Stop(o.fx, !VioletFx.J.B(p, "Kill"));
                    break;
                case "GameObject/AttachTransform":
                {
                    var target=Side(VioletFx.J.Str(p,"Target"));
                    if(target==null)
                    {
                        if(VioletFx.J.Str(p,"Target")=="BulletWeakHandle")
                        {
                            var carrier=objs.Find(x=>x!=o&&x.name.StartsWith("bullet"));
                            if(carrier!=null){tr.position=carrier.fx.transform.position;tr.rotation=carrier.fx.transform.rotation;}
                        }
                        break;
                    }
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
                case "GameObject/SetRotationFront":
                {
                    string name=VioletFx.J.Str(p,"Target");var target=Side(name);var carrier=objs.Find(x=>x.name==name);
                    var direction=target!=null?target.Forward:carrier!=null?carrier.fx.transform.position-carrier.previousPosition:attacker.Forward;
                    if(VioletFx.J.F(p,"AxisType")>=1)direction.y=0;
                    if(direction.sqrMagnitude>.00001f)tr.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(-VioletFx.J.F(p,"OffsetRotateX"),-VioletFx.J.F(p,"OffsetRotateY"),0);
                    break;
                }
                case "GameObject/DisableCulling":
                    if(begins)foreach(var ps in o.fx.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main=ps.main;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                        // A native hammer can extend far beyond its emitter's small origin bounds.
                        ps.GetComponent<ParticleSystemRenderer>().localBounds=new Bounds(Vector3.zero,Vector3.one*200);
                    }
                    break;
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
                case "Title/Character/SetTargetPosition":
                    tr.position=(SteeredDig?attacker:defender).root.position;
                    break;
                case "Title/Bullet/RotateGroundDirection":
                    // The lab arena has one flat floor; native slope orientation reduces
                    // to horizontal facing here, with no pitch into the ground.
                    var forward=attacker.Forward;forward.y=0;
                    if(forward.sqrMagnitude>.0001f)tr.rotation=Quaternion.LookRotation(forward,Vector3.up);
                    break;
                case "Physics/CastRay":
                    // The battle lab's floor is y=0. Native ray endpoints locate this same surface.
                    tr.position=new Vector3(tr.position.x,0,tr.position.z);
                    break;
                case "Title/Battle/AdjustScale":
                    if(begins){adjust[o]=effectScale*(p.ContainsKey("Scale")?VioletFx.J.F(p,"Scale"):1);tr.localScale=Vector3.one*adjust[o];}
                    break;
                case "GameObject/SetLengthScale":
                {
                    var target=Side(VioletFx.J.Str(p,"Target"));if(target==null)break;
                    float length=Vector3.Distance(tr.position,Node(target,VioletFx.J.Str(p,"Node")))/Mathf.Max(.001f,(p.ContainsKey("BaseLength")?VioletFx.J.F(p,"BaseLength"):1));
                    var scale=tr.localScale;
                    if(VioletFx.J.B(p,"X"))scale.x=length;if(VioletFx.J.B(p,"Y"))scale.y=length;if(VioletFx.J.B(p,"Z"))scale.z=length;
                    tr.localScale=scale;break;
                }
                case "Title/Battle/SetCenterPosition":
                    // Native battle-center effects attach to the flat arena midpoint.
                    tr.position=(attacker.root.position+defender.root.position)*.5f;
                    tr.position=new Vector3(tr.position.x,0,tr.position.z);
                    break;
                case "Title/Battle/AdjustScaleTarget":
                {
                    var target = Side(VioletFx.J.Str(p, "Target"));
                    // Violet sizes these to the target Pokémon; heights here already include the size setting
                    if (target != null && begins)
                    {
                        float previous=adjust.TryGetValue(o,out var old)?old:effectScale;
                        adjust[o]=Mathf.Clamp(target.height,0.4f,2.5f);
                        tr.localScale*=adjust[o]/Mathf.Max(.001f,previous);
                    }
                    break;
                }
            }
        }

        readonly Dictionary<Obj, Vector3> tweenFrom = new Dictionary<Obj, Vector3>();
        readonly Dictionary<Obj, Vector3> scaleFrom = new Dictionary<Obj, Vector3>();
        readonly Dictionary<Obj, float> adjust = new Dictionary<Obj, float>();

        readonly Dictionary<string,Battler> anchorBindings=new();
        Battler Side(string s)
        {
            if(s=="ATK")return attacker;
            if(s=="DEF"||s.StartsWith("DEF")&&int.TryParse(s.Substring(3),out _))return defender;
            if(anchorBindings.TryGetValue(s,out var cached))return cached;
            var anchor=string.IsNullOrEmpty(s)||s=="0"?transform:objs.Find(o=>o.name==s)?.fx.transform;
            if(!anchor)return null;
            var result=new Battler{root=anchor,height=1,radius=.1f,scale=1,center=()=>anchor.position};anchorBindings[s]=result;return result;
        }

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
