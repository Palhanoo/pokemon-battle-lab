using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BattleLab.Native;
using Random = System.Random;

namespace BattleLab
{
    // The lab owns the clock and action lifecycle. Presentation cannot resolve damage twice.
    public partial class BattleLabApp : MonoBehaviour
    {
        public LabCatalog catalog;
        public BattleMode mode=BattleMode.ZA;
        public MoveStyle style;
        public bool Paused { get; private set; }
        public int ResolvedHits { get; private set; }
        public int ActiveActions => fighters==null ? 0 : fighters.Count(f=>f.action!=null);
        const int ActiveSlots=4;
        class Fighter
        {
            public SpeciesData data; public LabActor body;
            public int hp; public int[] pp; public string[] moves;public AudioSource voice;
            public float[] ready=new float[ActiveSlots]; public Action action;
            public double next; public Vector3 home; public int team;
            public float dodgeUntil,dodgeReady; public Vector3 dodgeDirection;
            public string state="Ready";
            public int MaxHp => data.stats[0]+100;
        }
        class Action
        {
            public MoveData move; public MoveStyle style; public Fighter target;
            public int phase,slot; public float clock;

            public AttackPresentation presentation; public bool hit,sounded,blocked;
            public int? remainingDamage; public readonly HashSet<Action> met=new();
        }
        Fighter[] fighters;
        readonly List<LabActor> gallery=new();
        readonly Queue<string> log=new();
        readonly List<(GameObject go,float end)> flashes=new();
        Camera cam;UnityEngine.Rendering.VolumeProfile volumeProfile;
        VioletFxCatalog zaFx; BdspFxCatalog bdspFx; NativeTiming nativeTiming;
        readonly List<AttackPresentation> tails=new();
        bool setup,practice=false;
        readonly Dictionary<string,int[]> loadouts=new();
        LabAudio soundCatalog;float soundVolume=.5f;
        float yaw=90,pitch=24;
        Vector3 cameraFocus;
        Vector3 testInput; bool testDash;
        bool Direct => !inspect&&mode==BattleMode.ZA;
        Material floorMat,gridMat;
        readonly Dictionary<Color,Material> materials=new();

