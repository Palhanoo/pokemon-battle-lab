"""Rebake native material layers while preserving every mesh/skin/animation accessor byte."""
from pathlib import Path
import concurrent.futures,hashlib,json,struct,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path[:0]=[str(ROOT/'Tools/vendor'),str(ROOT/'Tools/vendor/arceusx')]
from sources import VioletDir,open_violet
from hoenngen.za_files import open_assets
from convert import Files,MaterialBaker,GLB,params,tex_by_binding
import numpy as np
from fb import T

def rebake(marker, force=False):
    marker=Path(marker);row=json.loads(marker.read_text())
    if row['source'] not in ('ZA','SV','Arceus') or (row.get('materialBake') in (3,4) and not force):return (row['species'],'unchanged')
    path=marker.parent/row['file'];blob=path.read_bytes();size=struct.unpack_from('<I',blob,12)[0];j=json.loads(blob[20:20+size]);binary=blob[28+size:];c=row['candidate'];model=Path(c['path']).stem
    files=Files([c['path']]) if row['source']=='Arceus' else VioletDir(open_assets() if row['source']=='ZA' else open_violet(),str(Path(c['path']).parent))
    mdl=T(files.get(model+'.trmdl'));byname={m.string(0):m for f in mdl.strings(3) for m in T(files.get(f)).tables(1)}
    baked=GLB();baker=MaterialBaker(files,baked,{},False)
    affected=force
    for old in j['materials']:
        mp=byname[old['name']];f4,f1,sh=params(mp);tex=tex_by_binding(mp)
        if 'LayerMaskMap' not in tex:continue
        mask=baker.tex(tex['LayerMaskMap'])
        if mask is None:continue
        count=np.zeros(mask.shape[:2],np.uint8)
        for i in range(4):
            layer=f4.get('BaseColorLayer'+str(i+1))
            if layer and layer[3]>0 and f1.get('LayerMaskScale'+str(i+1),1)>0:count+=(mask[...,i]>0)
        if np.any(count>1) or sh.get('BaseColorMultiply')=='False' and np.any(count):affected=True;break
    if not affected:
        row.update(materialBake=4,materialNote='Native masks checked: no overlapping active colour layers; existing textures retained.')
        marker.write_text(json.dumps(row,indent=2)+'\n');return (row['species'],'audited; no overlapping layers')
    materials=[baker.build(byname[m['name']]) for m in j['materials']]
    discarded={im['bufferView'] for im in j.get('images',[])};out=bytearray();views=[];mapping={};preserved=hashlib.sha256()
    def append(view,data):
        while len(out)%4:out.append(0)
        v=dict(view);v['byteOffset']=len(out);v['buffer']=0;v['byteLength']=len(data);out.extend(data);views.append(v)
    for i,v in enumerate(j['bufferViews']):
        if i in discarded:continue
        mapping[i]=len(views);b=binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];preserved.update(b);append(v,b)
    for a in j['accessors']:
        if 'bufferView' in a:a['bufferView']=mapping[a['bufferView']]
        if 'sparse' in a:
            for k in ('indices','values'):a['sparse'][k]['bufferView']=mapping[a['sparse'][k]['bufferView']]
    texture_offset=len(views)
    for v in baked.j['bufferViews']:append(v,baked.bin[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']])
    for image in baked.j['images']:image['bufferView']+=texture_offset
    for key in ('images','textures','samplers'):j[key]=baked.j[key]
    j['extensionsUsed']=list(dict.fromkeys(j.get('extensionsUsed',[])+baked.j.get('extensionsUsed',[])))
    j['materials']=materials;j['bufferViews']=views;j['buffers']=[{'byteLength':len(out)}]
    check=hashlib.sha256()
    for v in views[:texture_offset]:check.update(out[v['byteOffset']:v['byteOffset']+v['byteLength']])
    assert check.digest()==preserved.digest(),'Geometry/animation data changed'
    text=json.dumps(j,separators=(',',':')).encode();text+=b' '*(-len(text)%4);out+=b'\0'*(-len(out)%4)
    result=struct.pack('<III',0x46546c67,2,28+len(text)+len(out))+struct.pack('<II',len(text),0x4e4f534a)+text+struct.pack('<II',len(out),0x004e4942)+out
    temp=path.with_suffix('.tmp');temp.write_bytes(result);temp.replace(path)
    row.update(materialBake=4,materialNote='Ordered mask layers blend against original albedo; layer opacity respected.',geometryAnimationHash=preserved.hexdigest(),sha256=hashlib.sha256(result).hexdigest(),bytes=len(result));marker.write_text(json.dumps(row,indent=2)+'\n')
    return (row['species'],'rebaked; geometry/animation bytes unchanged')

def main():
    files=[p for p in (ROOT/'LocalRoster').glob('*.json') if p.name!='catalog.json'];files.sort(key=lambda p:(p.stem!='TREECKO',p.stem));report=[]
    with concurrent.futures.ProcessPoolExecutor(max_workers=2) as pool:
        for i,r in enumerate(pool.map(rebake,files)):
            report.append(r)
            if (i+1)%25==0:print('MATERIALS',i+1,'/',len(files),flush=True)
    (ROOT/'Docs/material-bake-audit.json').write_text(json.dumps({'models':report,'rule':'Later layer masks replace previous tints against original albedo, preserving all geometry/skin/animation bytes.'},indent=2)+'\n');print('MATERIAL REBAKE COMPLETE',len(report),flush=True)
if __name__=='__main__':main()
