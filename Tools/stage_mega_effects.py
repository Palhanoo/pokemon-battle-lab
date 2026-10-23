"""Export original ZA Mega particles; lab timing coordinates charge, body swap and reveal.
Run with Unity closed. Requires the locally configured ZA source archives.
"""
import sys,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'HoennVR/Tools~'))
from hoenngen import za_movefx,movefx

def stage():
 fs=za_movefx.ZaFS();out=ROOT/'Assets/NativeFx/ZA/zamega';out.mkdir(parents=True,exist_ok=True)
 tex=ROOT/'Assets/NativeFx/ZA/textures'
 # Populate the texture registry so Mega cannot overwrite an existing move's pixels.
 from PIL import Image
 import numpy as np,hashlib
 registry={}
 for p in tex.glob('*.png'):
  a=np.array(Image.open(p));registry[p.stem]=hashlib.sha1(a.tobytes()+str(a.shape).encode()).hexdigest()[:10]
 specs=[('cmn_mega_charge_00',0,1.2,'eff_center01'),('cmn_mega_charge_foot_00',0,1.2,'origin'),('cmn_mega_charge_end_00',1.2,2,'eff_center01'),('cmn_mega_charge_foot_end_00',1.2,2,'origin')]
 objects=[]
 for name,start,end,node in specs:
  path='ik_effect/data/battle/cmn/mega/'+name+'.ptcl';raw=fs.get(path)
  if not raw:raise RuntimeError('Missing native Mega set: '+path)
  data,_=movefx.export_set(raw,out,tex,registry);(out/(name+'.json')).write_text(json.dumps(data,separators=(',',':')))
  def clip(cmd,a,b,params):return {'object':name,'cmd':cmd,'start':a,'end':b,'params':params}
  objects.append({'name':name,'set':name+'.json','ptcl':path,'clips':[
   clip('GameObject/AttachTransform',0,4,{'Target':'ATK','Node':node,'InherSRT':True}),
   clip('Particle/Emit',start,start+.0333,{'Link':True,'ForceCalcFrame':0}),
   clip('Particle/Stop',end,end+.0333,{'Fade':True,'Kill':False})]})
 (out/'move.json').write_text(json.dumps({'objects':objects,'other_clips':[]},indent=2))
 p=ROOT/'Assets/NativeFx/ZA/index.json';d=json.loads(p.read_text());d['moves']['MOVE_MEGA_EVOLUTION']='zamega';p.write_text(json.dumps(d,indent=2))
 sys.path.insert(0,str(ROOT/'Tools'))
 from enrich_effect_data import enrich
 enrich(moves={'zamega'})
 print('Staged four native ZA Mega charge/reveal particle sets')
if __name__=='__main__':stage()
