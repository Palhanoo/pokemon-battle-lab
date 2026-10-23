"""Stage exact native BDSP legacy moves, never borrowed substitute animations.
Only publish a catalog entry after every particle prefab and model layer exports.
"""
from pathlib import Path
import csv,hashlib,json,sys,traceback
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Tools/vendor'))
import bdspfx
from export_bdsp_effect_models import export_models
REF=Path('/home/bruno/pokemon/refs/pokeapi-81d20919')
def write(p,d):p.write_text(json.dumps(d,indent=2)+'\n')
out=ROOT/'Assets/NativeFx/BDSP';index=json.loads((out/'index.json').read_text());store=bdspfx.TextureStore(out/'textures')
for p in (out/'textures').glob('*.png'):
 pixels=np.asarray(Image.open(p));store.hashes[p.stem]=hashlib.sha1(pixels.tobytes()+str(pixels.shape).encode()).hexdigest()[:10]
seed=json.loads((ROOT/'Tools/seed-data.json').read_text());existing={m['id'] for m in seed['moves']};meta={r['identifier'].upper().replace('-','_'):r for r in csv.DictReader((REF/'moves.csv').open())};types={r['id']:r['identifier'].upper() for r in csv.DictReader((REF/'types.csv').open())}
multi={r['move_id']:r for r in csv.DictReader((REF/'move_meta.csv').open())}
timings=json.loads((ROOT.parent/'HoennVR/Data/za_moves.json').read_text());table=json.loads((ROOT/'Assets/NativeFx/za-timing.json').read_text());results=json.loads((ROOT/'Docs/bdsp-legacy-staging.json').read_text()) if (ROOT/'Docs/bdsp-legacy-staging.json').exists() else []
for row in json.loads((ROOT/'Docs/remaining-bdsp-inventory.json').read_text())['nativeSequencesToStage']:
 name=row['id'];key='MOVE_'+name
 if key in index['moves']:continue
 results=[r for r in results if r['id']!=name]
 try:
  seq=bdspfx.sequence(row['sourceId']);attack,hit=bdspfx.timing(seq);groups=bdspfx.effect_groups(seq)
  for group in groups:
   for c in group['cmds']:
    if c['cmd']=='ParticleCreate':
     file=c['args']['file'];p=out/'prefabs'/(file+'.json')
     if not p.exists():write(p,bdspfx.export_prefab(file,store))
    elif c['cmd']=='ModelCreate':
     file=c['args']['file'];p=out/'prefabs'/('model_'+file.split('/')[0]+'.json')
     if not p.exists():
      export_models({file})
      for image in (out/'textures').glob('*.png'):
       pixels=np.asarray(Image.open(image));store.hashes[image.stem]=hashlib.sha1(pixels.tobytes()+str(pixels.shape).encode()).hexdigest()[:10]
  (out/key).mkdir(exist_ok=True);write(out/key/'move.json',dict(move=key,source=f"ew{row['sourceId']:03d}",fps=30,attack=attack,hit=hit,groups=groups));index['moves'][key]=key
  if name not in existing:
   r=meta[name];t=timings.get(name,{})
   move=dict(id=name,name=r['identifier'].replace('-',' ').title(),type=types[r['type_id']],physical=r['damage_class_id']=='2',power=int(r['power']),zaPower=int(r['power']),accuracy=int(r['accuracy'] or 0),pp=int(r['pp'] or 5),priority=int(r['priority']),shape=t.get('shape','Melee' if r['damage_class_id']=='2' else 'Projectile'),source='BDSP-native',effectOrigin='BDSP',labNotes='Native BDSP presentation with lab direct damage; secondary/special rules remain incomplete.')
   for k,v in dict(cooldown=6+int(r['power'])/30,windup=max(.1,hit),recovery=.8,rangeMin=0,rangeMax=3,radius=1,speed=8).items():move[k]=t.get(k,v)
   hits=multi.get(r['id'],{})
   if hits.get('min_hits'):move['hitCount']=int(hits['min_hits']) if hits['min_hits']==hits['max_hits'] else round((int(hits['min_hits'])+int(hits['max_hits']))/2)
   seed['moves'].append(move);existing.add(name)
   table['rows'].append(dict(id=name,move={**move,'sourceId':row['sourceId'],'shape':['Melee','Projectile','Beam','Arc','Area','Self'].index(move['shape'])}))
  results.append(dict(id=name,state='staged',sourceId=row['sourceId'],models=row['models']))
 except Exception as e:
  traceback.print_exc();results.append(dict(id=name,state='blocked-native-layer',reason=type(e).__name__+': '+str(e)))
 write(out/'index.json',index);write(ROOT/'Tools/seed-data.json',seed);write(ROOT/'Assets/NativeFx/za-timing.json',table);write(ROOT/'Docs/bdsp-legacy-staging.json',results)
 print(results[-1],flush=True)
print('TOTAL',len(seed['moves']),'BDSP',len(index['moves']),flush=True)
