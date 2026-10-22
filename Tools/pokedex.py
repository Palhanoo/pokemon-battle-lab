"""Build one locally sourced model per species, newest usable source first.
National IDs come from resource/personal tables, NEVER from pmNNNN model IDs.
Run: pokemon/tools/venv/bin/python Tools/pokedex.py [--export] [--workers 3]
"""
from pathlib import Path
import argparse, collections, concurrent.futures, csv, hashlib, json, os, shutil, struct, sys, traceback
ROOT=Path(__file__).resolve().parents[1]
sys.path[:0]=[str(ROOT/'Tools/vendor'),str(ROOT/'Tools/vendor/arceusx')]
from fb import T
from sources import open_violet, VioletDir, ARCEUS_POKEMON
from hoenngen.za_files import open_assets
from convert import Files, convert, clip_name
from hoenngen.species import pick_clips
PRIORITY=['ZA','SV','Arceus','BDSP','Sword']
MAIN=ROOT.parent/'HoennVR'
CSV=Path('/home/bruno/pokemon/refs/pokeapi-81d20919')
OUT=ROOT/'LocalRoster'
def rows(name):return list(csv.DictReader((CSV/(name+'.csv')).open()))
def inventory():
    found=collections.defaultdict(list)
    def add(dex,source,path,form=0,gender=0):
        if int(dex)>0:found[int(dex)].append(dict(source=source,path=path,form=int(form),gender=int(gender)))
    za=open_assets(); actual=set(za.find('ik_pokemon/data/','.trmdl'))
    forms={(r['internal'],r['form']):r['dex'] for r in json.loads((MAIN/'Data/za_source/forms.json').read_text())}
    for model, refs in json.loads((MAIN/'Data/za_source/model_map.json').read_text()).items():
        path=f'ik_pokemon/data/{model[:6]}/{model}/{model}.trmdl'
        if path in actual:
            for internal,form,name in refs:
                if (internal,form) in forms:add(forms[internal,form],'ZA',path,form)
    sv=open_violet();actual=set(sv.find('pokemon/data/','.trmdl'))
    personal={r.struct(0,'HHH')[:2]:r.struct(0,'HHH')[2] for r in T(sv.get('avalon/data/personal_array.bin')).tables(0)}
    for r in T(sv.get('pokemon/catalog/catalog/poke_resource_table.trpmcatalog')).tables(1):
        s=r.table(0);key=(s.scalar(0,'H'),s.scalar(1,'H'));path='pokemon/data/'+r.string(1)
        if path in actual and key in personal:add(personal[key],'SV',path,key[1],s.scalar(2,'B'))
    la=Path(ARCEUS_POKEMON).parents[1]/'pokemon/table/poke_resource_table.trpmcatalog'
    for r in T(la.read_bytes()).tables(1):
        s=r.table(0);model=Path(r.string(1)).stem;path=Path(ARCEUS_POKEMON)/(model+'.gfpak')
        if path.exists():add(s.scalar(0,'H'),'Arceus',str(path),s.scalar(1,'H'),s.scalar(2,'B'))
    from hoenngen.bdsp import model_base
    for dex in range(1,494):
        try:add(dex,'BDSP',model_base(dex))
        except FileNotFoundError:pass
    from hoenngen.sword import pack_path
    b=Path('/mnt/d/pokemon/sword-work/romfs/bin/pml/personal/personal_total.bin').read_bytes()
    for dex in range(1,min(899,len(b)//0xA8)):
        model=struct.unpack_from('<H',b,dex*0xA8+0x4c)[0];path=pack_path(model)
        if model:
            options=sorted(Path(path).parent.glob(f'pm{model:04d}_*.gfpak'))
            if options:add(dex,'Sword',str(Path(path) if Path(path).exists() else options[0]))
    names={int(r['id']):r['identifier'].upper().replace('-','_') for r in rows('pokemon_species')}
    result=[]
    for dex,candidates in sorted(found.items()):
        if dex not in names:continue
        unique={json.dumps(c,sort_keys=True):c for c in candidates}
        ordered=sorted(unique.values(),key=lambda c:(PRIORITY.index(c['source']),c['form']!=0,c['form'],c['gender'],c['path']))
        result.append(dict(dex=dex,species=names[dex],candidates=ordered))
    report=dict(preference=PRIORITY,species=result,missing=[dict(dex=d,species=n) for d,n in names.items() if d not in found])
    (ROOT/'Docs/pokedex-inventory.json').write_text(json.dumps(report,indent=2)+'\n')
    print('Inventory',len(result),'species; newest candidates',dict(collections.Counter(r['candidates'][0]['source'] for r in result)), 'missing',report['missing'],flush=True)
    return result

def glb_info(path):
    b=path.read_bytes();size=struct.unpack_from('<I',b,12)[0];data=json.loads(b[20:20+size]);clips=[a['name'] for a in data.get('animations',[])]
    if not data.get('meshes') or not clips:raise ValueError('No meshes or animations')
    return clips,hashlib.sha256(b).hexdigest()

def export_one(row):
    dest=OUT/(row['species']+'.glb');marker=OUT/(row['species']+'.json');errors=[]
    if marker.exists() and dest.exists():
        old=json.loads(marker.read_text())
        if old.get('pipeline')==2 and old['candidate']==row['candidates'][0]:return old
    for c in row['candidates']:
        try:
            source=c['source'];path=c['path'];roles=None
            # Reuse exact existing exports only for unchanged older source converters.
            existing=json.loads((MAIN/'Data/species.json').read_text()).get(row['species'],{})
            old_source={'violet':'SV','za':'ZA','arceus':'Arceus','bdsp':'BDSP','sword':'Sword'}.get(existing.get('source','').split(':')[0])
            old_path=MAIN/'Assets/Generated/Models/pokemon'/(row['species']+'.glb')
            if source not in ('ZA','SV') and old_source==source and c['form']==0 and old_path.exists():
                shutil.copy2(old_path,dest);roles=json.loads(old_path.with_suffix('.clips.json').read_text())
            elif source=='BDSP':
                from hoenngen.bdsp import convert_species
                roles=convert_species(row['dex'],str(dest))
            elif source=='Sword':
                from hoenngen.sword import convert_pack
                roles=pick_clips(convert_pack(path,Path(path).name.replace('.gfpak','.gfbmdl'),str(dest)))
            else:
                files=Files([path]) if source=='Arceus' else VioletDir(open_assets() if source=='ZA' else open_violet(),str(Path(path).parent))
                model=Path(path).stem
                convert(files,[model+'.trmdl'],str(dest),'all',guess_cutouts=False)
            clips,sha=glb_info(dest);roles=roles or pick_clips(clips)
            aliases={}
            # Some species have one native attack animation for both categories.
            for role,other in [('attack','special'),('special','attack'),('battle','idle'),('run','walk'),('walk','idle'),('faint','damage')]:
                if not roles.get(role) and roles.get(other):roles[role]=roles[other];aliases[role]=other
            if not roles.get('run') and roles.get('idle'):roles['run']=roles['idle'];aliases['run']='idle'
            for role in ('idle','battle','attack','special','damage','faint'):
                if not roles.get(role) or roles[role] not in clips:raise ValueError('Missing required animation role '+role)
            result=dict(pipeline=2,dex=row['dex'],species=row['species'],source=source,candidate=c,file=dest.name,sha256=sha,bytes=dest.stat().st_size,clipCount=len(clips),roles=[dict(role=k,clip=v) for k,v in roles.items() if v],roleAliases=aliases,fallbacks=errors)
            marker.write_text(json.dumps(result,indent=2)+'\n');return result
        except Exception as exc:errors.append(dict(candidate=c,reason=str(exc)));print('Fallback',row['species'],source,str(exc)[:180],flush=True)
    return dict(dex=row['dex'],species=row['species'],errors=errors)

def build_data(results):
    seed=json.loads((ROOT/'Tools/seed-data.json').read_text());moves={m['id']:m for m in seed['moves']}
    ids={int(r['species_id']):int(r['id']) for r in rows('pokemon') if r['is_default']=='1'}
    types={int(r['id']):r['identifier'].upper() for r in rows('types')};stat=collections.defaultdict(dict);tp=collections.defaultdict(list)
    for r in rows('pokemon_stats'):stat[int(r['pokemon_id'])][int(r['stat_id'])]=int(r['base_stat'])
    for r in rows('pokemon_types'):tp[int(r['pokemon_id'])].append((int(r['slot']),types[int(r['type_id'])]))
    move_ids={int(r['id']):r['identifier'].upper().replace('-','_') for r in rows('moves') if r['identifier'].upper().replace('-','_') in moves}
    learned=collections.defaultdict(set)
    with (CSV/'pokemon_moves.csv').open() as f:
        for r in csv.DictReader(f):
            m=move_ids.get(int(r['move_id']))
            if m:learned[int(r['pokemon_id'])].add(m)
    original={s['id']:s for s in seed['species']};ready=[]
    for r in results:
        if 'errors' in r:continue
        p=ids[r['dex']];stats=[stat[p][i] for i in (1,2,3,6,4,5)];types_=[v for k,v in sorted(tp[p])]
        legal=sorted(learned[p],key=lambda m:(moves[m]['type'] not in types_,moves[m]['shape']!='Melee',moves[m]['power'],m))
        choices=list(original.get(r['species'],{}).get('moves',[])) or legal[:6]
        supplements=[]
        for m in sorted(moves,key=lambda m:(moves[m]['type'] not in types_,moves[m]['power'],m)):
            if len(choices)>=6:break
            if m not in choices:choices.append(m);supplements.append(m)
        # Form stats/types must match the selected body, not silently use base stats.
        form=r['candidate']['form'];note='Base species stats; pooled learnsets across games.'
        if form:note+=' Selected source form '+str(form)+'; form-specific stats not yet mapped.'
        ready.append({**r,'data':dict(id=r['species'],stats=stats,types=types_,moves=choices),'testOnlyMoves':[m for m in choices if m not in learned[p]],'dataNote':note})
    doc=dict(preference=PRIORITY,models=ready,failed=[r for r in results if 'errors' in r])
    (OUT/'catalog.json').write_text(json.dumps(doc,separators=(',',':'))+'\n')
    (ROOT/'Docs/pokedex-ready.json').write_text(json.dumps(doc,indent=2)+'\n')
    print('READY',len(ready),'FAILED',len(doc['failed']),dict(collections.Counter(r['source'] for r in ready)),flush=True)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--export',action='store_true');parser.add_argument('--workers',type=int,default=3);parser.add_argument('--limit',type=int);args=parser.parse_args()
    records=inventory()
    if args.export:
        OUT.mkdir(parents=True,exist_ok=True)
        if args.limit:records=records[:args.limit]
        results=[]
        with concurrent.futures.ProcessPoolExecutor(max_workers=args.workers) as pool:
            for i,r in enumerate(pool.map(export_one,records)):
                results.append(r);print('PROGRESS',i+1,'/',len(records),r['species'],r.get('source','FAILED'),flush=True)
        from normalize_roster import main as normalize_all
        normalize_all()
        from rebake_materials import main as rebake_all
        rebake_all()
        results=[json.loads((OUT/(r['species']+'.json')).read_text()) if 'errors' not in r else r for r in results]
        build_data(results)
if __name__=='__main__':main()
