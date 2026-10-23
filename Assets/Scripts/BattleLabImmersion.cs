using System;
using System.Linq;
using UnityEngine;
using BattleLab.Native;
namespace BattleLab
{
    public static class AimRules
    {
        // A finite world-space attack segment, not a distance-to-selected-target check.
        public static bool Contact(Vector3 start,Vector3 end,Bounds body,float radius,out float distance)
        {
            var delta=end-start;float length=delta.magnitude;body.Expand(radius*2);
            if(body.Contains(start)){distance=0;return true;}
            distance=0;return length>.0001f&&body.IntersectRay(new Ray(start,delta/length),out distance)&&distance<=length;
        }
        public static float WaterSpeed(bool waterType,Vector3 at,float altitude)=>waterType&&altitude<.3f&&at.x>=-10&&at.x<=-3&&at.z>=3&&at.z<=10?1.65f:1;
    }
    public partial class BattleLabApp
    {
        EffectSource effectSource=EffectSource.Auto;
        bool immersive=false,marked,immersionCheck,testFly;
        float earthquakeRadius=6;int selectedAttack;
        Vector3? testAim;
        static bool FlyCapable(Fighter f)=>f.data.types.Contains("FLYING")||f.body.entry.fly||new[]{"ROTOM","GASTLY","HAUNTER","MISDREAVUS","MISMAGIUS","BRONZOR","BRONZONG","MAGNEMITE","MAGNETON","MAGNEZONE","BELDUM","METANG","FLYGON","VIBRAVA","LATIAS","LATIOS","RAYQUAZA"}.Contains(f.data.id);
        float HabitatSpeed(Fighter f)=>AimRules.WaterSpeed(f.data.types.Contains("WATER"),f.body.transform.position,f.altitude);
        void ImmersionInput()
        {
            if(immersive&&Input.GetKeyDown(KeyCode.T))
            {
                if(marked){marked=false;Note("Free aim");}
                else
                {
                    var best=fighters.Where(f=>f.team!=Player.team&&f.hp>0)
                        .OrderByDescending(f=>Vector3.Dot(cam.transform.forward,(f.body.Centre-cam.transform.position).normalized)).FirstOrDefault();
                    if(best!=null&&Vector3.Dot(cam.transform.forward,(best.body.Centre-cam.transform.position).normalized)>.92f)
                    {targetIndex=Array.IndexOf(fighters,best);marked=true;Note("Marked "+best.data.id+" · beams still use your aim");}
                    else Note("Aim near an enemy to mark it");
                }
            }
            if(Input.GetKeyDown(KeyCode.M))ToggleMega();
            if(Input.GetKeyDown(KeyCode.C))CycleCamera();
        }
        Vector3 AimPoint(Fighter f,MoveData move)
        {
            if(move.id=="EARTHQUAKE")return f.body.transform.position;
            if(testAim.HasValue)return testAim.Value;
            if(marked&&move.shape!="Beam"&&Target!=null)return Target.body.Centre;
            float range=Mathf.Clamp(move.rangeMax,7,16);
            var origin=VioletMove.Node(AttackPresentation.Bind(f.body),"eff_rangeattack01");
            var ray=cam.ViewportPointToRay(new Vector3(.5f,.5f,0));var point=ray.GetPoint(range+Vector3.Distance(cam.transform.position,origin));
            float closest=float.PositiveInfinity;
            foreach(var enemy in fighters.Where(v=>v.team!=f.team&&v.hp>0))
            {var bounds=enemy.body.Bounds;if(bounds.IntersectRay(ray,out float d)&&d<closest){closest=d;point=ray.GetPoint(d)+ray.direction*.1f;}}
            bool ground=move.shape=="Area"||move.id=="MUDDY_WATER";
            if(ground)
            {
                var plane=new Plane(Vector3.up,Vector3.zero);
                if(plane.Raycast(ray,out float distance))point=ray.GetPoint(distance);
                var flat=point-f.body.transform.position;flat.y=0;point=f.body.transform.position+Vector3.ClampMagnitude(flat,range);point.y=move.id=="MUDDY_WATER"?.5f:0;
            }
            else point=origin+Vector3.ClampMagnitude(point-origin,range);
            return point;
        }
        void ResolveAimedHit(Fighter f)
        {
            var a=f.action;
            if(a.blocked){a.hit=true;return;}
            if(a.presentation.Travel(out var from,out var to))
            {
                Fighter struck=null;float nearest=float.PositiveInfinity;
                foreach(var v in fighters)
                {
                    if(v.team==f.team||v.hp<=0||now<v.dodgeUntil||IsUnderground(v))continue;
                    if(AimRules.Contact(from,to,v.body.Bounds,Mathf.Clamp(a.move.radius*.15f,.12f,.4f),out float distance)&&distance<nearest){struck=v;nearest=distance;}
                }
                if(struck!=null)
                {
                    a.hit=true;a.presentation.FreezeAim(struck.body.Centre);Impact(f,true,struck);
                    // Stop a travelling packet at the first real contact. Beam release/tails keep their native clocks.
                    if(a.move.shape=="Projectile")a.presentation.StopProjectileAtContact();
                    return;
                }
            }
            if(a.clock>=a.presentation.HitAt){a.hit=true;Note(a.move.name+" missed · aim at the body");}
        }
        void TickHabitats(float dt)
        {
            foreach(var f in fighters)
            {
                bool hold=f==Player&&(smoke?testFly:!setup&&Input.GetKey(KeyCode.F));
                bool can=FlyCapable(f)&&!IsUnderground(f)&&f.hp>0;
                bool transforming=f.pendingMega!=null||f.state=="Mega Evolved";
                bool rise=can&&!transforming&&hold&&f.flightEnergy>0&&(f.action==null||f.action.aimed);
                if(rise)f.flightEnergy=Mathf.Max(0,f.flightEnergy-dt);
                else if(f.altitude<.05f&&!hold)f.flightEnergy=Mathf.Min(3,f.flightEnergy+dt*.6f);
                float old=f.altitude;f.altitude=Mathf.MoveTowards(f.altitude,rise?(f.data.id=="ROTOM"?1.6f:2.8f):0,dt*(rise?3:2.3f));
                var position=f.body.transform.position;position.y=f.altitude;f.body.transform.position=position;
                if(!transforming&&f.action==null&&f.altitude>.05f)MovementPose(f,"fly");
                else if(!transforming&&f.action==null&&old>.05f&&f.altitude<=.05f){f.body.Idle();f.state="Ready";}
            }
        }
        void MovementPose(Fighter f,string role)
        {
            AnimationClip clip=null;
            if(f.altitude>.05f)clip=f.body.entry.fly??f.body.entry.clips.FirstOrDefault(c=>c.name.Contains("fly")&&c.name.Contains("loop"));
            if(!clip&&HabitatSpeed(f)>1)clip=f.body.entry.clips.FirstOrDefault(c=>c.name.Contains("swim")&&(c.name.Contains("move")||c.name.Contains("walk")||c.name.Contains("run")));
            if(!clip)clip=f.body.entry.Role(role=="fly"?"battle":role);
            if(clip&&f.body.CurrentClip!=clip.name)f.body.Play(clip,true);
        }
        void ImmersionSetupUI()
        {
            if(Button(new Rect(1068,725,348,32),"Effects: "+effectSource+"  ›"))effectSource=(EffectSource)(((int)effectSource+1)%3);
            if(Button(new Rect(1068,762,348,32),immersive?"Attacks: manual aim (experimental)":"Attacks: focused commands")){immersive=!immersive;marked=false;}
            Text(1068,802,348,$"Earthquake radius: {earthquakeRadius:0.0} m",small);
            earthquakeRadius=GUI.HorizontalSlider(new Rect(1075,832,330,20),earthquakeRadius,4,10);
            Text(348,716,680,"Effects can change independently of models · BDSP falls back to ZA if absent",small);
            if(Button(new Rect(348,747,330,32),"Camera: "+cameraView+"  [C] ›"))CycleCamera();
            Text(348,787,680,"Hold RMB to rotate · scroll to zoom · E changes the focused enemy",small);
            FormSetupUI();
        }
        void ImmersionHUD()
        {
            if(immersive)
            {
                var old=GUI.color;GUI.color=marked?accent:Color.white;
                GUI.DrawTexture(new Rect(708,449,8,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(724,449,8,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(719,438,2,8),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(719,454,2,8),Texture2D.whiteTexture);GUI.color=old;
                Text(590,480,400,marked?"MARKED · beams remain free aim":"FREE AIM · T to mark",small);
            }
            var p=Player;string flight=FlyCapable(p)?$"F hold to fly · {p.flightEnergy:0.0}s energy":"";
            Text(22,300,320,flight,small);
            if(HabitatSpeed(p)>1)Text(22,330,320,"WATER · swim speed ×1.65",small);
            string form=p.pendingMega!=null?"MEGA EVOLUTION…":p.megaId!=null?$"MEGA · {Mathf.Max(0,p.formEnds-now):0}s":"M Mega Evolution · C camera";
            Text(22,360,400,form,small);
            if(p.action?.move.id=="EARTHQUAKE")Text(590,510,360,$"GROUND FIELD · {p.action.fieldRadius:0.0} m",small);
        }
        void BuildWaterLane()
        {
            // Replace the floor mesh with disjoint rectangles: water never overlaps another floor.
            var floor=GameObject.Find("Only arena floor");var mesh=new Mesh{name="Arena with water lane"};
            var vertices=new System.Collections.Generic.List<Vector3>();var indices=new System.Collections.Generic.List<int>();
            void Quad(float x0,float z0,float x1,float z1)
            {int n=vertices.Count;vertices.AddRange(new[]{new Vector3(x0,0,z0),new Vector3(x0,0,z1),new Vector3(x1,0,z1),new Vector3(x1,0,z0)});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            Quad(-50,-50,50,3);Quad(-50,10,50,50);Quad(-50,3,-10,10);Quad(-3,3,50,10);
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            floor.transform.localScale=Vector3.one;floor.GetComponent<MeshFilter>().sharedMesh=mesh;floor.GetComponent<MeshCollider>().sharedMesh=mesh;
            var water=GameObject.CreatePrimitive(PrimitiveType.Plane);water.name="Water movement test lane";water.transform.position=new Vector3(-6.5f,0,6.5f);water.transform.localScale=new Vector3(.7f,1,.7f);
            water.GetComponent<Renderer>().sharedMaterial=Material(new Color(.035f,.34f,.47f));
            foreach(var grid in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(x=>x.name=="Grid"))
            {
                // Trim the crossing strips into pieces; remove their source geometry over water.
                var t=grid.transform;bool horizontal=t.localScale.x>1;float coordinate=horizontal?t.position.z:t.position.x;
                bool crosses=horizontal?coordinate>=3&&coordinate<=10:coordinate>=-10&&coordinate<=-3;
                if(!crosses)continue;
                float lo=horizontal?-10:3,hi=horizontal?-3:10;
                foreach(var ends in new[]{(-48f,lo),(hi,48f)})
                {
                    var piece=Instantiate(grid.gameObject);piece.name="Grid shoreline";var pos=t.position;var scale=t.localScale;
                    if(horizontal){pos.x=(ends.Item1+ends.Item2)*.5f;scale.x=ends.Item2-ends.Item1;}else{pos.z=(ends.Item1+ends.Item2)*.5f;scale.z=ends.Item2-ends.Item1;}
                    piece.transform.position=pos;piece.transform.localScale=scale;
                }
                Destroy(grid.gameObject);
            }
        }
    }
}
