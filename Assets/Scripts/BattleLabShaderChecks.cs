using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        void CheckRefractionPixels(string folder)
        {
            var position=cam.transform.position;var rotation=cam.transform.rotation;
            var root=new GameObject("Isolated refraction pixel fixture");
            var checker=new Texture2D(64,64,TextureFormat.RGB24,false,true);checker.filterMode=FilterMode.Point;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)checker.SetPixel(x,y,((x/4+y/4)%2)==0?Color.white:new Color(.05f,.15f,.3f));checker.Apply();
            var backdrop=Shape("Checker backdrop",PrimitiveType.Quad,new Vector3(100,3,0),new Vector3(5,5,1),Color.white);backdrop.transform.parent=root.transform;
            var backgroundMaterial=backdrop.GetComponent<Renderer>().sharedMaterial;backgroundMaterial.SetTexture("_BaseMap",checker);
            var front=Shape("Foreground occluder",PrimitiveType.Cube,new Vector3(100.4f,3,-2),new Vector3(.35f,.8f,.2f),Color.red);front.transform.parent=root.transform;
            var frontMaterial=front.GetComponent<Renderer>().sharedMaterial;
            var lens=GameObject.CreatePrimitive(PrimitiveType.Quad);lens.name="Refraction fixture";lens.transform.parent=root.transform;lens.transform.position=new Vector3(100,3,-1);lens.transform.localScale=new Vector3(2,2,1);
            var cutout=new Texture2D(2,1,TextureFormat.RGBA32,false,true);cutout.SetPixels(new[]{new Color(1,1,1,0),Color.white});cutout.filterMode=FilterMode.Point;cutout.wrapMode=TextureWrapMode.Clamp;cutout.Apply();
            Mesh primitiveMask=null;
            var mat=new Material(zaFx.material);mat.SetFloat("_Model",1);mat.SetColor("_ModelColor",Color.white);mat.SetTexture("_MainTex",zaFx.textures.First(t=>t.name=="cpt_0_uv0101"));
            mat.SetFloat("_Refraction",1);mat.SetFloat("_RefractionScale",.035f);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.renderQueue=2990;
            var renderer=lens.GetComponent<Renderer>();renderer.sharedMaterial=mat;
            cam.transform.position=new Vector3(100,3,-6);cam.transform.LookAt(new Vector3(100,3,0));
            try
            {
                renderer.enabled=false;var before=Pixels("refraction-baseline");
                renderer.enabled=true;var after=Pixels("refraction-on");
                mat.SetFloat("_RefractionScale",0);var zero=Pixels("refraction-zero");
                int changed=0,outside=0,zeroChanged=0,foreground=0;
                for(int y=0;y<640;y++)for(int x=0;x<1024;x++)
                {
                    int i=y*1024+x;bool diff=Difference(before[i],after[i])>6;
                    if(diff){changed++;if(x<300||x>724||y<100||y>540)outside++;}
                    if(Difference(before[i],zero[i])>6)zeroChanged++;
                    // Interior of the foreground box, safely away from rasterized edges.
                    if(x>575&&x<610&&y>290&&y<350&&diff)foreground++;
                }
                File.WriteAllText(Path.Combine(folder,"refraction-pixels.txt"),$"Changed pixels={changed}; outside footprint={outside}; zero strength={zeroChanged}; foreground interior={foreground}\n");
                if(changed<100||outside>0||zeroChanged>0||foreground>0)throw new Exception($"Refraction pixel regression: {changed}/{outside}/{zeroChanged}/{foreground}");
                // A behind-surface soft particle must disappear, without touching unflagged emitters.
                mat.SetFloat("_Refraction",0);mat.SetFloat("_ColorMode",0);mat.SetColor("_ModelColor",Color.magenta);
                mat.SetVector("_SoftParticle",new Vector4(1,2,1,0));var soft=Pixels("soft-intersection-hidden");
                int softChanged=before.Where((p,i)=>Difference(p,soft[i])>6).Count();
                if(softChanged>0)throw new Exception("Soft intersection did not fade: "+softChanged);
                mat.SetVector("_SoftParticle",Vector4.zero);mat.SetTexture("_MainTex",cutout);mat.SetColor("_ModelColor",Color.green);
                mat.SetFloat("_OpaqueParticle",1);mat.SetFloat("_AlphaTest",1);mat.SetFloat("_AlphaCompare",4);mat.SetFloat("_AlphaThreshold",.5f);
                mat.SetFloat("_ZWrite",1);mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.SetFloat("_Cull",2);mat.renderQueue=2450;
                var masked=Pixels("native-cutout-front");
                if(Difference(before[380*1024+450],masked[380*1024+450])>6||Difference(before[380*1024+650],masked[380*1024+650])<30)throw new Exception("Native alpha cutout/front-face regression");
                mat.SetFloat("_Cull",1);var back=Pixels("native-cutout-back");
                if(before.Where((p,i)=>Difference(p,back[i])>6).Any())throw new Exception("Native back-face culling regression");
                renderer.enabled=false;
                primitiveMask=Instantiate(lens.GetComponent<MeshFilter>().sharedMesh);
                primitiveMask.SetUVs(1,primitiveMask.vertices.Select(v=>Vector2.one).ToList());
                primitiveMask.SetUVs(2,primitiveMask.vertices.Select(v=>new Vector2(1,v.x<0?0:1)).ToList());
                var particles=new GameObject("Native primitive mask transport");particles.transform.parent=root.transform;particles.transform.position=lens.transform.position;
                var ps=particles.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.startLifetime=100;main.startSpeed=0;main.startSize=2;main.startColor=Color.white;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var data=ps.customData;data.enabled=true;
                foreach(var stream in new[]{ParticleSystemCustomData.Custom1,ParticleSystemCustomData.Custom2})
                {data.SetMode(stream,ParticleSystemCustomDataMode.Vector);data.SetVectorComponentCount(stream,4);for(int c=0;c<4;c++)data.SetVector(stream,c,new ParticleSystem.MinMaxCurve(c==3||c==1&&stream==ParticleSystemCustomData.Custom1?1:0));}
                mat.SetFloat("_Model",0);mat.SetFloat("_PrimitiveStream",1);mat.SetFloat("_NativeCombiner",1);mat.SetFloat("_Cull",2);mat.SetTexture("_MainTex",Texture2D.whiteTexture);
                var particleRenderer=ps.GetComponent<ParticleSystemRenderer>();particleRenderer.sharedMaterial=mat;particleRenderer.renderMode=ParticleSystemRenderMode.Mesh;particleRenderer.mesh=primitiveMask;particleRenderer.alignment=ParticleSystemRenderSpace.Local;
                particleRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                particleRenderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,ParticleSystemVertexStream.Custom1XYZW,ParticleSystemVertexStream.Custom2XYZW,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.UV3});
                ps.Emit(1);ps.Simulate(.01f,false,false);
                var primitive=Pixels("native-primitive-mask");
                if(Difference(before[380*1024+450],primitive[380*1024+450])>6||Difference(before[380*1024+650],primitive[380*1024+650])<30)throw new Exception("Native primitive-channel transport regression");
                File.AppendAllText(Path.Combine(folder,"refraction-pixels.txt"),"Soft-intersection, native alpha cutout front/back culling and actual particle primitive-channel pixel checks passed.\n");
                Debug.Log($"LAB REFRACTION PIXELS PASSED changed={changed}, outside/zero/foreground/soft=0");
            }
            finally
            {
                cam.transform.SetPositionAndRotation(position,rotation);root.SetActive(false);
                Destroy(root);Destroy(mat);Destroy(checker);Destroy(cutout);if(primitiveMask)Destroy(primitiveMask);Destroy(backgroundMaterial);Destroy(frontMaterial);
            }
            Color32[] Pixels(string name)
            {
                string path=Path.Combine(folder,name+".png");CaptureEvidence(path);
                var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(path));var pixels=t.GetPixels32();Destroy(t);return pixels;
            }
        }
        static int Difference(Color32 a,Color32 b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
    }
}
