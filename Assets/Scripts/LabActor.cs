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
        // Captured from the neutral posed model, independent of walking wings/feet/recoil.
        public Vector3 CameraAnchor => transform.position+Vector3.up*Mathf.Max(.25f,footprint.center.y*transform.lossyScale.y);
        Bounds footprint;
        public float FootprintRadius(Vector3 direction)
        {
            var local=transform.InverseTransformDirection(direction.normalized);
            return transform.lossyScale.x*Mathf.Max(.15f,Vector3.Dot(footprint.center,local)+Mathf.Abs(local.x)*footprint.extents.x+Mathf.Abs(local.z)*footprint.extents.z);
        }
        Renderer[] bodyRenderers;
        Dictionary<string,Transform> bones;
        Transform visualRoot;Vector3 visualRest,recoilDirection;float recoilRemaining;
        (Transform bone,Vector3 position,Quaternion rotation,Vector3 scale)[] restPose;
        public void Recoil(Vector3 away,float strength)
        {recoilDirection=transform.InverseTransformDirection(away.normalized)*Mathf.Clamp(strength,.04f,.3f);recoilRemaining=.18f;}
        float burrowDepth;
        public void SetBurrowDepth(float depth){burrowDepth=Mathf.Max(0,depth);if(visualRoot)visualRoot.localPosition=visualRest+Vector3.down*burrowDepth;}
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
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            actor.bones=body.GetComponentsInChildren<Transform>().GroupBy(t=>t.name.Replace("_","").ToLowerInvariant()).ToDictionary(g=>g.Key,g=>g.First());
            actor.visualRoot=body.transform;
            actor.restPose=body.GetComponentsInChildren<Transform>().Where(t=>t!=body.transform)
                .Select(t=>(t,t.localPosition,t.localRotation,t.localScale)).ToArray();
            if(entry.clips.Any(c=>c.legacy))
            {
                actor.legacy=body.GetComponent<Animation>()??body.AddComponent<Animation>();actor.legacy.enabled=false;actor.legacy.playAutomatically=false;
                // The glTF can contain category/menu clips with the same name as land
                // clips. Unity's imported library keeps the last; ModelEntry selects first.
                // Rebuild it in the same order so Idle cannot silently sample a menu pose.
                foreach(var state in actor.legacy.Cast<AnimationState>().ToArray())actor.legacy.RemoveClip(state.name);
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
            body.transform.localPosition-=Vector3.up*(actor.PosedBounds().min.y-at.y);
            actor.visualRest=body.transform.localPosition;
            var posed=actor.PosedBounds();actor.footprint=new Bounds(posed.center-at,posed.size);
            actor.BodyHeight=posed.size.y;
            return actor;
        }
        public Bounds PosedBounds()
        {
            var result=new Bounds();bool found=false;var baked=new Mesh();
            foreach(var renderer in bodyRenderers)
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                Mesh mesh=null;
                if(renderer is SkinnedMeshRenderer skin){skin.BakeMesh(baked);mesh=baked;}
                else mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(!mesh)continue;
                foreach(var vertex in mesh.vertices)
                {
                    var world=renderer.transform.TransformPoint(vertex);
                    if(!found){result=new Bounds(world,Vector3.zero);found=true;}else result.Encapsulate(world);
                }
            }
            Destroy(baked);return found?result:Bounds;
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
                legacyCurrent.enabled=true;legacyCurrent.wrapMode=looping?WrapMode.Loop:WrapMode.ClampForever;
                legacyCurrent.time=legacyClock;legacyCurrent.weight=legacyPrevious!=null?blend:1;
                if(legacyPrevious!=null){previousClock+=dt*previousRate;legacyPrevious.time=previousClock;legacyPrevious.weight=1-blend;if(blend>=1){legacyPrevious.enabled=false;legacyPrevious=null;}}
                // Native clips are sparse: idle can omit shoulder/spine channels keyed
                // by run. Clear the previous pose before sampling the active blend.
                foreach(var pose in restPose){pose.bone.localPosition=pose.position;pose.bone.localRotation=pose.rotation;pose.bone.localScale=pose.scale;}
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
            recoilRemaining=Mathf.Max(0,recoilRemaining-dt);
            visualRoot.localPosition=visualRest+Vector3.down*burrowDepth+recoilDirection*Mathf.Sin(Mathf.PI*recoilRemaining/.18f);
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
