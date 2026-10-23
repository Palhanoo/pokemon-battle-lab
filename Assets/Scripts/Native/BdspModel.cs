using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using J=BattleLab.Native.VioletFx.J;
namespace BattleLab.Native
{
    // Native effect models use the same move clock as particles, including paused/manual previews.
    public sealed class BdspModel:MonoBehaviour
    {
        sealed class Track {public Transform node;public int property;public float[] times,values;}
        sealed class MaterialTrack {public int material,component;public string property;public float[] times,values;}
        readonly List<MaterialTrack> materialTracks=new();
        readonly List<Track> tracks=new();readonly List<Mesh> meshes=new();readonly List<Material> materials=new();
        bool groundPresentation;
        public int TrackCount=>tracks.Count;
        public static GameObject Build(string json,Func<string,Texture2D> texture,Material basis,Transform parent)
        {
            var data=J.Obj(MiniJson.Parse(json));var root=new GameObject("native effect model");root.transform.SetParent(parent,false);
            var model=root.AddComponent<BdspModel>();var nodes=J.List(data,"nodes");var transforms=new Transform[nodes.Count];
            for(int i=0;i<nodes.Count;i++)
            {
                var n=J.Obj(nodes[i]);var go=new GameObject(J.Str(n,"name"));var tr=transforms[i]=go.transform;int p=(int)J.F(n,"parent");
                tr.SetParent(p<0?root.transform:transforms[p],false);tr.localPosition=J.Vec(n,"pos");tr.localScale=J.Vec(n,"scale");var q=J.Floats(n["rot"]);tr.localRotation=new Quaternion(q[0],q[1],q[2],q[3]);go.SetActive(J.B(n,"active"));
            }
            foreach(var item in J.List(data,"materials"))
            {
                var d=J.Obj(item);var m=new Material(basis);model.materials.Add(m);m.SetFloat("_Model",1);m.SetFloat("_MeshParticles",J.B(d,"lit")?1:0);
                foreach(var pair in new[]{("src","_SrcBlend"),("dst","_DstBlend"),("zwrite","_ZWrite"),("cull","_Cull"),("color_mode","_ColorMode"),("alpha_mode","_AlphaMode")})m.SetFloat(pair.Item2,J.F(d,pair.Item1));
                var c=J.Floats(d["color"]);m.SetVector("_ModelColor",new Vector4(c[0],c[1],c[2],c[3]));c=J.Floats(d["emission"]);m.SetVector("_ModelEmission",new Vector4(c[0],c[1],c[2],c[3]));
                if(d.TryGetValue("combiner",out var cb))VioletFx.NativeCombiner(m,J.Obj(cb));
                BdspFx.Texture(m,"_MainTex",d,"tex0",texture,"_MainScroll");m.SetFloat("_UseSub",BdspFx.Texture(m,"_SubTex",d,"tex1",texture,"_SubScroll")?1:0);m.SetFloat("_UseThird",BdspFx.Texture(m,"_ThirdTex",d,"tex2",texture,"_ThirdScroll")?1:0);
                if(J.F(d,"zwrite")>.5f)m.renderQueue=2000;
            }
            foreach(var item in J.List(data,"meshes"))
            {
                var d=J.Obj(item);var m=BdspFx.BuildMesh(d);model.meshes.Add(m);
                var submeshes=J.List(d,"submeshes");if(submeshes.Count>0)
                {m.subMeshCount=submeshes.Count;for(int sm=0;sm<submeshes.Count;sm++)m.SetTriangles(J.Floats(submeshes[sm]).Select(x=>(int)x).ToArray(),sm);}
                if(d.ContainsKey("bi"))
                {
                    var indices=J.Floats(d["bi"]);var weights=J.Floats(d["bw"]);var bw=new BoneWeight[m.vertexCount];
                    for(int i=0;i<bw.Length;i++){int k=i*4;bw[i]=new BoneWeight{boneIndex0=(int)indices[k],boneIndex1=(int)indices[k+1],boneIndex2=(int)indices[k+2],boneIndex3=(int)indices[k+3],weight0=weights[k],weight1=weights[k+1],weight2=weights[k+2],weight3=weights[k+3]};}
                    m.boneWeights=bw;var binds=J.List(d,"bind");var matrices=new Matrix4x4[binds.Count];
                    for(int i=0;i<binds.Count;i++){var rows=(List<object>)binds[i];for(int r=0;r<4;r++){var values=J.Floats(rows[r]);for(int c=0;c<4;c++)matrices[i][r,c]=values[c];}}m.bindposes=matrices;
                }
            }
            foreach(var item in J.List(data,"renderers"))
            {
                var d=J.Obj(item);var tr=transforms[(int)J.F(d,"node")];var mesh=model.meshes[(int)J.F(d,"mesh")];var bones=J.Floats(d["bones"]);Renderer renderer;
                if(bones.Length>0)
                {
                    var skin=tr.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;var bt=new Transform[bones.Length];for(int i=0;i<bt.Length;i++)bt[i]=transforms[(int)bones[i]];skin.bones=bt;skin.rootBone=transforms[0];skin.updateWhenOffscreen=true;renderer=skin;
                }
                else{tr.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=tr.gameObject.AddComponent<MeshRenderer>();}
                var refs=J.Floats(d["materials"]);var mats=new Material[refs.Length];for(int i=0;i<refs.Length;i++)mats[i]=model.materials[(int)refs[i]];renderer.sharedMaterials=mats;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach(var item in J.List(data,"tracks")){var d=J.Obj(item);model.tracks.Add(new Track{node=transforms[(int)J.F(d,"node")],property=(int)J.F(d,"property"),times=J.Floats(d["times"]),values=J.Floats(d["values"])});}
            foreach(var item in J.List(data,"materialTracks")){var d=J.Obj(item);model.materialTracks.Add(new MaterialTrack{material=(int)J.F(d,"material"),component=(int)J.F(d,"component"),property=J.Str(d,"property"),times=J.Floats(d["times"]),values=J.Floats(d["values"])});}
            model.Sample(0);return root;
        }
        // The source models include offsets for BDSP's fixed cinematic stage. Normalize
        // the contact pose once; keep the original skeleton motion relative to that pose.
        public void FitContact(float at,bool ground=false)
        {
            groundPresentation=ground;Sample(ground?0:at);bool found=false;var bounds=new Bounds();
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh=null;bool temporary=false;
                if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);temporary=true;}
                else if(renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;
                if(!mesh)continue;
                foreach(var v in mesh.vertices){var local=transform.InverseTransformPoint(renderer.transform.TransformPoint(v));if(!found){bounds=new Bounds(local,Vector3.zero);found=true;}else bounds.Encapsulate(local);}
                if(temporary)Release(mesh);
            }
            if(found){float k=(ground?7:1.1f)/Mathf.Max(.01f,ground?Mathf.Max(bounds.size.x,bounds.size.z):Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));transform.localScale=Vector3.one*k;transform.localPosition=ground?new Vector3(-bounds.center.x*k,0,-bounds.center.z*k):-bounds.center*k;}
            Sample(0);
        }
        public void Sample(float time)
        {
            foreach(var t in tracks)
            {
                int a=Array.BinarySearch(t.times,time);if(a<0)a=~a-1;a=Mathf.Clamp(a,0,t.times.Length-1);int b=Mathf.Min(a+1,t.times.Length-1);float u=a==b?0:Mathf.Clamp01((time-t.times[a])/(t.times[b]-t.times[a]));int dim=t.property==2?4:3;int x=a*dim,y=b*dim;var v=t.values;
                if(t.property==2)t.node.localRotation=Quaternion.Slerp(new Quaternion(v[x],v[x+1],v[x+2],v[x+3]),new Quaternion(v[y],v[y+1],v[y+2],v[y+3]),u);
                else{var value=Vector3.Lerp(new Vector3(v[x],v[x+1],v[x+2]),new Vector3(v[y],v[y+1],v[y+2]),u);if(t.property==1)
                    {
                        // The original centimetre-scale floor tremor needs readable
                        // displacement after fitting its stage-sized pieces to this arena.
                        if(groundPresentation&&t.node.name.Contains(":joint")&&!t.node.name.EndsWith(":joint1"))value.y*=12;
                        t.node.localPosition=value;
                    }else if(t.property==3)t.node.localScale=value;}
            }
            foreach(var t in materialTracks)
            {
                int a=Array.BinarySearch(t.times,time);if(a<0)a=~a-1;a=Mathf.Clamp(a,0,t.times.Length-1);int b=Mathf.Min(a+1,t.times.Length-1);float u=a==b?0:Mathf.Clamp01((time-t.times[a])/(t.times[b]-t.times[a]));
                string property=t.component<2?"_MainTex":"_SubTex";var m=materials[t.material];bool scale=t.property=="_UvScale0";var v=scale?m.GetTextureScale(property):m.GetTextureOffset(property);v[t.component%2]=Mathf.Lerp(t.values[a],t.values[b],u);if(scale)m.SetTextureScale(property,v);else m.SetTextureOffset(property,v);
            }
            foreach(var m in materials)m.SetFloat("_EffectTime",time);
        }
        static void Release(UnityEngine.Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void OnDestroy(){foreach(var m in meshes)if(m)Release(m);foreach(var m in materials)if(m)Release(m);}
    }
}
