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
        readonly bool projectile;bool sustainedStopped;float sustainedStopAt=float.PositiveInfinity;float previousTime,shootAt;Vector3 travelOrigin,travelTarget;bool launched;
        readonly ZaMove timing;
        readonly Material[] effectMaterials;
        int cueIndex;
        float baseRate=1;
        public bool Alive => Effect is Component component && component && component.gameObject;
        public bool TailDone => !Alive || Time>=Mathf.Min(Effect.EndTime,sustainedStopAt+.5f);
        public static VioletMove.Battler Bind(LabActor actor)=>new VioletMove.Battler{
            root=actor.transform,head=actor.Bone("head"),height=actor.Bounds.size.y,radius=Mathf.Max(actor.Bounds.extents.x,actor.Bounds.extents.z),
            scale=actor.transform.lossyScale.x,center=()=>actor.Centre,bone=actor.Bone};
        public AttackPresentation(LabActor actor,LabActor target,MoveData move,VioletFxCatalog za,BdspFxCatalog bdsp,NativeTiming table)
        {
            body=actor;this.target=target;projectile=move.shape=="Projectile";
            if(actor.entry.source=="BDSP"&&bdsp.Has(move.id))
            {
                var fx=bdsp.Play(move.id,Bind(actor),Bind(target));Effect=fx;
                if(!fx)throw new InvalidOperationException("Missing BDSP effects: "+move.id);
                fx.time=0;HitAt=Mathf.Max(.1f,fx.HitTime);
                var clip=actor.entry.Role(move.physical?"attack":"special");
                float at=Mathf.Max(0,fx.attackTime);SoundAt=at;
                FreeAt=SplitPlan(actor.entry,clip,at,HitAt,cues);
            }
            else
            {
                timing=table.Get(move.id)??throw new InvalidOperationException("Missing native timing: "+move.id);
                var fx=za.Play(move.id,Bind(actor),Bind(target),move.id=="HYDRO_PUMP" ? name=>!name.Contains("0c0") : null);Effect=fx;
                if(fx&&move.id=="HYDRO_PUMP")fx.FitHydroBeam=true;
                if(!fx)throw new InvalidOperationException("Missing ZA effects: "+move.id);
                fx.time=0;HitAt=Mathf.Max(ZaTimeline.ShootAt(timing),fx.HitTime);SoundAt=ZaTimeline.AttackAt(timing);
                FreeAt=ZaTimeline.Plan(timing,actor.entry,move.physical?"Physical":"Special",cues);
            }
            FreeAt=Mathf.Max(FreeAt,HitAt+.15f);
            // Native Fire Spin is a lingering field. The lab currently resolves one hit,
            // so end its emission after contact instead of retaining a 13-second field.
            if(move.id=="FIRE_SPIN")sustainedStopAt=HitAt+.8f;
            shootAt=timing==null?Mathf.Max(0,SoundAt):ZaTimeline.ShootAt(timing);
            shootAt=Mathf.Min(shootAt,HitAt-.05f);
            effectMaterials=Effect.gameObject.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.HasProperty("_EffectTime")).Distinct().ToArray();
            ApplyReadability(Effect.gameObject,move.id);Effect.ManualSimulate=true;Step(0);
        }
        public static void ApplyReadability(GameObject root,string move)
        {
            foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                float scale=1,opacity=1;
                if(ps.name=="glow"){scale=.3f;opacity=.5f;}
                if(move=="CLOSE_COMBAT")
                {
                    // The reconstructed backdrop renders as a filled disk, not a native
                    // textured impact. Omit that decorative layer until its compositor is supported.
                    if(ps.name=="base"){ps.GetComponent<ParticleSystemRenderer>().enabled=false;continue;}
                    else if(ps.name.StartsWith("wind")||ps.name.StartsWith("trail")){scale=.45f;opacity=.45f;}
                    else if(ps.transform.parent.name.Contains("warp")){scale=.55f;opacity=.45f;}
                }
                var main=ps.main;
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
            if(!launched&&Time>=shootAt){launched=true;travelOrigin=body.Centre+body.transform.forward*.25f;travelTarget=target.Centre;}
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
            if(Effect is VioletMove native && native.ProjectileSegment(out previous,out current))return true;
            float duration=Mathf.Max(.05f,HitAt-shootAt);
            previous=Vector3.Lerp(travelOrigin,travelTarget,Mathf.Clamp01((previousTime-shootAt)/duration));
            current=Vector3.Lerp(travelOrigin,travelTarget,Mathf.Clamp01((Time-shootAt)/duration));return true;
        }
        public void BlockEffects(){Dispose();}
        public void Dispose()
        {if(Alive)UnityEngine.Object.Destroy(Effect.gameObject);Effect=null;}
    }
}
