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
        int teamSize=3,controlled,targetIndex=1;
        int[] allySpecies={3,5},foeSpecies={1,8};
        bool teamSmoke,clashDrill;
        Fighter Player=>fighters==null?null:fighters[Mathf.Clamp(controlled,0,fighters.Length-1)];
        Fighter Target
        {
            get
            {
                if(fighters==null)return null;
                if(targetIndex>=fighters.Length||fighters[targetIndex].hp<=0||fighters[targetIndex].team==Player.team)
                {var next=NearestEnemy(Player);if(next==null)return null;targetIndex=Array.IndexOf(fighters,next);}
                return fighters[targetIndex];
            }
        }
        void CreateTeams()
        {
            controlled=0;targetIndex=1;clashCount=0;clashMessage=null;clashMessageUntil=0;
            var list=new List<Fighter>();int count=mode==BattleMode.ZA?teamSize:1;
            for(int slot=0;slot<count;slot++)
            {
                float z=slot==0?0:slot==1?-3:3;
                int a=slot==0?playerSpecies:allySpecies[slot-1],b=slot==0?enemySpecies:foeSpecies[slot-1];
                var left=CreateFighter(a,ValidSource(a,slot==0?playerSource:1),new Vector3(-3,0,z));left.team=0;list.Add(left);
                var right=CreateFighter(b,ValidSource(b,slot==0?enemySource:1),new Vector3(3,0,z));right.team=1;list.Add(right);
                left.body.Face(right.home);right.body.Face(left.home);
            }
            fighters=list.ToArray();SeparateTeamSpawn();
        }
        Fighter NearestEnemy(Fighter f)=>fighters.Where(x=>x.team!=f.team&&x.hp>0).OrderBy(x=>(x.body.transform.position-f.body.transform.position).sqrMagnitude).FirstOrDefault();
        void SwitchControl()
        {
            if(fighters==null)return;
            for(int step=1;step<=fighters.Length;step++)
            {
                int index=(controlled+step)%fighters.Length;
                if(fighters[index].team==0&&fighters[index].hp>0)
                {controlled=index;var target=Target;Note("Controlling "+Player.data.id+" · Q switches ally");return;}
            }
        }
        void CycleTarget()
        {
            if(fighters==null)return;
            for(int step=1;step<=fighters.Length;step++)
            {
                int index=(targetIndex+step)%fighters.Length;
                if(fighters[index].team!=Player.team&&fighters[index].hp>0)
                {targetIndex=index;Note("Target: "+Target.data.id);return;}
            }
        }
        static float Clearance(Fighter a,Fighter b)
        {
            var axis=b.body.transform.position-a.body.transform.position;axis.y=0;
            if(axis.sqrMagnitude<.001f)axis=Vector3.right;
            return a.body.FootprintRadius(axis)+b.body.FootprintRadius(-axis)+.12f;
        }
        void SeparateTeamSpawn()
        {
            // Uncapped authored footprints; resolve every pair, including teammates.
            for(int pass=0;pass<32;pass++)
            {
                bool changed=false;
                for(int i=0;i<fighters.Length;i++)for(int j=i+1;j<fighters.Length;j++)
                {
                    var a=fighters[i];var b=fighters[j];var delta=b.body.transform.position-a.body.transform.position;delta.y=0;
                    float overlap=Clearance(a,b)+.15f-delta.magnitude;if(overlap<=.001f)continue;
                    var push=(delta.sqrMagnitude>.001f?delta.normalized:Vector3.right)*(overlap*.5f);
                    a.body.transform.position-=push;b.body.transform.position+=push;changed=true;
                }
                if(!changed)break;
            }
            foreach(var f in fighters)f.home=f.body.transform.position;
        }
        Vector3 ClearPosition(Fighter f,Vector3 next)
        {
            for(int pass=0;pass<4;pass++)foreach(var other in fighters)
            {
                if(other==f||other.hp<=0||!other.body||IsUnderground(other))continue;
                var away=next-other.body.transform.position;away.y=0;float clearance=f.body.FootprintRadius(-away)+other.body.FootprintRadius(away)+.12f;
                if(away.magnitude<clearance)next=other.body.transform.position+(away.sqrMagnitude>.001f?away.normalized:Vector3.back)*clearance;
            }
            next.y=0;next.x=Mathf.Clamp(next.x,-12,12);next.z=Mathf.Clamp(next.z,-12,12);return next;
        }
        void TickReposition(float dt)
        {
            for(int i=0;i<fighters.Length;i++)
            {
                var f=fighters[i];if(i==controlled||f.hp<=0||f.action!=null)continue;
                var enemy=NearestEnemy(f);if(enemy==null)continue;
                if(now>=f.repositionUntil)
                {
                    var away=f.body.transform.position-enemy.body.transform.position;away.y=0;
                    if(away.sqrMagnitude<.01f)away=f.team==0?Vector3.left:Vector3.right;
                    // Small deliberate flanks between committed attacks; close fighters make room.
                    var tangent=Vector3.Cross(Vector3.up,away.normalized)*((i%3==0)?-1:1);
                    float spacing=Clearance(f,enemy)+.4f;
                    f.reposition=ClearPosition(f,f.body.transform.position+tangent*.85f+away.normalized*Mathf.Max(0,spacing-away.magnitude));
                    f.repositionUntil=now+1.1f;
                }
                MoveActor(f,f.reposition,dt,1.5f);
            }
        }
        void ReleaseAction(Fighter f)
        {
            var a=f.action;if(a==null)return;EndDig(f);
            if(a.presentation!=null){if(!a.presentation.TailDone)tails.Add(a.presentation);else a.presentation.Dispose();a.presentation=null;}
            if(mode!=BattleMode.ZA&&Vector3.Distance(f.body.transform.position,f.home)>.1f){a.phase=4;f.state="Returning";}
            else Finish(f);
        }
        void TeamSetupUI()
        {
            if(Button(new Rect(1068,260,348,38),teamSize==3?"Team battle · 3 versus 3":"Duel · 1 versus 1"))
            {teamSize=teamSize==3?1:3;clashDrill=false;ResetBattle();}
            if(Button(new Rect(1068,685,348,34),clashDrill?"Stop clash drill":"Clash drill · current move slots"))StartClashDrill();
            if(teamSize!=3)return;
            Panel(1068,310,348,280);Text(1082,322,320,"TEAM ROSTER · click to change",small);
            for(int i=0;i<2;i++)
            {
                if(Button(new Rect(1082,355+i*51,320,43),"Ally "+(i+2)+": "+Species[allySpecies[i]]))
                {OpenPicker(2+i);}
                if(Button(new Rect(1082,463+i*51,320,43),"Enemy "+(i+2)+": "+Species[foeSpecies[i]]))
                {OpenPicker(4+i);}
            }
            Text(1082,598,320,"Q switches living allies · E changes target. Other Pokémon fight automatically when AI is enabled.",small,80);
        }
        void StartClashDrill()
        {
            if(clashDrill){clashDrill=false;ResetBattle();return;}
            teamSize=1;mode=BattleMode.ZA;practice=true;ai=false;setup=false;
            ResetBattle();StartCoroutine(CompleteClashDrill());
        }
        IEnumerator CompleteClashDrill()
        {
            while(loading)yield return null;
            if(loadError!=null||fighters==null||fighters.Length!=2)yield break;
            fighters[0].body.transform.position=new Vector3(-1.3f,0,0);fighters[1].body.transform.position=new Vector3(1.3f,0,0);
            clashDrill=true;
            Note("Clash drill ON · opponent uses the matching equipped slot · any opposing projectiles can clash");
        }
        void TeamBattleUI()
        {
            if(teamSize!=3)return;
            Text(22,174,300,"YOUR TEAM · Q to switch",small);
            int row=0;foreach(var f in fighters.Where(f=>f.team==0))
                Text(22,201+row++*27,300,$"{(f==Player?"▶ ":"")}{f.data.id}  {f.hp}/{f.MaxHp}",small);
            Text(1110,134,300,"ENEMIES · E to target",small);row=0;
            foreach(var f in fighters.Where(f=>f.team==1))Text(1110,164+row++*27,300,$"{(f==Target?"▶ ":"")}{f.data.id}  {f.hp}/{f.MaxHp}",small);
            if(Target!=null)
            {
                var at=cam.WorldToScreenPoint(Target.body.Centre+Vector3.up*(Target.body.BodyHeight*.65f));
                if(at.z>0)Text(at.x*1440/Screen.width-75,(Screen.height-at.y)*900/Screen.height,150,"▼ TARGET",small);
            }
        }
        IEnumerator TeamSmokeRun()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/team-battle"));Directory.CreateDirectory(folder);
            var report=new List<string>();clashDrill=false;mode=BattleMode.ZA;teamSize=3;inspect=setup=false;ai=false;practice=false;gameSpeed=1.5f;ResetBattle();
            if(fighters.Length!=6||fighters.Count(f=>f.team==0)!=3)throw new Exception("Team creation failed");
            int start=controlled;SwitchControl();if(controlled==start||Player.team!=0)throw new Exception("Ally switch failed");
            SwitchControl();SwitchControl();if(controlled!=start)throw new Exception("Ally cycle incomplete");
            int oldTarget=targetIndex;CycleTarget();if(targetIndex==oldTarget||Target.team==0)throw new Exception("Target cycle failed");targetIndex=1;
            fighters[2].hp=0;if(Ended)throw new Exception("One faint ended team fight");ResetBattle();
            var p=Player;var origin=p.body.transform.position;
            if(!Command(0)||p.action.phase!=0||p.ready[0]!=0)throw new Exception("Distant attack was rejected or cooldown spent before approach");
            float until=now+.4f;while(now<until)yield return null;
            if(Vector3.Distance(origin,p.body.transform.position)<.5f)throw new Exception("Automatic approach did not move");
            float deadline=Time.realtimeSinceStartup+15;while(p.action!=null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(p.action!=null||ResolvedHits!=1)throw new Exception($"Approach/attack did not finish once: phase={p.action?.phase}, clock={p.action?.clock}, position={p.body.transform.position}, hits={ResolvedHits}");
            report.Add("3v3 roster, ally/target cycling, one-faint survival, distant approach -> one hit -> recovery passed");
            ResetBattle();p=Player;Command(0);testInput=Vector3.back;yield return null;testInput=Vector3.zero;
            if(p.action!=null||p.ready[0]!=0)throw new Exception("Manual movement failed to cancel approach without spending cooldown");
            report.Add("Manual approach cancellation preserves cooldown");
            ResetBattle();fighters[0].hp=0;yield return null;if(Player.hp<=0||controlled==0)throw new Exception("No living-ally handoff");
            report.Add("Controlled faint hands input to living ally");
            var clash=ClashSmoke(folder,report);while(clash.MoveNext())yield return clash.Current;
            teamSize=3;ResetBattle();ai=true;deadline=Time.realtimeSinceStartup+150;
            bool captured=false;
            while(!Ended&&Time.realtimeSinceStartup<deadline)
            {
                if(Player.action==null){int move=ChooseEnemy(Player);if(move>=0)Command(move);}
                if(!captured&&ResolvedHits>=4){CaptureEvidence(Path.Combine(folder,"three-versus-three.png"));captured=true;}
                yield return null;
            }
            if(!Ended)throw new Exception("Team battle failed to reach a winner");
            report.Add($"Finite 3v3 completed: hits={ResolvedHits}, clashes={clashCount}, survivors={fighters.Count(f=>f.hp>0)}");
            ai=false;ResetBattle();Command(1);yield return new WaitForSecondsRealtime(2);ResetBattle();int hp=fighters.Sum(f=>f.hp);
            yield return new WaitForSecondsRealtime(3);
            if(ActiveActions!=0||tails.Count!=0||ResolvedHits!=0||fighters.Sum(f=>f.hp)!=hp)throw new Exception("Team reset leaked actions, damage or effects");
            report.Add("Reset clears all six actors/actions/effect tails without late damage");
            File.WriteAllLines(Path.Combine(folder,"team-smoke.txt"),report);Debug.Log("LAB TEAM SMOKE PASSED");Application.Quit(0);
        }
    }
}
