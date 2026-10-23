using System.Linq;
using UnityEngine;
using BattleLab.Native;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        sealed class DigTravel
        {
            public readonly Vector2 window;public bool surfaced;public GameObject trail;
            public DigTravel(VioletMove fx){window=fx.HideWindow;}
        }
        static bool IsUnderground(Fighter f)=>f?.action?.dig!=null&&f.action.clock>=f.action.dig.window.x&&f.action.clock<f.action.dig.window.y;
        void MoveUnderground(Fighter f,Vector3 input,float dt)
        {
            var next=f.body.transform.position+Vector3.ClampMagnitude(input,1)*4.2f*dt;
            next.x=Mathf.Clamp(next.x,-12,12);next.z=Mathf.Clamp(next.z,-12,12);next.y=0;
            if(input.sqrMagnitude>.001f)f.body.Face(next);
            f.body.transform.position=next;f.state="Underground · WASD to steer";
        }
        void TickDig(Fighter f,float dt)
        {
            var a=f.action;var dig=a.dig;if(dig==null)return;
            bool buried=IsUnderground(f);
            float sink=Mathf.Clamp01((a.clock-(dig.window.x-.25f))/.25f);
            f.body.SetBurrowDepth(a.clock<dig.window.y?sink*(f.body.BodyHeight+.2f):0);
            if(buried)
            {
                if(f!=Player&&ai)
                {
                    var delta=a.target.body.transform.position-f.body.transform.position;
                    MoveUnderground(f,delta.magnitude>Clearance(f,a.target)+.2f?delta.normalized:Vector3.zero,dt);
                }
                if(!dig.trail)
                {
                    var set=zaFx.moves.SelectMany(m=>m.sets).First(s=>s.name=="ew0091_sign");
                    dig.trail=VioletFx.Build(set.text,n=>zaFx.textures.FirstOrDefault(t=>t.name==n),zaFx.material);dig.trail.name="Dig travelling soil";
                    dig.trail.transform.localScale=Vector3.one*Mathf.Clamp(f.body.BodyHeight*.25f,.2f,.5f);
                    foreach(var ps in dig.trail.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        bool keep=ps.name=="smoketop"||ps.name=="stones_Copy1";
                        var main=ps.main;main.loop=true;main.duration=.6f;main.startDelay=0;main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.55f);main.simulationSpace=ParticleSystemSimulationSpace.World;
                        var em=ps.emission;em.SetBursts(new ParticleSystem.Burst[0]);em.enabled=keep;em.rateOverTime=keep?12:0;ps.GetComponent<ParticleSystemRenderer>().enabled=keep;
                    }
                    dig.trail.transform.position=f.body.transform.position;
                    foreach(var ps in VioletFx.Roots(dig.trail))ps.Simulate(0,true,true,false);
                }
                dig.trail.transform.position=f.body.transform.position;
                foreach(var ps in VioletFx.Roots(dig.trail))ps.Simulate(dt,true,false,false);
            }
            else if(a.clock>=dig.window.y&&!dig.surfaced)
            {
                dig.surfaced=true;f.body.transform.position=ClearPosition(f,f.body.transform.position);
                if(dig.trail){DestroyBurst(dig.trail);dig.trail=null;}
            }
        }
        void EndDig(Fighter f)
        {
            var dig=f.action?.dig;if(dig==null)return;
            if(dig.trail)DestroyBurst(dig.trail);
            if(f.body){f.body.SetBurrowDepth(0);f.body.transform.position=ClearPosition(f,f.body.transform.position);}
            f.action.dig=null;
        }
    }
}
