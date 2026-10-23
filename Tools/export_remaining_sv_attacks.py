"""Export native damage presentations missing from the old Gen3-centric subset.
Writes only LocalMoveExports; safe while a previously built player is running.
No source-game or main-game project data is modified.
"""
from pathlib import Path
import sys,json
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'HoennVR/Tools~'))
from hoenngen import movefx,za_movefx
out=ROOT/'LocalMoveExports';out.mkdir(exist_ok=True)
registry=za_movefx.seed_registry(ROOT/'Assets/NativeFx/ZA/textures')
index=json.loads((out/'index.json').read_text()) if (out/'index.json').exists() else dict(moves={},failures={})
rows=json.loads((ROOT/'Docs/remaining-native-inventory.json').read_text())['additionalDamageMoves']
for i,row in enumerate(rows):
 key='MOVE_'+row['id'];tag=f"ew{row['sourceId']:04d}"
 if key in index['moves']:continue
 try:
  mv=movefx.export_move(row['sourceId'],movefx.violet_files(row['sourceId']),out/tag,tex_dir=out/'textures',registry=registry,resolve=movefx._fs().get)
  if not mv['objects']:raise ValueError('No native effect objects in selected timeline')
  index['moves'][key]=tag;index['failures'].pop(key,None)
 except Exception as exc:index['failures'][key]=str(exc)
 (out/'index.json').write_text(json.dumps(index,indent=2)+'\n')
 print(i+1,'/',len(rows),row['id'],'exported' if key in index['moves'] else index['failures'][key],flush=True)
print('EXPORTED',len(index['moves']),'FAILED',len(index['failures']),flush=True)
