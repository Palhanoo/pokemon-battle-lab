"""Copy the validated roster beside a NEW, stopped standalone player.
Avoid Unity importing thousands of source GLBs; the player loads only its selected team.
"""
from pathlib import Path
import argparse,json,shutil
ROOT=Path(__file__).resolve().parents[1]
def stage(build):
    source=ROOT/'LocalRoster';data=json.loads((source/'catalog.json').read_text());dest=Path(build)/'PokemonBattleLab_Data/StreamingAssets/Roster'
    dest.mkdir(parents=True,exist_ok=True)
    for r in data['models']:shutil.copy2(source/r['file'],dest/r['file'])
    shutil.copy2(source/'catalog.json',dest/'catalog.json');print('Staged',len(data['models']),'species to',dest)
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('build',type=Path);stage(p.parse_args().build)
