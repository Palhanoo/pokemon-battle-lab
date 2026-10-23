"""Restore native combiner metadata omitted by the initial particle export. Unity must be closed."""
from pathlib import Path
import sys,json,struct,hashlib
ROOT=Path(__file__).resolve().parents[1]
def enrich(source=None,moves=None):
    source=source or ROOT.parent/'HoennVR'
    sys.path.insert(0,str(source/'Tools~'))
    # The BDSP model exporter may already have loaded the vendored package.
    # Extend its search path rather than relying on sys.path to replace it.
    import hoenngen
    native_package=str(source/'Tools~'/'hoenngen')
    if native_package not in hoenngen.__path__:hoenngen.__path__.append(native_package)
    from hoenngen import za_movefx,vfxb
    fs=za_movefx.ZaFS();cache={};count=0
    class NativePtcl(vfxb.Ptcl):
        def _emitter(self, section):
            data=super()._emitter(section)
            if section.attr!=vfxb.NONE:
                for attr in vfxb.Section(self.b,section.pos+section.attr).siblings(self.b):
                    if attr.magic=='CSDP' and attr.bin!=vfxb.NONE:
                        raw=self.b[attr.pos+attr.bin:attr.pos+attr.size]
                        data['custom_values']=list(struct.unpack('<'+str(len(raw)//4)+'f',raw))
            return data
    def fragment(native,index):
        a,n=native.shader_blob;b=native.b[a:a+n]
        q=lambda offset:struct.unpack_from('<Q',b,offset)[0]
        program=q(q(128)+64*index+16);stage=q(program+40);raw=q(stage+8)
        start=b.find(b'!!NVfp',raw)
        assert start>=0
        return b[start:].split(bytes([0]))[0].decode('ascii')
    def merge(dst,src):
        nonlocal count
        assert dst['name']==src['name'],(dst['name'],src['name'])
        c=src['combiner'];v=c['input_types']
        dst['combiner']={'color_channels':v[:3]+[v[6]],'alpha_channels':v[3:6]+[v[7]],
            'color_blends':[c[k] for k in ['tex1_color_blend','tex2_color_blend','primitive_color_blend']],
            'alpha_blends':[c[k] for k in ['tex1_alpha_blend','tex2_alpha_blend','primitive_alpha_blend']]}
        color=src['color'];static=src['static']
        if current_batch:
            for key,kind in [('c0','color0_type'),('c1','color1_type'),('a0','alpha0_type'),('a1','alpha1_type')]:
                if color[kind]==3 and dst['color'][key]['keys']:dst['color'][key]['type']='keys'
        dst['render_state']=src['render']
        dst['draw']=bool(src['info']['is_particle_draw'])
        dst['surface']={'refraction':c['shader_type']==1,'shader_type':c['shader_type'],
            'apply_alpha':c['apply_alpha'],'distance':c['distortion_by_distance'],
            'soft':color['is_soft_particle'],'soft_range':static['soft_edge'],
            'fresnel':color['is_fresnel_alpha'],'fresnel_range':static['fresnel_alpha'],
            'near':color['is_near_dist_alpha'],'near_range':static['near_dist_alpha'],
            'far':color['is_far_dist_alpha'],'far_range':static['far_dist_alpha'],
            'params':[[k['t'],k['x'],k['y'],k['z']] for k in static['param_keys'][:static['num_param_keys']]]}
        code=fragment(native,src['shader']['shader_index'])
        # The embedded custom program overrides the generic EFT combiner. Its
        # three maps are shape, colour and erosion noise, not three alpha masks.
        dst['native_shader']={'index':src['shader']['custom_shader_index'],
            'switch':src['shader']['custom_shader_switch'],
            'fragment_sha1':hashlib.sha1(code.encode()).hexdigest(),
            'flow':5 if hashlib.sha1(code.encode()).hexdigest()=='8c08c6dabf948ec904759b48d63d5e6f3eeb76ea' else 1 if '.675000012' in code else (2 if hashlib.sha1(code.split('REP.S ;')[0].encode()).hexdigest()[:10]=='f6a36f5f89' else 3 if hashlib.sha1(code.split('REP.S ;')[0].encode()).hexdigest()[:10]=='75a59ce422' else 4 if hashlib.sha1(code.split('REP.S ;')[0].encode()).hexdigest()[:10]=='5f14b1dcf1' else 0),
            'flow_color':1 if '.675000012' in code and '-|R' in code else 0,
            'values':src.get('custom_values',[])}
        count+=1
        # Child emitters in this format are a recursive list.
        for d,s in zip(dst.get('children',[]),src.get('children',[])):merge(d,s)
    batch={f"zaew{m['sourceId']:04d}" for m in json.loads((ROOT/'Tools/hoenn-moves.json').read_text())['moves']}
    for p in (ROOT/'Assets/NativeFx/ZA').glob('*/move.json'):
        if moves is not None and p.parent.name not in moves:continue
        current_batch=p.parent.name in batch
        seen=set()
        for o in json.loads(p.read_text())['objects']:
            path=p.parent/o['set']
            if path in seen:continue
            seen.add(path)
            ptcl=o.get('ptcl')
            if not ptcl:continue
            if ptcl not in cache:cache[ptcl]=NativePtcl(fs.get(ptcl))
            data=json.loads(path.read_text());native=cache[ptcl]
            assert len(data['sets'])==len(native.sets)
            for a,b in zip(data['sets'],native.sets):
                assert len(a['emitters'])==len(b['emitters'])
                for d,s in zip(a['emitters'],b['emitters']):merge(d,s)
            path.write_text(json.dumps(data,separators=(',',':')))
    print('Restored native combiner metadata:',count,'emitters')
if __name__=='__main__':enrich()
