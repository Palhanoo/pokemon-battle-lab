"""Repair duplicate animation timestamps in local GLBs without resampling poses.
Some source tracks contain repeated timestamps. glTF requires strictly increasing
keys. Keep the last authored value at each time; retain interpolation/tangents.
"""
from pathlib import Path
import struct,json,hashlib,math,copy
COMP={5120:1,5121:1,5122:2,5123:2,5125:4,5126:4}
DIM={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
def normalize(path):
    path=Path(path);blob=path.read_bytes();jlen=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+jlen]);binary=bytearray(blob[28+jlen:]);changed=0
    def data(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];width=COMP[a['componentType']]*DIM[a['type']];stride=v.get('byteStride',width);start=v.get('byteOffset',0)+a.get('byteOffset',0)
        return a,[bytes(binary[start+i*stride:start+i*stride+width]) for i in range(a['count'])]
    def add(a,blocks,values=None):
        while len(binary)%4:binary.append(0)
        offset=len(binary);binary.extend(b''.join(blocks));vi=len(doc['bufferViews']);doc['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(binary)-offset})
        out=copy.deepcopy(a);out['bufferView']=vi;out['byteOffset']=0;out['count']=len(blocks);out.pop('min',None);out.pop('max',None)
        if values is not None:out['min']=[min(values)];out['max']=[max(values)]
        ai=len(doc['accessors']);doc['accessors'].append(out);return ai
    for anim in doc.get('animations',[]):
        for sampler in anim['samplers']:
            ia,ib=data(sampler['input']);times=[struct.unpack('<f',b)[0] for b in ib]
            if not all(math.isfinite(t) for t in times):raise ValueError(str(path)+' nonfinite animation time')
            if all(a<b for a,b in zip(times,times[1:])):continue
            keys={t:i for i,t in enumerate(times)};order=[keys[t] for t in sorted(keys)]
            oa,ob=data(sampler['output']);factor=len(ob)//len(ib)
            if len(ob)%len(ib):raise ValueError('Invalid output count '+str(path))
            sampler['input']=add(ia,[ib[i] for i in order],[times[i] for i in order])
            sampler['output']=add(oa,[ob[i*factor+k] for i in order for k in range(factor)]);changed+=1
    if changed:
        doc['buffers'][0]['byteLength']=len(binary);j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*((-len(j))%4);binary+=b'\0'*((-len(binary))%4)
        out=struct.pack('<III',0x46546c67,2,12+8+len(j)+8+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary
        path.write_bytes(out)
    return changed

def main():
    root=Path(__file__).resolve().parents[1];folder=root/'LocalRoster';total=0;affected=[]
    for p in sorted(folder.glob('*.json')):
        if p.name=='catalog.json':continue
        r=json.loads(p.read_text());model=folder/r['file'];n=normalize(model)
        if n:total+=n
        roles={v['role']:v['clip'] for v in r['roles']};aliases=r.get('roleAliases',{})
        for role,other in [('attack','special'),('special','attack'),('battle','idle'),('walk','idle'),('run','walk'),('faint','damage')]:
            if not roles.get(role) and roles.get(other):roles[role]=roles[other];aliases[role]=other
        r['roles']=[dict(role=k,clip=v) for k,v in roles.items()];r['roleAliases']=aliases
        r['normalizedTracks']=r.get('normalizedTracks',0)+n
        if r['normalizedTracks']:affected.append({'species':r['species'],'tracks':r['normalizedTracks']})
        r['sha256']=hashlib.sha256(model.read_bytes()).hexdigest();r['bytes']=model.stat().st_size;p.write_text(json.dumps(r,indent=2)+'\n')
    (root/'Docs/animation-key-audit.json').write_text(json.dumps({'changedTracks':sum(x['tracks'] for x in affected),'changedThisRun':total,'affected':affected,'rule':'Sort timestamps; last authored value wins for duplicates; no pose resampling.'},indent=2)+'\n')
    print('Normalized',total,'tracks in',len(affected),'models',flush=True)
if __name__=='__main__':main()
