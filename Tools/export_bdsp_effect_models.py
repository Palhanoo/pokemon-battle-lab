"""Export the staged moves' native effect geometry, skeletons and sampled animation.
The shipped ew280_tail bundle contains missing-prefab placeholders; use the identically
named trail mesh embedded in ew280_hand and suppress its duplicate in the hand layer.
"""
from pathlib import Path
import sys,json,zlib
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/vendor'))
import bdspfx
from hoenngen import bdsp

def export_models():
 import UnityPy
 out=ROOT/'Assets/NativeFx/BDSP';store=bdspfx.TextureStore(out/'textures');report=[]
 requests={c['args']['file'] for p in out.glob('*/move.json') for g in json.loads(p.read_text())['groups'] for c in g['cmds'] if c['cmd']=='ModelCreate'}
 for file in sorted(requests):
  bundle=file.split('/')[0].lower();trail=bundle=='ew280_tail';bundle='ew280_hand' if trail else bundle
  env=UnityPy.load(str(Path(bdsp.AA)/'Effects/effmodel'/bundle));objs={o.path_id:o for o in env.objects};trees={k:o.read_typetree() for k,o in objs.items() if o.type.name in ['GameObject','Transform','MeshRenderer','MeshFilter','SkinnedMeshRenderer','Material','AnimationClip']}
  trs={k:t for k,t in trees.items() if objs[k].type.name=='Transform'};root=next(k for k,t in trs.items() if t['m_Father']['m_PathID']==0)
  nodes=[];indices={};paths=[]
  def walk(k,parent,path):
   t=trs[k];g=trees[t['m_GameObject']['m_PathID']];i=len(nodes);indices[k]=i;paths.append(path)
   nodes.append({'name':g['m_Name'],'parent':parent,'pos':bdsp.v3(t['m_LocalPosition']),'rot':bdsp.v4(t['m_LocalRotation']),'scale':bdsp.v3(t['m_LocalScale']),'active':bool(g['m_IsActive'])})
   for c in t['m_Children']:
    q=c['m_PathID'];n=trees[trs[q]['m_GameObject']['m_PathID']]['m_Name'];walk(q,i,(path+'/' if path else '')+n)
  walk(root,-1,'');renderers=[];meshes=[];materials=[]
  def tex(pid):return store.save((bundle,pid),objs[pid]) if pid in objs and objs[pid].type.name=='Texture2D' else None
  for k,t in trs.items():
   g=trees[t['m_GameObject']['m_PathID']];comps=[c['component']['m_PathID'] for c in g['m_Component']];isTrail=g['m_Name']=='ew280_tail'
   if trail!=isTrail and (trail or bundle=='ew280_hand'):continue
   for pid in comps:
    if objs[pid].type.name not in ['MeshRenderer','SkinnedMeshRenderer']:continue
    r=trees[pid];meshRef=r.get('m_Mesh') or next(trees[c]['m_Mesh'] for c in comps if objs[c].type.name=='MeshFilter');mo=objs[meshRef['m_PathID']].read()
    v,n,u,bi,bw,tris=bdsp._mesh_arrays(mo)
    m={'name':mo.m_Name,'v':v.flatten().tolist(),'i':np.array(tris).flatten().tolist()}
    if n is not None:m['n']=n.flatten().tolist()
    if u is not None:m['uv']=u.flatten().tolist()
    if bi is not None:
     bi=np.pad(bi,((0,0),(0,4-bi.shape[1])));bw=np.pad(bw,((0,0),(0,4-bw.shape[1])))
     m.update(bi=bi.flatten().tolist(),bw=bw.flatten().tolist(),bind=[bdsp.bp_matrix(b) for b in mo.m_BindPose])
    row={'node':indices[k],'mesh':len(meshes),'materials':[],'bones':[indices[b['m_PathID']] for b in r.get('m_Bones',[])]};meshes.append(m)
    for ref in r['m_Materials']:
     mt=trees[ref['m_PathID']];sp=mt['m_SavedProperties'];co=dict(sp['m_Colors']);fl=dict(sp['m_Floats']);kw=mt['m_ShaderKeywords']
     if '_COLORTEXTUREEXPRESSION_' in kw:
      mat=bdspfx.material(mt,None,tex);mat['color']=[mat['color_scale']]*3+[1];mat['emission']=[0]*4;mat['lit']=False;mat['zwrite']=0
     else:
      color=co.get('_Color',dict(r=1,g=1,b=1,a=1));em=co.get('_EmissionColor',dict(r=0,g=0,b=0,a=0))
      mat={'color':[color[c] for c in 'rgba'],'emission':[em[c] for c in 'rgba'],'lit':True,'src':int(fl.get('_SrcBlend',1)),'dst':int(fl.get('_DstBlend',0)),'zwrite':int(fl.get('_ZWrite',1)),'cull':0,'color_mode':1,'alpha_mode':0}
     row['materials'].append(len(materials));materials.append(mat)
    renderers.append(row)
  tracks=[];unmapped=[];materialTracks=[]
  # Hashes are animator-relative; prefer full paths, then nested Animator roots.
  hashes={zlib.crc32(p.encode()):i for i,p in enumerate(paths)}
  for i,p in enumerate(paths):
   parts=p.split('/')
   for start in range(1,len(parts)):hashes.setdefault(zlib.crc32('/'.join(parts[start:]).encode()),i)
  for c in trees.values():
   if 'm_ClipBindingConstant' not in c:continue
   times,curves=bdsp.clip_curves(c);ci=0
   if trail and c['m_Name']!='ew280_tail_scroll':continue
   if not trail and c['m_Name']=='ew280_tail_scroll':continue
   for b in c['m_ClipBindingConstant']['genericBindings']:
    dim=bdsp.DIMS.get(b['attribute'],1) if b['typeID']==4 else 1;values=curves[ci:ci+dim];ci+=dim
    node=hashes.get(b['path'])
    if b['typeID']!=4:
     prop=next((k for k in ['_UvScale0','_UvScroll0'] if zlib.crc32(k.encode())&0xfffffff==b['attribute']&0xfffffff),None)
     if trail and prop:
      materialTracks.append({'material':0,'property':prop,'component':(b['attribute']>>28)&3,'times':times.tolist(),'values':values[0].tolist()});continue
     unmapped.append({'clip':c['m_Name'],'type':b['typeID'],'attribute':b['attribute']});continue
    if node is None or len(values)!=dim or any(v is None for v in values):raise ValueError(('Unmapped skeleton track',file,b))
    vals=np.stack(values,-1)
    if b['attribute']==4:vals=bdsp.euler_to_quat(vals)
    tracks.append({'node':node,'property':2 if b['attribute']==4 else b['attribute'],'times':times.tolist(),'values':vals.flatten().tolist()})
  assert renderers,(file,'empty model')
  data={'file':file,'model':True,'nodes':nodes,'meshes':meshes,'renderers':renderers,'materials':materials,'tracks':tracks,'materialTracks':materialTracks}
  (out/'prefabs'/('model_'+file.split('/')[0]+'.json')).write_text(json.dumps(data,separators=(',',':')))
  report.append({'file':file,'bundle':bundle,'renderers':len(renderers),'tracks':len(tracks),'materialTracks':len(materialTracks),'vertices':sum(len(m['v'])//3 for m in meshes),'unmappedMaterialTracks':unmapped,'trailFallback':trail})
 (ROOT/'Docs/bdsp-effect-models.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':export_models()
