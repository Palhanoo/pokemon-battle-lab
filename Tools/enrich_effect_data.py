"""Restore native combiner metadata omitted by the initial particle export. Unity must be closed."""
from pathlib import Path
import sys,json
ROOT=Path(__file__).resolve().parents[1]
def enrich(source=None):
    source=source or ROOT.parent/'HoennVR'
    sys.path.insert(0,str(source/'Tools~'))
    from hoenngen import za_movefx,vfxb
    fs=za_movefx.ZaFS();cache={};count=0
    def merge(dst,src):
        nonlocal count
        assert dst['name']==src['name'],(dst['name'],src['name'])
        c=src['combiner'];v=c['input_types']
        dst['combiner']={'color_channels':v[:3]+[v[6]],'alpha_channels':v[3:6]+[v[7]],
            'color_blends':[c[k] for k in ['tex1_color_blend','tex2_color_blend','primitive_color_blend']],
            'alpha_blends':[c[k] for k in ['tex1_alpha_blend','tex2_alpha_blend','primitive_alpha_blend']]}
        count+=1
        # Child emitters in this format are a recursive list.
        for d,s in zip(dst.get('children',[]),src.get('children',[])):merge(d,s)
    for p in (ROOT/'Assets/NativeFx/ZA').glob('*/move.json'):
        seen=set()
        for o in json.loads(p.read_text())['objects']:
            path=p.parent/o['set']
            if path in seen:continue
            seen.add(path)
            ptcl=o.get('ptcl')
            if not ptcl:continue
            if ptcl not in cache:cache[ptcl]=vfxb.Ptcl(fs.get(ptcl))
            data=json.loads(path.read_text());native=cache[ptcl]
            assert len(data['sets'])==len(native.sets)
            for a,b in zip(data['sets'],native.sets):
                assert len(a['emitters'])==len(b['emitters'])
                for d,s in zip(a['emitters'],b['emitters']):merge(d,s)
            path.write_text(json.dumps(data,separators=(',',':')))
    print('Restored native combiner metadata:',count,'emitters')
if __name__=='__main__':enrich()
