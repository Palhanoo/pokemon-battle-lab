"""Stage available native damage presentations without replacing reviewed moves.
Keeps the lab's direct-damage baseline; unsupported status/fixed-damage mechanics
and missing presentations stay in the coverage report, never fake generic moves.
Unity must be closed. Uses the existing local source exports and pinned move data.
"""
from pathlib import Path
import collections,csv,json,shutil,sys,re
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT.parent/'HoennVR'
REF=Path('/home/bruno/pokemon/refs/pokeapi-81d20919')
def rows(name):return list(csv.DictReader((REF/(name+'.csv')).open()))
def write(p,d):p.write_text(json.dumps(d,indent=2)+'\n')
def stage():
 seed=json.loads((ROOT/'Tools/seed-data.json').read_text());old={m['id'] for m in seed['moves']}
 timing=json.loads((SOURCE/'Data/za_moves.json').read_text());source=SOURCE/'Assets/Generated/MoveFx';index=json.loads((source/'index.json').read_text())
 extra=ROOT/'LocalMoveExports'
 if (extra/'index.json').exists():
  for key,value in json.loads((extra/'index.json').read_text())['moves'].items():index['moves'].setdefault(key,value)
 # Pooled references use modern names; older assets retain e.g. HI_JUMP_KICK.
 for row in rows('moves'):
  canonical=row['identifier'].upper().replace('-','_')
  aliases=[key[5:] for key,folder in index['moves'].items() if re.fullmatch(r'(?:za)?ew0*'+row['id'],folder)]
  if canonical not in timing:
   alias=next((a for a in aliases if a in timing),None)
   if alias:timing[canonical]=timing[alias]
  if 'MOVE_'+canonical not in index['moves'] and aliases:index['moves']['MOVE_'+canonical]=index['moves']['MOVE_'+aliases[0]]
 out=ROOT/'Assets/NativeFx/ZA';dest=json.loads((out/'index.json').read_text());types={r['id']:r['identifier'].upper() for r in rows('types')}
 native_rows=json.loads((ROOT/'Assets/NativeFx/za-timing.json').read_text());have_timing={r['id'] for r in native_rows['rows']}
 meta={r['move_id']:r for r in rows('move_meta')};added=[];coverage=[];folders=set();textures=set()
 sys.path.insert(0,str(SOURCE/'Tools~'))
 from hoenngen import movefx
 receipt=extra/'enriched.json';enriched=set(json.loads(receipt.read_text())) if receipt.exists() else set()
 def scan(x):
  if isinstance(x,str):textures.add(x)
  elif isinstance(x,list):
   for v in x:scan(v)
  elif isinstance(x,dict):
   for v in x.values():scan(v)
 for r in rows('moves'):
  name=r['identifier'].upper().replace('-','_');folder=index['moves'].get('MOVE_'+name)
  entry=dict(id=name,sourceId=int(r['id']),presentation=folder)
  existing=next((m for m in seed['moves'] if m['id']==name),None)
  if existing and (existing.get('effectOrigin') not in ('ZA','SV') or ('MOVE_'+name in dest['moves'] and (out/folder/'move.json').exists())):
   entry['state']='playable';coverage.append(entry)
   if existing.get('effectOrigin') in ('ZA','SV'):
    if folder not in enriched:folders.add(folder)
    if name not in have_timing:
     t=timing[name];native_rows['rows'].append(dict(id=name,move={**t,'shape':['Melee','Projectile','Beam','Arc','Area','Self'].index(existing['shape'])}))
   continue
  if r['damage_class_id']=='1' or int(r['power'] or 0)<=0:
   entry['state']='needs-special-mechanics';coverage.append(entry);continue
  if not folder or name not in timing:
   entry['state']='missing-native-presentation-or-timing';coverage.append(entry);continue
  path=out/folder;path.mkdir(parents=True,exist_ok=True)
  for p in ((source if (source/folder).exists() else extra)/folder).glob('*.json'):shutil.copy2(p,path/p.name);scan(json.loads(p.read_text()))
  # SV's older export omitted original paths. Recover them from its scene template
  # so the same native combiner/refraction enrichment can be applied.
  if not folder.startswith('za'):
   get,names=movefx.violet_files(int(r['id']));tag=folder
   variant='' if tag+'.trtml' in names else next((n[len(tag):-6] for n in sorted(names) if n.startswith(tag+'_') and n.endswith('.trtml')),'')
   sot=get(tag+variant+'_0.trsot');objects=movefx.scene_objects(sot) if sot else {}
   d=json.loads((path/'move.json').read_text())
   for o in d['objects']:
    if o['name'] not in objects:raise RuntimeError('Missing source path '+name+'/'+o['name'])
    o['ptcl']=objects[o['name']]
   write(path/'move.json',d)
  t=dict(timing[name]);m=meta.get(r['id'],{})
  native_move=json.loads((path/'move.json').read_text())
  if any(c['cmd']=='GameObject/SetLengthScale' for o in native_move['objects'] for c in o['clips']):t['shape']='Beam'
  move=dict(id=name,name=r['identifier'].replace('-',' ').title(),type=types[r['type_id']],physical=r['damage_class_id']=='2',power=int(r['power']),accuracy=int(r['accuracy'] or 0),pp=int(r['pp'] or 5),priority=int(r['priority']),zaPower=max(1,int(t.get('power') or r['power'])),shape=t['shape'],source=t['source'],effectOrigin='ZA' if folder.startswith('za') else 'SV',labNotes='Direct damage baseline; secondary effects and special move rules are not simulated.')
  for k in ['cooldown','windup','recovery','rangeMin','rangeMax','radius','speed']:move[k]=t.get(k,0)
  # Multi-hit moves retain their summed per-cast power in this lab. Native visual
  # contacts are still played, with a single damage resolution (explicit in UI).
  if m.get('min_hits'):
   move['labNotes']='Multi-hit animation; one combined damage event. Secondary effects are not simulated.'
   move['hitCount']=int(m['min_hits']) if m['min_hits']==m['max_hits'] else round((int(m['min_hits'])+int(m['max_hits']))/2)
  if existing:existing.update(move)
  else:seed['moves'].append(move);added.append(name)
  folders.add(folder);dest['moves']['MOVE_'+name]=folder
  if name not in have_timing:native_rows['rows'].append(dict(id=name,move={**t,'shape':['Melee','Projectile','Beam','Arc','Area','Self'].index(t['shape'])}))
  entry['state']='playable';entry['origin']=move['effectOrigin'];coverage.append(entry)
 for p in list((source/'textures').glob('*.png'))+list((extra/'textures').glob('*.png')):
  if p.stem in textures and not (out/'textures'/p.name).exists():shutil.copy2(p,out/'textures'/p.name)
 write(out/'index.json',dest);write(ROOT/'Assets/NativeFx/za-timing.json',native_rows);write(ROOT/'Tools/seed-data.json',seed)
 from enrich_effect_data import enrich
 enrich(SOURCE,folders)
 extra.mkdir(exist_ok=True);write(receipt,sorted(enriched|folders))
 # All known legal moves in the available catalog, preserving existing slot order.
 available={m['id'] for m in seed['moves']};moveids={r['id']:r['identifier'].upper().replace('-','_') for r in rows('moves')};species={r['id']:int(r['species_id']) for r in rows('pokemon') if r['is_default']=='1'}
 legal=collections.defaultdict(set)
 for r in csv.DictReader((REF/'pokemon_moves.csv').open()):
  name=moveids.get(r['move_id']);dex=species.get(r['pokemon_id'])
  if dex and name in available:legal[dex].add(name)
 roster=json.loads((ROOT/'LocalRoster/catalog.json').read_text())
 for r in roster['models']:
  pool=r['data']['moves'];pool.extend(sorted(legal[r['dex']]-set(pool)))
  r['testOnlyMoves']=[m for m in pool if m not in legal[r['dex']]]
 write(ROOT/'LocalRoster/catalog.json',roster);write(ROOT/'Docs/pokedex-ready.json',roster)
 # Seed-only drills also get the expanded legal pool.
 dex_by_name={r['species']:r['dex'] for r in roster['models']}
 for s in seed['species']:s['moves'].extend(sorted(legal[dex_by_name[s['id']]]-set(s['moves'])))
 write(ROOT/'Tools/seed-data.json',seed)
 # Keep real existing Emerald recordings where available; documented same-type
 # substitutions cover newer moves. No repeated looping samples are introduced.
 audio=json.loads((ROOT/'Tools/audio-index.json').read_text());sounds={m['id']:m['sound'] for m in audio['moves']};bytype={m['type']:sounds[m['id']] for m in seed['moves'] if m['id'] in sounds}
 for m in seed['moves']:
  if m['id'] in sounds:continue
  native='SE_M_'+m['id'];p=SOURCE/'Assets/Generated/Audio/sfx'/(native+'.wav');sound=native if p.exists() else bytype.get(m['type'],sounds['TACKLE'])
  if sound=='SE_M_GUST':sound='SE_M_GUST_SHORT'
  audio['moves'].append(dict(id=m['id'],sound=sound,substitute=not p.exists(),note='Emerald recording' if p.exists() else 'Same-type substitute for lab testing'))
 write(ROOT/'Tools/audio-index.json',audio)
 write(ROOT/'Docs/attack-coverage.json',dict(playable=len(seed['moves']),added=added,counts=collections.Counter(r['state'] for r in coverage),scope='Native damage presentations using the lab direct-damage baseline. Visual review tracked separately. Status, fixed-damage and absent effects remain gaps.',moves=coverage))
 import runpy;runpy.run_path(str(ROOT/'Tools/repair_expanded_wave.py'))
 print('EXPANDED',len(added),'TOTAL',len(seed['moves']),'GAPS',dict(collections.Counter(r['state'] for r in coverage)),flush=True)
if __name__=='__main__':stage()
