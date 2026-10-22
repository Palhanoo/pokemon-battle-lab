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
