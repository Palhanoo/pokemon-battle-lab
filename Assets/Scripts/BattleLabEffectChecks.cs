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
        IEnumerator EffectAuditSmoke()
        {
            while(loading)yield return null;
            bool travelReview=Environment.GetCommandLineArgs().Contains("-lab-travel-review");
            bool full=Environment.GetCommandLineArgs().Contains("-lab-attack-completion");
            bool shaders=Environment.GetCommandLineArgs().Contains("-lab-shader-details");
            bool visuals=Environment.GetCommandLineArgs().Contains("-lab-visual-layers")||shaders||full;
            bool lifecycle=Environment.GetCommandLineArgs().Contains("-lab-effect-lifecycle");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs/evidence/"+(full?"attack-completion":shaders?"shader-details":visuals?"visual-layers":"effect-repair"+(lifecycle?"/lifecycle":""))));Directory.CreateDirectory(folder);
            var report=new List<string>();teamSize=1;ai=false;practice=true;setup=inspect=false;mode=BattleMode.ZA;gameSpeed=1;
            playerSpecies=Array.IndexOf(Species,"PIKACHU");enemySpecies=Array.IndexOf(Species,"MUDKIP");ResetBattle();while(loading)yield return null;
            if(loadError!=null)throw new Exception(loadError);
            Paused=true;Player.body.Idle();Player.body.Tick(0);
            float floorError=Player.body.PosedBounds().min.y-Player.body.transform.position.y;
            if(Mathf.Abs(floorError)>.025f)throw new Exception("Pikachu posed ground offset: "+floorError);
            cam.transform.position=Player.body.Centre+new Vector3(1,.4f,-2);cam.transform.LookAt(Player.body.Centre);CaptureEvidence(Path.Combine(folder,"pikachu-ground.png"));
            report.Add("Pikachu actual skinned vertices ground error: "+floorError);
            foreach(string move in new[]{"PECK","AERIAL_ACE"})if(soundCatalog.MoveClip(move).length>.7f)throw new Exception(move+" still has long Gust tail");
            report.Add("Both Flying test moves use the 0.65s faded clip (was 11.2167s)");
            Paused=false;ai=true;
            foreach(var f in fighters)for(int s=0;s<ActiveSlots;s++)f.ready[s]=now+20;
            var playerAt=Player.body.transform.position;var enemyAt=Target.body.transform.position;
            float until=now+.8f;while(now<until)yield return null;
            if(Vector3.Distance(enemyAt,Target.body.transform.position)<.3f||Vector3.Distance(playerAt,Player.body.transform.position)>.01f)throw new Exception("AI reposition/control ownership failed");
            ai=false;Paused=true;report.Add("Uncontrolled fighter repositions on cooldown; controlled Pikachu stays put without input");
            Player.body.transform.position=new Vector3(-2,0,0);Target.body.transform.position=new Vector3(2,0,0);Player.body.Face(Target.body.Centre);Target.body.Face(Player.body.Centre);
            var midpoint=(Player.body.Centre+Target.body.Centre)*.5f;
            cam.transform.position=midpoint+new Vector3(0,2,-6);cam.transform.LookAt(midpoint);
            if(shaders)CheckRefractionPixels(folder);
            int sphereMasks=0;
            foreach(var source in travelReview?new[]{"ZA"}:new[]{"ZA","BDSP"})
            {
                var ids=source=="ZA"?zaFx.moves.Select(m=>m.id).ToArray():bdspFx.moves.Select(m=>m.id).ToArray();
                if(full&&ids.Length!=(source=="ZA"?catalog.data.moves.Length:28))throw new Exception(source+" staged attack set changed: "+ids.Length);
                if(full&&source=="BDSP"){playerSpecies=Array.IndexOf(Species,"NIDOKING");ResetBattle();while(loading)yield return null;Paused=true;Player.body.transform.position=new Vector3(-2,0,0);Target.body.transform.position=new Vector3(2,0,0);Player.body.Face(Target.body.Centre);Target.body.Face(Player.body.Centre);midpoint=(Player.body.Centre+Target.body.Centre)*.5f;}
                if(shaders)ids=ids.Where(id=>(source=="ZA"?new[]{"MOVE_FLAMETHROWER","MOVE_DRAGON_PULSE","MOVE_HYDRO_PUMP","MOVE_PSYBEAM","MOVE_PSYCHIC","MOVE_SHADOW_BALL","MOVE_THUNDERBOLT"}:new[]{"MOVE_THUNDERBOLT","MOVE_PSYCHIC","MOVE_BRICK_BREAK"}).Contains(id)).ToArray();
                else if(visuals&&!full)ids=ids.Where(id=>(source=="ZA"?new[]{"MOVE_FLAMETHROWER","MOVE_EMBER","MOVE_DRAGON_PULSE","MOVE_HYDRO_PUMP","MOVE_PSYBEAM"}:new[]{"MOVE_BRICK_BREAK","MOVE_IRON_TAIL","MOVE_LICK"}).Contains(id)).ToArray();
                if(lifecycle)ids=ids.Where(id=>id==(source=="ZA"?"MOVE_HYDRO_PUMP":"MOVE_PSYCHIC")).ToArray();
                var only=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-lab-only="));
                if(only!=null)ids=ids.Where(id=>only.Substring(10).Split(',').Contains(id)).ToArray();
                foreach(string id in ids)
                {
                    IMoveFx fx=source=="ZA"?zaFx.Play(id,AttackPresentation.Bind(Player.body),AttackPresentation.Bind(Target.body),id=="MOVE_HYDRO_PUMP"?n=>!n.Contains("0c0"):null):bdspFx.Play(id,AttackPresentation.Bind(Player.body),AttackPresentation.Bind(Target.body));
                    if(fx==null)throw new Exception(source+id+" missing presentation");
                    if(fx is VioletMove v){v.time=0;v.FitUnitBeam=id=="MOVE_HYDRO_PUMP"||id=="MOVE_PSYBEAM";}else if(fx is BdspMove b)b.time=0;
                    AttackPresentation.ApplyReadability(fx.gameObject,id.Replace("MOVE_",""));
                    if(source=="ZA"&&(id=="MOVE_FIRE_SPIN"||id=="MOVE_SHADOW_BALL"))
                    {
                        var group=fx.gameObject.transform.Find(id=="MOVE_FIRE_SPIN"?"bullet0":"impact0_0");
                        if(!group||group.childCount==0||Mathf.Abs(group.GetChild(0).localScale.x-.3f)>.001f)throw new Exception(id+" readability scale was not applied to the inner emitter set");
                    }
                    fx.ManualSimulate=true;var systems=fx.gameObject.GetComponentsInChildren<ParticleSystem>(true);
                    var materials=fx.gameObject.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
                    foreach(var m in materials)
                    {
                        if(!m.shader.isSupported)throw new Exception(id+" unsupported shader");
                        foreach(string prefix in new[]{"Main","Sub","Third"})
                        {
                            if(m.HasProperty("_"+prefix+"Sphere")&&m.GetFloat("_"+prefix+"Sphere")>.5f)sphereMasks++;
                            if(prefix!="Main"&&m.GetFloat("_Use"+prefix)<.5f)continue;
                            if(prefix!="Main"&&!m.GetTexture("_"+prefix+"Tex"))throw new Exception(id+" missing bound "+prefix+" mask");
                        }
                    }
                    var models=fx.gameObject.GetComponentsInChildren<BdspModel>(true);
                    if(visuals&&source=="BDSP"&&new[]{"MOVE_LICK","MOVE_IRON_TAIL","MOVE_BRICK_BREAK"}.Contains(id)&&models.Length!=(id=="MOVE_LICK"?1:2))throw new Exception(id+" missing model layers");
                    var perLayer=new Dictionary<string,int>();
                    bool travelDiagnosed=false;
                    bool refractionCaptured=false;int maximum=0;int snapshots=0;bool captured=false;float captureAt=Mathf.Max(.1f,fx.HitTime+.1f);
                    float step=travelReview?1f/60:.1f;
                    for(float t=0;t<Mathf.Min(fx.EndTime+1,20);t+=step)
                    {
                        fx.Step(step);foreach(var m in materials)if(m.HasProperty("_EffectTime"))m.SetFloat("_EffectTime",fx.Now);
                        maximum=Mathf.Max(maximum,systems.Sum(ps=>ps.particleCount));
                        foreach(var ps in systems){string key=ps.transform.parent.name+"/"+ps.name;perLayer[key]=Mathf.Max(perLayer.TryGetValue(key,out int n)?n:0,ps.particleCount);}
                        if(travelReview&&!travelDiagnosed&&fx.Now>=1.5f)
                        {
                            travelDiagnosed=true;var details=new List<string>();var saved=new Dictionary<Renderer,Material>();
                            foreach(var ps in systems.Where(p=>p.transform.parent.name=="pos"||p.transform.parent.name=="root"))
                            {
                                var r=ps.GetComponent<ParticleSystemRenderer>();var original=r.sharedMaterial;if(!original)continue;
                                var parts=new ParticleSystem.Particle[ps.particleCount];ps.GetParticles(parts);
                                var custom=new List<Vector4>();ps.GetCustomParticleData(custom,ParticleSystemCustomData.Custom1);
                                details.Add(ps.transform.parent.parent.parent.name+"/"+ps.name+" bounds="+r.bounds+" scale="+ps.transform.lossyScale+" pos="+ps.transform.position+" custom="+string.Join(";",custom));
                                foreach(var part in parts)details.Add("  local="+part.position+" size="+part.GetCurrentSize3D(ps)+" remaining="+part.remainingLifetime);
                                saved[r]=original;var diagnostic=new Material(original);diagnostic.SetFloat("_Model",1);diagnostic.SetColor("_ModelColor",Color.magenta);diagnostic.SetFloat("_NativeCombiner",0);diagnostic.SetFloat("_AlphaMode",0);diagnostic.SetFloat("_UseSub",0);diagnostic.SetFloat("_UseThird",0);diagnostic.SetFloat("_MainGrey",0);diagnostic.SetFloat("_Refraction",0);diagnostic.SetTexture("_MainTex",Texture2D.whiteTexture);diagnostic.SetFloat("_Cull",0);r.sharedMaterial=diagnostic;
                            }
                            CaptureEvidence(Path.Combine(folder,source+"-"+id+"-geometry.png"));
                            foreach(var entry in saved){Destroy(entry.Key.sharedMaterial);entry.Key.sharedMaterial=entry.Value;}
                            File.WriteAllLines(Path.Combine(folder,source+"-"+id+"-geometry.txt"),details);
                            var coreMaterials=saved.Values.Distinct().ToArray();
                            foreach(var variant in new[]{"local-clock","no-secondary-alpha","eft1-alpha"})
                            {
                                var copies=coreMaterials.ToDictionary(m=>m,m=>new Material(m));
                                foreach(var m in coreMaterials)
                                {
                                    if(variant=="local-clock")m.SetFloat("_EffectTime",.15f);
                                    if(variant=="no-secondary-alpha"){var channels=m.GetVector("_AlphaChannels");channels.y=channels.z=2;m.SetVector("_AlphaChannels",channels);}
                                    if(variant=="eft1-alpha")m.SetFloat("_AlphaMode",id=="MOVE_FLAMETHROWER"?6:2);
                                }
                                CaptureEvidence(Path.Combine(folder,source+"-"+id+"-"+variant+".png"));
                                foreach(var m in coreMaterials){m.CopyPropertiesFromMaterial(copies[m]);Destroy(copies[m]);}
                            }
                        }
                        if(shaders&&!refractionCaptured&&systems.Any(ps=>ps.particleCount>0&&ps.GetComponent<ParticleSystemRenderer>().sharedMaterial&&ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetFloat("_Refraction")>.5f))
                        {
                            refractionCaptured=true;
                            var refractors=systems.Select(ps=>ps.GetComponent<ParticleSystemRenderer>()).Where(r=>r.enabled&&r.sharedMaterial&&r.sharedMaterial.GetFloat("_Refraction")>.5f).ToArray();
                            CaptureEvidence(Path.Combine(folder,source+"-"+id+"-refraction-on.png"));
                            foreach(var r in refractors)r.enabled=false;
                            CaptureEvidence(Path.Combine(folder,source+"-"+id+"-refraction-off.png"));
                            foreach(var r in refractors)r.enabled=true;
                        }
                        if(visuals&&fx.Now>=(travelReview?1.3f+snapshots*.1f:fx.StartTime+.2f+snapshots*(full?Mathf.Max(.15f,(fx.EndTime-fx.StartTime)/11):.25f))&&snapshots<12)
                        {
                            cam.transform.position=midpoint+new Vector3(0,2,-6);cam.transform.LookAt(midpoint);
                            CaptureEvidence(Path.Combine(folder,source+"-"+id+"-"+(snapshots++).ToString("00")+".png"));
                        }
                        if(!captured&&fx.Now>=captureAt)
                        {
                            cam.transform.position=midpoint+new Vector3(0,2,-6);cam.transform.LookAt(midpoint);CaptureEvidence(Path.Combine(folder,source+"-"+id+".png"));captured=true;
                            if(Environment.GetCommandLineArgs().Contains("-lab-isolate"))
                            {
                                var active=systems.Select(p=>p.GetComponent<ParticleSystemRenderer>()).Where(r=>r.enabled&&r.GetComponent<ParticleSystem>().particleCount>0).ToArray();
                                foreach(var r in active)r.enabled=false;
                                string layers=Path.Combine(folder,"isolated",source+"-"+id);Directory.CreateDirectory(layers);
                                for(int k=0;k<active.Length;k++)
                                {
                                    var r=active[k];r.enabled=true;CaptureEvidence(Path.Combine(layers,k.ToString("00")+"-"+r.name+".png"));
                                    if(source=="BDSP"&&id=="MOVE_SEED_BOMB"&&r.name=="seed_base")
                                    {
                                        var m=r.sharedMaterial;var original=new Material(m);
                                        foreach(string variant in new[]{"no-third","no-sub","no-tint","no-primitive","colour-only","no-tint-colour-only","white"})
                                        {
                                            m.CopyPropertiesFromMaterial(original);
                                            if(variant=="no-third")m.SetFloat("_UseThird",0);
                                            if(variant=="no-sub")m.SetFloat("_UseSub",0);
                                            if(variant=="no-tint")m.SetFloat("_MeshParticles",1);
                                            if(variant=="no-primitive")m.SetFloat("_PrimitiveStream",0);
                                            if(variant=="no-tint-colour-only"){m.SetFloat("_ColorMode",0);m.SetFloat("_MeshParticles",1);}
                                            if(variant=="colour-only")m.SetFloat("_ColorMode",0);
                                            if(variant=="white"){m.SetFloat("_Model",1);m.SetColor("_ModelColor",Color.white);m.SetFloat("_ColorMode",0);m.SetFloat("_MeshParticles",1);}
                                            CaptureEvidence(Path.Combine(layers,k.ToString("00")+"-"+variant+".png"));
                                        }
                                        m.CopyPropertiesFromMaterial(original);Destroy(original);
                                        var d0=new List<Vector4>();var d1=new List<Vector4>();var ps=r.GetComponent<ParticleSystem>();ps.GetCustomParticleData(d0,ParticleSystemCustomData.Custom1);ps.GetCustomParticleData(d1,ParticleSystemCustomData.Custom2);
                                        File.WriteAllText(Path.Combine(layers,k.ToString("00")+"-data.txt"),"C0 "+string.Join(";",d0)+" C1 "+string.Join(";",d1)+" Main "+m.GetTexture("_MainTex").name+" Channels "+m.GetVector("_ColorChannels"));
                                        var streams=new List<ParticleSystemVertexStream>();r.GetActiveVertexStreams(streams);
                                        var baked=new Mesh();r.BakeMesh(baked,cam,ParticleSystemBakeMeshOptions.BakeRotationAndScale);
                                        string layout="\nStreams "+string.Join(",",streams)+"\nColors "+string.Join(";",baked.colors.Take(8));
                                        for(int channel=0;channel<5;channel++){var uv=new List<Vector4>();baked.GetUVs(channel,uv);layout+="\nUV"+channel+" "+string.Join(";",uv.Take(8));}
                                        File.AppendAllText(Path.Combine(layers,k.ToString("00")+"-data.txt"),layout);Destroy(baked);

                                    }
                                    r.enabled=false;
                                }
                                foreach(var r in active)r.enabled=true;
                            }
                        }
                    }
                    if(shaders&&materials.Any(m=>m.GetFloat("_Refraction")>.5f)&&!refractionCaptured)throw new Exception(id+" refraction never emitted");
                    if(maximum==0)throw new Exception(source+id+" never emitted particles");
                    if((id=="MOVE_HYDRO_PUMP"||id=="MOVE_PSYBEAM")&&source=="ZA")
                    {
                        var beam=fx.gameObject.transform.Find("bullet0");var direction=Target.body.Centre-VioletMove.Node(AttackPresentation.Bind(Player.body),"eff_rangeattack01");
                        if(Vector3.Dot(beam.forward,direction.normalized)<.999f||Mathf.Abs(beam.localScale.z*1.1f-direction.magnitude)>.01f)throw new Exception("Hydro beam axis/length mismatch");
                    }
                    if(visuals)foreach(var model in models)
                    {
                        if(model.gameObject.activeInHierarchy)throw new Exception(id+" model layer stayed visible after ModelCreate end");
                        var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                        if(skin){var baked=new Mesh();model.Sample(0);skin.BakeMesh(baked);var before=baked.vertices;model.Sample(.25f);skin.BakeMesh(baked);var after=baked.vertices;
                            if(!before.Where((v,i)=>Vector3.Distance(v,after[i])>.0001f).Any())throw new Exception(id+" model skeleton never deformed");Destroy(baked);}
                    }
                    report.Add($"{source} {id}: models={models.Length}; {systems.Length} systems, peak {maximum} particles; refraction={materials.Count(m=>m.GetFloat("_Refraction")>.5f)}, soft={materials.Count(m=>m.GetVector("_SoftParticle").x>.5f)}; material bindings/clock exercised; contact capture saved");
                    File.WriteAllLines(Path.Combine(folder,source+"-"+id+"-layers.txt"),perLayer.Select(p=>p.Key+"="+p.Value));
                    Destroy(fx.gameObject);File.WriteAllLines(Path.Combine(folder,"focused-checks-in-progress.txt"),report);yield return null;
                }
            }
            report.Add("Sphere-projected material bindings exercised: "+sphereMasks+". These checks do not establish native visual parity.");
            if(visuals)
            {
                if(travelReview)
                {
                    Paused=false;ai=false;gameSpeed=1;
                    foreach(var move in new[]{"FLAMETHROWER","DRAGON_PULSE"})
                    {
                        ResetBattle();while(loading)yield return null;Player.moves[0]=move;
                        Player.body.transform.position=Vector3.left*2;Target.body.transform.position=Vector3.right*2;
                        if(!Command(0))throw new Exception(move+" stream command rejected");
                        float limit=Time.realtimeSinceStartup+20;
                        while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<limit)yield return null;
                        if(ActiveActions>0||tails.Count>0||ResolvedHits!=1)throw new Exception(move+" stream must hit once and fully expire");
                        report.Add(move+": restored volley resolves exactly one hit and all tails expire");
                    }
                    enemySpecies=playerSpecies;ResetBattle();while(loading)yield return null;Player.moves[0]=Target.moves[0]="FLAMETHROWER";
                    Player.body.transform.position=Vector3.left*2;Target.body.transform.position=Vector3.right*2;
                    Command(0);Begin(1,0,MoveStyle.Normal);float clashLimit=Time.realtimeSinceStartup+20;
                    var flight=new List<string>();
                    while((ActiveActions>0||tails.Count>0)&&Time.realtimeSinceStartup<clashLimit)
                    {
                        foreach(var fighter in fighters)if(fighter.action?.presentation!=null&&fighter.action.presentation.Travel(out var from,out var to))flight.Add($"{fighter.team}: {fighter.action.clock:F3} {from} -> {to}");
                        yield return null;
                    }
                    File.WriteAllLines(Path.Combine(folder,"travel-clash-paths.txt"),flight);
                    if(ActiveActions>0||tails.Count>0||clashCount!=1||ResolvedHits!=0)throw new Exception($"Flamethrower volleys must clash once and cancel every child: actions={ActiveActions}, tails={tails.Count}, clashes={clashCount}, hits={ResolvedHits}");
                    report.Add("Opposing Flamethrower volleys: one clash, no damage, no remaining actions/tails");
                }
                if(smokeErrors>0)throw new Exception(lastSmokeError);
                File.WriteAllLines(Path.Combine(folder,"focused-checks.txt"),report);Debug.Log(full?"LAB ATTACK COMPLETION CAPTURES PASSED":shaders?"LAB SHADER DETAILS PASSED":"LAB VISUAL LAYERS PASSED");Application.Quit(0);yield break;
            }
            SpawnClash(midpoint);var ring=clashBursts.Last().root.GetComponent<LineRenderer>();now+=.12f;TickClashes(.12f);
            if(!ring||ring.GetPosition(0).magnitude<.2f)throw new Exception("Clash ring did not expand");
            CaptureEvidence(Path.Combine(folder,"clash-contact.png"));now+=.5f;TickClashes(.5f);
            if(clashBursts.Count!=0)throw new Exception("Clash flash leaked");
            report.Add("Collision flash expands at the contact point and cleans up within .45s");
            Paused=false;ResetBattle();while(loading)yield return null;
            Player.moves[0]=Target.moves[0]="EMBER";Player.body.transform.position=new Vector3(-1.3f,0,0);Target.body.transform.position=new Vector3(1.3f,0,0);
            Command(0);Begin(1,0,MoveStyle.Normal);float deadline=Time.realtimeSinceStartup+20;
            while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)yield return null;
            if(ActiveActions>0||clashCount!=1)throw new Exception("Actual opposing projectile clash failed");
            report.Add("Opposing Ember actions produce one actual clash event");
            playerSpecies=Array.IndexOf(Species,"GIRATINA");enemySpecies=Array.IndexOf(Species,"LATIAS");ResetBattle();while(loading)yield return null;
            if(loadError!=null)throw new Exception(loadError);
            if(Vector3.Distance(Player.body.transform.position,Target.body.transform.position)+.01f<Clearance(Player,Target))throw new Exception("Large bodies overlap at spawn");
            Player.moves[0]="TACKLE";Command(0);deadline=Time.realtimeSinceStartup+25;
            while(ActiveActions>0&&Time.realtimeSinceStartup<deadline)
            {
                if(Vector3.Distance(Player.body.transform.position,Target.body.transform.position)+.03f<Clearance(Player,Target))throw new Exception("Large melee approach overlaps bodies");
                yield return null;
            }
            if(ActiveActions>0||ResolvedHits!=1)throw new Exception("Large melee couldn't reach contact");
            midpoint=(Player.body.Centre+Target.body.Centre)*.5f;cam.transform.position=midpoint+new Vector3(0,4,-11);cam.transform.LookAt(midpoint);CaptureEvidence(Path.Combine(folder,"large-body-spacing.png"));
            report.Add("Giratina/Latias: separate at spawn and throughout actual melee approach; one confirmed hit");
            if(lifecycle)
            {
                var disposed=new AttackPresentation(Player.body,Target.body,catalog.Move("HYDRO_PUMP"),zaFx,bdspFx,nativeTiming);
                Destroy(disposed.Effect.gameObject);yield return null;
                if(disposed.Alive)throw new Exception("Externally destroyed effect still alive");disposed.Dispose();
                report.Add("Externally destroyed effect can be queried/disposed safely through interface; shutdown log checked separately");
            }
            if(smokeErrors>0)throw new Exception(lastSmokeError);
            File.WriteAllLines(Path.Combine(folder,"focused-checks.txt"),report);Debug.Log("LAB EFFECT AUDIT PASSED");Application.Quit(0);
        }
    }
}
