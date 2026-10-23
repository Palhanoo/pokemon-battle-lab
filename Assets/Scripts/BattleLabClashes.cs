using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleLab.Native;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        int clashCount;string clashMessage;float clashMessageUntil;
        readonly List<(GameObject root,float end)> clashBursts=new();
        void ResolveClashes()
        {
            if(mode!=BattleMode.ZA)return;
            for(int i=0;i<fighters.Length;i++)for(int j=i+1;j<fighters.Length;j++)
            {
                var f=fighters[i];var g=fighters[j];var a=f.action;var b=g.action;
                if(f.team==g.team||a==null||b==null||a.hit||b.hit||a.blocked||b.blocked||a.phase!=1||b.phase!=1||a.met.Contains(b)||a.move.shape!="Projectile"||b.move.shape!="Projectile")continue;
                if(!a.presentation.Travel(out var a0,out var a1)||!b.presentation.Travel(out var b0,out var b1))continue;
                float radius=Mathf.Clamp(a.move.radius*.2f,.12f,.35f)+Mathf.Clamp(b.move.radius*.2f,.12f,.35f);
                if(!ClashRules.SweptContact(a0,a1,b0,b1,radius,out var point))continue;
                int da=a.remainingDamage??CombatRules.Damage(f.data,a.target.data,a.move,catalog.data.types,mode,a.style);
                int db=b.remainingDamage??CombatRules.Damage(g.data,b.target.data,b.move,catalog.data.types,mode,b.style);
                var result=ClashRules.Resolve(da,db);a.met.Add(b);b.met.Add(a);clashCount++;
                a.remainingDamage=result.a;b.remainingDamage=result.b;
                if(result.a==0){a.blocked=true;a.presentation.BlockEffects();}
                if(result.b==0){b.blocked=true;b.presentation.BlockEffects();}
                SpawnClash(point);
                clashMessage=$"CLASH · {a.move.name} {da} vs {b.move.name} {db} · "+(result.a>0?$"{a.move.name} continues: {result.a} damage":result.b>0?$"{b.move.name} continues: {result.b} damage":"both cancelled");
                clashMessageUntil=now+4;Note(clashMessage);
            }
        }
        void ResolveHits()
        {
            foreach(var f in fighters)
            {
                var a=f.action;if(a==null||a.phase!=1||a.hit||a.clock<a.presentation.HitAt)continue;
                a.hit=true;
                if(!a.blocked&&f.hp>0&&a.target.hp>0)
                {
                    bool contact=!Direct||Vector3.Distance(f.body.transform.position,a.target.body.transform.position)<=Reach(f,a.target,a.move);
                    if(Direct&&now<a.target.dodgeUntil)contact=false;
                    Impact(f,contact);
                }
                if(f.action!=null)f.state=a.blocked?"Attack blocked":"Recovering";
            }
        }
        void ContactFeedback(Fighter attacker,Fighter target,int damage)
        {
            var away=target.body.Centre-attacker.body.Centre;away.y=0;
            target.body.Recoil(away,Mathf.Lerp(.055f,.13f,Mathf.Clamp01((float)damage/target.MaxHp)));
            var contact=target.body.Centre-away.normalized*Mathf.Min(.3f,target.body.Bounds.extents.magnitude*.25f);
            SpawnBurst(contact,Mathf.Clamp(target.body.BodyHeight*.2f,.18f,.38f),.28f);
            var sound=soundCatalog.MoveClip("TACKLE");if(sound)target.voice.PlayOneShot(sound,.32f);
            if(Direct&&(attacker==Player||target==Player))hitStopRemaining=Mathf.Max(hitStopRemaining,.045f);
            contactFeedbackCount++;
        }
        void SpawnClash(Vector3 position)
        {
            var root=SpawnBurst(position,.6f,.45f);
            var ring=root.AddComponent<LineRenderer>();ring.sharedMaterial=Material(new Color(.45f,.85f,1));
            ring.useWorldSpace=false;ring.loop=true;ring.positionCount=48;ring.widthMultiplier=.09f;
            ring.alignment=LineAlignment.View;
            var sound=soundCatalog.MoveClip("TACKLE");if(sound&&Player!=null)Player.voice.PlayOneShot(sound,.45f);
        }
        GameObject SpawnBurst(Vector3 position,float scale,float duration)
        {
            var set=zaFx.moves.SelectMany(m=>m.sets).First(s=>s.name=="cmn_impact_tp01_00");
            var root=VioletFx.Build(set.text,n=>zaFx.textures.FirstOrDefault(t=>t.name==n),zaFx.material);
            root.name="Ranged clash";root.transform.position=position;root.transform.localScale=Vector3.one*scale;
            foreach(var ps in VioletFx.Roots(root)){var em=ps.emission;em.enabled=true;ps.Simulate(0,true,true,false);}
            clashBursts.Add((root,now+duration));return root;
        }
        void TickClashes(float dt)
        {
            for(int i=clashBursts.Count-1;i>=0;i--)
            {
                var burst=clashBursts[i];foreach(var ps in VioletFx.Roots(burst.root))ps.Simulate(dt,true,false,false);
                var ring=burst.root.GetComponent<LineRenderer>();
                if(ring)
                {
                    float t=Mathf.Clamp01(1-(burst.end-now)/.45f);ring.widthMultiplier=.09f*(1-t);
                    burst.root.transform.rotation=cam.transform.rotation;
                    for(int n=0;n<ring.positionCount;n++){float a=n*Mathf.PI*2/ring.positionCount;ring.SetPosition(n,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*Mathf.Lerp(.12f,1.6f,t));}
                }
                if(now>=burst.end){DestroyBurst(burst.root);clashBursts.RemoveAt(i);}
            }
        }
        void DestroyBurst(GameObject root)
        {
            if(!root)return;
            foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>())
            {if(r.sharedMaterial)Destroy(r.sharedMaterial);if(r.mesh&&r.mesh.name.StartsWith("violet primitive"))Destroy(r.mesh);}
            Destroy(root);
        }
        IEnumerator ClashSmoke(string folder,List<string> report)
        {
            teamSize=1;playerSpecies=enemySpecies=4;playerSource=enemySource=1;ai=false;practice=false;ResetBattle();
            fighters[0].body.transform.position=new Vector3(-1.2f,0,0);fighters[1].body.transform.position=new Vector3(1.2f,0,0);
            int hp=fighters.Sum(f=>f.hp);Command(1);Begin(1,1,MoveStyle.Normal);
            float deadline=Time.realtimeSinceStartup+12;bool captured=false;
            while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)
            {if(clashCount>0&&!captured){CaptureEvidence(Path.Combine(folder,"equal-ranged-clash.png"));captured=true;}yield return null;}
            if(clashCount!=1||fighters.Sum(f=>f.hp)!=hp||ResolvedHits!=0)throw new Exception("Equal ranged attacks did not cancel without damage");
            report.Add("Equal opposing Ember projectiles collide mid-flight, cancel both effects/hits, no HP loss");
            ResetBattle();fighters[0].body.transform.position=new Vector3(-1.2f,0,0);fighters[1].body.transform.position=new Vector3(1.2f,0,0);
            int strong=CombatRules.Damage(fighters[0].data,fighters[1].data,catalog.Move("FLAMETHROWER"),catalog.data.types,mode,MoveStyle.Normal);
            int weak=CombatRules.Damage(fighters[1].data,fighters[0].data,catalog.Move("EMBER"),catalog.data.types,mode,MoveStyle.Normal);
            int expected=ClashRules.Resolve(strong,weak).a;hp=fighters[1].hp;Command(2);Begin(1,1,MoveStyle.Normal);deadline=Time.realtimeSinceStartup+12;
            while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)yield return null;
            if(expected<=0||clashCount!=1||hp-fighters[1].hp!=expected||fighters[0].hp!=fighters[0].MaxHp)throw new Exception($"Clash survivor damage mismatch: {strong}/{weak} expected {expected}, actual {hp-fighters[1].hp}, clashes {clashCount}");
            report.Add($"Stronger Flamethrower {strong} vs Ember {weak}: survivor deals exactly {expected} after STAB/type/defense");
            playerSpecies=0;enemySpecies=2;
        }
    }
}
