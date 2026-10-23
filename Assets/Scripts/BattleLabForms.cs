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
            if(teamSize==3&&mode==BattleMode.ZA)names.AddRange(allySpecies.Concat(foeSpecies).Select(i=>Species[i]));
            foreach(var name in names.Distinct()){var form=SelectedForm(name);if(form!=null)yield return megaModels[form.id];}
        }
        void FormSetupUI()
        {
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
            var f=Player;if(!Direct||f==null||f.hp<=0||loading)return;
            if(f.megaId!=null){RevertForm(f);return;}
            var form=SelectedForm(f.data.id);
            if(form==null){Note("No staged Mega form for "+f.data.id);return;}
            if(f.dynamax||f.megaUsed){Note("Mega already used, or Dynamax is active · R resets the test");return;}
            var model=megaModels[form.id];if(!model.prefab){Note("Choose this form in Setup to load it");return;}
            foreach(var ally in fighters.Where(v=>v.team==f.team&&v.megaId!=null).ToArray())RevertForm(ally);
            ClearFormAttachments(f);float fraction=(float)f.hp/f.MaxHp;
            f.data=new SpeciesData{id=f.baseData.id,stats=form.stats,types=form.types,moves=f.baseData.moves};f.megaId=form.id;f.megaUsed=true;f.formEnds=now+30;
            f.hp=Mathf.Max(1,Mathf.RoundToInt(fraction*f.MaxHp));SwapBody(f,model);
            if(f.body.entry.mega)f.body.Play(f.body.entry.mega);else if(f.body.entry.roar)f.body.Play(f.body.entry.roar);
            var cry=soundCatalog.CryClip(f.data.id);if(cry)f.voice.PlayOneShot(cry,.5f);SpawnBurst(f.body.Centre,.7f,.45f);
            f.state="Mega Evolved";Note("Mega "+f.data.id+" · native form stats/types · 30-second lab timer");
        }
        void ToggleDynamax()
        {
            var f=Player;if(!Direct||f==null||f.hp<=0||loading)return;
            if(f.dynamax){RevertForm(f);return;}
            if(f.megaId!=null||f.maxUsed){Note("Dynamax already used, or Mega is active · R resets the test");return;}
            foreach(var ally in fighters.Where(v=>v.team==f.team&&v.dynamax).ToArray())RevertForm(ally);
            ClearFormAttachments(f);f.altitude=0;var at=f.body.transform.position;at.y=0;f.body.transform.position=at;
            f.baseScale=f.body.transform.localScale;f.dynamax=true;f.maxUsed=true;f.formEnds=now+20;f.maxAttacks=3;f.hp*=2;
            // Bound enlargement to the arena; all body hitboxes and footprints scale with it.
            float scale=Mathf.Clamp(8/Mathf.Max(.2f,f.body.BodyHeight),1.25f,2.5f);f.body.transform.localScale=f.baseScale*scale;SeparateTeamSpawn();
            CreateFormHalo(f,new Color(1,.22f,.5f));SpawnBurst(f.body.Centre,.9f,.5f);
            f.state="Dynamax";Note("Dynamax prototype · 2× HP · 20 seconds / 3 attacks · original moves retained");
        }
        void RevertForm(Fighter f)
        {
            if(!f.dynamax&&f.megaId==null)return;
            ClearFormAttachments(f);bool faint=f.hp<=0;float fraction=(float)f.hp/f.MaxHp;bool wasMega=f.megaId!=null;
            f.dynamax=false;f.megaId=null;f.data=f.baseData;f.hp=faint?0:Mathf.Clamp(Mathf.RoundToInt(fraction*f.MaxHp),1,f.MaxHp);
            if(wasMega)SwapBody(f,f.baseModel);else f.body.transform.localScale=f.baseScale;
            if(f.formHalo)Destroy(f.formHalo);f.formHalo=null;f.body.Idle();if(faint)f.body.Role("faint");
            f.state=faint?"Fainted":"Ready";Note(f.data.id+" returned to normal");
        }
        void CreateFormHalo(Fighter f,Color color)
        {
            f.formHalo=new GameObject("Transformation aura ring");var line=f.formHalo.AddComponent<LineRenderer>();line.sharedMaterial=Material(color);line.useWorldSpace=false;line.loop=true;line.positionCount=64;line.widthMultiplier=.07f;
            UpdateFormHalo(f);
        }
        void UpdateFormHalo(Fighter f)
        {
            var ring=f.formHalo.GetComponent<LineRenderer>();var bounds=f.body.Bounds;
            f.formHalo.transform.position=new Vector3(bounds.center.x,bounds.max.y+.4f,bounds.center.z);
            f.formHalo.transform.rotation=Quaternion.Euler(0,now*45,0);
            float radius=Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z)*.7f,.45f,3);
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a*3+now*3)*.08f,Mathf.Sin(a)*radius));}
        }
        void TickForms(float dt)
        {
            foreach(var f in fighters)
            {
                if((f.dynamax||f.megaId!=null)&&(f.hp<=0||now>=f.formEnds||f.dynamax&&f.maxAttacks<=0)&&f.action==null)RevertForm(f);
                if(f.formHalo)UpdateFormHalo(f);
                if(f.state=="Mega Evolved"&&f.action==null&&f.body.ClipTime>=f.body.ClipLength-.05f){f.body.Idle();f.state="Ready";}
            }
        }
    }
}
