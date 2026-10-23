"""Audit all staged attack libraries, including layers not supported by the reconstruction.
Asset completeness is deliberately separate from original-engine visual fidelity.
"""
from pathlib import Path
import collections,json,re
ROOT=Path(__file__).resolve().parents[1];FX=ROOT/'Assets/NativeFx'
surface_counts=collections.Counter()
missing=[];layers=[];commands=collections.Counter();warnings=[];omitted=[];unsupported=[]
za_supported=set(re.findall(r'case "([^"]+)":',(ROOT/'Assets/Scripts/Native/VioletMove.cs').read_text()))
bdsp_supported=set(re.findall(r'case "([^"]+)":',(ROOT/'Assets/Scripts/Native/BdspMove.cs').read_text()))
def walk(e,path):
    surface=e.get('surface',{});state=e.get('render_state',{})
    surface_counts['ZA_refraction']+=bool(surface.get('refraction'))
    surface_counts['ZA_soft']+=bool(surface.get('soft'))
    surface_counts['ZA_masked']+=bool(state) and not state.get('blend_enable',True)
    surface_counts['ZA_one_sided']+=state.get('display_side',0)!=0
    if not surface or not state:warnings.append(dict(asset=str(path.relative_to(FX)),emitter=e['name'],reason='Missing native surface/render-state metadata'))
    if e.get('draw') is False:omitted.append(dict(asset=str(path.relative_to(FX)),emitter=e['name'],reason='Native is_particle_draw=0 carrier; refraction renderers are restored from their native draw flags.'))
    for tex in e.get('textures',[]):
        name=tex.get('name');exists=(FX/'ZA/textures'/(name+'.png')).exists()
        if not exists:missing.append(str(path)+': '+name)
        supported=0<=tex.get('slot',0)<=2
        layers.append(dict(source='ZA',asset=str(path.relative_to(FX)),emitter=e['name'],texture=name,slot=tex.get('slot'),exists=exists,bound=supported))
        if not supported:warnings.append(dict(asset=str(path.relative_to(FX)),emitter=e['name'],reason='Native lighting lookup / texture slot beyond supported masks',texture=name))
    for child in e.get('children',[]):walk(child,path)
za=json.loads((FX/'ZA/index.json').read_text())['moves']
for move,folder in za.items():
    path=FX/'ZA'/folder/'move.json';d=json.loads(path.read_text())
    for obj in d.get('objects',[]):
        if obj.get('set') and not (path.parent/obj['set']).exists():missing.append(str(path)+': '+obj['set'])
        for cmd in obj.get('clips',[]):
            commands['ZA:'+cmd['cmd']]+=1
            if cmd['cmd'] not in za_supported:unsupported.append(dict(asset=str(path.relative_to(FX)),command=cmd['cmd']))
    for path in (FX/'ZA'/folder).glob('*.json'):
        if path.name=='move.json':continue
        for group in json.loads(path.read_text()).get('sets',[]):
            for e in group.get('emitters',[]):walk(e,path)
models={json.loads(p.read_text())['file']:p for p in (FX/'BDSP/prefabs').glob('model_*.json')}
bdsp=list((FX/'BDSP').glob('MOVE_*/move.json'))
for path in bdsp:
    for group in json.loads(path.read_text()).get('groups',[]):
        for cmd in group.get('cmds',[]):
            commands['BDSP:'+cmd['cmd']]+=1
            if cmd['cmd'] not in bdsp_supported:unsupported.append(dict(asset=str(path.relative_to(FX)),command=cmd['cmd']))
            if cmd['cmd'] in ('ParticleCreate','ModelCreate'):
                name=cmd['args'].get('file')
                if name and name not in models and not (FX/'BDSP/prefabs'/(str(name)+'.json')).exists():missing.append(str(path)+': '+str(name))
for path in (FX/'BDSP/prefabs').glob('*.json'):
    data=json.loads(path.read_text())
    entries=[(n['name'],n.get('r',{}).get('mat',{})) for n in data.get('nodes',[])]+[('model',m) for m in data.get('materials',[])]
    for label,mat in entries:
        surface_counts['BDSP_soft']+=bool(mat.get('surface',{}).get('soft'))
        for slot in ('tex0','tex1','tex2'):
            if not mat.get(slot):continue
            name=mat[slot]['name'];exists=(FX/'BDSP/textures'/(name+'.png')).exists()
            if not exists:missing.append(str(path)+': '+name)
            layers.append(dict(source='BDSP',asset=str(path.relative_to(FX)),emitter=label,texture=name,slot=slot,exists=exists,bound=True))
report=dict(surfaceSummary=dict(surface_counts),scope={'ZA':len(za),'BDSP':len(bdsp),'Arceus/SV/Sword':'Bodies use shared ZA effects; their separate native effect engines are not imported.'},missing=missing,layerCount=len(layers),layers=layers,unsupportedLightingLookups=warnings,omittedRenderers=omitted,unhandledTimelineCommands=unsupported,commands=dict(sorted(commands.items())),limitations=['Native texture channel/blend selectors retained; view-normal sphere projection and refraction/material fidelity remain approximate.','Opaque-scene refraction uses a calibrated displacement factor; transparent-on-transparent refraction and native near/far distance fading remain outside this reconstruction. Native Fresnel and BDSP distortion modes are now applied.','BDSP drawType=1 camera layers are attached to the render camera. Original cinematic camera tracks, native mesh deformation and full source-game state machines are not replicated.','Manually compare release, travel, contact and cleanup; use F2 layers and Esc pause.'])
(ROOT/'Docs/effect-layer-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('layers','commands','unsupportedLightingLookups','omittedRenderers','unhandledTimelineCommands')},indent=2));print('Unsupported texture slots:',len(warnings),'Omitted renderers:',len(omitted),'Unhandled timeline commands:',len(unsupported))
raise SystemExit(bool(missing))
