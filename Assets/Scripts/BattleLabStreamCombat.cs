using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        IEnumerator HitFeelSmoke()
        {
            while(loading)yield return null;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/hit-feel"));Directory.CreateDirectory(folder);
            var report=new List<string>();teamSize=1;ai=false;practice=false;setup=inspect=false;mode=BattleMode.ZA;gameSpeed=1;
            playerSpecies=Array.IndexOf(Species,"ARCANINE");enemySpecies=0;ResetBattle();while(loading)yield return null;
            if(loadError!=null)throw new Exception(loadError);
            testInput=Vector3.forward;float until=now+.67f;while(now<until)yield return null;
            testInput=Vector3.zero;until=now+.3f;while(now<until)yield return null;
            if(Player.body.CurrentClip!=Player.body.entry.battle.name)throw new Exception("Stopping did not select idle");
            Paused=true;
            var clean=LabActor.Spawn(Player.body.entry,new Vector3(20,0,20));clean.Idle();clean.Tick(.37f);
            Player.body.Idle();Player.body.Tick(.37f);
            var expected=clean.GetComponentsInChildren<Transform>();var actual=Player.body.GetComponentsInChildren<Transform>();
            for(int i=1;i<expected.Length;i++)
                if(Vector3.Distance(expected[i].localPosition,actual[i].localPosition)>.0001f||Quaternion.Angle(expected[i].localRotation,actual[i].localRotation)>.05f||Vector3.Distance(expected[i].localScale,actual[i].localScale)>.0001f)
                    throw new Exception("Run pose leaked into idle: "+actual[i].name);
            Destroy(clean.gameObject);cam.transform.position=Player.body.Centre+new Vector3(0,1.2f,-4);cam.transform.LookAt(Player.body.Centre);CaptureEvidence(Path.Combine(folder,"arcanine-stopped.png"));Paused=false;
            report.Add("Arcanine: actual movement then stop; every local bone pose matches a freshly spawned idle at the same time");
            foreach(string id in new[]{"CLOSE_COMBAT","EMBER","BRICK_BREAK","DRAGON_PULSE"})
            {
                ResetBattle();while(loading)yield return null;Player.moves[0]=id;
                Player.body.transform.position=new Vector3(-1,0,0);Target.body.transform.position=new Vector3(1,0,0);
                int hp=Target.hp;if(!Command(0))throw new Exception(id+" rejected");
                float deadline=Time.realtimeSinceStartup+20;bool impact=false;
                while(Player.action!=null&&Time.realtimeSinceStartup<deadline)
                {
                    if(!impact&&contactFeedbackCount>0)
                    {
                        impact=true;float frameAt=now+.18f;while(now<frameAt)yield return null;
                        var midpoint=(Player.body.Centre+Target.body.Centre)*.5f;cam.transform.position=midpoint+new Vector3(0,2.3f,-5);cam.transform.LookAt(midpoint);
                        CaptureEvidence(Path.Combine(folder,id+"-contact.png"));
                    }
                    yield return null;
                }
                if(Player.action!=null||ResolvedHits!=1||contactFeedbackCount!=1||Target.hp>=hp)throw new Exception(id+" hit/feedback mismatch");
                until=now+.5f;while(now<until)yield return null;
                if(clashBursts.Count!=0)throw new Exception("Contact burst lingered");
                report.Add(id+": exactly one damage/contact-feedback event; compact burst cleaned up");
            }
            ResetBattle();while(loading)yield return null;Begin(0,0,MoveStyle.Normal);Impact(Player,false);
            if(contactFeedbackCount!=0)throw new Exception("Miss produced hit feedback");Cancel(Player);
            report.Add("Miss: no impact feedback; reset clears recoil actors/bursts/hit-stop");
            if(smokeErrors>0)throw new Exception(lastSmokeError);
            File.WriteAllLines(Path.Combine(folder,"focused-checks.txt"),report);Debug.Log("LAB HIT FEEL PASSED");Application.Quit(0);
        }
        IEnumerator ReviewFixSmoke()
        {
            while(loading)yield return null;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/review-fixes"));Directory.CreateDirectory(folder);
            var report=new List<string>();teamSize=1;ai=false;practice=true;setup=inspect=false;mode=BattleMode.ZA;
            foreach(string id in new[]{"ARCANINE","IRON_JUGULIS","CHARIZARD"})
            {
                playerSpecies=Array.IndexOf(Species,id);ResetBattle();while(loading)yield return null;
                if(loadError!=null)throw new Exception(loadError);
                // Freeze normal controller updates, then sample several complete loops.
                Paused=true;var body=Player.body;var bones=body.GetComponentsInChildren<Transform>();
                foreach(string role in new[]{"battle","run"})
                {
                    body.Role(role,true);for(int i=0;i<100;i++)body.Tick(.073f);
                    var before=bones.Select(b=>b.localToWorldMatrix).ToArray();body.Tick(.137f);
                    if(!bones.Where((b,i)=>b.localToWorldMatrix!=before[i]).Any())throw new Exception(id+" frozen "+role+" after repeated loops");
                }
                body.Idle();body.Tick(.2f);Paused=false;UpdateCamera(1);cam.transform.position=body.Centre+body.transform.forward*Mathf.Max(2,body.BodyHeight*1.6f)+Vector3.up*.2f;cam.transform.LookAt(body.Centre);CaptureEvidence(Path.Combine(folder,id+".png"));
                report.Add(id+": idle and run still move after multiple loops");
            }
            // Switch to an uncached species immediately before starting the drill.
            playerSpecies=Array.IndexOf(Species,"GENGAR");StartClashDrill();while(loading)yield return null;yield return null;
            if(!clashDrill||fighters.Length!=2)throw new Exception("Async drill setup failed");
            ResetBattle();while(loading)yield return null;
            if(clashDrill||clashMessage!=null)throw new Exception("Drill/message leaked across reset");
            report.Add("Uncached drill load completes; ordinary reset clears drill and stale clash text");
            foreach(string moveId in new[]{"THUNDERBOLT","THUNDER","DRAGON_PULSE","FIRE_SPIN"})
            {
                var fx=new AttackPresentation(Player.body,Target.body,catalog.Move(moveId),zaFx,bdspFx,nativeTiming);
                float limit=fx.HitAt+(moveId=="FIRE_SPIN"?1.5f:4f);
                while(fx.Time<limit){fx.Step(.033f);Player.body.Tick(.033f);if(Mathf.Abs(fx.Time-fx.HitAt)<.02f)CaptureEvidence(Path.Combine(folder,moveId+".png"));yield return null;}
                if(moveId=="FIRE_SPIN"&&!fx.TailDone)throw new Exception("Fire Spin field did not expire");fx.Dispose();
                report.Add(moveId+": presentation and cleanup exercised");
            }
            if(smokeErrors>0)throw new Exception(lastSmokeError);
            File.WriteAllLines(Path.Combine(folder,"focused-checks.txt"),report);Debug.Log("LAB REVIEW FIXES PASSED");Application.Quit(0);
        }
        IEnumerator StreamCombatSmoke()
        {
            while(loading)yield return null;if(pokedex==null)throw new Exception("No streaming roster");
            var report=new List<string>();var ids=catalog.data.species.Take(9).Select(s=>s.id).Concat(new[]{"RAYQUAZA"})
                .Concat(pokedex.rows.GroupBy(r=>r.source).Select(g=>g.First().species)).Distinct().ToArray();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/stream-combat"));Directory.CreateDirectory(folder);
            teamSize=1;ai=false;practice=true;setup=inspect=false;mode=BattleMode.ZA;gameSpeed=1.5f;
            foreach(string id in ids)
            {
                playerSpecies=Array.IndexOf(Species,id);enemySpecies=id=="LUCARIO"?0:2;
                var data=catalog.Species(id);
                for(int choice=0;choice<data.moves.Length;choice++)
                {
                    loadouts[id]=new[]{choice}.Concat(Enumerable.Range(0,data.moves.Length).Where(i=>i!=choice).Take(3)).ToArray();
                    ResetBattle();while(loading)yield return null;if(loadError!=null)throw new Exception(loadError);yield return null;
                    var p=Player;var move=catalog.Move(p.moves[0]);p.body.transform.position=new Vector3(-1,0,0);
                    Target.body.transform.position=new Vector3(-1+Mathf.Min(4,Reach(p,Target,move)*.8f),0,0);
                    if(!Command(0))throw new Exception(id+" command rejected");
                    float deadline=Time.realtimeSinceStartup+18;bool captured=false;
                    while(p.action!=null&&Time.realtimeSinceStartup<deadline)
                    {
                        if(!captured&&p.action.presentation!=null&&p.action.clock>=p.action.presentation.SoundAt+.25f)
                        {CaptureEvidence(Path.Combine(folder,id+"-"+move.id+".png"));captured=true;}
                        yield return null;
                    }
                    if(p.action!=null||ResolvedHits!=1||smokeErrors>0)throw new Exception(id+" / "+move.id+" did not complete once; errors="+smokeErrors+" "+lastSmokeError);
                    if(p.body.CurrentClip!=p.body.entry.battle.name)throw new Exception(id+" failed idle recovery");
                    var origin=p.body.transform.position;testInput=Vector3.back;float until=now+.3f;while(now<until)yield return null;testInput=Vector3.zero;
                    if(Vector3.Distance(origin,p.body.transform.position)<.3f)throw new Exception(id+" movement remained locked");
                    report.Add(id+" / "+p.body.entry.source+" / "+move.id+": one hit, native cues, idle + movement recovery, no runtime errors");
                    File.WriteAllLines(Path.Combine(folder,"combat-smoke.txt"),report);
                }
            }
            Debug.Log("LAB STREAM COMBAT PASSED: "+report.Count+" attacks / "+ids.Length+" species");Application.Quit(0);
        }
    }
}
