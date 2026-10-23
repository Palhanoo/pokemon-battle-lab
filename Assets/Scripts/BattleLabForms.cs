using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace BattleLab
{
    [Serializable] public class MegaFormData
    {
        public string id,species,label,file,source;public int[] stats;public string[] types;public ClipRole[] roles;
    }
    [Serializable] public class MegaFormDocument {public MegaFormData[] forms;}
    public partial class BattleLabApp
    {
        MegaFormData[] megaForms=Array.Empty<MegaFormData>();
        readonly Dictionary<string,ModelEntry> megaModels=new();
        readonly Dictionary<string,int> selectedMega=new();
        void LoadForms()
        {
            string path=Path.Combine(Application.streamingAssetsPath,"Forms/catalog.json");
            if(pokedex==null||!File.Exists(path))return;
            megaForms=JsonUtility.FromJson<MegaFormDocument>(File.ReadAllText(path)).forms;
            foreach(var form in megaForms)
            {
                var model=new ModelEntry{species=form.id,source="ZA",roles=form.roles,clips=Array.Empty<AnimationClip>()};megaModels.Add(form.id,model);
                pokedex.RegisterForm(new RosterRow{species=form.id,source="ZA",file="../Forms/"+form.file,roles=form.roles});
            }
        }
        MegaFormData SelectedForm(string species)
        {
            var forms=megaForms.Where(f=>f.species==species).ToArray();if(forms.Length==0)return null;
            selectedMega.TryGetValue(species,out int index);return forms[index%forms.Length];
        }
        IEnumerable<ModelEntry> RequiredForms()
        {
            if(inspect)yield break;
            var names=new List<string>{Species[playerSpecies],Species[enemySpecies]};
            if(mode==BattleMode.ZA)names.AddRange(allySpecies.Take(Allies-1).Concat(foeSpecies.Take(Enemies-1)).Select(i=>Species[i]));
            foreach(var name in names.Distinct()){var form=SelectedForm(name);if(form!=null)yield return megaModels[form.id];}
        }
        void FormSetupUI()
        {
            if(loading||Player==null)return;
            var form=SelectedForm(Player.data.id);
            if(form==null)return;
            if(Button(new Rect(698,185,340,34),"Mega form: "+form.label+" ›"))
            {selectedMega.TryGetValue(Player.data.id,out int index);selectedMega[Player.data.id]=index+1;ResetBattle();}
        }
        void ClearFormAttachments(Fighter f)
        {
            foreach(var other in fighters)if(other==f||other.action?.target==f)Cancel(other);
            foreach(var tail in tails)tail.Dispose();tails.Clear();
            if(f.voice)f.voice.Stop();
        }
        void SwapBody(Fighter f,ModelEntry model)
        {
            var old=f.body;var at=old.transform.position;var rotation=old.transform.rotation;
            f.body=LabActor.Spawn(model,at);f.body.transform.rotation=rotation;
            f.voice=f.body.gameObject.AddComponent<AudioSource>();f.voice.spatialBlend=.25f;f.voice.maxDistance=28;f.voice.volume=soundVolume;
            Destroy(old.gameObject);
            SeparateTeamSpawn();
        }
        void ToggleMega()
        {
            var f=Player;if(!Direct||f==null||f.hp<=0||loading||f.pendingMega!=null)return;
            if(f.megaId!=null){RevertForm(f);return;}
            var form=SelectedForm(f.data.id);
            if(form==null){Note("No staged Mega form for "+f.data.id);return;}
            if(f.megaUsed){Note("Mega already used · R resets the test");return;}
            if(!megaModels[form.id].prefab){Note("Choose this form in Setup to load it");return;}
            foreach(var ally in fighters.Where(v=>v.team==f.team&&v.megaId!=null).ToArray())RevertForm(ally);
            ClearFormAttachments(f);
            f.pendingMega=form;f.megaUsed=true;f.megaSwapAt=now+1.2f;
            var charge=f.body.entry.Named("charge01_loop")??f.body.entry.Role("special");f.body.Play(charge,true);
            f.megaFx=zaFx.Play("MEGA_EVOLUTION",AttackPresentation.Bind(f.body),AttackPresentation.Bind(f.body));
            if(f.megaFx){f.megaFx.LabMove="MEGA_EVOLUTION";f.megaFx.ManualSimulate=true;f.megaFx.Step(0);}
            f.state="Mega charge";Note("Mega Evolution · "+f.data.id);
        }
        void CompleteMega(Fighter f)
        {
            var form=f.pendingMega;float fraction=(float)f.hp/f.MaxHp;
            // Incoming effects cannot retain references to the body being replaced.
            ClearFormAttachments(f);
            f.data=new SpeciesData{id=f.baseData.id,stats=form.stats,types=form.types,moves=f.baseData.moves};
            f.megaId=form.id;f.pendingMega=null;f.formEnds=now+30;
            f.hp=Mathf.Max(1,Mathf.RoundToInt(fraction*f.MaxHp));SwapBody(f,megaModels[form.id]);
            if(f.megaFx){f.megaFx.attacker=AttackPresentation.Bind(f.body);f.megaFx.defender=AttackPresentation.Bind(f.body);}
            if(f.body.entry.mega)f.body.Play(f.body.entry.mega);else if(f.body.entry.roar)f.body.Play(f.body.entry.roar);
            var cry=soundCatalog.CryClip(f.data.id);if(cry)f.voice.PlayOneShot(cry,.5f);
            f.state="Mega Evolved";
        }
        void RevertForm(Fighter f)
        {
            if(f.pendingMega!=null){f.pendingMega=null;if(f.megaFx)Destroy(f.megaFx.gameObject);f.megaFx=null;f.body.Idle();f.state="Ready";}
            if(f.megaId==null)return;
            ClearFormAttachments(f);bool faint=f.hp<=0;float fraction=(float)f.hp/f.MaxHp;
            f.megaId=null;f.data=f.baseData;f.hp=faint?0:Mathf.Clamp(Mathf.RoundToInt(fraction*f.MaxHp),1,f.MaxHp);
            SwapBody(f,f.baseModel);if(f.megaFx)Destroy(f.megaFx.gameObject);f.megaFx=null;
            f.body.Idle();if(faint)f.body.Role("faint");
            f.state=faint?"Fainted":"Ready";Note(f.data.id+" returned to normal");
        }
        void TickForms(float dt)
        {
            foreach(var f in fighters)
            {
                if(f.pendingMega!=null)
                {
                    if(f.hp<=0){RevertForm(f);continue;}
                    if(now>=f.megaSwapAt)CompleteMega(f);
                }
                if(f.megaFx)
                {
                    f.megaFx.Step(dt);
                    foreach(var renderer in f.megaFx.GetComponentsInChildren<Renderer>(true))
                        foreach(var material in renderer.sharedMaterials)
                            if(material&&material.HasProperty("_EffectTime"))material.SetFloat("_EffectTime",f.megaFx.Now);
                    if(f.megaFx.Now>=4){Destroy(f.megaFx.gameObject);f.megaFx=null;}
                }
                if(f.megaId!=null&&(f.hp<=0||now>=f.formEnds)&&f.action==null)RevertForm(f);
                if(f.state=="Mega Evolved"&&f.action==null&&f.body.ClipTime>=f.body.ClipLength-.05f){f.body.Idle();f.state="Ready";}
            }
        }
    }
}
