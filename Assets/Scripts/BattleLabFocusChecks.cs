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
        IEnumerator FocusSmoke()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/battle-focus"));Directory.CreateDirectory(folder);
            var report=new List<string>();
            void Check(bool valid,string detail){if(!valid)throw new Exception(detail);report.Add(detail);Debug.Log("FOCUS CHECK "+detail);File.WriteAllLines(Path.Combine(folder,"checks.txt"),report);}
            void Equip(string species,string move)
            {
                playerSpecies=Array.IndexOf(Species,species);enemySpecies=Array.IndexOf(Species,"MUDKIP");var s=catalog.Species(species);int choice=Array.IndexOf(s.moves,move);
                if(choice<0)throw new Exception("Missing test move "+species+"/"+move);
                loadouts[species]=new[]{choice}.Concat(Enumerable.Range(0,s.moves.Length).Where(i=>i!=choice).Take(3)).ToArray();
            }
            IEnumerator Screenshot(string name)
            {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return null;yield return null;}
            while(loading)yield return null;
            immersionCheck=true;immersive=false;teamSize=1;mode=BattleMode.ZA;ai=false;practice=true;setup=inspect=false;gameSpeed=1;
            if(Environment.GetCommandLineArgs().Contains("-lab-flight-hud"))
            {
                playerSpecies=Array.IndexOf(Species,"ROTOM");ResetBattle();while(loading)yield return null;
                yield return new WaitForSecondsRealtime(.4f);yield return Screenshot("flight-ready");
                Check(Mathf.Approximately(Player.flightEnergy,FlightCapacity),"Flight starts with full three-second reserve");
                testFly=true;yield return new WaitForSecondsRealtime(2.4f);yield return Screenshot("flight-low");
                Check(Player.altitude>1.3f&&Player.flightEnergy<.75f&&Player.flightEnergy>0,"Rotom levitates with a visible low reserve");
                yield return new WaitForSecondsRealtime(1.5f);yield return Screenshot("flight-empty");
                Check(Player.altitude<.05f&&Player.flightEnergy==0,"Exhaustion lands Rotom and holding F cannot recharge");
                testFly=false;yield return new WaitForSecondsRealtime(2);yield return Screenshot("flight-recovering");
                Check(Player.flightEnergy>1&&Player.flightEnergy<1.5f,"Grounded reserve recovers after F is released");
                yield return new WaitForSecondsRealtime(3.2f);
                Check(Mathf.Approximately(Player.flightEnergy,FlightCapacity),"Reserve returns to full without changing flight mechanics");
                Check(smokeErrors==0,"Flight HUD review has no runtime errors");
                File.WriteAllLines(Path.Combine(folder,"flight-stamina-checks.txt"),report);
                Debug.Log("LAB FLIGHT HUD PASSED");Application.Quit(0);yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-lab-setup-final"))
            {
                Equip("SWAMPERT","HYDRO_PUMP");ResetBattle();while(loading)yield return null;
                setup=true;yield return null;yield return null;yield return Screenshot("setup-controls");
                Check(smokeErrors==0,"Final Setup renders after viewport settles, with no exceptions");
                Debug.Log("LAB SETUP FINAL PASSED");Application.Quit(0);yield break;
            }
            bool rockOnly=Environment.GetCommandLineArgs().Contains("-lab-rock-final");
            bool visualsOnly=Environment.GetCommandLineArgs().Contains("-lab-focus-visuals");
            if(!visualsOnly)
            {
                Equip("SWAMPERT","HYDRO_PUMP");ResetBattle();while(loading)yield return null;
                Command(0);yield return null;setup=true;CycleLoadoutSlot(1);FormSetupUI(); // Same event order as the reported OnGUI failure.
                Check(loading&&Player==null,"Swampert live-action loadout change safely clears fighters before dependent form UI");
                while(loading)yield return null;setup=false;Check(loadError==null&&Player.data.id=="SWAMPERT","Swampert reload completes with changed move");
                foreach(var entry in new[]{("SWAMPERT","HYDRO_PUMP"),("AERODACTYL","ROCK_THROW")}.Where(e=>!rockOnly||e.Item2=="ROCK_THROW"))
                foreach(var source in new[]{EffectSource.ZA,EffectSource.BDSP})
                foreach(bool aim in new[]{false,true})
                {
                    Equip(entry.Item1,entry.Item2);ResetBattle();while(loading)yield return null;
                    effectSource=source;immersive=aim;testAim=Target.body.Centre;
                    int before=ResolvedHits;Check(Command(0),entry.Item1+" "+source+" "+(aim?"aimed":"focused")+" accepted");
                    float deadline=Time.realtimeSinceStartup+20;bool captured=false,fallCaptured=false;
                    while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)
                    {
                        if(rockOnly&&!fallCaptured&&Player.action?.presentation!=null&&Player.action.clock>=Player.action.presentation.HitAt-.2f)
                        {
                            Check(ResolvedHits==before,"Falling rock has not damaged target halfway through descent");
                            Check(Player.action.presentation.Travel(out var previous,out var current)&&current.y<previous.y,"Rock collision path descends with the rendered projectile");
                            yield return Screenshot("ROCK_THROW-falling-"+source+"-"+aim);fallCaptured=true;
                        }
                        if(!captured&&Player.action?.presentation!=null&&Player.action.clock>=Player.action.presentation.HitAt+.02f)
                        {yield return Screenshot(entry.Item2+"-"+source+"-"+(aim?"aimed":"focused"));captured=true;}
                        yield return null;
                    }
                    yield return WaitIdle();Check(ResolvedHits==before+1,entry.Item1+" "+source+" "+(aim?"aimed":"focused")+" resolves exactly one hit and expires");
                    if(aim)
                    {
                        Player.ready[0]=0;testAim=Target.body.Centre+Vector3.forward*6;Command(0);yield return WaitIdle();
                        Check(ResolvedHits==before+1,entry.Item2+" "+source+" deliberate aimed miss has no false hit");
                    }
                }
            }
            if(visualsOnly)
            {
                foreach(var entry in new[]{("SWAMPERT","HYDRO_PUMP"),("AERODACTYL","ROCK_THROW")}.Where(e=>!rockOnly||e.Item2=="ROCK_THROW"))
                {
                    Equip(entry.Item1,entry.Item2);ResetBattle();while(loading)yield return null;
                    effectSource=EffectSource.ZA;immersive=false;testAim=null;Command(0);
                    while(Player.action?.presentation==null)yield return null;
                    var a=Player.action;float at=a.presentation.HitAt;
                    while(a.clock<at-.02f)yield return null;
                    yield return Screenshot(entry.Item2+"-final-contact");
                    yield return new WaitForSecondsRealtime(.2f);yield return Screenshot(entry.Item2+"-final-followthrough");
                    yield return WaitIdle();Check(ResolvedHits==1,entry.Item2+" final camera/visual hit and expiry");
                }
            }
            if(rockOnly)
            {
                Check(smokeErrors==0,"No runtime errors after falling Rock Throw repair");
                Debug.Log("LAB ROCK FINAL PASSED");Application.Quit(0);yield break;
            }
            immersive=false;testAim=null;effectSource=EffectSource.ZA;
            foreach(string species in new[]{"PIKACHU","ROTOM"})
            {
                playerSpecies=Array.IndexOf(Species,species);ResetBattle();while(loading)yield return null;
                foreach(CameraView view in Enum.GetValues(typeof(CameraView)))
                {
                    cameraView=view;pitch=view==CameraView.RPG?38:23;var initial=Player.body.CameraAnchor-Player.body.transform.position;
                    testInput=Vector3.forward;float end=now+.75f;
                    while(now<end){CheckAnchor();yield return null;}
                    testInput=Vector3.zero;yield return new WaitForSecondsRealtime(.5f);yield return Screenshot(species+"-"+view);
                    Check(Vector3.Distance(initial,Player.body.CameraAnchor-Player.body.transform.position)<.001f,species+" "+view+" walk/stop camera anchor stays stable");
                    void CheckAnchor(){if(Vector3.Distance(initial,Player.body.CameraAnchor-Player.body.transform.position)>.001f)throw new Exception("Animated camera anchor drift");}
                }
                if(species=="ROTOM")
                {
                    testFly=true;yield return new WaitForSecondsRealtime(1);Check(Player.altitude>1.3f&&Player.altitude<1.7f,"Rotom bounded levitation rises to 1.6m");
                    testFly=false;yield return new WaitForSecondsRealtime(1);Check(Player.altitude<.05f,"Rotom descends when F released");
                }
            }
            cameraView=CameraView.Classic;pitch=23;
            Equip("SWAMPERT","HYDRO_PUMP");ResetBattle();while(loading)yield return null;
            var original=Player.body.entry;ToggleMega();Check(Player.pendingMega!=null&&Player.body.entry==original,"Mega starts charge on original body before swapping");
            yield return new WaitForSecondsRealtime(.65f);yield return Screenshot("mega-charge");
            yield return new WaitForSecondsRealtime(.85f);yield return Screenshot("mega-reveal");
            Check(Player.megaId!=null&&Player.body.entry!=original&&Player.megaFx,"Mega reveals native model with ZA charge/reveal effects");
            yield return new WaitForSecondsRealtime(3);Check(Player.pendingMega==null&&!Player.megaFx,"Mega effects expire and release controls");
            Check(Command(0),"Mega can attack after its native appeal");yield return WaitIdle();Check(ResolvedHits==1,"Mega Hydro Pump hits once");
            RevertForm(Player);Check(Player.body.entry==original,"Mega reverts to original body");
            playerSpecies=Array.IndexOf(Species,"ETERNATUS");enemySpecies=playerSpecies;ResetBattle();while(loading)yield return null;
            foreach(var f in fighters)
            {
                var b=f.body.PosedBounds();Check(b.min.x>=-50&&b.max.x<=50&&b.min.z>=-50&&b.max.z<=50,"Eternatus posed body fits expanded floor");
                var edge=ClampToArena(f,new Vector3(100,0,100));Check(edge.x<49&&edge.z<49,"Arena edge respects Eternatus footprint");
            }
            Check(Vector3.Distance(Player.body.transform.position,Target.body.transform.position)>=Clearance(Player,Target)-.01f,"Two Eternatus spawn without footprint overlap");
            yield return new WaitForSecondsRealtime(.6f);yield return Screenshot("eternatus-arena");
            setup=true;yield return null;yield return Screenshot("setup-controls");setup=false;
            ResetBattle();while(loading)yield return null;
            Check(ActiveActions==0&&tails.Count==0&&!fighters.Any(f=>f.megaFx||f.pendingMega!=null||f.megaId!=null),"Reset clears attacks and Mega transitions");
            Check(smokeErrors==0,"No runtime exceptions in focused regression");Debug.Log("LAB FOCUS PASSED");Application.Quit(0);
        }
    }
}
