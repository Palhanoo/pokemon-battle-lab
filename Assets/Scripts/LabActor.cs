using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BattleLab
{
    public class LabActor : MonoBehaviour
    {
        public ModelEntry entry;
        public string CurrentClip { get; private set; }
        public float ClipTime => current.IsValid() ? (float)current.GetTime() : 0;
        public float ClipLength => clip ? clip.length : 0;
        public Bounds Bounds { get { var rs=GetComponentsInChildren<Renderer>(); var b=rs[0].bounds; foreach(var r in rs)b.Encapsulate(r.bounds); return b; } }
        public Vector3 Centre => Bounds.center;
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
            var body=Instantiate(entry.prefab,root.transform);
            foreach(var c in body.GetComponentsInChildren<Collider>())Destroy(c);
            foreach(var a in body.GetComponentsInChildren<Animation>())a.enabled=false;
            var animator=body.GetComponent<Animator>()??body.AddComponent<Animator>();
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            actor.graph=PlayableGraph.Create(root.name);actor.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            actor.mixer=AnimationMixerPlayable.Create(actor.graph,2);
            AnimationPlayableOutput.Create(actor.graph,"Body",animator).SetSourcePlayable(actor.mixer);
            actor.graph.Play();actor.Idle();actor.graph.Evaluate(0);
            // Ground once from the posed body. Never chase animated foot bounds each frame.
            body.transform.localPosition-=Vector3.up*actor.Bounds.min.y;
            return actor;
        }
        public void Idle() => Play(entry.Role("battle")??entry.Role("idle"),true);
        public void Role(string role, bool loop=false) => Play(entry.Role(role)??entry.Role("idle"),loop);
        public void Play(AnimationClip next, bool loop=false)
        {
            if(!next)return;
            if(previous.IsValid()){mixer.DisconnectInput(0);graph.DestroyPlayable(previous);}
            if(current.IsValid()){mixer.DisconnectInput(1);previous=current;graph.Connect(previous,0,mixer,0);}
            current=AnimationClipPlayable.Create(graph,next);current.SetApplyFootIK(false);current.SetApplyPlayableIK(false);
            graph.Connect(current,0,mixer,1);clip=next;looping=loop;blend=0;CurrentClip=next.name;
            mixer.SetInputWeight(0,previous.IsValid()?1:0);mixer.SetInputWeight(1,previous.IsValid()?0:1);
        }
        public void Tick(float dt)
        {
            if(!graph.IsValid())return;
            blend=Mathf.Min(1,blend+dt/.1f);mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);
            if(current.IsValid()&&clip)
            {
                double end=Mathf.Max(.001f,clip.length-.001f);
                if(looping){if(current.GetTime()>=end)current.SetTime(current.GetTime()%end);}
                else if(current.GetTime()+dt>=end){current.SetTime(end);current.SetSpeed(0);}
            }
            graph.Evaluate(dt);
        }
        public void Face(Vector3 at){var d=at-transform.position;d.y=0;if(d.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(d);}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
