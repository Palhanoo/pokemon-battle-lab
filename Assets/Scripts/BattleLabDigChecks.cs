using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        IEnumerator DigSmoke()
        {
            while(loading)yield return null;
            teamSize=1;mode=BattleMode.ZA;ai=false;practice=true;setup=inspect=false;gameSpeed=1;
            playerSpecies=Array.IndexOf(Species,"NIDOKING");enemySpecies=Array.IndexOf(Species,"MUDKIP");ResetBattle();while(loading)yield return null;
            if(loadError!=null)throw new Exception(loadError);
            Paused=true;var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/attack-completion/dig"));Directory.CreateDirectory(folder);
            void Step(float seconds)
            {for(float t=0;t<seconds;t+=.02f){now+=.02f;foreach(var f in fighters){f.body.Tick(.02f);if(f.action!=null)TickAction(f,.02f);}ResolveHits();}}
            void StartDig(Fighter who)
            {Cancel(who);who.moves[0]="DIG";Begin(Array.IndexOf(fighters,who),0,MoveStyle.Normal);Step(2.1f);if(!IsUnderground(who))throw new Exception("Dig never entered native hidden interval");}
            void Restored(Fighter f)
            {if(f.body.GetComponentsInChildren<Renderer>(true).Any(r=>r.forceRenderingOff)||Mathf.Abs(f.body.PosedBounds().min.y)>.08f)throw new Exception("Dig visibility/grounding not restored");}
            Player.body.transform.position=new Vector3(-4,0,0);Target.body.transform.position=new Vector3(4,0,0);StartDig(Player);
            if(!Player.body.GetComponentsInChildren<Renderer>(true).Any(r=>r.forceRenderingOff))throw new Exception("Buried body is visible");
            var at=Player.body.transform.position;testInput=Vector3.forward;yaw=0;TickRealtime(.2f);testInput=Vector3.zero;
            if(Vector3.Distance(at,Player.body.transform.position)<.8f)throw new Exception("Direct input did not steer underground");
            Step(.2f);if(Player.action.dig.trail.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)==0)throw new Exception("Underground trail is invisible");
            cam.transform.position=Player.body.transform.position+new Vector3(0,3,-6);cam.transform.LookAt(Player.body.transform.position);CaptureEvidence(Path.Combine(folder,"steering.png"));
            at=Player.body.transform.position;float clock=Player.action.clock;yield return new WaitForSecondsRealtime(.2f);
            if(Player.action.clock!=clock||Player.body.transform.position!=at)throw new Exception("Paused Dig continued");
            int hits=ResolvedHits;var enemyBefore=Target.body.transform.position;Target.body.transform.position=Player.body.transform.position;Begin(1,0,MoveStyle.Normal);TickAction(Target,.02f);if(Target.action?.presentation==null)throw new Exception("Incoming attack fixture did not wind up");Target.action.clock=Target.action.presentation.HitAt;ResolveHits();Cancel(Target);Target.body.transform.position=enemyBefore;
            if(ResolvedHits!=hits)throw new Exception("Buried actor took an ordinary attack");
            Step(3.6f);if(ResolvedHits!=hits)throw new Exception("Dig hit a distant target after steering away");Restored(Player);
            StartDig(Player);Player.body.transform.position=Target.body.transform.position+Vector3.left*(Clearance(Player,Target)+.2f);Step(3.6f);
            if(ResolvedHits!=hits+1)throw new Exception("Dig did not hit at actual emergence");Restored(Player);
            CaptureEvidence(Path.Combine(folder,"emerged.png"));
            StartDig(Player);Cancel(Player);Restored(Player);
            StartDig(Player);Player.hp=0;TickAction(Player,.02f);Player.hp=Player.MaxHp;Player.body.Idle();Player.body.Tick(0);Restored(Player);
            Player.body.transform.position=new Vector3(-4,0,0);Target.body.transform.position=new Vector3(4,0,0);StartDig(Target);ai=true;var before=Vector3.Distance(Player.body.transform.position,Target.body.transform.position);Step(.6f);ai=false;
            if(Vector3.Distance(Player.body.transform.position,Target.body.transform.position)>before-1)throw new Exception("AI did not travel underground");Cancel(Target);Restored(Target);
            StartDig(Player);ResetBattle();while(loading)yield return null;Paused=true;Restored(Player);
            if(GameObject.Find("Dig travelling soil"))throw new Exception("Reset leaked Dig trail");
            if(smokeErrors>0)throw new Exception(lastSmokeError);
            File.WriteAllText(Path.Combine(folder,"checks.txt"),"PASS: Nidoking direct underground input, visible soil trail, pause, incoming immunity, distant miss, nearby hit, emergence grounding, cancel, faint, AI travel, reset cleanup.\n");
            Debug.Log("LAB DIG CHECKS PASSED");Application.Quit(0);
        }
    }
}