        Random rng=new(42);
        float now, aiAt, gameSpeed=1, cameraDistance=5.8f;
        float dodgeUntil { get=>Player?.dodgeUntil??0; set { if(Player!=null)Player.dodgeUntil=value; } }
        float dodgeReady { get=>Player?.dodgeReady??0; set { if(Player!=null)Player.dodgeReady=value; } }
        Vector3 dodgeDirection { get=>Player.dodgeDirection; set=>Player.dodgeDirection=value; }
        int playerSpecies=0,enemySpecies=2,playerSource=1,enemySource=1,inspectSpecies;
        bool inspect,ai=true,diagnostics=false;
        int waitingActor=-1,queuedActor=-1,queuedMove=-1,round;
        float sequenceAt;
        string message="Choose a move to begin.", clipRole="battle";
        static readonly string[] Sources={"Arceus","ZA","BDSP","SV","Sword"};
        string[] Species => catalog.data.species.Select(s=>s.id).ToArray();
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
            zaFx=Resources.Load<VioletFxCatalog>("NativeZA");bdspFx=Resources.Load<BdspFxCatalog>("NativeBDSP");nativeTiming=Resources.Load<NativeTiming>("NativeTiming");
            soundCatalog=Resources.Load<LabAudio>("LabAudio");
            Application.logMessageReceived+=SmokeLog;catalog=Instantiate(catalog);LoadPokedex();BuildArena();ResetBattle();
            teamSmoke=Environment.GetCommandLineArgs().Contains("-lab-team-smoke");
            smoke=Environment.GetCommandLineArgs().Contains("-lab-stream-combat")||Environment.GetCommandLineArgs().Contains("-lab-roster-smoke")||teamSmoke||Environment.GetCommandLineArgs().Contains("-lab-smoke");
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
            var cameraData=cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;
            var volume=new GameObject("HDR colour mapping").AddComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=true;
            volumeProfile=Instantiate(Resources.Load<UnityEngine.Rendering.VolumeProfile>("LabTone"));volume.sharedProfile=volumeProfile;
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

        }
        int ValidSource(int species,int source)
        {return catalog.models.Any(m=>m.species==Species[species]&&m.source==Sources[source])?source:Array.IndexOf(Sources,catalog.models.First(m=>m.species==Species[species]).source);}
        int NextSource(int species,int source)
        {for(int i=1;i<=Sources.Length;i++){int next=(source+i)%Sources.Length;if(catalog.models.Any(m=>m.species==Species[species]&&m.source==Sources[next]))return next;}return source;}
        int[] Loadout(string species)
        {if(!loadouts.TryGetValue(species,out var slots)){slots=Enumerable.Range(0,ActiveSlots).ToArray();loadouts[species]=slots;}return slots;}
        void ResetBattleReady()
        {
            StopActions();foreach(var g in gallery)if(g)Destroy(g.gameObject);gallery.Clear();
            playerSource=ValidSource(playerSpecies,playerSource);enemySource=ValidSource(enemySpecies,enemySource);
            now=0;aiAt=1;sequenceAt=0;round=0;waitingActor=queuedActor=queuedMove=-1;
            dodgeUntil=dodgeReady=0;ResolvedHits=0;Paused=false;rng=new Random(42);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(inspect)
            {
                float commonScale=1;
                var available=catalog.models.Where(m=>m.species==Species[inspectSpecies]).ToArray();
                for(int i=0;i<available.Length;i++)
                {
                    var a=LabActor.Spawn(available[i],new Vector3((i-(available.Length-1)*.5f)*2.75f,0,0));
                    if(i==0)commonScale=1.7f/Mathf.Max(.1f,a.Bounds.size.y);
                    a.transform.localScale=Vector3.one*commonScale;
                    a.Face(new Vector3(a.transform.position.x,0,-10));gallery.Add(a);
                }
                message="Native model sources available for this species.";return;
            }
            CreateTeams();
            foreach(var f in fighters)f.next=CombatRules.ActionDelay(f.data.stats[3],MoveStyle.Normal);
            if(!ai)fighters[1].next=double.PositiveInfinity;
            fighters[0].body.Face(fighters[1].home);fighters[1].body.Face(fighters[0].home);
            log.Clear();Note("Reset · same matchup, seed 42");
            cameraFocus=fighters[0].body.Centre;testInput=Vector3.zero;testDash=false;
            message=Direct?"You control the Pokémon. WASD move · hold right mouse to look · 1–4 attack · Space dash · Tab setup.":"Choose a move to begin.";
        }
        Fighter CreateFighter(int species,int source,Vector3 at)
        {
            var data=catalog.Species(Species[species]);
            var f=new Fighter{data=data,body=LabActor.Spawn(catalog.Model(data.id,Sources[source]),at),home=at};
            f.moves=Loadout(data.id).Select(i=>data.moves[i]).ToArray();
            f.hp=f.MaxHp;f.pp=f.moves.Select(m=>catalog.Move(m).pp).ToArray();
            f.voice=f.body.gameObject.AddComponent<AudioSource>();f.voice.spatialBlend=.25f;f.voice.maxDistance=28;f.voice.volume=soundVolume;
            var cry=soundCatalog.CryClip(data.id);if(cry)f.voice.PlayOneShot(cry,.35f);return f;
        }
        void StopActions()
        {
            if(fighters!=null)foreach(var f in fighters){f.action?.presentation?.Dispose();if(f.voice)f.voice.Stop();if(f.body)Destroy(f.body.gameObject);}
            foreach(var tail in tails)tail.Dispose();tails.Clear();
            foreach(var burst in clashBursts)DestroyBurst(burst.root);clashBursts.Clear();
            fighters=null;foreach(var f in flashes)if(f.go)Destroy(f.go);flashes.Clear();
        }
        void Note(string text){message=text;log.Enqueue($"{now:0.0}s  {text}");while(log.Count>6)log.Dequeue();Debug.Log("LAB "+text);}
        bool Ended => fighters!=null&&(!fighters.Any(f=>f.team==0&&f.hp>0)||!fighters.Any(f=>f.team==1&&f.hp>0));
        void Update()
        {
            if(!catalog||loading||loadError!=null)return;
            if(!smoke&&Input.GetKeyDown(KeyCode.Escape))Paused=!Paused;
            if(!smoke&&Input.GetKeyDown(KeyCode.Tab))setup=!setup;
            if(!smoke&&Input.GetKeyDown(KeyCode.R))ResetBattle();
            if(!smoke&&Input.GetKeyDown(KeyCode.F2))effectAudit=!effectAudit;
            if(!smoke&&Input.GetKeyDown(KeyCode.F1)){inspect=!inspect;ResetBattle();}
            if(!smoke&&Direct&&!setup&&!Paused){if(Input.GetKeyDown(KeyCode.Q))SwitchControl();if(Input.GetKeyDown(KeyCode.E))CycleTarget();}
            if(!smoke&&!inspect&&!Paused)for(int i=0;i<ActiveSlots;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))Command(i);
            AudioListener.pause=Paused||(setup&&Direct);
            float dt=Paused||(setup&&Direct)?0:Mathf.Min(Time.deltaTime,.05f)*gameSpeed;now+=dt;
            foreach(var a in gallery)a.Tick(dt);
            if(!inspect&&fighters!=null)
            {
                foreach(var f in fighters){f.body.Tick(dt);f.voice.volume=soundVolume;f.voice.pitch=gameSpeed;}
                if(dt>0&&!Ended)
                {
                    if(mode==BattleMode.ZA)TickRealtime(dt);else TickTurns();
                    foreach(var f in fighters.ToArray())if(f.action!=null)TickAction(f,dt);
                    ResolveClashes();ResolveHits();
                }
                else if(Ended)foreach(var f in fighters)if(f.action!=null)Cancel(f);
            }
            for(int i=tails.Count-1;i>=0;i--){tails[i].Step(dt,false);if(tails[i].TailDone){tails[i].Dispose();tails.RemoveAt(i);}}
            for(int i=flashes.Count-1;i>=0;i--)if(now>=flashes[i].end){Destroy(flashes[i].go);flashes.RemoveAt(i);}
            TickClashes(dt);UpdateCamera(dt);
        }
        void UpdateCamera(float dt)
        {
            bool orbit=Direct&&!Paused&&!smoke&&Input.GetMouseButton(1);
            Cursor.lockState=orbit?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!orbit;
            if(orbit){yaw+=Input.GetAxis("Mouse X")*3;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,8,65);}
            if(Direct&&!Paused&&!smoke)cameraDistance=Mathf.Clamp(cameraDistance-Input.mouseScrollDelta.y*.5f,3.5f,12);
            cam.rect=Direct&&!setup?new Rect(0,0,1,1):new Rect(.215f,.18f,.785f,.73f);
            cam.orthographic=inspect;cam.orthographicSize=2.5f*cameraDistance/5.8f;
            Vector3 focus=inspect?new Vector3(0,.85f,0):fighters!=null?(Direct?Player.body.transform.position+Vector3.up*Mathf.Clamp(Player.body.BodyHeight*.6f,.4f,1.6f):(fighters[0].body.Centre+fighters[1].body.Centre)*.5f):Vector3.zero;
            cameraFocus=Vector3.Lerp(cameraFocus,focus,1-Mathf.Exp(-12*dt));
            if(inspect)cameraFocus=focus;
            float followDistance=Direct?cameraDistance*Mathf.Clamp(Player.body.BodyHeight/1.2f,.85f,1.5f):cameraDistance;
            cam.transform.position=Direct?cameraFocus+Quaternion.Euler(pitch,yaw,0)*Vector3.back*followDistance:focus+new Vector3(0,inspect?0:cameraDistance*.42f,-cameraDistance);
            cam.transform.LookAt(Direct?cameraFocus:focus);
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
        int ChooseEnemy(Fighter actor=null)
        {
            var f=actor??fighters[1];var valid=Enumerable.Range(0,ActiveSlots).Where(i=>mode==BattleMode.ZA?f.ready[i]<=now:f.pp[i]>0).ToArray();
            return valid.Length==0?-1:valid[rng.Next(valid.Length)];
        }
        public bool Command(int slot)
        {
            if(loading||fighters==null||inspect||Paused||(setup&&Direct)||Ended||slot<0||slot>=ActiveSlots||Player.action!=null||Player.hp<=0)return false;
            var f=Player;
            if(mode==BattleMode.ZA)
            {
                if(f.ready[slot]>now){message="That move is cooling down.";return false;}
                if(now<dodgeUntil||Target==null)return false;
                Begin(controlled,slot,MoveStyle.Normal);
                if(clashDrill&&fighters[1].action==null)Begin(1,0,MoveStyle.Normal);return true;
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
            int first=CombatRules.First(f.data.stats[3],catalog.Move(f.moves[slot]).priority,fighters[1].data.stats[3],catalog.Move(fighters[1].moves[enemy]).priority,rng);
            queuedActor=1-first;queuedMove=queuedActor==0?slot:enemy;
            Begin(first,first==0?slot:enemy,MoveStyle.Normal);return true;
        }
        void Begin(int index,int slot,MoveStyle selected)
        {
            if(slot<0)return;
            var f=fighters[index];var target=Direct&&index==controlled?Target:NearestEnemy(f);if(target==null)return;var move=catalog.Move(f.moves[slot]);
            if(mode!=BattleMode.ZA)f.pp[slot]-=mode==BattleMode.Arceus?CombatRules.StyleCost(selected):1;
            f.action=new Action{move=move,style=selected,target=target,slot=slot};
            f.state="Approaching";Note($"{f.data.id}: {move.name}"+(selected==MoveStyle.Normal?"":" · "+selected));
        }
        static float Reach(Fighter f,Fighter target,MoveData move)=>Mathf.Max(move.physical?.8f:2,move.rangeMax)+Mathf.Min(.6f,target.body.Bounds.extents.x);
        void TickRealtime(float dt)
        {
            if(Player.hp<=0)SwitchControl();
            var p=Player;if(p.hp<=0)return;
            Vector3 axes=smoke?testInput:new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            var input=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(axes,1);
            bool dash=smoke?testDash:Input.GetKeyDown(KeyCode.Space);testDash=false;
            if(p.action!=null&&(input.sqrMagnitude>.01f||dash))
            {
                if(p.action.phase==0)Cancel(p);
                else if(p.action.hit&&p.action.clock>=p.action.presentation.HitAt+.18f)ReleaseAction(p);
            }
            if(p.action==null)
            {
                if(dash&&now>=dodgeReady)
                {dodgeDirection=input.sqrMagnitude>.01f?input:p.body.transform.forward;dodgeUntil=now+.24f;dodgeReady=now+.85f;}
                var velocity=now<dodgeUntil?dodgeDirection*8:input*(!smoke&&Input.GetKey(KeyCode.LeftShift)?1.6f:3.6f);
                if(velocity.sqrMagnitude>.01f)
                {
                    var at=p.body.transform.position;var next=at+velocity*dt;
                    next.x=Mathf.Clamp(next.x,-12,12);next.z=Mathf.Clamp(next.z,-12,12);
                    // Separate footprints; direct input never gets handed to partner-follow logic.
                    next=ClearPosition(p,next);
                    p.body.Face(at+velocity);p.body.transform.position=next;
                    string role=velocity.magnitude<2?"walk":"run";
                    if(p.body.CurrentClip!=p.body.entry.Role(role)?.name)p.body.Role(role,true);
                    p.state=now<dodgeUntil?"Dashing":"Moving";
                }
                else if(p.state=="Moving"||p.state=="Dashing"){p.body.Idle();p.state="Ready";}
            }
            if(ai&&now>=aiAt)
            {
                for(int i=0;i<fighters.Length;i++)if(i!=controlled&&fighters[i].hp>0&&fighters[i].action==null)
                {int m=ChooseEnemy(fighters[i]);if(m>=0)Begin(i,m,MoveStyle.Normal);}
                aiAt=now+.4f;
            }
        }
        void MoveActor(Fighter f,Vector3 destination,float dt,float speed)
        {
            if(Vector3.Distance(f.body.transform.position,destination)<.005f){if(f.state=="Moving"){f.body.Idle();f.state="Ready";}return;}
            f.body.Face(destination);f.body.transform.position=ClearPosition(f,Vector3.MoveTowards(f.body.transform.position,destination,dt*speed));
            if(f.body.CurrentClip!=f.body.entry.Role("run")?.name)f.body.Role("run",true);
            if(f.action==null)f.state="Moving";
        }
        void TickAction(Fighter f,float dt)
        {
            var a=f.action;
            if(f.hp<=0){Cancel(f);return;}
            if(a.target.hp<=0){Cancel(f);return;}
            Vector3 from=f.body.transform.position,to=a.target.body.transform.position;
            if(a.phase==0)
            {
                float desired=a.move.shape=="Melee"?Mathf.Max(Clearance(f,a.target)+.08f,Mathf.Min(1.2f,Reach(f,a.target,a.move)*.8f)):Mathf.Min(6,Reach(f,a.target,a.move)*.8f);
                if(Vector3.Distance(from,to)>desired+.05f)
                {MoveActor(f,to+(from-to).normalized*desired,dt,4);return;}
                f.body.Face(to);
                if(mode==BattleMode.ZA)f.ready[a.slot]=now+a.move.cooldown;
                a.presentation=new AttackPresentation(f.body,a.target.body,a.move,zaFx,bdspFx,nativeTiming);
                a.phase=1;a.clock=0;f.state="Wind-up";
                Note($"{f.body.entry.source} body · {(a.presentation.Effect is BdspMove?"BDSP":"Z-A")} attack effects");
            }
            if(a.phase==1)
            {
                a.clock+=dt;a.presentation.Step(dt);
                if(!a.sounded&&a.clock>=a.presentation.SoundAt)
                {a.sounded=true;var clip=soundCatalog.MoveClip(a.move.id);if(clip){f.voice.volume=soundVolume;f.voice.PlayOneShot(clip);}}
                // Quick Attack's small committed burst is lab movement, synchronized to the native release.
                if(a.move.id=="QUICK_ATTACK"&&a.clock>=a.presentation.HitAt-.18f&&a.clock<a.presentation.HitAt)
                {
                    var delta=to-from;delta.y=0;float travel=Mathf.Max(0,delta.magnitude-.85f);
                    f.body.transform.position+=delta.normalized*Mathf.Min(travel,dt*22);
                }
                if(a.clock>=a.presentation.FreeAt)
                {
                    ReleaseAction(f);
                }
            }
            else if(a.phase==4)
            {MoveActor(f,f.home,dt,4);if(Vector3.Distance(f.body.transform.position,f.home)<.1f)Finish(f);}
        }
        void Impact(Fighter f,bool contact)
        {
            var a=f.action;var t=a.target;
            bool hit=contact&&(mode==BattleMode.ZA||CombatRules.Hits(a.move.accuracy,rng));
            if(!hit){Note(a.move.name+" missed");return;}
            int damage=a.remainingDamage??CombatRules.Damage(f.data,t.data,a.move,catalog.data.types,mode,a.style);
            t.hp=practice?t.hp:Mathf.Max(0,t.hp-damage);ResolvedHits++;
            if(damage>0)
            {
                if(t.hp<=0)t.body.Role("faint");else if(t.action==null)t.body.React();


            }
            Note(damage==0?"No effect · type immunity":$"{t.data.id} −{damage} HP");
            if(t.hp<=0){Cancel(t);if(Ended){Note(fighters.Any(x=>x.team==0&&x.hp>0)?"Your team won · R to repeat":"Enemy team won · R to repeat");queuedActor=queuedMove=-1;}}
        }
        void Finish(Fighter f)
        {
            var a=f.action;if(mode==BattleMode.Arceus)f.next+=CombatRules.ActionDelay(f.data.stats[3],a.style);
            f.action=null;f.state="Ready";f.body.Idle();f.body.Face(a.target.body.transform.position);sequenceAt=now+.25f;

        }
        void Cancel(Fighter f)
        {f.action?.presentation?.Dispose();f.action=null;f.state=f.hp<=0?"Fainted":"Ready";if(f.hp>0)f.body.Idle();}
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
            if(loading||loadError!=null)
            {
                Panel(320,310,800,240);Text(350,340,740,loading?"Loading your Pokémon…":"Model could not load",title,45);
                Text(350,400,740,loadError??"Only the selected battle models are loaded into memory.",small,90);
                if(loadError!=null&&Button(new Rect(350,500,280,35),"Return to default team")){playerSpecies=0;enemySpecies=2;ResetBattle();}
                return;
            }
            if(picker>=0){PokedexUI();return;}
            if(Direct&&!setup){DirectUI();if(effectAudit)EffectAuditUI();return;}
            Text(24,16,450,"POKÉMON / BATTLE LAB",title,40);
            Text(24,57,850,"04   /   POKÉDEX BATTLE LAB     ·     direct control + native attack presentation",muted);
            if(Button(new Rect(1090,20,150,40),inspect?"Back to battle":"Model viewer",inspect)){inspect=!inspect;ResetBattle();}
            if(Button(new Rect(1250,20,165,40),Paused?"Resume  [Esc]":"Pause  [Esc]"))Paused=!Paused;
            Panel(24,104,270,604);
            Text(40,118,235,"EXPERIMENT",muted);
            for(int i=0;i<3;i++)if(Button(new Rect(40,150+i*43,238,36),ModeNames[i],(int)mode==i))
            {mode=(BattleMode)i;inspect=false;style=MoveStyle.Normal;ResetBattle();}
            Text(40,290,238,inspect?"Species to compare":"Your Pokémon",small);
            if(Button(new Rect(40,318,238,34),Species[inspect?inspectSpecies:playerSpecies]+"  ›"))
            {OpenPicker(inspect?6:0);}
            if(!inspect)
            {
                Text(40,368,238,"Newest model: "+Sources[playerSource],small);
                Text(40,408,238,"Opponent",small);
                if(Button(new Rect(40,435,238,34),Species[enemySpecies]+"  ›")){OpenPicker(1);}
                Text(40,485,238,"Newest model: "+Sources[enemySource],small);
                bool nextAi=GUI.Toggle(new Rect(43,532,230,25),ai," Opponent commands enabled");if(nextAi!=ai){ai=nextAi;ResetBattle();}
            }
            if(Button(new Rect(40,578,238,40),"Reset experiment  [R]"))ResetBattle();
            bool nextPractice=GUI.Toggle(new Rect(43,629,230,25),practice," Practice · keep HP full");if(nextPractice!=practice){practice=nextPractice;ResetBattle();}
            if(Direct&&Button(new Rect(40,663,238,34),"Play Pokémon  [Tab]"))setup=false;
            if(loading)return;
            if(inspect)GalleryUI();else {BattleUI();LoadoutUI();if(mode==BattleMode.ZA)TeamSetupUI();}
            Panel(24,724,270,152);Text(40,736,238,$"Playback speed   {gameSpeed:0.00}×",small);
            gameSpeed=GUI.HorizontalSlider(new Rect(43,769,230,20),gameSpeed,.25f,1.5f);
            Text(40,796,238,"Camera distance",small);cameraDistance=GUI.HorizontalSlider(new Rect(43,829,230,20),cameraDistance,4.5f,14);
            Text(322,872,1090,"Native model/clip exports · reconstructed mechanics subset · native ZA / BDSP effects · Arceus / SV use ZA effects · desktop prototype",muted,25);
        }
        void DirectUI()
        {
            var p=Player;var foe=Target??fighters.First(f=>f.team==1);
            Panel(22,20,410,92);Text(38,29,380,p.data.id+"  /  "+p.body.entry.source,title,36);
            Text(38,68,380,$"{p.hp}/{p.MaxHp} HP · {(practice?"Practice":"Battle")} · {p.state}",small);
            Panel(1090,20,328,92);Text(1106,31,296,foe.data.id+"  /  "+foe.body.entry.source,label);
            Text(1106,68,296,$"{foe.hp}/{foe.MaxHp} HP · {(ai?"Opponent active":"Training target")}",small);
            if(Button(new Rect(22,125,195,36),"Setup / models  [Tab]"))setup=true;
            Text(450,27,620,"PLAY AS YOUR POKÉMON",small);
            Text(450,58,620,"WASD move · Shift walk · hold RMB look · Space dash",small);
            Text(450,85,620,"1–4 attack · Q ally · E target · wheel zoom · R reset",small);
            if(Paused)Text(560,370,400,"PAUSED · Esc to resume",title,50);
            Text(330,663,900,message,label,55);
            for(int i=0;i<ActiveSlots;i++)
            {
                var m=catalog.Move(p.moves[i]);float left=Mathf.Max(0,p.ready[i]-now);
                GUI.enabled=!Paused&&!Ended&&p.action==null&&left<=0&&now>=dodgeUntil;
                string status=left>0?$"{left:0.0}s":p.action!=null?p.state:"Ready";
                float stab=p.data.types.Contains(m.type)?1.5f:1;float type=CombatRules.Effectiveness(catalog.data.types,m.type,foe.data.types,mode);
                int preview=CombatRules.Damage(p.data,foe.data,m,catalog.data.types,mode,style);
                if(Button(new Rect(230+i*270,735,258,76),$"[{i+1}] {m.name} · {status}\n{preview} damage · STAB {stab:0.#}× · type {type:0.##}×"))Command(i);GUI.enabled=true;
            }
            Text(302,823,950,$"Body: {p.body.CurrentClip}  ·  Effects: {(p.body.entry.source=="BDSP"?"BDSP where available / Z-A fallback":"Z-A")}"+(p.body.entry.source is "Arceus" or "SV" or "Sword"?" (cross-source presentation)":""),small);
            Text(302,852,1000,"Attacks approach automatically · WASD cancels approach / exits recovery · Tab changes teams and moves",muted);
            TeamBattleUI();
            if(now<clashMessageUntil)Text(360,130,700,clashMessage,label,95);
        }
        void LoadoutUI()
        {
            var f=Player;if(f==null)return;var chosen=Loadout(f.data.id);
            Panel(330,302,710,318);Text(348,316,670,"EQUIPPED MOVES · click to cycle · * test-only choice",label);
            for(int slot=0;slot<ActiveSlots;slot++)
                if(Button(new Rect(348,352+slot*46,668,40),$"[{slot+1}] {catalog.Move(f.moves[slot]).name}{(pokedex?.Row(f.data.id).testOnlyMoves?.Contains(f.moves[slot])==true?" *":"")}  ›"))
                {int next=chosen[slot];do{next=(next+1)%f.data.moves.Length;}while(chosen.Contains(next));chosen[slot]=next;ResetBattle();}
            Text(348,545,670,"Sound effects · Emerald recordings, substitutes for newer moves",small);
            soundVolume=GUI.HorizontalSlider(new Rect(352,583,650,20),soundVolume,0,1);
        }
        void GalleryUI()
        {
            for(int i=0;i<gallery.Count;i++)
            {
                float x=337+(i+(3-gallery.Count)*.5f)*357;Panel(x,109,334,85);Text(x+15,119,300,gallery[i].entry.source,title,35);
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
            else if(mode==BattleMode.ZA)Text(330,677,1000,"WASD  Pokémon movement     RMB  camera     SPACE  dash     1–4  attacks",small);
            else Text(330,677,1000,$"Round {round}  ·  both sides select, then priority and Speed decide order",small);
            Text(330,244,1065,message,label,50);
            if(diagnostics)
            {
                Panel(1090,411,325,239);Text(1104,422,296,"OBSERVATION LOG",muted);
                int row=0;foreach(var line in log)Text(1104,454+row++*28,296,line,small,28);
            }
            for(int i=0;i<ActiveSlots;i++)
            {
                var f=fighters[0];var m=catalog.Move(f.moves[i]);float left=Mathf.Max(0,f.ready[i]-now);
                string detail=mode==BattleMode.ZA?(left>0?$"Cooldown {left:0.0}s":f.action!=null?"Action in progress":"Ready"):$"PP {f.pp[i]} / {m.pp}  ·  {m.type}";
                GUI.enabled=!Paused&&!Ended&&f.action==null&&(mode==BattleMode.ZA?left<=0:ActiveActions==0&&queuedActor<0&&now>=sequenceAt&&f.pp[i]>=(mode==BattleMode.Arceus?CombatRules.StyleCost(style):1)&&(mode!=BattleMode.Arceus||CombatRules.NextActor(f.next,fighters[1].next)==0));
                if(Button(new Rect(324+i*273,737,263,76),$"[{i+1}]   {m.name}\n{detail}"))Command(i);GUI.enabled=true;
            }
            Text(330,825,1060,mode==BattleMode.ZA?"Z-A cooldown/range rows imported; movement, aim, animation staging and damage remain a prototype.":mode==BattleMode.Arceus?"Agile trades power for earlier actions; Strong delays the next action. Formulas are lab tuning.":"Traditional-turn subset: priority, Speed, PP, accuracy and type damage. Items, abilities and status omitted.",muted,45);
        }
        // Render the arena directly so background verification never waits for a focused window.
        // Null-device runs still exercise gameplay, but deliberately produce no visual evidence.
        void CaptureEvidence(string path)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var previous=RenderTexture.active;var target=cam.targetTexture;var rect=cam.rect;
            var frame=RenderTexture.GetTemporary(1024,640,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(1024,640,TextureFormat.RGB24,false);
            try
            {
                cam.targetTexture=frame;cam.rect=new Rect(0,0,1,1);cam.Render();
                RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,1024,640),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture=target;cam.rect=rect;RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(frame);Destroy(pixels);
            }
        }
        IEnumerator SafeSmoke()
        {
            var run=Environment.GetCommandLineArgs().Contains("-lab-stream-combat")?StreamCombatSmoke():Environment.GetCommandLineArgs().Contains("-lab-roster-smoke")?RosterSmoke():teamSmoke?TeamSmokeRun():SmokeRun();
            while(true)
            {
                object item;
                try { if(!run.MoveNext())yield break;item=run.Current; }
                catch(Exception e){Debug.LogException(e);Application.Quit(1);yield break;}
                yield return item;
            }
        }
        IEnumerator PresentationSmoke(string folder,List<string> summaries)
        {
            teamSize=1;mode=BattleMode.ZA;inspect=false;ai=false;practice=true;setup=false;gameSpeed=1.5f;
            int cases=0;
            foreach(var model in catalog.models)
            {
                int source=Array.IndexOf(Sources,model.source),species=Array.IndexOf(Species,model.species);
                playerSource=source;playerSpecies=species;enemySpecies=species==2?0:2;enemySource=1;ResetBattle();
                var p=fighters[0];var start=p.body.transform.position;
                testInput=Vector3.back;float until=now+.6f;while(now<until)yield return null;testInput=Vector3.zero;
                if(Vector3.Distance(start,p.body.transform.position)<.8f)throw new Exception("Direct movement failed "+p.body.name);
                yield return null;
                if(p.body.CurrentClip!=p.body.entry.battle.name)throw new Exception("Run did not recover to idle");
                testDash=true;yield return new WaitForSecondsRealtime(.3f);
                summaries.Add(p.body.name+": direct move/idle/dash passed");
                for(int choice=0;choice<p.data.moves.Length;choice++)
                {
                    // Put each of the six choices in slot four, retaining a unique four-move loadout.
                    loadouts[p.data.id]=Enumerable.Range(0,p.data.moves.Length).Where(i=>i!=choice).Take(3).Append(choice).ToArray();
                    ResetBattle();p=fighters[0];var foe=fighters[1];int slot=3;
                    p.body.transform.position=new Vector3(-1,0,0);
                    var testMove=catalog.Move(p.moves[slot]);float distance=Mathf.Min(testMove.shape=="Melee"?1.3f:4.5f,Reach(p,foe,testMove)*.8f);
                    foe.body.transform.position=new Vector3(-1+distance,0,0);
                    string id=p.moves[slot];
                    if(!Command(slot))throw new Exception("Move rejected "+p.body.name+id);
                    yield return null;float approachDeadline=Time.realtimeSinceStartup+8;
                    while(p.action!=null&&p.action.presentation==null&&Time.realtimeSinceStartup<approachDeadline)yield return null;
                    if(p.action?.presentation==null)throw new Exception("Presentation never started "+id);
                    var presentation=p.action.presentation;
                    if(choice==0)
                    {
                        float clock=presentation.Time,clip=p.body.ClipTime;var position=p.body.transform.position;
                        Paused=true;yield return new WaitForSecondsRealtime(.15f);
                        if(presentation.Time!=clock||p.body.ClipTime!=clip||p.body.transform.position!=position)throw new Exception("Pause advanced presentation");
                        Paused=false;
                    }
                    float deadline=Time.realtimeSinceStartup+20;int particles=0;bool captured=false,impactCaptured=false,sounded=false;
                    var seen=new HashSet<string>();
                    while(p.action!=null&&Time.realtimeSinceStartup<deadline)
                    {
                        seen.Add(p.body.CurrentClip);sounded|=p.action.sounded;
                        if(presentation.Alive)particles=Mathf.Max(particles,presentation.Effect.gameObject.GetComponentsInChildren<ParticleSystem>().Sum(x=>x.particleCount));
                        if(!captured&&presentation.Time>=presentation.HitAt*.8f)
                        {
                            captured=true;CaptureEvidence(Path.Combine(folder,$"{model.source}-{model.species}-{id}.png"));
                        }
                        if(!impactCaptured&&presentation.Time>=presentation.HitAt+.04f)
                        {
                            impactCaptured=true;CaptureEvidence(Path.Combine(folder,$"{model.source}-{model.species}-{id}-impact.png"));
                        }
                        yield return null;
                    }
                    if(p.action!=null||ResolvedHits!=1)throw new Exception("Attack failed to settle exactly once "+p.body.name+id+" hits="+ResolvedHits);
                    if(particles==0)throw new Exception("No live native particles "+p.body.name+id);
                    if(!sounded)throw new Exception("Move audio cue did not fire "+id);
                    if(p.body.CurrentClip!=p.body.entry.battle.name)throw new Exception("Attack stuck "+p.body.name+id);
                    start=p.body.transform.position;testInput=Vector3.back;until=now+.4f;while(now<until)yield return null;testInput=Vector3.zero;
                    if(Vector3.Distance(start,p.body.transform.position)<.4f)throw new Exception("Movement remained locked after attack");
                    summaries.Add($"{p.body.name}/{id}: one hit, audio cue, release + movement; peak particles={particles}; clips={string.Join(",",seen)}");cases++;
                }
                loadouts[p.data.id]=Enumerable.Range(0,ActiveSlots).ToArray();
                ResetBattle();Command(1);yield return null;ResetBattle();yield return new WaitForSecondsRealtime(.15f);
                if(ActiveActions!=0||tails.Count!=0||FindObjectsByType<VioletMove>(FindObjectsSortMode.None).Length!=0||FindObjectsByType<BdspMove>(FindObjectsSortMode.None).Length!=0)throw new Exception("Reset retained native presentation");
                File.WriteAllLines(Path.Combine(folder,"presentation-smoke.txt"),summaries);
            }
            // Damage reaction must recover independently of the attacker's state.
            playerSource=0;playerSpecies=0;ResetBattle();fighters[0].body.React();yield return new WaitForSecondsRealtime(2);
            if(fighters[0].body.CurrentClip!=fighters[0].body.entry.battle.name)throw new Exception("Arceus damage reaction stuck");
            summaries.Add("Arceus Pikachu: split damage reaction returned to idle");
            gameSpeed=1;playerSpecies=0;enemySpecies=2;
            File.WriteAllLines(Path.Combine(folder,"presentation-smoke.txt"),summaries);
            Debug.Log($"LAB PRESENTATION SMOKE PASSED: {cases} attacks across {catalog.models.Length} models");
        }
        IEnumerator SmokeRun()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/team-battle"));Directory.CreateDirectory(folder);
            var summaries=new List<string>();
            var presentation=PresentationSmoke(folder,summaries);
            while(presentation.MoveNext())yield return presentation.Current;
            foreach(BattleMode testMode in Enum.GetValues(typeof(BattleMode)))
            {
                mode=testMode;style=MoveStyle.Normal;playerSource=enemySource=mode==BattleMode.BDSP?2:mode==BattleMode.Arceus?0:1;inspect=false;ai=true;practice=false;ResetBattle();
                float deadline=Time.realtimeSinceStartup+45;
                while(ResolvedHits<2&&Time.realtimeSinceStartup<deadline&&!Ended){Command(1);yield return null;}
                if(ResolvedHits<2)throw new Exception("Smoke failed: fewer than two landed hits in "+mode);
                summaries.Add(mode+": "+ResolvedHits+" hits, HP="+fighters[0].hp+"/"+fighters[1].hp);
                if(mode!=testMode)throw new Exception("Test scenario changed unexpectedly");
                yield return new WaitForSecondsRealtime(.25f);
                Paused=true;CaptureEvidence(Path.Combine(folder,mode+".png"));yield return new WaitForSecondsRealtime(.3f);
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
                inspectSpecies=modelIndex;ResetBattle();yield return new WaitForSecondsRealtime(1);
                CaptureEvidence(Path.Combine(folder,"models-"+Species[inspectSpecies]+".png"));yield return new WaitForSecondsRealtime(.3f);
            }
            File.WriteAllLines(Path.Combine(folder,"smoke.txt"),summaries);Debug.Log("LAB SMOKE PASSED");Application.Quit(0);
        }
        void OnDestroy(){Application.logMessageReceived-=SmokeLog;if(volumeProfile)Destroy(volumeProfile);AudioListener.pause=false;StopActions();foreach(var a in gallery)if(a)Destroy(a.gameObject);foreach(var m in materials.Values)if(m)Destroy(m);pokedex?.Dispose();}
    }
}
