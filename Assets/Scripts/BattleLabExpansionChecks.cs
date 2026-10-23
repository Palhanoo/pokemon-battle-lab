using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BattleLab.Native;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        IEnumerator ExpansionSmoke()
        {
            while(loading)yield return null;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/attack-expansion"));Directory.CreateDirectory(folder);
            var report=new List<string>();var failures=new List<string>();
            void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add(message);Debug.Log("EXPANSION "+message);File.WriteAllLines(Path.Combine(folder,"checks.txt"),report);}
            IEnumerator Shot(string name){yield return null;yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return null;yield return null;}
            Check(catalog.data.moves.Length>=306,"Expanded source catalog is present in player");
            mode=BattleMode.ZA;setup=inspect=false;ai=false;practice=true;gameSpeed=1;immersive=false;
            if(Environment.GetCommandLineArgs().Contains("-lab-new-clashes"))
            {
                teamSize=1;practice=false;playerSpecies=enemySpecies=Array.IndexOf(Species,"SWAMPERT");
                foreach(string id in new[]{"FIRE_BLAST","AURORA_BEAM","SURF"})
                {
                    ResetBattle();while(loading)yield return null;
                    foreach(var f in fighters)f.moves[0]=id;
                    int hp=fighters.Sum(f=>f.hp);Begin(0,0,MoveStyle.Normal);Begin(1,0,MoveStyle.Normal);
                    float deadline=Time.realtimeSinceStartup+30;
                    while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(clashCount==1&&fighters.Sum(f=>f.hp)==hp&&ResolvedHits==0,id+": equal opposing native attacks cancel at the clash without damage");
                }
                Check(smokeErrors==0,"New native clash drills have no runtime errors");
                File.WriteAllLines(Path.Combine(folder,"new-clashes.txt"),report);Debug.Log("LAB NEW CLASHES PASSED");Application.Quit(0);yield break;
            }
            bool movesOnly=Environment.GetCommandLineArgs().Contains("-lab-moves-only");
            if(!movesOnly)
            {
                playerSpecies=Array.IndexOf(Species,"PIKACHU");enemySpecies=Array.IndexOf(Species,"MUDKIP");
                for(int i=0;i<allySpecies.Length;i++)allySpecies[i]=playerSpecies;
                for(int i=0;i<foeSpecies.Length;i++)foeSpecies[i]=enemySpecies;
                foreach(var size in new[]{(2,2),(2,1),(3,1),(3,2),(6,1),(6,2),(6,6),(6,12)})
                {
                    allyCount=size.Item1;enemyCount=size.Item2;ResetBattle();while(loading)yield return null;
                    Check(loadError==null&&fighters.Count(f=>f.team==0)==Allies&&fighters.Count(f=>f.team==1)==Enemies,$"{Allies}v{Enemies}: exact independent rosters load");
                    var visited=new HashSet<Fighter>();for(int i=0;i<Allies;i++){visited.Add(Player);SwitchControl();}
                    Check(visited.Count==Allies&&controlled==0,"Every living ally controllable once per cycle");
                    visited.Clear();for(int i=0;i<Enemies;i++){visited.Add(Target);CycleTarget();}
                    Check(visited.Count==Enemies,"Every living enemy focusable");
                    for(int i=0;i<fighters.Length;i++)for(int j=i+1;j<fighters.Length;j++)
                        if(Vector3.Distance(fighters[i].body.transform.position,fighters[j].body.transform.position)+.02f<Clearance(fighters[i],fighters[j]))throw new Exception("Overlapping team spawn");
                    fighters.First(f=>f.team==0&&f!=Player).hp=0;Check(!Ended,"One ally faint does not end the fight");
                    Player.hp=0;SwitchControl();if(Allies>2)Check(Player.hp>0,"Control hands off to surviving ally");
                    foreach(var f in fighters.Where(f=>f.team==1))f.hp=0;Check(Ended,"Enemy team defeat ends fight");
                }
                ResetBattle();while(loading)yield return null;setup=true;yield return Shot("six-v-twelve-setup");setup=false;yield return Shot("six-v-twelve-hud");
                ai=true;float until=now+6;while(now<until)yield return null;ai=false;
                Check(ResolvedHits>0,"6v12 autonomous fighters approach and resolve attacks");
                ResetBattle();while(loading)yield return null;Check(ActiveActions==0&&tails.Count==0&&ResolvedHits==0,"Large-team reset clears damage and effect tails");
            }
            teamSize=1;playerSpecies=Array.IndexOf(Species,"SWAMPERT");enemySpecies=Array.IndexOf(Species,"SNORLAX");ResetBattle();while(loading)yield return null;
            Check(loadError==null,"Expanded-move review models loaded");
            setup=true;OpenMovePicker(0);allTestMoves=true;moveSearch="";yield return Shot("searchable-moves");movePickerSlot=-1;setup=false;Paused=true;
            var only=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-lab-move-filter="));
            foreach(var move in catalog.data.moves.Where(m=>only==null||only.Substring(17).Split(',').Contains(m.id)))
            foreach(var source in bdspFx.Has(move.id)?new[]{EffectSource.ZA,EffectSource.BDSP}:new[]{EffectSource.ZA})
            {
                int errors=smokeErrors;var p=Player;var t=Target;AttackPresentation presentation=null;bool failed=false;string reason=null;
                p.body.transform.position=new Vector3(-2,0,0);t.body.transform.position=new Vector3(2,0,0);t.body.transform.position=p.body.transform.position+Vector3.right*(Clearance(p,t)+Mathf.Min(3,Mathf.Max(.1f,move.rangeMax*.7f)));p.body.Face(t.body.Centre);t.body.Face(p.body.Centre);p.body.Idle();t.body.Idle();p.body.Tick(0);t.body.Tick(0);
                try
                {
                    presentation=new AttackPresentation(p.body,t.body,move,zaFx,bdspFx,nativeTiming,source);
                    if(!float.IsFinite(presentation.HitAt)||!float.IsFinite(presentation.FreeAt)||presentation.HitAt<0||presentation.FreeAt>45)throw new Exception("Non-finite or excessive action clock");
                    foreach(var r in presentation.Effect.gameObject.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled))foreach(var m in r.sharedMaterials)
                        if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Unsupported effect material");
                }
                catch(Exception e){failed=true;reason=e.Message;}
                int before=ResolvedHits;
                if(!failed)
                {
                    p.action=new Action{move=move,target=t,presentation=presentation,phase=1,style=MoveStyle.Normal,fieldRadius=move.radius};
                    int steps=0;bool shot=false;float limit=Mathf.Max(presentation.FreeAt,presentation.Effect.EndTime)+.2f;
                    while(presentation.Time<limit&&!failed)
                    {
                        try
                        {
                            now+=.05f;presentation.Step(.05f);p.action.clock=presentation.Time;p.body.Tick(.05f);t.body.Tick(.05f);
                            // The regular hit resolver is used, with focused contact in range.
                            ResolveHits();TickClashes(.05f);
                        }
                        catch(Exception e){failed=true;reason=e.Message;}
                        if(!shot&&presentation.Time>=presentation.HitAt)
                        {shot=true;yield return Shot(move.id+"-"+source);}
                        if(++steps%12==0)yield return null;
                        if(steps>1500){failed=true;reason="Effect did not terminate within 75 seconds";}
                    }
                    if(!failed&&(ResolvedHits!=before+1||!presentation.TailDone)){failed=true;reason="Hit count or finite tail failed";}
                }
                if(p.action!=null){p.action.presentation=null;p.action=null;}
                presentation?.Dispose();p.body.Idle();t.body.Idle();yield return null;
                if(smokeErrors!=errors){failed=true;reason="Runtime errors during move: "+lastSmokeError;}
                string result=move.id+"/"+source+": "+(failed?"FAIL "+reason:"native playback, one damage event, finite tail and cleanup passed");
                if(failed)failures.Add(result);report.Add(result);Debug.Log("EXPANSION "+result);
                File.WriteAllLines(Path.Combine(folder,"move-playback.txt"),report);
            }
            File.WriteAllLines(Path.Combine(folder,"failures.txt"),failures);
            Check(failures.Count==0,"Expanded catalog playback failures: "+failures.Count);
            Debug.Log("LAB EXPANSION PASSED");Application.Quit(0);
        }
    }
}
