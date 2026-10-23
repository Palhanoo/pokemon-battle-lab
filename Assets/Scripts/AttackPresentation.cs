using System;
using System.Collections.Generic;
using System.Linq;
using BattleLab.Native;
using UnityEngine;
namespace BattleLab
{
    // A manually stepped presentation; damage belongs exclusively to BattleLabApp.
    // Effects may trail after body recovery, but never keep the controls locked.
    public sealed class AttackPresentation
    {
        public readonly List<(float at,AnimationClip clip,float speed)> cues=new();
        public IMoveFx Effect { get; private set; }
        public float Time { get; private set; }
        public float HitAt { get; private set; }
        public float FreeAt { get; private set; }
        public float SoundAt { get; private set; }
        public int CueCount => cueIndex;
        readonly LabActor body,target;
        readonly Func<Vector3> aim; readonly Transform aimAnchor; readonly VioletMove.Battler defender;
        Vector3 frozenAim; bool aimFrozen;
        public void FreezeAim(Vector3 point){frozenAim=point;aimFrozen=true;if(aimAnchor)aimAnchor.position=point;}
        Vector3 Destination=>aimAnchor?aimAnchor.position:target.Centre;
        readonly bool projectile,beam,groundWave;bool sustainedStopped;float sustainedStopAt=float.PositiveInfinity;float previousTime,shootAt;Vector3 travelOrigin,travelTarget;bool launched,nativeTravelSeen;
        readonly ZaMove timing;
        readonly Material[] effectMaterials;
        int cueIndex;
        float baseRate=1;
        public bool Alive => Effect is Component component && component && component.gameObject;
        public bool TailDone => !Alive || Time>=Mathf.Min(Effect.EndTime,sustainedStopAt+.5f);
        public static VioletMove.Battler Bind(LabActor actor)=>new VioletMove.Battler{
            root=actor.transform,head=actor.Bone("head"),height=actor.Bounds.size.y,radius=Mathf.Max(actor.Bounds.extents.x,actor.Bounds.extents.z),
            scale=actor.transform.lossyScale.x,center=()=>actor.Centre,bone=actor.Bone};
        public AttackPresentation(LabActor actor,LabActor target,MoveData move,VioletFxCatalog za,BdspFxCatalog bdsp,NativeTiming table,EffectSource source=EffectSource.Auto,Func<Vector3> aim=null,float fieldRadius=4)
        {
            body=actor;this.target=target;projectile=move.shape=="Projectile"||move.shape=="Beam";beam=move.shape=="Beam";groundWave=move.id=="MUDDY_WATER"||move.id=="SURF";
            this.aim=aim;defender=Bind(target);
            if(aim!=null)
            {
                aimAnchor=new GameObject("Attack aim point").transform;aimAnchor.position=aim();aimAnchor.rotation=actor.transform.rotation;
                defender=new VioletMove.Battler{root=aimAnchor,height=1,radius=.35f,scale=1,center=()=>aimAnchor.position,bone=_=>null};
            }
            if((source==EffectSource.BDSP||source==EffectSource.Auto&&actor.entry.source=="BDSP"||!za.Has(move.id))&&bdsp.Has(move.id))
            {
                var fx=bdsp.Play(move.id,Bind(actor),defender);Effect=fx;
                if(!fx)throw new InvalidOperationException("Missing BDSP effects: "+move.id);
                fx.time=0;HitAt=Mathf.Max(.1f,fx.HitTime);
                var clip=actor.entry.Role(move.physical?"attack":"special");
                float at=Mathf.Max(0,fx.attackTime);SoundAt=at;
                FreeAt=SplitPlan(actor.entry,clip,at,HitAt,cues);
            }
            else
            {
                timing=table.Get(move.id)??throw new InvalidOperationException("Missing native timing: "+move.id);
                var fx=za.Play(move.id,Bind(actor),defender,move.id=="HYDRO_PUMP" ? name=>!name.Contains("0c0") : null);Effect=fx;
                if(fx && (move.id=="HYDRO_PUMP" || move.id=="PSYBEAM" || move.id=="ICE_BEAM"))fx.FitUnitBeam=true;
                if(!fx)throw new InvalidOperationException("Missing ZA effects: "+move.id);
                fx.LabMove=move.id;fx.time=0;
                if(za.OnMoveClock(move.id))
                {
                    HitAt=Mathf.Max(ZaTimeline.ShootAt(timing),fx.HitTime);SoundAt=ZaTimeline.AttackAt(timing);
                    FreeAt=ZaTimeline.Plan(timing,actor.entry,move.physical?"Physical":"Special",cues);
                }
                else
                {
                    // SV exports already contain their contact time, without ZA charge holds.
                    HitAt=Mathf.Max(.1f,fx.HitTime);SoundAt=Mathf.Max(0,fx.attackTime);
                    FreeAt=SplitPlan(actor.entry,actor.entry.Role(move.physical?"attack":"special"),SoundAt,HitAt,cues);
                }
            }
            FreeAt=Mathf.Max(FreeAt,HitAt+.15f);
            // Native Fire Spin is a lingering field. The lab currently resolves one hit,
            // so end its emission after contact instead of retaining a 13-second field.
            if(move.id=="FIRE_SPIN")sustainedStopAt=HitAt+.8f;
            shootAt=timing==null?Mathf.Max(0,SoundAt):ZaTimeline.ShootAt(timing);
            shootAt=Mathf.Min(shootAt,HitAt-.05f);
            effectMaterials=Effect.gameObject.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.HasProperty("_EffectTime")).Distinct().ToArray();
            ApplyReadability(Effect.gameObject,move.id);
            if(move.id=="EARTHQUAKE")
            {
                if(Effect is BdspMove quake)quake.FieldRadius=fieldRadius;
                else foreach(var t in Effect.gameObject.GetComponentsInChildren<Transform>(true))
                    if(t.parent&&t.parent.parent==Effect.gameObject.transform&&!t.GetComponent<ParticleSystem>()&&t.parent.name!="ready")t.localScale*=fieldRadius/4;
            }
            Effect.ManualSimulate=true;Step(0);
        }
        public static void ApplyReadability(GameObject root,string move)
        {
            // Scale the presentation's inner effect root, leaving native attachment and
            // travel transforms intact. Restored layers otherwise dominate a close camera.
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.parent&&t.parent.parent==root.transform&&!t.GetComponent<ParticleSystem>())
                {
                    if(move=="BLAZE_KICK"&&t.parent.name=="bullet1")t.localScale*=.3f;
                    if(move=="DRAGON_CLAW"&&t.parent.name=="bullet1")t.localScale*=.28f;
                    if(root.GetComponent<VioletMove>()&&move=="SURF"&&t.parent.name!="ready")t.localScale*=.35f;
                    if(root.GetComponent<VioletMove>()&&move=="MUDDY_WATER"&&t.parent.name!="ready")t.localScale*=t.parent.name=="bullet0_1"?.2f:.32f;
                    if(root.GetComponent<VioletMove>()&&move=="EARTHQUAKE"&&t.parent.name!="ready")t.localScale*=.22f;
                    if(move=="ROCK_SLIDE"&&t.parent.name=="ew0157_line")t.localScale*=.4f;
                    if(move=="FIRE_SPIN"&&(t.parent.name=="bullet0"||t.parent.name=="bullet0_1"))t.localScale*=.3f;
                    if(move=="SHADOW_BALL"&&(t.parent.name=="ew0247_impact"||t.parent.name=="impact0_0"))t.localScale*=.3f;
                }
            }
            foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                float scale=1,opacity=1;
                bool beam=(move=="HYDRO_PUMP"||move=="PSYBEAM")&&ps.transform.parent.name.Contains("_beam");
                if(ps.name=="glow"){scale=beam?1:.3f;opacity=.5f;}
                if(root.GetComponent<BdspMove>()&&move=="ICE_BEAM"&&(ps.name=="shock"||ps.name=="hit_ring"))scale=.3f;
                if(move=="CLOSE_COMBAT")
                {
                    // Keep the authored backdrop and ink shapes readable around
                    // a directly controlled fighter in this close battle camera.
                    if(ps.name=="base"){scale=.5f;opacity=.22f;}
                    else if(ps.name.StartsWith("shock_")){scale=.6f;}
                    else if(ps.name.StartsWith("wind")||ps.name.StartsWith("trail")){scale=.45f;opacity=.45f;}
                    else if(ps.transform.parent.name.Contains("warp")){scale=.55f;opacity=.45f;}
                }
                var main=ps.main;
                if(root.GetComponent<VioletMove>()&&move=="MUDDY_WATER"&&ps.name=="nami")
                    {ps.transform.localRotation*=Quaternion.Euler(180,180,0);ps.transform.localScale*=.5f;}
                if(root.GetComponent<VioletMove>()&&move=="EARTHQUAKE"&&(ps.name=="ring_main"||ps.name=="ring_main2"||ps.name=="ring_wave"))
                {
                    main.startRotationX=0;var p=ps.transform.localPosition;p.y=0;ps.transform.localPosition=p;
                    main.startSizeYMultiplier*=.15f;
                }
                if(main.startSize3D){main.startSizeXMultiplier*=scale;main.startSizeYMultiplier*=scale;main.startSizeZMultiplier*=scale;}
                else main.startSizeMultiplier*=scale;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();
                if(renderer&&renderer.sharedMaterial&&renderer.sharedMaterial.HasProperty("_ReadabilityAlpha"))renderer.sharedMaterial.SetFloat("_ReadabilityAlpha",opacity);
            }
        }
        // Use each body's matching family, including PLA's rangeattack01_start/loop/end.
        public static float SplitPlan(ModelEntry entry,AnimationClip clip,float at,float hit,List<(float at,AnimationClip clip,float speed)> list)
        {
            list.Add((at,clip,1));float finish=at+(clip?clip.length:0);
            if(clip&&clip.name.EndsWith("_start"))
            {
                var loop=entry.Named(clip.name.Replace("_start","_loop"));var end=entry.Named(clip.name.Replace("_start","_end"));
                float release=Mathf.Max(finish,hit);
                if(loop&&release>finish)list.Add((finish,loop,1));
                if(end){list.Add((release,end,1));finish=release+end.length;}else finish=release;
            }
            list.Add((finish,entry.battle??entry.idle,1));return Mathf.Max(finish,hit+.15f);
        }
        public void Step(float dt,bool bodyOwned=true)
        {
            previousTime=Time;Time+=dt;
            if(aimAnchor)aimAnchor.position=aimFrozen?frozenAim:aim();
            if(!launched&&Time>=shootAt){launched=true;travelOrigin=body.Centre+body.transform.forward*.25f;travelTarget=Destination; if(aim!=null&&!beam)FreezeAim(travelTarget);}
            if(bodyOwned&&body)
            {
                while(cueIndex<cues.Count&&cues[cueIndex].at<=Time+.0001f)
                {var cue=cues[cueIndex++];baseRate=cue.speed;body.Play(cue.clip,ClipPlayback.Loops(cue.clip),baseRate);}
                body.Rate(baseRate*(timing==null?1:ZaTimeline.MasterSpeed(timing,Time)));
            }
            if(Alive){Effect.Step(dt);
                if(!sustainedStopped&&Time>=sustainedStopAt){sustainedStopped=true;VioletFx.Stop(Effect.gameObject,false);}
                foreach(var material in effectMaterials)if(material)material.SetFloat("_EffectTime",Time);}
        }
        public bool Travel(out Vector3 previous,out Vector3 current)
        {
            previous=current=Vector3.zero;
            if(!projectile||!Alive||Time<shootAt||previousTime>HitAt)return false;
            if(beam){previous=VioletMove.Node(Bind(body),"eff_rangeattack01");current=Vector3.Lerp(previous,Destination,Mathf.Clamp01((Time-shootAt)/Mathf.Max(.01f,Mathf.Min(.28f,HitAt-shootAt))));return true;}
            if(Effect is VioletMove native && native.ProjectileSegment(out previous,out current))
            {nativeTravelSeen=true;if(groundWave){previous.y+=.55f;current.y+=.55f;}return true;}
            if(nativeTravelSeen)return false; // Expired native paths must not become synthetic caster-to-target shots.
            float duration=Mathf.Max(.05f,HitAt-shootAt);
            previous=Vector3.Lerp(travelOrigin,travelTarget,Mathf.Clamp01((previousTime-shootAt)/duration));
            current=Vector3.Lerp(travelOrigin,travelTarget,Mathf.Clamp01((Time-shootAt)/duration));return true;
        }
        public void StopProjectileAtContact()
        {
            // Keep the authored impact layers alive; a hit is not a clash cancellation.
            if(Effect is VioletMove native)native.StopProjectileBodies();
        }
        public void BlockEffects(){Dispose();}
        public void Dispose()
        {if(Alive)UnityEngine.Object.Destroy(Effect.gameObject);Effect=null;if(aimAnchor)UnityEngine.Object.Destroy(aimAnchor.gameObject);}
    }
}
