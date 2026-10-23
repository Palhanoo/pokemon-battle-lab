using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        int smokeErrors;string lastSmokeError;
        void SmokeLog(string condition,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){smokeErrors++;lastSmokeError=condition;}}
        PokedexRoster pokedex;bool loading;string loadError;int picker=-1;string search="";Vector2 dexScroll;
        void LoadPokedex()
        {
            if(Environment.GetCommandLineArgs().Contains("-lab-smoke")||Environment.GetCommandLineArgs().Contains("-lab-team-smoke"))return;
            string folder=Path.Combine(Application.streamingAssetsPath,"Roster");
            if(File.Exists(Path.Combine(folder,"catalog.json")))pokedex=new PokedexRoster(folder,catalog);
        }
        IEnumerable<ModelEntry> RequiredModels()
        {
            if(inspect){yield return catalog.models.First(m=>m.species==Species[inspectSpecies]);yield break;}
            yield return catalog.Model(Species[playerSpecies],Sources[ValidSource(playerSpecies,playerSource)]);
            yield return catalog.Model(Species[enemySpecies],Sources[ValidSource(enemySpecies,enemySource)]);
            if(mode==BattleMode.ZA)
                foreach(int s in allySpecies.Take(Allies-1).Concat(foeSpecies.Take(Enemies-1)))yield return catalog.Model(Species[s],Sources[ValidSource(s,1)]);
        }
        public async void ResetBattle()
        {
            if(loading)return;
            clashDrill=false;clashMessage=null;clashMessageUntil=0;
            if(pokedex==null){ResetBattleReady();return;}
            loading=true;loadError=null;StopActions();foreach(var g in gallery)if(g)Destroy(g.gameObject);gallery.Clear();
            try
            {
                // Old actors must be destroyed before their shared import resources are evicted.
                await Task.Yield();await pokedex.Prepare(RequiredModels().Concat(RequiredForms()));if(!this)return;
                ResetBattleReady();
            }
            catch(Exception e){loadError=e.Message;Debug.LogException(e);}
            finally{loading=false;}
        }
        void OpenPicker(int slot){picker=slot;search="";dexScroll=Vector2.zero;}
        void PokedexUI()
        {
            Panel(310,110,1110,750);Text(330,127,990,$"POKÉDEX · {Species.Length} species · one newest usable model each",title,40);
            if(Button(new Rect(1275,130,120,35),"Close")){picker=-1;return;}
            Text(332,183,235,"Search name or Pokédex number",small);
            search=GUI.TextField(new Rect(595,180,790,34),search);
            var matches=catalog.data.species.Select((s,i)=>(s,i,row:pokedex?.Row(s.id)))
                .Where(x=>string.IsNullOrWhiteSpace(search)||x.s.id.Replace('_',' ').IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0||x.row!=null&&x.row.dex.ToString()==search.Trim()).OrderBy(x=>x.row?.dex??x.i).ToArray();
            dexScroll=GUI.BeginScrollView(new Rect(330,230,1065,540),dexScroll,new Rect(0,0,1038,Mathf.CeilToInt(matches.Length/3f)*66));
            int first=Mathf.Max(0,Mathf.FloorToInt(dexScroll.y/66)-1)*3;int last=Mathf.Min(matches.Length,first+36);
            for(int i=first;i<last;i++)
            {
                var item=matches[i];string source=item.row?.source??catalog.models.First(m=>m.species==item.s.id).source;
                if(Button(new Rect(i%3*344,i/3*66,333,58),$"{(item.row==null?"":"#"+item.row.dex+"  ")}{item.s.id.Replace('_',' ')}\n{source}"))
                {
                    if(picker==0)playerSpecies=item.i;else if(picker==1)enemySpecies=item.i;else if(picker==100)inspectSpecies=item.i;
                    else if(picker<20)allySpecies[picker-2]=item.i;else foeSpecies[picker-20]=item.i;
                    picker=-1;ResetBattle();
                }
            }
            GUI.EndScrollView();Text(332,790,1060,$"Z-A → Scarlet/Violet → Arceus → BDSP → Sword/Shield. Native body clips; {catalog.data.moves.Length} shared test moves. Some move slots are test-only.",small,50);
        }
        IEnumerator RosterSmoke()
        {
            var report=new List<string>();string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/pokedex"));Directory.CreateDirectory(folder);
            while(loading)yield return null;
            if(pokedex==null)throw new Exception("Missing Pokédex catalog");
            var args=Environment.GetCommandLineArgs();int startAt=Array.IndexOf(args,"-lab-roster-start");int startDex=startAt>=0?int.Parse(args[startAt+1]):0;int endAt=Array.IndexOf(args,"-lab-roster-end");int endDex=endAt>=0?int.Parse(args[endAt+1]):int.MaxValue;
            teamSize=1;ai=false;practice=true;mode=BattleMode.ZA;gameSpeed=1.5f;setup=inspect=false;
            // Every exported model is instantiated and exercises native body roles; representative moves use full effects.
            foreach(var row in pokedex.rows.Where(r=>r.dex>=startDex&&r.dex<endDex))
            {
                playerSpecies=Array.IndexOf(Species,row.species);ResetBattle();while(loading)yield return null;
                if(loadError!=null)throw new Exception(loadError);
                yield return null;var actor=Player.body;actor.Tick(.1f);
                if(!float.IsFinite(actor.BodyHeight)||actor.BodyHeight<=.001f)throw new Exception(row.species+" invalid bounds");
                var bones=actor.GetComponentsInChildren<Transform>();var before=bones.Select(b=>b.localToWorldMatrix).ToArray();bool changed=false;
                foreach(string role in new[]{"battle","run","attack","special","damage","faint"})
                {
                    // A one-key native clip is a valid static pose (for example Ferroseed idle).
                    // Motion across the full role set is asserted below.
                    actor.Role(role);for(int frame=0;frame<5;frame++)actor.Tick(.1f);
                    changed|=bones.Where((b,i)=>b.localToWorldMatrix!=before[i]).Any();
                }
                if(!changed)throw new Exception(row.species+" clips did not animate any bones");
                if(smokeErrors>0)throw new Exception(row.species+" logged "+smokeErrors+" runtime errors: "+lastSmokeError);
                actor.Idle();actor.Tick(.2f);if(pokedex.LoadedCount>RequiredModels().Concat(RequiredForms()).Distinct().Count())throw new Exception("Pokédex cache retained unused models");
                report.Add(row.species+" / "+row.source+" · loaded, bounds, six roles, cache limited to required base/form models passed");
                if(row.dex%25==0||new[]{"TREECKO","RAYQUAZA","POOCHYENA","CATERPIE"}.Contains(row.species))CaptureEvidence(Path.Combine(folder,row.species+".png"));
                File.WriteAllLines(Path.Combine(folder,"roster-smoke-"+startDex+".txt"),report);yield return null;
            }
            Debug.Log("LAB ROSTER SMOKE PASSED: "+report.Count);Application.Quit(0);
        }
    }
}
