using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
namespace BattleLab
{
    [Serializable] public class RosterRow
    {
        public int dex,clipCount;public string species,source,file,sha256,dataNote;public ClipRole[] roles;public SpeciesData data;public string[] testOnlyMoves;
    }
    [Serializable] public class RosterDocument {public RosterRow[] models;}
    // Owns imports for the current arena only. Catalog metadata never retains the whole Pokédex's meshes.
    public sealed class PokedexRoster : IDisposable
    {
        public readonly RosterRow[] rows;
        readonly string folder;readonly Dictionary<string,RosterRow> bySpecies;
        public RosterRow Row(string species)=>bySpecies.TryGetValue(species,out var row)?row:null;
        readonly Dictionary<string,(GltfImport import,ModelEntry model)> loaded=new();
        bool disposed;
        public int LoadedCount=>loaded.Count;
        public PokedexRoster(string folder,LabCatalog catalog)
        {
            this.folder=folder;rows=JsonUtility.FromJson<RosterDocument>(File.ReadAllText(Path.Combine(folder,"catalog.json"))).models;
            if(rows==null||rows.Length==0||rows.Select(r=>r.species).Distinct().Count()!=rows.Length)throw new InvalidDataException("Roster must contain exactly one model per species");
            bySpecies=rows.ToDictionary(r=>r.species);
            var order=catalog.data.species.Select(s=>s.id).ToList();order.AddRange(rows.OrderBy(r=>r.dex).Select(r=>r.species).Where(s=>!order.Contains(s)));
            catalog.data=new LabData{moves=catalog.data.moves,types=catalog.data.types,species=order.Where(s=>rows.Any(r=>r.species==s)).Select(s=>rows.First(r=>r.species==s).data).ToArray()};
            catalog.models=rows.Select(r=>new ModelEntry{species=r.species,source=r.source,roles=r.roles,clips=Array.Empty<AnimationClip>()}).ToArray();
        }
        public async Task Prepare(IEnumerable<ModelEntry> models)
        {
            var needed=models.Distinct().ToArray();var ids=needed.Select(m=>m.species).ToHashSet();
            foreach(var key in loaded.Keys.Where(k=>!ids.Contains(k)).ToArray())Unload(key);
            foreach(var model in needed)
            {
                if(model.prefab)continue;
                var row=Row(model.species);var importer=new GltfImport(deferAgent:Environment.GetCommandLineArgs().Contains("-lab-fast-load")?new UninterruptedDeferAgent():null);GameObject root=null;
                try
                {
                    var settings=new ImportSettings{AnimationMethod=AnimationMethod.Legacy,NodeNameMethod=NameImportMethod.Original};
                    if(!await importer.LoadFile(Path.Combine(folder,row.file),importSettings:settings))throw new InvalidDataException("Cannot load "+row.species);
                    if(disposed)throw new OperationCanceledException();
                    // Runtime-created transform curves require legacy clips. LabActor samples
                    // their Animation states manually on the same battle clock as editor clips.
                    model.clips=importer.GetAnimationClips();
                    root=new GameObject("Cached "+row.species);root.SetActive(false);
                    if(!await importer.InstantiateMainSceneAsync(new GameObjectInstantiator(importer,root.transform,settings:new InstantiationSettings{SceneObjectCreation=SceneObjectCreation.Never})))throw new InvalidDataException("Cannot instantiate "+row.species);
                    if(disposed)throw new OperationCanceledException();
                    foreach(var animation in root.GetComponentsInChildren<Animation>(true)){animation.playAutomatically=false;animation.enabled=false;animation.clip=null;}
                    model.clips=importer.GetAnimationClips();model.prefab=root;
                    foreach(var role in new[]{"battle","attack","special","damage","faint"})if(!model.Role(role))throw new InvalidDataException(row.species+" missing "+role);
                    if(root.GetComponentsInChildren<Renderer>(true).Length==0)throw new InvalidDataException(row.species+" has no renderers");
                    loaded.Add(row.species,(importer,model));
                }
                catch{if(root)UnityEngine.Object.Destroy(root);model.prefab=null;model.clips=Array.Empty<AnimationClip>();importer.Dispose();throw;}
            }
        }
        void Unload(string id)
        {
            var entry=loaded[id];if(entry.model.prefab)UnityEngine.Object.Destroy(entry.model.prefab);
            entry.model.prefab=null;entry.model.clips=Array.Empty<AnimationClip>();entry.import.Dispose();loaded.Remove(id);
        }
        public void Dispose(){disposed=true;foreach(var id in loaded.Keys.ToArray())Unload(id);}
    }
}
