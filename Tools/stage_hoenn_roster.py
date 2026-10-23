"""Append this reviewed move batch to pooled legal learnsets; preserve old choices and model selection."""
from pathlib import Path
import collections,csv,json
ROOT=Path(__file__).resolve().parents[1]
REF=Path('/home/bruno/pokemon/refs/pokeapi-81d20919')
def stage():
 batch=json.loads((ROOT/'Tools/hoenn-moves.json').read_text())['moves'];selected={m['id'] for m in batch}
 move_ids={r['id']:r['identifier'].upper().replace('-','_') for r in csv.DictReader((REF/'moves.csv').open()) if r['identifier'].upper().replace('-','_') in selected}
 species={r['id']:int(r['species_id']) for r in csv.DictReader((REF/'pokemon.csv').open()) if r['is_default']=='1'}
 legal=collections.defaultdict(set)
 for row in csv.DictReader((REF/'pokemon_moves.csv').open()):
  if row['pokemon_id'] in species and row['move_id'] in move_ids:legal[species[row['pokemon_id']]].add(move_ids[row['move_id']])
 path=ROOT/'LocalRoster/catalog.json';doc=json.loads(path.read_text());counts=collections.Counter()
 for row in doc['models']:
  moves=row['data']['moves']
  for m in batch:
   if m['id'] in legal[row['dex']]:
    if m['id'] not in moves:moves.append(m['id'])
    counts[m['id']]+=1
 for m in batch:
  row=next(r for r in doc['models'] if r['species']==m['species'])
  assert m['id'] in row['data']['moves'] and m['id'] in legal[row['dex']],m
 path.write_text(json.dumps(doc,separators=(',',':'))+'\n')
 (ROOT/'Docs/pokedex-ready.json').write_text(json.dumps(doc,indent=2)+'\n')
 (ROOT/'Docs/hoenn-move-coverage.json').write_text(json.dumps(dict(species=len(doc['models']),eligibleSpecies=counts,examples=batch),indent=2)+'\n');print(counts)
if __name__=='__main__':stage()
