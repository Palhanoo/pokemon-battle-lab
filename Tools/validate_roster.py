"""Validate ready catalog provenance, embedded GLB resources and all clip/move bindings."""
from pathlib import Path
import hashlib,json,struct,collections
ROOT=Path(__file__).resolve().parents[1];folder=ROOT/'LocalRoster'
def main():
    catalog=json.loads((folder/'catalog.json').read_text());rows=catalog['models'];seen=set();clips=0;aliases=[]
    moves={m['id'] for m in json.loads((ROOT/'Tools/seed-data.json').read_text())['moves']}
    inventory={r['dex']:r for r in json.loads((ROOT/'Docs/pokedex-inventory.json').read_text())['species']}
    for r in rows:
        assert r['dex'] not in seen,r['species'];seen.add(r['dex'])
        assert r['source']==inventory[r['dex']]['candidates'][0]['source'],r['species']+' does not use latest source'
        b=(folder/r['file']).read_bytes();assert hashlib.sha256(b).hexdigest()==r['sha256'],r['species']+' hash mismatch'
        magic,version,length=struct.unpack_from('<III',b);assert magic==0x46546c67 and version==2 and length==len(b)
        size=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+size]);binary=b[28+size:];assert d['meshes'] and d['skins']
        for view in d['bufferViews']:assert view.get('byteOffset',0)+view['byteLength']<=len(binary),r['species']+' buffer outside GLB'
        for image in d.get('images',[]):assert 'bufferView' in image,r['species']+' external texture'
        for texture in d.get('textures',[]):assert texture['source']<len(d['images'])
        names={a['name'] for a in d['animations']};clips+=len(names)
        for role in r['roles']:assert role['clip'] in names,r['species']+' missing '+role['role']
        assert {'battle','attack','special','damage','faint'}<={x['role'] for x in r['roles']}
        assert len(r['data']['stats'])==6 and all(s>0 for s in r['data']['stats'])
        assert len(set(r['data']['moves']))>=4 and set(r['data']['moves'])<=moves
        if r.get('roleAliases'):aliases.append(dict(species=r['species'],aliases=r['roleAliases']))
    # Regression: internal model 0700 is Froakie, not National Dex 700 (Sylveon).
    assert next(r for r in rows if r['species']=='FROAKIE')['dex']==656
    report=dict(species=len(rows),clips=clips,sourceCounts=dict(collections.Counter(r['source'] for r in rows)),roleAliases=aliases,missingSpecies=json.loads((ROOT/'Docs/pokedex-inventory.json').read_text())['missing'],status='Static resources/provenance passed; standalone/manual results are separate.')
    (ROOT/'Docs/pokedex-validation.json').write_text(json.dumps(report,indent=2)+'\n');print('VALID',len(rows),'species,',clips,'clips,',len(aliases),'species with explicit role aliases',flush=True)
if __name__=='__main__':main()
