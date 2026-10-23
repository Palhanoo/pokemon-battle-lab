"""Stage existing validated ZA Mega exports outside Unity's asset database."""
from pathlib import Path
import json,shutil,hashlib,struct
root=Path(__file__).resolve().parents[1]; main=root.parent/'HoennVR'; out=root/'LocalForms';out.mkdir(exist_ok=True)
forms=[]
for f in json.loads((main/'Assets/Resources/ZaRoster.json').read_text())['forms']:
 if not f.get('mega') or f.get('zMega'):continue
 src=main/'Assets/Generated/Models/pokemon'/(f['id']+'.glb')
 if not src.exists():continue
 raw=src.read_bytes(); data=json.loads(raw[20:20+struct.unpack_from('<I',raw,12)[0]]);clips={a['name'] for a in data.get('animations',[])}
 roles=json.loads(src.with_suffix('.clips.json').read_text())
 for role,other in [('battle','idle'),('attack','special'),('special','attack'),('faint','damage')]:
  if not roles.get(role):roles[role]=roles.get(other)
 if any(roles.get(role) not in clips for role in ['battle','attack','special','damage','faint']):continue
 dest=out/src.name
 if not dest.exists() or dest.stat().st_size!=src.stat().st_size:shutil.copy2(src,dest)
 forms.append(dict(id=f['id'],species=f['species'],label=f['stone'].replace('_',' '),stats=f['stats'],types=f['types'],file=dest.name,source='ZA',sha256=hashlib.sha256(raw).hexdigest(),roles=[dict(role=k,clip=v) for k,v in roles.items() if v in clips]))
(out/'catalog.json').write_text(json.dumps({'forms':forms},separators=(',',':'))+'\n')
(root/'Docs/mega-form-manifest.json').write_text(json.dumps({'forms':forms,'scope':'Native standard Mega models and form stats; ability effects and Z-Mega excluded.'},indent=2)+'\n')
print('Staged',len(forms),'Mega forms')
