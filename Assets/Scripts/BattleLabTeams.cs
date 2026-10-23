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
        const int MaxAllies=6,MaxEnemies=12;
        int allyCount=3,enemyCount=3,controlled,targetIndex=1;
        // Existing isolated drills request symmetric teams through this setter.
        int teamSize { get=>allyCount; set { allyCount=Mathf.Clamp(value,1,MaxAllies);enemyCount=Mathf.Clamp(value,1,MaxEnemies); } }
        int Allies=>mode==BattleMode.ZA?allyCount:1;
        int Enemies=>mode==BattleMode.ZA?enemyCount:1;
        Vector2 teamScroll;bool editingEnemies;
        int[] allySpecies={3,5,0,2,4},foeSpecies={1,8,6,7,0,3,4,5,2,8,1};
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
            var list=new List<Fighter>();
            for(int slot=0;slot<Mathf.Max(Allies,Enemies);slot++)
            {
                float z=slot==0?0:((slot+1)/2)*4*(slot%2==1?-1:1);
                if(slot<Allies)
                {
                    int a=slot==0?playerSpecies:allySpecies[slot-1];
                    var left=CreateFighter(a,ValidSource(a,slot==0?playerSource:1),new Vector3(-3,0,z));left.team=0;list.Add(left);left.body.Face(new Vector3(3,0,z));
                }
                if(slot<Enemies)
                {
                    int b=slot==0?enemySpecies:foeSpecies[slot-1];
                    var right=CreateFighter(b,ValidSource(b,slot==0?enemySource:1),new Vector3(3,0,z));right.team=1;list.Add(right);right.body.Face(new Vector3(-3,0,z));
                }
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
                {controlled=index;marked=false;var target=Target;Note("Controlling "+Player.data.id+" · Q switches ally");return;}
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
                if(other==f||other.hp<=0||!other.body||IsUnderground(other)||Mathf.Abs(f.altitude-other.altitude)>Mathf.Max(1,other.body.BodyHeight))continue;
                var away=next-other.body.transform.position;away.y=0;float clearance=f.body.FootprintRadius(-away)+other.body.FootprintRadius(away)+.12f;
                if(away.magnitude<clearance)next=other.body.transform.position+(away.sqrMagnitude>.001f?away.normalized:Vector3.back)*clearance;
            }
            next.y=f.altitude;return ClampToArena(f,next);
        }
        void TickReposition(float dt)
        {
            for(int i=0;i<fighters.Length;i++)
            {
                var f=fighters[i];if(i==controlled||f.hp<=0||f.action!=null||f.pendingMega!=null||f.state=="Mega Evolved")continue;
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
        IEnumerable<int> TeamSpecies()=>new[]{playerSpecies,enemySpecies}.Concat(allySpecies.Take(Allies-1)).Concat(foeSpecies.Take(Enemies-1));
        void SetTeamCount(bool enemies,int value)
        {
            if(enemies)enemyCount=Mathf.Clamp(value,1,MaxEnemies);else allyCount=Mathf.Clamp(value,1,MaxAllies);
            teamScroll=Vector2.zero;clashDrill=false;ResetBattle();
        }
        void TeamSetupUI()
        {
            Text(1068,255,348,$"SIMULTANEOUS BATTLE · {Allies} vs {Enemies}",label);
            for(int side=0;side<2;side++)
            {
                bool enemies=side==1;int count=enemies?enemyCount:allyCount;float y=291+side*40;
                Text(1082,y+4,190,(enemies?"Enemies: ":"Your team: ")+count,small);
                if(Button(new Rect(1296,y,52,32),"−")){SetTeamCount(enemies,count-1);return;}
                if(Button(new Rect(1358,y,52,32),"+")){SetTeamCount(enemies,count+1);return;}
            }
            if(Button(new Rect(1078,380,160,32),"Your roster",!editingEnemies)){editingEnemies=false;teamScroll=Vector2.zero;}
            if(Button(new Rect(1248,380,160,32),"Enemy roster",editingEnemies)){editingEnemies=true;teamScroll=Vector2.zero;}
            int size=editingEnemies?Enemies:Allies;
            teamScroll=GUI.BeginScrollView(new Rect(1078,420,338,206),teamScroll,new Rect(0,0,314,size*42));
            for(int slot=0;slot<size;slot++)
            {
                int species=slot==0?(editingEnemies?enemySpecies:playerSpecies):(editingEnemies?foeSpecies:allySpecies)[slot-1];
                if(Button(new Rect(0,slot*42,308,36),$"{slot+1}. {Species[species]}  ›"))OpenPicker(slot==0?(editingEnemies?1:0):(editingEnemies?20+slot-1:2+slot-1));
            }
            GUI.EndScrollView();
            Text(1082,635,320,"Q switches allies · E changes focus. AI controls the other Pokémon.",small,43);
            if(Button(new Rect(1068,685,348,34),clashDrill?"Stop clash drill":"Clash drill · current move slots"))StartClashDrill();
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
            if(Allies==1&&Enemies==1)return;
            Text(22,160,300,"YOUR TEAM · Q to switch",small);
            int row=0;foreach(var f in fighters.Where(f=>f.team==0))
                Text(22,185+row++*17,300,$"{(f==Player?"▶ ":"")}{f.data.id}  {f.hp}/{f.MaxHp}",small);
            Text(1110,134,300,"ENEMIES · E to target",small);row=0;
            foreach(var f in fighters.Where(f=>f.team==1))Text(1110,164+row++*17,300,$"{(f==Target?"▶ ":"")}{f.data.id}  {f.hp}/{f.MaxHp}",small);

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
