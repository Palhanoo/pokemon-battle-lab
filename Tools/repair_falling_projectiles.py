"""Restore ZA Rock Throw's falling bullet travel, omitted by the initial exporter.
The original bullet 100 spawns 5m over DEF and moves at 12m/s; spawn-at-DEF
was incorrectly treated as stationary, so impact fired immediately. Unity closed.
"""
from pathlib import Path
import copy,json
ROOT=Path(__file__).resolve().parents[1]
def repair(source=None):
 source=source or ROOT.parent/'HoennVR'
 path=ROOT/'Assets/NativeFx/ZA/zaew0088/move.json';data=json.loads(path.read_text())
 if data.get('labFallingProjectile'):return
 bullets=json.loads((source/'Data/za_source/bullets.json').read_text())
 shoot=next(c for c in data['other_clips'] if c['cmd']=='Title/Bullet/Shoot')
 bullet=next(b for b in bullets if b['bulletId']==shoot['params']['BulletID'])
 body=next(o for o in data['objects'] if o['name']=='bullet0')
 start=next(c for c in body['clips'] if c['cmd']=='GameObject/SetTranslation')
 height=start['params']['Translation'][1];speed=bullet['velocity']
 assert bullet['moveType']==0 and start['params']['Target']=='DEF' and height>0 and speed>0
 old_hit=next(c['start'] for c in data['other_clips'] if c['cmd']=='Animation/SetTrigger')
 hit=round(start['end']+height/speed,4);shift=hit-old_hit
 travel=copy.deepcopy(start);travel['start']=start['end'];travel['end']=hit;travel['params']['Translation']=[0,0,0];body['clips'].append(travel)
 for c in body['clips']:
  if c['cmd']=='Particle/Stop':c.update(start=hit,end=round(hit+1/30,4));c['params'].update(Fade=False,Kill=True)
 for o in data['objects']:
  if not o['name'].startswith('impact'):continue
  for c in o['clips']:c['start']=round(c['start']+shift,4);c['end']=round(c['end']+shift,4)
 for c in data['other_clips']:
  if c['cmd']=='Animation/SetTrigger':c['start']=hit;c['end']=round(hit+1/30,4)
 data['labFallingProjectile']={'bulletId':bullet['bulletId'],'height':height,'speed':speed,'hit':hit,'oldHit':old_hit}
 path.write_text(json.dumps(data,separators=(',',':')))
 (ROOT/'Docs/rock-throw-motion.json').write_text(json.dumps(data['labFallingProjectile'],indent=2)+'\n')
 print('Restored falling Rock Throw:',data['labFallingProjectile'])
if __name__=='__main__':repair()
