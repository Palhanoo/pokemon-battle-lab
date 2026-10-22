using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BattleLab
{
    public class LabActor : MonoBehaviour
    {
        public ModelEntry entry;
        public string CurrentClip { get; private set; }
        public float BodyHeight { get; private set; }
        public float ClipTime => legacy ? legacyClock : current.IsValid() ? (float)current.GetTime() : 0;
        public float ClipLength => clip ? clip.length : 0;
        public Bounds Bounds { get { var rs=bodyRenderers; var b=rs[0].bounds; foreach(var r in rs)b.Encapsulate(r.bounds); return b; } }
        public Vector3 Centre => Bounds.center;
        Renderer[] bodyRenderers;
        Dictionary<string,Transform> bones;
        float reactionRemaining;
        AnimationClip reactionEnd;
        public Transform Bone(string name)=>name!=null&&bones.TryGetValue(name.Replace("_","").ToLowerInvariant(),out var bone)?bone:null;
        Animation legacy;AnimationState legacyCurrent,legacyPrevious;float legacyClock,legacyRate=1,previousClock,previousRate;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        AnimationClip clip;
        float blend;
        bool looping;
        public static LabActor Spawn(ModelEntry entry, Vector3 at)
        {
            var root=new GameObject(entry.source+" "+entry.species);
            root.transform.position=at;
            var actor=root.AddComponent<LabActor>();actor.entry=entry;
            var body=Instantiate(entry.prefab,root.transform);body.SetActive(true);
            foreach(var c in body.GetComponentsInChildren<Collider>())Destroy(c);
            foreach(var a in body.GetComponentsInChildren<Animation>())a.enabled=false;
            actor.bodyRenderers=body.GetComponentsInChildren<Renderer>();
            actor.bones=body.GetComponentsInChildren<Transform>().GroupBy(t=>t.name.Replace("_","").ToLowerInvariant()).ToDictionary(g=>g.Key,g=>g.First());
            if(entry.clips.Any(c=>c.legacy))
            {
                actor.legacy=body.GetComponent<Animation>()??body.AddComponent<Animation>();actor.legacy.enabled=false;actor.legacy.playAutomatically=false;
                foreach(var c in entry.clips){if(!actor.legacy[c.name])actor.legacy.AddClip(c,c.name);actor.legacy[c.name].enabled=false;}
                actor.Idle();actor.Tick(0);
            }
            else
            {
                var animator=body.GetComponent<Animator>()??body.AddComponent<Animator>();
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                actor.graph=PlayableGraph.Create(root.name);actor.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                actor.mixer=AnimationMixerPlayable.Create(actor.graph,2);
                AnimationPlayableOutput.Create(actor.graph,"Body",animator).SetSourcePlayable(actor.mixer);
                actor.graph.Play();actor.Idle();actor.graph.Evaluate(0);
            }
            // Ground once from the posed body. Never chase animated foot bounds each frame.
            body.transform.localPosition-=Vector3.up*(actor.Bounds.min.y-at.y);
            actor.BodyHeight=actor.Bounds.size.y;
            return actor;
        }
        public void Idle() => Play(entry.Role("battle")??entry.Role("idle"),true);
        public void Role(string role, bool loop=false) => Play(entry.Role(role)??entry.Role("idle"),loop);
        public void Play(AnimationClip next, bool loop=false, float speed=1)
        {
            if(!next)return;
            reactionRemaining=0;reactionEnd=null;
            if(legacy)
            {
                if(legacyPrevious!=null)legacyPrevious.enabled=false;
                legacyPrevious=legacyCurrent;previousClock=legacyClock;previousRate=legacyRate;
                legacyCurrent=legacy[next.name];legacyClock=0;legacyRate=speed;blend=0;clip=next;looping=loop;CurrentClip=next.name;
                if(legacyPrevious==legacyCurrent){legacyPrevious=null;blend=1;}
                legacyCurrent.enabled=true;legacyCurrent.speed=0;legacyCurrent.wrapMode=WrapMode.ClampForever;return;
            }
            if(previous.IsValid()){mixer.DisconnectInput(0);graph.DestroyPlayable(previous);}
            if(current.IsValid()){mixer.DisconnectInput(1);previous=current;graph.Connect(previous,0,mixer,0);}
            current=AnimationClipPlayable.Create(graph,next);current.SetApplyFootIK(false);current.SetApplyPlayableIK(false);
            current.SetSpeed(speed);graph.Connect(current,0,mixer,1);clip=next;looping=loop;blend=0;CurrentClip=next.name;
            mixer.SetInputWeight(0,previous.IsValid()?1:0);mixer.SetInputWeight(1,previous.IsValid()?0:1);
        }
        public void Rate(float rate)
        {if(legacy){legacyRate=rate;return;}if(current.IsValid()&&(looping||current.GetTime()<clip.length-.001f))current.SetSpeed(rate);}
        public void React()
        {
            var damage=entry.damage;if(!damage)return;Play(damage);
            reactionRemaining=damage.length;
            if(damage.name.EndsWith("_start"))reactionEnd=entry.Named(damage.name.Replace("_start","_end"));
        }
        public void Tick(float dt)
        {
            if(!legacy&&!graph.IsValid())return;
            blend=Mathf.Min(1,blend+dt/.1f);
            if(legacy)
            {
                legacyClock+=dt*legacyRate;float length=Mathf.Max(.001f,clip.length);
                legacyClock=looping?legacyClock%length:Mathf.Min(legacyClock,length);
                legacyCurrent.time=legacyClock;legacyCurrent.weight=legacyPrevious!=null?blend:1;
                if(legacyPrevious!=null){previousClock+=dt*previousRate;legacyPrevious.time=previousClock;legacyPrevious.weight=1-blend;if(blend>=1){legacyPrevious.enabled=false;legacyPrevious=null;}}
                legacy.Sample();
            }
            else
            {
            mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);
            if(current.IsValid()&&clip)
            {
                double end=Mathf.Max(.001f,clip.length-.001f);
                if(looping){if(current.GetTime()>=end)current.SetTime(current.GetTime()%end);}
                else if(current.GetTime()+dt*current.GetSpeed()>=end){current.SetTime(end);current.SetSpeed(0);}
            }
            graph.Evaluate(dt);
            }
            if(reactionRemaining>0)
            {
                reactionRemaining-=dt;
                if(reactionRemaining<=0)
                {var end=reactionEnd;if(end){Play(end);reactionRemaining=end.length;}else Idle();}
            }
        }
        public void Face(Vector3 at){var d=at-transform.position;d.y=0;if(d.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(d);}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
