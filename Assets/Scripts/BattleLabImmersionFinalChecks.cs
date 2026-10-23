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
        IEnumerator ImmersionFinalSmoke()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/immersion/final-review"));Directory.CreateDirectory(folder);
            var report=new List<string>();immersionCheck=true;immersive=true;teamSize=1;mode=BattleMode.ZA;ai=false;practice=false;setup=inspect=false;gameSpeed=1;
            while(loading)yield return null;
            void Equip(string species,string move)
            {
                playerSpecies=Array.IndexOf(Species,species);enemySpecies=Array.IndexOf(Species,"MUDKIP");var s=catalog.Species(species);int choice=Array.IndexOf(s.moves,move);
                loadouts[species]=new[]{choice}.Concat(Enumerable.Range(0,s.moves.Length).Where(i=>i!=choice).Take(3)).ToArray();
            }
            int hp;bool polish=Environment.GetCommandLineArgs().Contains("-lab-polish-only");
            if(!polish)
            {
            Equip("WALREIN","ICE_BEAM");enemySpecies=playerSpecies;effectSource=EffectSource.ZA;ResetBattle();while(loading)yield return null;
            testAim=Target.body.Centre;hp=fighters.Sum(f=>f.hp);Command(0);Begin(1,0,MoveStyle.Normal);
            float deadline=Time.realtimeSinceStartup+15;bool capture=false;
            while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)
            {if(clashCount>0&&!capture){CaptureEvidence(Path.Combine(folder,"aimed-beam-clash.png"));capture=true;}yield return null;}
            if(clashCount!=1||fighters.Sum(f=>f.hp)!=hp||ResolvedHits!=0)throw new Exception("Aimed beams failed equal-power clash");yield return WaitIdle();report.Add("Aimed Ice Beam versus opponent Ice Beam: one clash, no damage, cleanup");
            }
            Equip("SWAMPERT","EARTHQUAKE");earthquakeRadius=7;
            foreach(var source in new[]{EffectSource.ZA,EffectSource.BDSP})
            {
                effectSource=source;ResetBattle();while(loading)yield return null;testAim=null;
                Target.body.transform.position=Player.body.transform.position+Vector3.right*6;hp=Target.hp;Command(0);
                yield return null;float hit=Player.action.presentation.HitAt;
                yield return new WaitForSecondsRealtime(Mathf.Max(.1f,hit-.08f));CaptureEvidence(Path.Combine(folder,"quake-"+source+"-contact.png"));
                yield return new WaitForSecondsRealtime(.25f);CaptureEvidence(Path.Combine(folder,"quake-"+source+"-field.png"));yield return WaitIdle();
                if(Target.hp>=hp)throw new Exception("Expanded earthquake missed distant enemy");report.Add(source+" Earthquake: active field captures, 7m hit, expiry");
            }
            if(!polish)
            {
            Equip("BLAZIKEN","BLAZE_KICK");effectSource=EffectSource.ZA;ResetBattle();while(loading)yield return null;
            Begin(1,0,MoveStyle.Normal);ToggleMega();if(Target.action!=null)throw new Exception("Mega left stale incoming action");
            yield return new WaitForSecondsRealtime(4);hp=Target.hp;Command(0);yield return new WaitForSecondsRealtime(1.5f);CaptureEvidence(Path.Combine(folder,"mega-blaze-kick.png"));yield return WaitIdle();
            if(Target.hp>=hp||Player.megaId==null)throw new Exception("Mega could not complete an attack");report.Add("Mega Blaziken cancels old body attachments and performs native Blaze Kick with damage");
            }
            playerSpecies=Array.IndexOf(Species,"SALAMENCE");ResetBattle();while(loading)yield return null;yield return new WaitForSecondsRealtime(.8f);CaptureEvidence(Path.Combine(folder,"large-form-camera.png"));
            setup=true;yield return null;yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"setup-ui.png"));yield return null;yield return null;setup=false;yield return null;yield return null;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"play-ui.png"));yield return null;yield return null;
            playerSpecies=Array.IndexOf(Species,"STARMIE");ResetBattle();while(loading)yield return null;Player.body.transform.position=new Vector3(-6,0,6);yield return null;
            if(HabitatSpeed(Player)!=1.65f)throw new Exception("Starmie water movement missing");CaptureEvidence(Path.Combine(folder,"starmie-water.png"));report.Add("Starmie receives water speed bonus; revised setup and large-form camera captured");
            if(smokeErrors>0)throw new Exception(lastSmokeError);File.WriteAllLines(Path.Combine(folder,polish?"polish-checks.txt":"checks.txt"),report);Debug.Log("LAB IMMERSION FINAL PASSED");Application.Quit(0);
        }
    }
}
