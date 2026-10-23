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
        static readonly string[] HoennMoves={"BLAZE_KICK","MUDDY_WATER","ICE_BEAM","DRAGON_CLAW","ROCK_SLIDE","EARTHQUAKE"};
        static readonly string[] HoennUsers={"BLAZIKEN","SWAMPERT","WALREIN","SALAMENCE","AGGRON","FLYGON"};
        IEnumerator HoennSmoke()
        {
            while(loading)yield return null;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/hoenn-moves"));Directory.CreateDirectory(folder);
            var report=new List<string>();teamSize=1;ai=false;practice=true;setup=inspect=false;mode=BattleMode.ZA;gameSpeed=1;
            var only=Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith("-lab-only="))?.Substring(10).Split(',');
            for(int index=0;index<HoennMoves.Length;index++)
            {
                string id=HoennMoves[index];if(only!=null&&!only.Contains(id))continue;
                playerSpecies=Array.IndexOf(Species,HoennUsers[index]);enemySpecies=Array.IndexOf(Species,"MUDKIP");Paused=false;ResetBattle();while(loading)yield return null;
                if(loadError!=null)throw new Exception(loadError);
                if(!Player.data.moves.Contains(id))throw new Exception(HoennUsers[index]+" cannot select "+id);
                Player.moves[0]=id;Player.body.transform.position=Vector3.left*4;Target.body.transform.position=Vector3.right*4;
                if(!Command(0))throw new Exception(id+" command rejected");float limit=Time.realtimeSinceStartup+25;
                bool started=false;
                while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<limit)
                {started|=Player.action?.presentation!=null;yield return null;}
                if(!started||ActiveActions!=0||tails.Count!=0||ResolvedHits!=1||contactFeedbackCount!=1)throw new Exception(id+" approach/hit/cleanup failed "+ResolvedHits+" / "+contactFeedbackCount);
                report.Add(HoennUsers[index]+" "+id+": distant command approached; one hit/recoil/contact sound; no action or effect tail remains");
                File.WriteAllLines(Path.Combine(folder,"checks-in-progress.txt"),report);
                foreach(string source in new[]{"ZA","BDSP"})
                {
                    playerSpecies=Array.IndexOf(Species,source=="ZA"?HoennUsers[index]:"NIDOKING");ResetBattle();while(loading)yield return null;
                    if(loadError!=null)throw new Exception(loadError);Paused=true;
                    float distance=catalog.Move(id).shape=="Melee"?Clearance(Player,Target)+.15f:4;
                    Player.body.transform.position=Vector3.left*distance*.5f;Target.body.transform.position=Vector3.right*distance*.5f;
                    Player.body.Face(Target.body.Centre);Target.body.Face(Player.body.Centre);Player.body.Idle();Target.body.Idle();Player.body.Tick(0);Target.body.Tick(0);
                    var fx=new AttackPresentation(Player.body,Target.body,catalog.Move(id),zaFx,bdspFx,nativeTiming);
                    if(source=="BDSP"&&!(fx.Effect is BdspMove))throw new Exception(id+" missing BDSP variant");
                    var systems=fx.Effect.gameObject.GetComponentsInChildren<ParticleSystem>(true);
                    var mats=fx.Effect.gameObject.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
                    if(mats.Any(m=>!m.shader.isSupported))throw new Exception(id+" unsupported shader");
                    var layers=new Dictionary<string,int>();int peak=0,shot=0;bool contact=false;
                    var mid=(Player.body.Centre+Target.body.Centre)*.5f;
                    float end=Mathf.Max(fx.FreeAt,fx.Effect.EndTime)+.1f;
                    float start=fx.SoundAt;
                    for(float t=0;t<=end;t+=1f/60)
                    {
                        fx.Step(1f/60);Player.body.Tick(1f/60);Target.body.Tick(1f/60);
                        peak=Mathf.Max(peak,systems.Sum(ps=>ps.particleCount));
                        foreach(var ps in systems){string key=ps.transform.parent.name+"/"+ps.name;layers[key]=Mathf.Max(layers.TryGetValue(key,out int n)?n:0,ps.particleCount);}
                        if(fx.Time>=start+shot*Mathf.Max(.15f,(end-start)/12)&&shot<12)
                        {
                            cam.transform.position=mid+new Vector3(0,2.4f,-7.5f);cam.transform.LookAt(mid);
                            CaptureEvidence(Path.Combine(folder,source+"-"+id+"-"+(shot++).ToString("00")+".png"));
                        }
                        if(!contact&&fx.Time>=fx.HitAt)
                        {
                            contact=true;Target.body.React();cam.transform.position=mid+new Vector3(0,2.4f,-7.5f);cam.transform.LookAt(mid);
                            CaptureEvidence(Path.Combine(folder,source+"-"+id+"-contact.png"));
                            if(id=="MUDDY_WATER")
                            {
                                cam.transform.position=mid+new Vector3(-5,2.8f,-5);cam.transform.LookAt(mid);
                                CaptureEvidence(Path.Combine(folder,source+"-"+id+"-oblique.png"));
                            }
                        }
                    }
                    if(peak==0||fx.CueCount<1||!fx.TailDone)throw new Exception(id+" missing particles/body cues/expiry");
                    File.WriteAllLines(Path.Combine(folder,source+"-"+id+"-layers.txt"),layers.Select(p=>p.Key+"="+p.Value));
                    report.Add(source+" "+id+": "+systems.Length+" particle systems, peak "+peak+", "+fx.CueCount+" body cues; contact "+fx.HitAt.ToString("F3")+"; captures and cleanup checked");
                    fx.Dispose();yield return null;Paused=false;
                }
            }
            if(only==null)
            {
                foreach(string id in new[]{"MUDDY_WATER","ICE_BEAM","HYDRO_PUMP","PSYBEAM"})
                {
                    playerSpecies=enemySpecies=Array.IndexOf(Species,id=="MUDDY_WATER"?"SWAMPERT":"WALREIN");ResetBattle();while(loading)yield return null;
                    Player.moves[0]=Target.moves[0]=id;Player.body.transform.position=Vector3.left*2;Target.body.transform.position=Vector3.right*2;
                    Command(0);Begin(1,0,MoveStyle.Normal);float limit=Time.realtimeSinceStartup+25;bool captured=false;
                    while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<limit)
                    {
                        if(!captured&&clashCount>0){CaptureEvidence(Path.Combine(folder,"clash-"+id+".png"));captured=true;}yield return null;
                    }
                    if(clashCount!=1||ResolvedHits!=0||ActiveActions>0||tails.Count>0)throw new Exception(id+" equal clash failed: "+clashCount+" / "+ResolvedHits);
                    report.Add(id+": one equal-power clash, zero damage, all blocked effects disposed");
                }
                // A real mixed-type collision uses damage AFTER both STAB and target typing.
                playerSpecies=enemySpecies=Array.IndexOf(Species,"SWAMPERT");ResetBattle();while(loading)yield return null;
                Player.moves[0]="MUDDY_WATER";Target.moves[0]="ICE_BEAM";Player.body.transform.position=Vector3.left*2;Target.body.transform.position=Vector3.right*2;
                int waveDamage=CombatRules.Damage(Player.data,Target.data,catalog.Move("MUDDY_WATER"),catalog.data.types,mode,MoveStyle.Normal);
                int beamDamage=CombatRules.Damage(Target.data,Player.data,catalog.Move("ICE_BEAM"),catalog.data.types,mode,MoveStyle.Normal);
                var expected=ClashRules.Resolve(waveDamage,beamDamage);Command(0);float mixedLimit=Time.realtimeSinceStartup+25;
                while(Player.action!=null&&(Player.action.presentation==null||Player.action.clock<.6f)&&Time.realtimeSinceStartup<mixedLimit)yield return null;
                Begin(1,0,MoveStyle.Normal);bool mixedCaptured=false;int? residual=null;
                while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<mixedLimit)
                {
                    if(clashCount>0&&!mixedCaptured){mixedCaptured=true;residual=Player.action?.remainingDamage;CaptureEvidence(Path.Combine(folder,"clash-wave-vs-beam.png"));}
                    yield return null;
                }
                if(clashCount!=1||ActiveActions>0||tails.Count>0||residual!=expected.a||ResolvedHits!=(expected.a>0||expected.b>0?1:0))throw new Exception("Wave/beam STAB/type residual clash failed: "+clashCount+" / "+ResolvedHits+" / "+residual);
                report.Add($"Muddy Water vs Ice Beam: damage {waveDamage}/{beamDamage} after STAB/type; residual {expected.a}/{expected.b}; one contact flash and correct surviving hit");
                teamSize=3;practice=false;playerSpecies=Array.IndexOf(Species,"SWAMPERT");enemySpecies=Array.IndexOf(Species,"MUDKIP");
                foeSpecies[0]=foeSpecies[1]=enemySpecies;ResetBattle();while(loading)yield return null;Player.moves[0]="EARTHQUAKE";
                Player.body.transform.position=new Vector3(-1,0,0);var enemies=fighters.Where(f=>f.team==1).ToArray();
                for(int i=0;i<enemies.Length;i++)enemies[i].body.transform.position=new Vector3(1.6f,0,(i-1)*1.8f);
                var enemyHp=enemies.Select(f=>f.hp).ToArray();var allies=fighters.Where(f=>f.team==0).ToArray();var allyHp=allies.Select(f=>f.hp).ToArray();
                Command(0);float areaLimit=Time.realtimeSinceStartup+25;while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<areaLimit)yield return null;
                if(ActiveActions>0||ResolvedHits!=3||enemies.Where((f,i)=>f.hp>=enemyHp[i]).Any()||allies.Where((f,i)=>f.hp!=allyHp[i]).Any())throw new Exception("Earthquake area contacts/friendly fire failed "+ResolvedHits);
                report.Add("Earthquake: three nearby enemies each take one hit; lab allies remain unharmed");teamSize=1;practice=true;
                // Earthquake uses per-victim type checks; Flying immunity does not emit hit feedback.
                playerSpecies=Array.IndexOf(Species,"FLYGON");enemySpecies=Array.IndexOf(Species,"SALAMENCE");ResetBattle();while(loading)yield return null;Player.moves[0]="EARTHQUAKE";
                Command(0);float deadline=Time.realtimeSinceStartup+25;while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<deadline)yield return null;
                if(ActiveActions>0||contactFeedbackCount!=0)throw new Exception("Earthquake ignored Flying immunity");
                report.Add("Earthquake: Flying opponent immune, no damage feedback");
                // Reset during the moving wave must stop emission and prevent delayed hits.
                playerSpecies=Array.IndexOf(Species,"SWAMPERT");enemySpecies=Array.IndexOf(Species,"MUDKIP");ResetBattle();while(loading)yield return null;Player.moves[0]="MUDDY_WATER";Command(0);
                while(Player.action!=null&&(Player.action.presentation==null||Player.action.clock<1.6f))yield return null;
                ResetBattle();while(loading)yield return null;yield return new WaitForSecondsRealtime(.7f);
                if(ActiveActions>0||tails.Count>0||ResolvedHits!=0)throw new Exception("Reset leaked wave damage/effects");report.Add("Reset while wave travels: no stale damage, effects or actions");
            }
            if(smokeErrors>0)throw new Exception(lastSmokeError);
            File.WriteAllLines(Path.Combine(folder,"checks"+(only==null?"":"-"+string.Join("-",only))+".txt"),report);Debug.Log("LAB HOENN MOVES PASSED");Application.Quit(0);
        }
    }
}
