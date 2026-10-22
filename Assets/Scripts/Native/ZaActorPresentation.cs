using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleLab.Native
{
    // Per-body leases: an older effect tail cannot clear a newer move's glow or
    // invisibility. No shared model materials or prefab state are changed.
    public sealed class ZaActorPresentation : MonoBehaviour
    {
        public struct State { public bool hidden; public Color color; public float power, strength; public int priority; }
        readonly Dictionary<object,State> states=new();
        Renderer[] body;
        bool[] originalHidden;
        readonly List<(Renderer source,Renderer overlay)> overlays=new();
        Material rim;
        MaterialPropertyBlock block;
        bool hiding;

        public static State Evaluate(IEnumerable<Dictionary<string,object>> clips,float now)
        {
            var result=new State { power=5 };
            foreach(var c in clips.OrderBy(c=>VioletFx.J.F(c,"start")))
            {
                string target=VioletFx.J.Str(c,"object");
                if(target!="ATK")continue;
                float start=VioletFx.J.F(c,"start"),end=VioletFx.J.F(c,"end");
                string cmd=VioletFx.J.Str(c,"cmd");
                if(cmd=="Title/Character/EnableInvisible")
                {result.hidden|=now>=start && now<end;continue;}
                if(now<start || !c.TryGetValue("params",out var value))continue;
                var p=VioletFx.J.Obj(value);
                float t=end<=start?1:Mathf.Clamp01((now-start)/(end-start));
                int ease=(int)VioletFx.J.F(p,"EasingType");
                t=ease==1?t*t:ease==2?1-(1-t)*(1-t):t;
                if(cmd=="BlendColor/RimBlend")
                {
                    var v=VioletFx.J.Vec(p,"Color");
                    result.color=Color.Lerp(result.color,new Color(v.x,v.y,v.z,1),t);
                    result.power=Mathf.Lerp(result.power,VioletFx.J.F(p,"RimPower"),t);
                    result.strength=Mathf.Lerp(result.strength,VioletFx.J.F(p,"Alpha")*VioletFx.J.F(p,"Intensity"),t);
                    result.priority=(int)VioletFx.J.F(p,"Priority");
                }
                else if(cmd=="BlendColor/ResetRim")result.strength*=1-t;
            }
            return result;
        }
        public void Sample(object owner,IEnumerable<Dictionary<string,object>> clips,float time)
        {
            if(body==null)
            {
                body=GetComponentsInChildren<Renderer>(true).Where(x=>x is SkinnedMeshRenderer || x is MeshRenderer).ToArray();
                originalHidden=body.Select(x=>x.forceRenderingOff).ToArray();
            }
            states[owner]=Evaluate(clips,time);Apply();
        }
        public void Release(object owner){states.Remove(owner);Apply();}
        void LateUpdate(){if(states.Count>0)Apply();}
        void Apply()
        {
            if(body==null)return;
            bool hidden=states.Values.Any(x=>x.hidden);
            if(hidden || hiding)
                for(int i=0;i<body.Length;i++)if(body[i])body[i].forceRenderingOff=originalHidden[i]||hidden;
            hiding=hidden;
            var glow=states.Values.OrderByDescending(x=>x.priority).ThenByDescending(x=>x.strength).FirstOrDefault(x=>x.strength>0);
            if(glow.strength>0 && !rim)BuildRim();
            if(overlays.Count==0)return;
            block??=new MaterialPropertyBlock();
            block.SetColor("_RimColor",glow.color);block.SetFloat("_RimPower",Mathf.Max(.01f,glow.power));block.SetFloat("_Strength",glow.strength);
            foreach(var (source,overlay) in overlays)
            {
                if(!overlay || !source)continue;
                overlay.enabled=source.enabled && !source.forceRenderingOff && glow.strength>0;
                overlay.SetPropertyBlock(block);
                if(source is SkinnedMeshRenderer src && overlay is SkinnedMeshRenderer dst)
                    for(int i=0;i<src.sharedMesh.blendShapeCount;i++)dst.SetBlendShapeWeight(i,src.GetBlendShapeWeight(i));
            }
        }
        void BuildRim()
        {
            var shader=Resources.Load<Shader>("ZaMoveRim");if(!shader)return;
            rim=new Material(shader){name="Native move rim"};
            foreach(var source in body)
            {
                if(!source)continue;
                var mesh=source is SkinnedMeshRenderer skin?skin.sharedMesh:source.GetComponent<MeshFilter>()?.sharedMesh;
                if(!mesh)continue;
                var go=new GameObject("Move rim");go.transform.SetParent(source.transform,false);go.layer=source.gameObject.layer;
                Renderer overlay;
                if(source is SkinnedMeshRenderer s)
                {
                    var d=go.AddComponent<SkinnedMeshRenderer>();d.sharedMesh=mesh;d.bones=s.bones;d.rootBone=s.rootBone;
                    d.localBounds=s.localBounds;d.updateWhenOffscreen=s.updateWhenOffscreen;overlay=d;
                }
                else {go.AddComponent<MeshFilter>().sharedMesh=mesh;overlay=go.AddComponent<MeshRenderer>();}
                overlay.sharedMaterials=Enumerable.Repeat(rim,mesh.subMeshCount).ToArray();
                overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlays.Add((source,overlay));
            }
        }
        void OnDisable(){states.Clear();Apply();}
        void OnDestroy()
        {
            states.Clear();Apply();
            foreach(var pair in overlays)if(pair.overlay)Gone(pair.overlay.gameObject);
            if(rim)Gone(rim);
        }
        static void Gone(Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
    }
}
