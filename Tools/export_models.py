"""Export the three native rosters from local game data. No HoennVR runtime dependency.
Run with the existing pokemon/tools/venv Python (numpy, Pillow, UnityPy, ooz).
Source paths are configured in vendor/arceusx/sources.py and BDSP_ROMFS.
"""
from pathlib import Path
import hashlib
import json
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT / 'Tools/vendor'), str(ROOT / 'Tools/vendor/arceusx')]
from convert import Files, convert, clip_name
from sources import ARCEUS_POKEMON, VioletDir
from hoenngen.species import pick_clips
from hoenngen.bdsp import convert_species
from hoenngen.za_files import open_assets

def main():
    records = []
    for source in ('Arceus', 'ZA', 'BDSP'):
        dest = ROOT / 'Assets/Models' / source
        dest.mkdir(parents=True, exist_ok=True)
        for name, dex in [('PIKACHU', 25), ('GENGAR', 94), ('LUCARIO', 448)]:
            path = dest / (name + '.glb')
            model = f'pm{dex:04d}_00_00'
            if not path.exists():
                if source == 'BDSP':
                    roles = convert_species(dex, str(path))
                else:
                    files = (Files([str(Path(ARCEUS_POKEMON) / (model + '.gfpak'))]) if source == 'Arceus'
                             else VioletDir(open_assets(), f'ik_pokemon/data/pm{dex:04d}/{model}'))
                    convert(files, [model + '.trmdl'], str(path), 'all', guess_cutouts=False)
                    roles = pick_clips([clip_name(n) for n in files.all_named('.tranm') if n.startswith(model)])
                (dest / (name + '.clips.json')).write_text(json.dumps(roles, indent=2))
            records.append(dict(source=source, species=name, model=model,
                                file=str(path.relative_to(ROOT)), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            print(source, name, path.stat().st_size, flush=True)
    (ROOT / 'Assets/Data').mkdir(exist_ok=True)
    shutil.copy2(ROOT / 'Tools/seed-data.json', ROOT / 'Assets/Data/LabData.json')
    (ROOT / 'Docs/model-manifest.json').write_text(json.dumps(records, indent=2) + '\n')

if __name__ == '__main__':
    main()
