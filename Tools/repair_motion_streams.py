"""Restore motion-lived carrier volleys from the locally decoded ZA bullet table.
The original exporter used lifeTime=.05 instead of the attack hold when counting
children, and mapped BulletWeakHandle to the first travelling child. Unity closed.
"""
from pathlib import Path
import copy,json,math,re
ROOT=Path(__file__).resolve().parents[1]
def repair(source=None):
    source=source or ROOT.parent/'HoennVR'
    bullets={b['bulletId']:b for b in json.loads((source/'Data/za_source/bullets.json').read_text())}
    report=[]
    for path in (ROOT/'Assets/NativeFx/ZA').glob('*/move.json'):
        data=json.loads(path.read_text());rows=[]
        for index,shoot in enumerate(c for c in data.get('other_clips',[]) if c['cmd']=='Title/Bullet/Shoot'):
            b=bullets.get(shoot['params'].get('BulletID'),{});interval=b.get('shootChildInterval',0)
            if not(b.get('optionLifeTimeMotion') and b.get('childBulletId') and interval>0 and b.get('velocity',0)==0):continue
            holds=[c['end'] for c in data['other_clips'] if c['cmd']=='Title/Character/WazaAttackLoopFrame' and c['end']>shoot['start']]
            if not holds:continue
            end=max(holds);start=shoot['start']+(0 if b.get('isShootChildImmediately') else interval)
            count=max(0,math.ceil((end-start)/interval-1e-4))
            if count>64:raise ValueError(('Unexpected stream count',path,count))
            prefix=f'{index}c'
            pattern=re.compile(r'^(bullet|impact)'+re.escape(prefix)+r'(\d+)(.*)$')
            templates=[o for o in data['objects'] if (m:=pattern.match(o['name'])) and int(m[2])==0]
            if not templates:continue
            data['objects']=[o for o in data['objects'] if not pattern.match(o['name'])]
            for k in range(count):
                for template in templates:
                    obj=copy.deepcopy(template);m=pattern.match(obj['name']);obj['name']=m[1]+prefix+str(k)+m[3]
                    for c in obj['clips']:
                        c['object']=obj['name'];c['start']=round(c['start']+k*interval,4);c['end']=round(c['end']+k*interval,4)
                    data['objects'].append(obj)
            for obj in data['objects']:
                for c in obj['clips']:
                    if c['cmd']=='GameObject/AttachTransform' and c['params'].get('Target')=='BulletWeakHandle':
                        c['params']['Target']='ATK';c['params']['Node']=b.get('attachLocator') or shoot['params'].get('Locator','eff_rangeattack01')
            row={'carrierId':b['bulletId'],'childId':b['childBulletId'],'interval':interval,'count':count,'start':start,'end':end,'nativeBaseLifetime':b['lifeTime'],'source':'optionLifeTimeMotion / WazaAttackLoopFrame'}
            rows.append(row);report.append({'move':data['id'],**row})
        if rows:
            data['motionStreams']=rows;path.write_text(json.dumps(data,separators=(',',':')))
    (ROOT/'Docs/motion-streams.json').write_text(json.dumps(report,indent=2)+'\n');print('Restored motion-lived streams:',report)
if __name__=='__main__':repair()
