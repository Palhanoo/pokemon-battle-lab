using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace BattleLab
{
    // The lab owns the clock and action lifecycle. Presentation cannot resolve damage twice.
    public class BattleLabApp : MonoBehaviour
    {
        public LabCatalog catalog;
        public BattleMode mode=BattleMode.BDSP;
        public MoveStyle style;
        public bool Paused { get; private set; }
        public int ResolvedHits { get; private set; }
        public int ActiveActions => fighters==null ? 0 : fighters.Count(f=>f.action!=null);
        class Fighter
        {
            public SpeciesData data; public LabActor body;
            public int hp; public int[] pp;
            public float[] ready=new float[3]; public Action action;
            public double next; public Vector3 home;
            public string state="Ready";
            public int MaxHp => data.stats[0]+100;
        }
        class Action
        {
            public MoveData move; public MoveStyle style; public Fighter target;
            public int phase; public float clock, windup, recovery, elapsed;
            public Vector3 destination, start, aim;
            public GameObject fx;
        }
        Fighter[] fighters;
        readonly List<LabActor> gallery=new();
        readonly Queue<string> log=new();
        readonly List<(GameObject go,float end)> flashes=new();
        Camera cam; Transform trainer;
        Material floorMat,gridMat;
        readonly Dictionary<Color,Material> materials=new();
        AudioSource audioSource; AudioClip hitSound;
        Random rng=new(42);
        float now, aiAt, gameSpeed=1, impactFraction=.55f, cameraDistance=5.8f, dodgeUntil, dodgeReady;
        Vector3 dodgeDirection;
        int playerSpecies=0,enemySpecies=2,playerSource=2,enemySource=2,inspectSpecies;
        bool inspect,ai=true,diagnostics=true;
        int waitingActor=-1,queuedActor=-1,queuedMove=-1,round;
        float sequenceAt;
        string message="Choose a move to begin.", clipRole="battle";
        static readonly string[] Sources={"Arceus","ZA","BDSP"};
        static readonly string[] Species={"PIKACHU","GENGAR","LUCARIO"};
        static readonly string[] ModeNames={"BDSP · turns","Arceus · action order","Z-A · real time"};
        GUIStyle title,label,small,button,card,muted;
        Color ink=new(.88f,.94f,.96f),accent=new(.33f,.88f,.71f);
        bool smoke;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Launch()
        {
            if(FindAnyObjectByType<BattleLabApp>())return;
            new GameObject("Battle Lab").AddComponent<BattleLabApp>();
        }
        void Start()
        {
            catalog=Resources.Load<LabCatalog>("LabCatalog");
            if(!catalog){Debug.LogError("Run BattleLabBuilder.Setup first.");enabled=false;return;}
            Application.targetFrameRate=120;
            BuildArena();ResetBattle();
            smoke=Environment.GetCommandLineArgs().Contains("-lab-smoke");
            if(smoke)StartCoroutine(SafeSmoke());
        }
        Material Material(Color color)
        {
            if(materials.TryGetValue(color,out var cached))return cached;
            var m=new Material(Resources.Load<Material>("LabSurface"));m.color=color;materials[color]=m;return m;
        }
        GameObject Shape(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=Material(color);return go;
        }
        void BuildArena()
        {
            var background=new GameObject("Full-frame background").AddComponent<Camera>();
            background.depth=-10;background.cullingMask=0;background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.025f,.045f,.06f);
            cam=new GameObject("Comparison camera").AddComponent<Camera>();cam.tag="MainCamera";cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.085f,.11f);
            cam.fieldOfView=42;cam.nearClipPlane=.1f;cam.farClipPlane=70;
            cam.rect=new Rect(.215f,.18f,.785f,.73f);
            var light=new GameObject("Soft key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
            light.transform.rotation=Quaternion.Euler(45,-25,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.55f,.62f,.68f);
            floorMat=Material(new Color(.13f,.19f,.22f));gridMat=Material(new Color(.19f,.27f,.30f));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Only arena floor";
            floor.transform.localScale=new Vector3(3,1,3);floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            for(int i=-14;i<=14;i+=2)
            {
                foreach(bool horizontal in new[]{true,false})
                {
                    var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.name="Grid";
                    line.transform.position=horizontal?new Vector3(0,.006f,i):new Vector3(i,.006f,0);
                    line.transform.localScale=horizontal?new Vector3(28,.01f,.015f):new Vector3(.015f,.01f,28);
                    line.GetComponent<Renderer>().sharedMaterial=gridMat;Destroy(line.GetComponent<Collider>());
                }
            }
            trainer=Shape("Trainer position",PrimitiveType.Cylinder,new Vector3(-3,.025f,-2),new Vector3(.6f,.025f,.6f),accent).transform;
            audioSource=gameObject.AddComponent<AudioSource>();
            hitSound=AudioClip.Create("Diagnostic impact",4400,1,22050,false);
            var samples=new float[4400];for(int i=0;i<samples.Length;i++)samples[i]=Mathf.Sin(i*.095f)*Mathf.Exp(-i/600f)*.18f;
            hitSound.SetData(samples,0);
        }
        public void ResetBattle()
        {
            StopActions();foreach(var g in gallery)if(g)Destroy(g.gameObject);gallery.Clear();
            now=0;aiAt=1;sequenceAt=0;round=0;waitingActor=queuedActor=queuedMove=-1;
            dodgeUntil=dodgeReady=0;ResolvedHits=0;Paused=false;rng=new Random(42);
            trainer.position=new Vector3(-3,.025f,-2);trainer.gameObject.SetActive(!inspect&&mode==BattleMode.ZA);
            if(inspect)
            {
                float commonScale=1;
                for(int i=0;i<3;i++)
                {
                    var a=LabActor.Spawn(catalog.Model(Species[inspectSpecies],Sources[i]),new Vector3((i-1)*2.75f,0,0));
                    if(i==0)commonScale=1.7f/Mathf.Max(.1f,a.Bounds.size.y);
                    a.transform.localScale=Vector3.one*commonScale;
                    a.Face(new Vector3(a.transform.position.x,0,-10));gallery.Add(a);
                }
                message="Same species. Three native model and animation sets.";return;
            }
            fighters=new[]{CreateFighter(playerSpecies,playerSource,new Vector3(-2.5f,0,0)),CreateFighter(enemySpecies,enemySource,new Vector3(2.5f,0,0))};
            foreach(var f in fighters)f.next=CombatRules.ActionDelay(f.data.stats[3],MoveStyle.Normal);
            if(!ai)fighters[1].next=double.PositiveInfinity;
            fighters[0].body.Face(fighters[1].home);fighters[1].body.Face(fighters[0].home);
            log.Clear();Note("Reset · same matchup, seed 42");
            message=mode==BattleMode.ZA?"Command a move. WASD moves the trainer; your partner follows.":"Choose a move to begin.";
        }
        Fighter CreateFighter(int species,int source,Vector3 at)
        {
            var data=catalog.Species(Species[species]);
            var f=new Fighter{data=data,body=LabActor.Spawn(catalog.Model(data.id,Sources[source]),at),home=at};
            f.hp=f.MaxHp;f.pp=data.moves.Select(m=>catalog.Move(m).pp).ToArray();return f;
        }
        void StopActions()
        {
            if(fighters!=null)foreach(var f in fighters){if(f.action?.fx)Destroy(f.action.fx);if(f.body)Destroy(f.body.gameObject);}
            fighters=null;foreach(var f in flashes)if(f.go)Destroy(f.go);flashes.Clear();
        }
        void Note(string text){message=text;log.Enqueue($"{now:0.0}s  {text}");while(log.Count>6)log.Dequeue();Debug.Log("LAB "+text);}
        bool Ended => fighters!=null&&fighters.Any(f=>f.hp<=0);
        void Update()
        {
            if(!catalog)return;
            if(!smoke&&Input.GetKeyDown(KeyCode.Escape))Paused=!Paused;
            if(!smoke&&Input.GetKeyDown(KeyCode.R))ResetBattle();
            if(!smoke&&Input.GetKeyDown(KeyCode.F1)){inspect=!inspect;ResetBattle();}
            if(!smoke&&!inspect&&!Paused)for(int i=0;i<3;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))Command(i);
            float dt=Paused?0:Mathf.Min(Time.deltaTime,.05f)*gameSpeed;now+=dt;
            foreach(var a in gallery)a.Tick(dt);
            if(!inspect&&fighters!=null)
            {
                foreach(var f in fighters)f.body.Tick(dt);
                if(dt>0&&!Ended)
                {
                    if(mode==BattleMode.ZA)TickRealtime(dt);else TickTurns();
                    foreach(var f in fighters.ToArray())if(f.action!=null)TickAction(f,dt);
                }
                else if(Ended)foreach(var f in fighters)if(f.action!=null)Cancel(f);
            }
            for(int i=flashes.Count-1;i>=0;i--)if(now>=flashes[i].end){Destroy(flashes[i].go);flashes.RemoveAt(i);}
            Vector3 focus=inspect?new Vector3(0,.85f,0):fighters!=null?(fighters[0].body.Centre+fighters[1].body.Centre)*.5f:Vector3.zero;
            // Fixed viewing direction, no shake or combat cuts; only zoom is user controlled.
            cam.orthographic=inspect;cam.orthographicSize=2.5f*cameraDistance/5.8f;
            cam.transform.position=focus+new Vector3(0,inspect?0:cameraDistance*.42f,-cameraDistance);
            cam.transform.LookAt(focus);
        }
        void TickTurns()
        {
            if(ActiveActions>0||now<sequenceAt)return;
            if(queuedActor>=0)
            {
                int a=queuedActor,m=queuedMove;queuedActor=queuedMove=-1;Begin(a,m,a==0?style:MoveStyle.Normal);return;
            }
            if(mode==BattleMode.Arceus)
            {
                waitingActor=CombatRules.NextActor(fighters[0].next,fighters[1].next);
                if(waitingActor==1&&ai)Begin(1,ChooseEnemy(),MoveStyle.Normal);
            }
            else waitingActor=0;
        }
        int ChooseEnemy()
        {
            var f=fighters[1];var valid=Enumerable.Range(0,3).Where(i=>mode==BattleMode.ZA?f.ready[i]<=now:f.pp[i]>0).ToArray();
            return valid.Length==0?-1:valid[rng.Next(valid.Length)];
        }
        public bool Command(int slot)
        {
            if(inspect||Paused||Ended||slot<0||slot>2||fighters[0].action!=null)return false;
            var f=fighters[0];
            if(mode==BattleMode.ZA)
            {
                if(f.ready[slot]>now){message="That move is cooling down.";return false;}
                Begin(0,slot,MoveStyle.Normal);return true;
            }
            if(ActiveActions>0||queuedActor>=0||now<sequenceAt)return false;
            if(f.pp[slot]<(mode==BattleMode.Arceus?CombatRules.StyleCost(style):1)){message="Not enough PP. Reset to repeat this test.";return false;}
            if(mode==BattleMode.Arceus)
            {
                if(CombatRules.NextActor(f.next,fighters[1].next)!=0){message="Opponent acts next.";return false;}
                Begin(0,slot,style);return true;
            }
            round++;
            if(!ai){Begin(0,slot,MoveStyle.Normal);return true;}
            int enemy=ChooseEnemy();if(enemy<0){Begin(0,slot,MoveStyle.Normal);return true;}
            int first=CombatRules.First(f.data.stats[3],catalog.Move(f.data.moves[slot]).priority,fighters[1].data.stats[3],catalog.Move(fighters[1].data.moves[enemy]).priority,rng);
            queuedActor=1-first;queuedMove=queuedActor==0?slot:enemy;
            Begin(first,first==0?slot:enemy,MoveStyle.Normal);return true;
        }
        void Begin(int index,int slot,MoveStyle selected)
        {
            if(slot<0)return;
            var f=fighters[index];var target=fighters[1-index];var move=catalog.Move(f.data.moves[slot]);
            if(mode==BattleMode.ZA)f.ready[slot]=now+move.cooldown;
            else f.pp[slot]-=mode==BattleMode.Arceus?CombatRules.StyleCost(selected):1;
            f.action=new Action{move=move,style=selected,target=target,start=f.body.transform.position};
            f.state="Approaching";Note($"{f.data.id}: {move.name}"+(selected==MoveStyle.Normal?"":" · "+selected));
        }
        void TickRealtime(float dt)
        {
            Vector3 input=smoke?Vector3.zero:new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));input=Vector3.ClampMagnitude(input,1);
            if(!smoke&&Input.GetKeyDown(KeyCode.Space)&&now>=dodgeReady)
            {dodgeDirection=input.sqrMagnitude>.01f?input:Vector3.back;dodgeUntil=now+.3f;dodgeReady=now+1;}
            trainer.position+= (now<dodgeUntil?dodgeDirection*9:input*4)*dt;
            trainer.position=new Vector3(Mathf.Clamp(trainer.position.x,-10,10),.025f,Mathf.Clamp(trainer.position.z,-8,8));
            var p=fighters[0];
            if(p.action==null)
            {
                Vector3 destination=trainer.position+Vector3.forward*1.8f;destination.y=0;
                MoveActor(p,destination,dt,3);
                if(Vector3.Distance(p.body.transform.position,destination)<.1f)p.body.Face(fighters[1].body.transform.position);
            }
            if(ai&&fighters[1].action==null&&now>=aiAt){int m=ChooseEnemy();if(m>=0)Begin(1,m,MoveStyle.Normal);aiAt=now+.4f;}
        }
        void MoveActor(Fighter f,Vector3 destination,float dt,float speed)
        {
            if(Vector3.Distance(f.body.transform.position,destination)<.06f){if(f.state=="Moving"){f.body.Idle();f.state="Ready";}return;}
            f.body.Face(destination);f.body.transform.position=Vector3.MoveTowards(f.body.transform.position,destination,dt*speed);
            if(f.body.CurrentClip!=f.body.entry.Role("run")?.name)f.body.Role("run",true);
            if(f.action==null)f.state="Moving";
        }
        void TickAction(Fighter f,float dt)
        {
            var a=f.action;a.elapsed+=dt;
            if(a.target.hp<=0||f.hp<=0){Cancel(f);return;}
            Vector3 from=f.body.transform.position,to=a.target.body.transform.position;
            float gap=Vector3.Distance(from,to);
            if(a.phase==0)
            {
                float bodyReach=(f.body.Bounds.extents.x+a.target.body.Bounds.extents.x)*.65f;
                float desired=a.move.physical?bodyReach+.65f:mode==BattleMode.ZA?Mathf.Max(2,a.move.rangeMax*.65f):gap;
                if(gap>desired+.1f)
                {MoveActor(f,to+(from-to).normalized*desired,dt,4);return;}
                f.body.Face(to);f.body.Role(a.move.physical?"attack":"special");
                a.windup=mode==BattleMode.ZA?a.move.windup:Mathf.Max(.25f,f.body.ClipLength*impactFraction);
                a.recovery=mode==BattleMode.ZA?a.move.recovery:Mathf.Max(.2f,f.body.ClipLength-a.windup);
                a.phase=1;a.clock=0;f.state="Wind-up";
            }
            a.clock+=dt;
            if(a.phase==1&&a.clock>=a.windup)
            {
                a.clock=0;a.aim=a.target.body.Centre;
                if(!a.move.physical)
                {
                    a.fx=Shape("Diagnostic "+a.move.name,PrimitiveType.Sphere,f.body.Centre,Vector3.one*.22f,MoveColor(a.move));
                    a.phase=2;f.state="In flight";
                }
                else {Impact(f,mode!=BattleMode.ZA||gap< f.body.Bounds.extents.x+a.target.body.Bounds.extents.x+1.3f);a.phase=3;f.state="Recovering";}
            }
            else if(a.phase==2)
            {
                float speed=mode==BattleMode.ZA?Mathf.Max(8,a.move.speed):12;
                // Fixed aim at release deliberately exposes misses. Native homing is not reconstructed.
                var old=a.fx.transform.position;var next=Vector3.MoveTowards(old,a.aim,dt*speed);a.fx.transform.position=next;
                var bounds=a.target.body.Bounds;bounds.Expand(Mathf.Min(a.move.radius,.7f)*2);
                bool contact=bounds.Contains(next)||SegmentHits(bounds,old,next);
                if(contact||Vector3.Distance(next,a.aim)<.02f)
                {
                    Impact(f,mode!=BattleMode.ZA||contact);Destroy(a.fx);a.fx=null;a.phase=3;a.clock=0;f.state="Recovering";
                }
            }
            else if(a.phase==3&&a.clock>=a.recovery)
            {
                if(mode!=BattleMode.ZA&&Vector3.Distance(f.body.transform.position,f.home)>.1f){a.phase=4;f.state="Returning";}
                else Finish(f);
            }
            else if(a.phase==4)
            {MoveActor(f,f.home,dt,4);if(Vector3.Distance(f.body.transform.position,f.home)<.1f)Finish(f);}
        }
        static bool SegmentHits(Bounds b,Vector3 start,Vector3 end)
        {var d=end-start;return d.sqrMagnitude>.00001f&&b.IntersectRay(new Ray(start,d.normalized),out var hit)&&hit<=d.magnitude;}
        void Impact(Fighter f,bool contact)
        {
            var a=f.action;var t=a.target;
            bool hit=contact&&(mode==BattleMode.ZA||CombatRules.Hits(a.move.accuracy,rng));
            if(!hit){Note(a.move.name+" missed");return;}
            int damage=CombatRules.Damage(f.data,t.data,a.move,catalog.data.types,mode,a.style);
            t.hp=Mathf.Max(0,t.hp-damage);ResolvedHits++;
            if(damage>0)
            {
                if(t.hp<=0)t.body.Role("faint");else if(t.action==null)t.body.Role("damage");
                flashes.Add((Shape("Impact",PrimitiveType.Sphere,t.body.Centre,Vector3.one*.45f,MoveColor(a.move)),now+.12f));
                audioSource.PlayOneShot(hitSound);
            }
            Note(damage==0?"No effect · type immunity":$"{t.data.id} −{damage} HP");
            if(t.hp<=0){Note(t==fighters[1]?"You won · R to repeat":"Opponent won · R to repeat");queuedActor=queuedMove=-1;}
        }
        void Finish(Fighter f)
        {
            var a=f.action;if(mode==BattleMode.Arceus)f.next+=CombatRules.ActionDelay(f.data.stats[3],a.style);
            f.action=null;f.state="Ready";f.body.Idle();f.body.Face(a.target.body.transform.position);sequenceAt=now+.25f;
            if(a.target.action==null&&a.target.hp>0)a.target.body.Idle();
        }
        void Cancel(Fighter f)
        {if(f.action?.fx)Destroy(f.action.fx);f.action=null;f.state=f.hp<=0?"Fainted":"Ready";if(f.hp>0)f.body.Idle();}
        Color MoveColor(MoveData m)=>m.type=="ELECTRIC"?new Color(1,.8f,.15f):m.type=="GHOST"||m.type=="POISON"?new Color(.7f,.35f,.95f):m.type=="FIGHTING"?new Color(.25f,.7f,1):new Color(.8f,.9f,1);
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold,normal={textColor=ink}};
            label=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=ink}};
            small=new GUIStyle(label){fontSize=13};muted=new GUIStyle(small){normal={textColor=new Color(.57f,.68f,.72f)}};
            button=new GUIStyle(GUI.skin.button){fontSize=15,wordWrap=true,padding=new RectOffset(8,8,7,7)};
            card=new GUIStyle(GUI.skin.box){padding=new RectOffset(15,15,12,12)};
        }
        bool Button(Rect rect,string text,bool selected=false)
        {GUI.backgroundColor=selected?accent:new Color(.28f,.37f,.41f);bool pressed=GUI.Button(rect,text,button);GUI.backgroundColor=Color.white;return pressed;}
        void Text(float x,float y,float w,string text,GUIStyle font=null,float h=30)=>GUI.Label(new Rect(x,y,w,h),text,font??label);
        void Panel(float x,float y,float w,float h)=>GUI.Box(new Rect(x,y,w,h),"",card);
        void OnGUI()
        {
            if(!catalog)return;
            if(smoke&&(Event.current.isMouse||Event.current.isKey))Event.current.Use();
            Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
            Text(24,16,450,"POKÉMON / BATTLE LAB",title,40);
            Text(24,57,850,"01   /   FEEL STUDY     ·     one arena, three model sources, three battle rhythms",muted);
            if(Button(new Rect(1090,20,150,40),inspect?"Back to battle":"Model comparison",inspect)){inspect=!inspect;ResetBattle();}
            if(Button(new Rect(1250,20,165,40),Paused?"Resume  [Esc]":"Pause  [Esc]"))Paused=!Paused;
            Panel(24,104,270,604);
            Text(40,118,235,"EXPERIMENT",muted);
            for(int i=0;i<3;i++)if(Button(new Rect(40,150+i*43,238,36),ModeNames[i],(int)mode==i))
            {mode=(BattleMode)i;inspect=false;style=MoveStyle.Normal;ResetBattle();}
            Text(40,290,238,inspect?"Species to compare":"Your Pokémon",small);
            if(Button(new Rect(40,318,238,34),Species[inspect?inspectSpecies:playerSpecies]+"  ›"))
            {if(inspect)inspectSpecies=(inspectSpecies+1)%3;else playerSpecies=(playerSpecies+1)%3;ResetBattle();}
            if(!inspect)
            {
                if(Button(new Rect(40,360,238,34),"Model: "+Sources[playerSource]+"  ›")){playerSource=(playerSource+1)%3;ResetBattle();}
                Text(40,408,238,"Opponent",small);
                if(Button(new Rect(40,435,238,34),Species[enemySpecies]+"  ›")){enemySpecies=(enemySpecies+1)%3;ResetBattle();}
                if(Button(new Rect(40,477,238,34),"Model: "+Sources[enemySource]+"  ›")){enemySource=(enemySource+1)%3;ResetBattle();}
                bool nextAi=GUI.Toggle(new Rect(43,532,230,25),ai," Opponent commands enabled");if(nextAi!=ai){ai=nextAi;ResetBattle();}
            }
            if(Button(new Rect(40,578,238,40),"Reset experiment  [R]"))ResetBattle();
            Text(40,638,230,"Model/rule changes reset the fight. No saves or story dependencies.",muted,55);
            if(inspect)GalleryUI();else BattleUI();
            Panel(24,724,270,152);Text(40,736,238,$"Playback speed   {gameSpeed:0.00}×",small);
            gameSpeed=GUI.HorizontalSlider(new Rect(43,769,230,20),gameSpeed,.25f,1.5f);
            Text(40,796,238,"Camera distance",small);cameraDistance=GUI.HorizontalSlider(new Rect(43,829,230,20),cameraDistance,4.5f,14);
            Text(322,872,1090,"Native model/clip exports · reconstructed mechanics subset · diagnostic effects · desktop prototype",muted,25);
        }
        void GalleryUI()
        {
            for(int i=0;i<gallery.Count;i++)
            {
                float x=337+i*357;Panel(x,109,334,85);Text(x+15,119,300,Sources[i],title,35);
                Text(x+15,158,300,$"{gallery[i].entry.clips.Length} clips · {Species[inspectSpecies]}",muted);
                Text(x,620,335,gallery[i].CurrentClip,small);
                Text(x,649,335,$"{gallery[i].ClipTime:0.00}s / {gallery[i].ClipLength:0.00}s",muted);
            }
            string[] roles={"battle","walk","run","attack","special","damage","faint"};
            for(int i=0;i<roles.Length;i++)if(Button(new Rect(324+i*155,739,147,42),roles[i],clipRole==roles[i]))
            {clipRole=roles[i];foreach(var a in gallery)a.Role(clipRole,clipRole=="battle"||clipRole=="walk"||clipRole=="run");}
            Text(330,806,1060,"Compare silhouette, stance, contact poses and recovery. Viewer uses one common display scale per species; battle keeps native size.",small,45);
        }
        void BattleUI()
        {
            if(fighters==null)return;
            for(int i=0;i<2;i++)
            {
                float x=i==0?324:1068;var f=fighters[i];Panel(x,109,348,122);
                Text(x+14,119,325,(i==0?"YOU  /  ":"FOE  /  ")+f.data.id,label);
                Text(x+14,148,325,$"{f.body.entry.source} model · {f.hp} / {f.MaxHp} HP",small);
                GUI.color=new Color(.12f,.2f,.24f);GUI.DrawTexture(new Rect(x+14,179,320,8),Texture2D.whiteTexture);
                GUI.color=i==0?accent:new Color(1,.52f,.4f);GUI.DrawTexture(new Rect(x+14,179,320*(float)f.hp/f.MaxHp,8),Texture2D.whiteTexture);GUI.color=Color.white;
                Text(x+14,195,325,f.state,small);
            }
            Text(698,121,340,ModeNames[(int)mode],label,50);
            if(mode==BattleMode.Arceus)
            {
                var order=CombatRules.Preview(fighters[0].next,fighters[1].next,fighters[0].data.stats[3],fighters[1].data.stats[3],style,6);
                Text(700,171,330,string.Join(" → ",order.Select(x=>x==0?"YOU":"FOE")),small,55);
                Text(330,650,1000,"Action-order preview assumes repeated selected style; experimental delay curve.",muted);
                for(int i=0;i<3;i++)if(Button(new Rect(330+i*145,685,137,34),((MoveStyle)i).ToString(),(int)style==i))style=(MoveStyle)i;
            }
            else if(mode==BattleMode.ZA)Text(330,677,1000,"WASD  trainer movement     SPACE  trainer dash     1–3  partner commands",small);
            else Text(330,677,1000,$"Round {round}  ·  both sides select, then priority and Speed decide order",small);
            Text(330,244,1065,message,label,50);
            if(diagnostics)
            {
                Panel(1090,411,325,239);Text(1104,422,296,"OBSERVATION LOG",muted);
                int row=0;foreach(var line in log)Text(1104,454+row++*28,296,line,small,28);
            }
            for(int i=0;i<3;i++)
            {
                var f=fighters[0];var m=catalog.Move(f.data.moves[i]);float left=Mathf.Max(0,f.ready[i]-now);
                string detail=mode==BattleMode.ZA?(left>0?$"Cooldown {left:0.0}s":f.action!=null?"Action in progress":"Ready"):$"PP {f.pp[i]} / {m.pp}  ·  {m.type}";
                GUI.enabled=!Paused&&!Ended&&f.action==null&&(mode==BattleMode.ZA?left<=0:ActiveActions==0&&queuedActor<0&&now>=sequenceAt&&f.pp[i]>=(mode==BattleMode.Arceus?CombatRules.StyleCost(style):1)&&(mode!=BattleMode.Arceus||CombatRules.NextActor(f.next,fighters[1].next)==0));
                if(Button(new Rect(324+i*366,737,352,76),$"[{i+1}]   {m.name}\n{detail}"))Command(i);GUI.enabled=true;
            }
            Text(330,825,1060,mode==BattleMode.ZA?"Z-A cooldown/range rows imported; movement, aim, animation staging and damage remain a prototype.":mode==BattleMode.Arceus?"Agile trades power for earlier actions; Strong delays the next action. Formulas are lab tuning.":"Traditional-turn subset: priority, Speed, PP, accuracy and type damage. Items, abilities and status omitted.",muted,45);
        }
        IEnumerator SafeSmoke()
        {
            var run=SmokeRun();
            while(true)
            {
                object item;
                try { if(!run.MoveNext())yield break;item=run.Current; }
                catch(Exception e){Debug.LogException(e);Application.Quit(1);yield break;}
                yield return item;
            }
        }
        IEnumerator SmokeRun()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Docs/evidence"));Directory.CreateDirectory(folder);
            var summaries=new List<string>();
            foreach(BattleMode testMode in Enum.GetValues(typeof(BattleMode)))
            {
                mode=testMode;style=MoveStyle.Normal;playerSource=enemySource=mode==BattleMode.BDSP?2:mode==BattleMode.Arceus?0:1;inspect=false;ai=true;ResetBattle();
                float deadline=Time.realtimeSinceStartup+45;
                while(ResolvedHits<2&&Time.realtimeSinceStartup<deadline&&!Ended){Command(1);yield return null;}
                if(ResolvedHits<2)throw new Exception("Smoke failed: fewer than two landed hits in "+mode);
                summaries.Add(mode+": "+ResolvedHits+" hits, HP="+fighters[0].hp+"/"+fighters[1].hp);
                if(mode!=testMode)throw new Exception("Test scenario changed unexpectedly");
                yield return new WaitForSecondsRealtime(.25f);
                Paused=true;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,mode+".png"));yield return new WaitForSecondsRealtime(.3f);
                Paused=false;deadline=Time.realtimeSinceStartup+60;
                while(!Ended&&Time.realtimeSinceStartup<deadline){Command(1);yield return null;}
                if(!Ended)throw new Exception("Battle did not finish: "+mode);
                summaries.Add(mode+": completed, HP="+fighters[0].hp+"/"+fighters[1].hp);
                ai=false;ResetBattle();Command(1);yield return null;
                ResetBattle();if(ActiveActions!=0||ResolvedHits!=0)throw new Exception("Reset leaked actions");
                int hp=fighters[0].hp+fighters[1].hp;yield return new WaitForSecondsRealtime(2);
                if(ActiveActions!=0||ResolvedHits!=0||hp!=fighters[0].hp+fighters[1].hp)throw new Exception("Late impact after reset");
                summaries.Add(mode+": reset during attack, no delayed damage");
            }
            inspect=true;
            for(int modelIndex=0;modelIndex<3;modelIndex++)
            {
                inspectSpecies=modelIndex;ResetBattle();yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"models-"+Species[inspectSpecies]+".png"));yield return new WaitForSecondsRealtime(.3f);
            }
            File.WriteAllLines(Path.Combine(folder,"smoke.txt"),summaries);Debug.Log("LAB SMOKE PASSED");Application.Quit(0);
        }
        void OnDestroy(){StopActions();foreach(var a in gallery)if(a)Destroy(a.gameObject);foreach(var m in materials.Values)if(m)Destroy(m);}
    }
}
