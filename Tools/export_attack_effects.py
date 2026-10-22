"""Stage only the battle lab's selected moves, without importing the source project.
Run with Unity closed. ZA uses already-decoded local effect data from HoennVR;
BDSP exports its own selected original sequences from BDSP_ROMFS (default in vendor).
"""
from pathlib import Path
import argparse,hashlib,json,shutil,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/vendor'))
import bdspfx

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,default=ROOT.parent/'HoennVR');args=parser.parse_args()
    out=ROOT/'Assets/NativeFx';src=args.source/'Assets/Generated/MoveFx'
    index=json.loads((src/'index.json').read_text());subset={};strings=set()
    data=json.loads((ROOT/'Tools/seed-data.json').read_text())
    timing=json.loads((args.source/'Data/za_moves.json').read_text())
    MOVES={m['id']:timing[m['id']]['sourceId'] for m in data['moves']}
    def scan(x):
        if isinstance(x,str):strings.add(x)
        elif isinstance(x,list):
            for value in x:scan(value)
        elif isinstance(x,dict):
            for value in x.values():scan(value)
    for move in MOVES:
        name=index['moves']['MOVE_'+move];subset['MOVE_'+move]=name
        folder=out/'ZA'/name;folder.mkdir(parents=True,exist_ok=True)
        for path in (src/name).glob('*.json'):
            shutil.copy2(path,folder/path.name);scan(json.loads(path.read_text()))
    tex=out/'ZA/textures';tex.mkdir(parents=True,exist_ok=True)
    for path in (src/'textures').glob('*.png'):
        if path.stem in strings:shutil.copy2(path,tex/path.name)
    (out/'ZA/index.json').write_text(json.dumps({'moves':subset},indent=2))
    roster=json.loads((ROOT/'Tools/roster.json').read_text())
    bdspSpecies={m['species'] for m in roster['models'] if m['source']=='BDSP'}
    ids={m for species in data['species'] if species['id'] in bdspSpecies for m in species['moves']}
    bdspfx.SOURCE={m:MOVES[m] for m in ids};exported,missing=bdspfx.export_all(out/'BDSP')
    if missing or len(exported)!=len(ids):raise RuntimeError(f'Incomplete BDSP export: {missing}')
    rows=[]
    for move in MOVES:
        row=timing[move];row['shape']=['Melee','Projectile','Beam','Arc','Area','Self'].index(row['shape']);rows.append({'id':move,'move':row})
    (out/'za-timing.json').write_text(json.dumps({'rows':rows},indent=2))
    manifest=[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in out.rglob('*') if p.is_file() and p.suffix!='.meta']
    (ROOT/'Docs/native-fx-manifest.json').write_text(json.dumps(manifest,indent=2))
    print(f'Staged {len(manifest)} native effect files. Run BattleLabBuilder.Setup next.')
if __name__=='__main__':main()
