"""Surf uses a motion-lived ground wave, like the already reviewed Muddy Water.
The .05-second carrier lifetime is not its travelling lifetime. Preserve native
wave/impact assets and velocity; separate start and endpoint rather than stacking.
"""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'Assets/NativeFx/ZA/zaew0057/move.json';d=json.loads(p.read_text())
timing=json.loads((ROOT.parent/'HoennVR/Data/za_moves.json').read_text())['SURF']
start=next(c['start'] for c in d['other_clips'] if c['cmd']=='Title/Bullet/Shoot');contact=start+4/timing['speed']
for o in d['objects']:
 if not o['name'].startswith('bullet'):continue
 end=o['set'].endswith('_end.json')
 for c in o['clips']:
  if end:
   if c['cmd']=='GameObject/SetTranslation':c['params'].update(Target='DEF',Node='origin',Translation=[0,0,0])
   c['start']=round(contact+(.5 if c['cmd']=='Particle/Stop' else 0),4);c['end']=round(c['start']+1/30,4)
  elif c['cmd']=='GameObject/SetTranslation':
   c['params']['Node']='origin';c['params']['Translation']=[0,0,0]
   if c['params']['Target']=='DEF':c['end']=round(contact,4)
  elif c['cmd']=='Particle/Stop':c['start']=round(contact,4);c['end']=round(contact+1/30,4)
  if c['cmd']=='GameObject/SetRotationtoTarget':c['params']['AxisType']=1
for c in d['other_clips']:
 if c['cmd']=='Animation/SetTrigger' and c['params'].get('Trigger')=='damage_trigger':c['start']=round(contact,4);c['end']=round(contact+1/30,4)
d['labGroundWaveSpeed']=timing['speed'];p.write_text(json.dumps(d,separators=(',',':')))
print('Surf native ground wave speed',timing['speed'],'reference contact',contact)
