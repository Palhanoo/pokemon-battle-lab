using UnityEngine;
namespace BattleLab.Native
{
    public partial class BdspMove
    {
        static void FeatherWaveEdges(GameObject fx)
        {
            // The native crest spans a cinematic viewport and has hard outer edges.
            // Keep its mesh/flow textures, feathering only the finite arena boundary.
            foreach(var renderer in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var mesh=renderer.mesh;if(!mesh)continue;
                var vertices=mesh.vertices;var mask=new System.Collections.Generic.List<Vector2>();mesh.GetUVs(2,mask);
                var b=mesh.bounds;
                for(int i=0;i<vertices.Length;i++)
                {
                    float edge=Mathf.Min(vertices[i].x-b.min.x,b.max.x-vertices[i].x)/Mathf.Max(.01f,b.size.x*.15f);
                    float crest=(b.max.y-vertices[i].y)/Mathf.Max(.01f,b.size.y*.12f);
                    mask[i]=new Vector2(mask[i].x,mask[i].y*Mathf.SmoothStep(0,1,Mathf.Min(edge,crest)));
                }
                mesh.SetUVs(2,mask);
            }
        }
        // Preserve native particles and clocks; replace fixed cinematic coordinates
        // with our current fighters' positions and readable action-camera dimensions.
        void AdaptToArena(Group g)
        {
            if(!g.fx||g.cameraLayer)return;
            var direction=defender.Center-attacker.Center;
            var facing=direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):attacker.root.rotation;
            var groundDirection=defender.root.position-attacker.root.position;groundDirection.y=0;
            var groundFacing=groundDirection.sqrMagnitude>.001f?Quaternion.LookRotation(groundDirection):attacker.root.rotation;
            var from=VioletMove.Node(attacker,"eff_rangeattack01");var beam=defender.Center-from;
            if(moveId=="MOVE_MUDDY_WATER")
            {
                if(g.file.StartsWith("ew330_base_wave"))
                {
                    g.tr.position=Vector3.Lerp(attacker.root.position,defender.root.position,Mathf.Clamp01(time/Mathf.Max(.1f,hitTime)));
                    g.tr.rotation=groundFacing;
                    g.tr.localScale=Vector3.Scale(g.scale,new Vector3(.035f,.3f,.045f));
                    // Native floor waves have negative troughs; fit their mesh baseline
                    // above the arena using the authored 14.27054cm trough and .01 particle Y scale.
                    if(g.file.EndsWith("01"))g.tr.position+=Vector3.up*(14.27054f*.01f*g.tr.localScale.y);
                    if(g.file.EndsWith("04")){g.tr.position=defender.root.position;g.tr.localScale=Vector3.one*.15f;}
                }
                else g.tr.localScale=g.scale*Size(g.anchor??attacker)*.45f;
            }
            if(moveId=="MOVE_ICE_BEAM")
            {
                if(g.file=="ew058_beam")
                {
                    g.tr.position=from;g.tr.rotation=beam.sqrMagnitude>.001f?Quaternion.LookRotation(beam):facing;
                    float grow=Mathf.Clamp01((time-g.born)/.28f);
                    g.tr.localScale=new Vector3(.7f,.7f,beam.magnitude/4.8f*grow);
                }
                else if(g.file=="ew058_beam_ice")
                {g.tr.position=Vector3.Lerp(from,defender.Center,.65f);g.tr.rotation=facing;g.tr.localScale=Vector3.one*.3f;}
                else if(g.file=="ew058_charge"||g.file.StartsWith("ew058_muzzle"))
                {g.tr.position=from;g.tr.rotation=facing;g.tr.localScale=g.scale*.3f;}
                else if(g.file=="ew058_freezen_field")
                {g.tr.position=Vector3.Lerp(attacker.root.position,defender.root.position,Mathf.Clamp01((time-1)/1.2667f));g.tr.rotation=groundFacing;g.tr.localScale=Vector3.one*.18f;}
                else g.tr.localScale=g.scale*Mathf.Clamp(Size(defender)*.4f,.1f,.3f);
                if(g.file=="ew058_hit_last")g.tr.localScale*=.4f;
            }
            if(moveId=="MOVE_EARTHQUAKE")
            {
                if(g.model){g.tr.position=attacker.root.position;g.tr.localScale=Vector3.one;g.tr.rotation=groundFacing;}
                else if(g.file=="ew089_at_kem"||g.file=="ew089_crack_kem01")
                {g.tr.position=attacker.root.position;g.tr.localScale=g.scale*Size(attacker)*.22f;}
            }
            if(moveId=="MOVE_BLAZE_KICK")
            {
                g.tr.localScale=g.scale*Size(g.anchor??attacker)*.4f;
                if(g.file=="ew299_foot")g.tr.position=Vector3.Lerp(attacker.Center,defender.Center,Mathf.Clamp01((time-.4333f)/.1667f));
            }
            if(moveId=="MOVE_DRAGON_CLAW")
            {
                if(g.file=="ew337_charge")g.tr.localScale=g.scale*Size(attacker)*.6f;
                if(g.file=="ew337_hit"){g.tr.position=defender.Center;g.tr.localScale=g.scale*Mathf.Clamp(Size(defender),.6f,1);}
            }
        }
    }
}
