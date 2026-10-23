"""Restore native secondary Rock Slide timeline and motion-lived wave staging.
The original source export flattened only particle-bearing bullet templates.
Keep these corrections reproducible; never edit an imported JSON by hand.
"""
from pathlib import Path
import copy,json,sys
ROOT=Path(__file__).resolve().parents[1]
def stage(source=None):
 source=source or ROOT.parent/'HoennVR';out=ROOT/'Assets/NativeFx/ZA'
 sys.path.insert(0,str(source/'Tools~'))
 import hoenngen
 if str(source/'Tools~/hoenngen') not in hoenngen.__path__:hoenngen.__path__.append(str(source/'Tools~/hoenngen'))
 from hoenngen import za_movefx as z
 fs=z.ZaFS();bp,wp=z.load_params(fs);registry=z.seed_registry(out/'textures');notes=[]
 def write(path,data):path.write_text(json.dumps(data,separators=(',',':')))
 def hit(data,t):
  for c in data['other_clips']:
   if c['cmd']=='Animation/SetTrigger' and c['params'].get('Trigger')=='damage_trigger':c['start']=round(t,4);c['end']=round(t+z.F1,4)
 # Motion-lived wave: native speed defines travel, not its carrier's .05s base life.
 p=out/'zaew0330/move.json';data=json.loads(p.read_text());start=next(c['start'] for c in data['other_clips'] if c['cmd']=='Title/Bullet/Shoot');contact=start+4/bp[2740]['velocity']
 for o in data['objects']:
  if not o['name'].startswith('bullet'):continue
  end=o['set'].endswith('_end.json')
  for c in o['clips']:
   if end:
    if c['cmd']=='GameObject/SetTranslation':c['params'].update(Target='DEF',Node='origin',Translation=[0,0,0])
    c['start']=round(contact+(0.5 if c['cmd']=='Particle/Stop' else 0),4);c['end']=round(c['start']+z.F1,4)
   elif c['cmd']=='GameObject/SetTranslation':
    c['params']['Node']='origin';c['params']['Translation']=[0,0,0]
    if c['params']['Target']=='DEF':c['end']=round(contact,4)
   elif c['cmd']=='Particle/Stop':c['start']=round(contact,4);c['end']=round(contact+z.F1,4)
 hit(data,contact);data['labGroundWaveSpeed']=bp[2740]['velocity'];write(p,data)
 # Earthquake's native floor-slope orientation is a world-floor attachment here.
 p=out/'zaew0089/move.json';data=json.loads(p.read_text())
 for o in data['objects']:
  if o['name'] in ('bullet0','ew0089_charge'):
   for c in o['clips']:
    if c['cmd']=='GameObject/SetTranslation':c['params']['Node']='origin'
    if c['cmd']=='GameObject/SetRotationtoTarget':c['params']['AxisType']=1
 write(p,data)
 # Secondary Rock Slide carrier 382 runs its own timeline, shooting 389 rocks.
 p=out/'zaew0157/move.json';data=json.loads(p.read_text());data['objects']=[o for o in data['objects'] if not o['name'].startswith(('bulletfall','impactfall')) and o['name']!='ew0157_line'];base=next(c['start'] for c in data['other_clips'] if c['cmd']=='Title/Bullet/Shoot')+bp[131]['shootChildInterval']
 timeline='ik_effect/data/battle/ew01/ew0157/ew0157_1_bullet.trtml';raw=fs.get(timeline);clips=z.movefx.timeline(raw)
 shoots=sorted((c for c in clips if c['cmd']=='Title/Bullet/Shoot'),key=lambda c:c['start'])
 assert shoots and all(c['params']['BulletID']==389 for c in shoots)
 height=wp[157]['spawnOffsetY'];speed=bp[389]['velocity'];fall=height/speed
 def add(name,ptcl,cs):
  file=Path(ptcl).with_suffix('.json').name
  if not (out/'zaew0157'/file).exists():
   js,_=z.export_set(fs.get(ptcl),out/'zaew0157',out/'textures',registry,notes,ptcl);write(out/'zaew0157'/file,js)
  data['objects'].append(dict(name=name,set=file,ptcl=ptcl,clips=sorted(cs,key=lambda c:c['start'])))
 rock=z.template(fs,bp[389]['objectTemplatePath']);impact=z.template(fs,bp[389]['impactEffectParams'][0]['impactEffectObjectPath']);assert rock and impact
 for index,shoot in enumerate(shoots):
  at=base+shoot['start'];land=at+fall
  # Native carrier shakes around the target. Deterministic bounded spread for the lab;
  # keep the first rock centered so first contact and the damage cue coincide.
  x=0 if index==0 else ((index*7)%9-4)*.13;zz=0 if index==0 else ((index*5)%7-3)*.13
  for suffix,ptcl in enumerate(rock.values()):
   name=f'bulletfall{index}_{suffix}';move=z.place(name,at+z.F1,'DEF','origin',(x,0,zz));move['end']=round(land,4)
   cs=[z.place(name,at,'DEF','origin',(x,height,zz)),z.clip(name,'GameObject/SetRotation',at,Rotation=[90,0,0],isRelative=False),z.emit(name,at),move,z.stop(name,land)]
   add(name,ptcl,cs)
  for suffix,ptcl in enumerate(impact.values()):
   name=f'impactfall{index}_{suffix}';add(name,ptcl,[z.place(name,land,'DEF','origin',(x,0,zz)),z.emit(name,land),z.stop(name,land+.5)])
 for scene in z.T(raw).strings(7):
  for name,ptcl in z.template(fs,scene).items():
   cs=copy.deepcopy([c for c in clips if c['object']==name])
   for c in cs:
    c['start']=round(c['start']+base,4);c['end']=round(c['end']+base,4)
    if c['cmd']=='GameObject/SetTranslation':c['params'].update(Target='DEF',Node='origin',Translation=[0,height,0])
   add(name,ptcl,cs)
 hit(data,base+shoots[0]['start']+fall);data['secondaryTimeline']=timeline;data['secondaryShots']=len(shoots);write(p,data)
 (ROOT/'Docs/hoenn-effect-staging.json').write_text(json.dumps(dict(rockSlide=dict(timeline=timeline,shots=len(shoots),firstContact=base+shoots[0]['start']+fall,fallSpeed=speed,spread='Lab deterministic bounded spread in place of native carrier shake'),muddyWater=dict(speed=bp[2740]['velocity'],referenceDistance=4),notes=notes),indent=2)+'\n');print('Hoenn secondary staging',len(shoots),'Rock Slide rocks; wave speed',bp[2740]['velocity'],flush=True)
if __name__=='__main__':stage()
