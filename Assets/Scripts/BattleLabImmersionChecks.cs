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
        IEnumerator WaitIdle()
        {
            float deadline=Time.realtimeSinceStartup+35;
            while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(ActiveActions>0||tails.Count>0)throw new Exception("Immersion action/tail did not finish");
        }
        IEnumerator ImmersionSmoke()
        {
            if(Environment.GetCommandLineArgs().Contains("-lab-immersion-final")){yield return ImmersionFinalSmoke();yield break;}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/immersion"));Directory.CreateDirectory(folder);
            var report=new List<string>();immersionCheck=true;immersive=true;teamSize=1;mode=BattleMode.ZA;ai=false;practice=false;setup=inspect=false;gameSpeed=1;
            while(loading)yield return null;
            void Equip(string species,string move)
            {
                playerSpecies=Array.IndexOf(Species,species);enemySpecies=Array.IndexOf(Species,"MUDKIP");
                var s=catalog.Species(species);int choice=Array.IndexOf(s.moves,move);if(choice<0)throw new Exception("Missing tryout move "+species+"/"+move);
                loadouts[species]=new[]{choice}.Concat(Enumerable.Range(0,s.moves.Length).Where(i=>i!=choice).Take(3)).ToArray();
            }
            Equip("SWAMPERT","MUDDY_WATER");ResetBattle();while(loading)yield return null;
            if(loadError!=null)throw new Exception(loadError);var entry=Player.body.entry;
            foreach(var source in new[]{EffectSource.ZA,EffectSource.BDSP})
            {
                effectSource=source;testAim=Target.body.Centre;Player.ready[0]=0;Command(0);yield return null;
                if(Player.body.entry!=entry||Player.action==null)throw new Exception("Effect selection changed body or rejected move");
                if((Player.action.presentation.Effect is BdspMove)!=(source==EffectSource.BDSP))throw new Exception("Wrong selected effect source");
                yield return new WaitForSecondsRealtime(1.2f);CaptureEvidence(Path.Combine(folder,"same-swampert-"+source+".png"));
                yield return WaitIdle();
            }
            report.Add("Same Swampert model: independent ZA and BDSP native effects, cleanup");
            Equip("WALREIN","ICE_BEAM");effectSource=EffectSource.ZA;ResetBattle();while(loading)yield return null;
            testAim=Target.body.Centre+Vector3.forward*4;int hp=Target.hp;Command(0);yield return WaitIdle();
            if(Target.hp!=hp)throw new Exception("Free-aim miss damaged selected target");report.Add("Ice Beam aimed away: selected target takes no damage");
            Player.ready[0]=0;testAim=Target.body.Centre;Command(0);yield return new WaitForSecondsRealtime(1.7f);CaptureEvidence(Path.Combine(folder,"aimed-ice-beam.png"));yield return WaitIdle();
            if(Target.hp>=hp||ResolvedHits!=1)throw new Exception("Aimed beam failed one physical hit");report.Add("Ice Beam aimed at body: exactly one hit");
            Equip("FLYGON","EARTHQUAKE");ResetBattle();while(loading)yield return null;earthquakeRadius=7;testAim=null;
            var start=Player.body.transform.position;Target.body.transform.position=start+Vector3.right*6;hp=Target.hp;Command(0);yield return new WaitForSecondsRealtime(.8f);CaptureEvidence(Path.Combine(folder,"earthquake-radius.png"));yield return WaitIdle();
            if(Target.hp>=hp||Vector3.Distance(start,Player.body.transform.position)>.05f)throw new Exception("Earthquake not self-centered or radius ignored");report.Add("Earthquake casts in place; 7m radius hits distant grounded enemy");
            testFly=true;yield return new WaitForSecondsRealtime(1.2f);if(Player.altitude<2||Player.flightEnergy>=3)throw new Exception("Flight did not rise/spend energy");CaptureEvidence(Path.Combine(folder,"flygon-flight.png"));
            testFly=false;yield return new WaitForSecondsRealtime(1.6f);if(Player.altitude>.05f)throw new Exception("Flight did not land");report.Add("Flight rises, spends energy and lands");
            Equip("SWAMPERT","MUDDY_WATER");ResetBattle();while(loading)yield return null;
            Player.body.transform.position=new Vector3(-6,0,6);if(HabitatSpeed(Player)<1.6f)throw new Exception("Water speed missing");CaptureEvidence(Path.Combine(folder,"water-lane.png"));Player.body.transform.position=Vector3.zero;if(HabitatSpeed(Player)!=1)throw new Exception("Water speed leaked onto land");report.Add("Water-type speed bonus restricted to water lane");
            foreach(string species in new[]{"BLAZIKEN","SWAMPERT","SALAMENCE"})
            {
                playerSpecies=Array.IndexOf(Species,species);ResetBattle();while(loading)yield return null;
                if(loadError!=null)throw new Exception(loadError);var f=Player;var original=f.body.entry;f.hp=f.MaxHp/2;float ratio=(float)f.hp/f.MaxHp;
                ToggleMega();yield return new WaitForSecondsRealtime(1.5f);
                if(f.megaId==null||f.body.entry==original||f.data.stats.SequenceEqual(f.baseData.stats))throw new Exception("Mega model/stats not swapped: "+species);
                CaptureEvidence(Path.Combine(folder,"mega-"+species+".png"));RevertForm(f);yield return null;
                if(f.megaId!=null||f.body.entry!=original||Mathf.Abs((float)f.hp/f.MaxHp-ratio)>.01f)throw new Exception("Mega revert changed body or health fraction");
                report.Add(species+": native Mega model/stats, HP fraction preserved on return");
            }
            ResetBattle();while(loading)yield return null;yield return null;
            if(GameObject.Find("Attack aim point")||fighters.Any(f=>f.megaFx||f.pendingMega!=null||f.megaId!=null)||ActiveActions!=0)throw new Exception("Reset leaked aim/form state");
            if(smokeErrors>0)throw new Exception("Immersion runtime error: "+lastSmokeError);
            report.Add("Reset clears transformations, aim anchors, actions and effects");
            setup=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"setup-ui.png"));yield return null;yield return null;setup=false;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"play-ui.png"));yield return null;yield return null;
            File.WriteAllLines(Path.Combine(folder,"checks.txt"),report);
            Debug.Log("LAB IMMERSION PASSED");Application.Quit(0);
        }
    }
}
