"""Audit all staged attack libraries, including layers not supported by the reconstruction.
Asset completeness is deliberately separate from original-engine visual fidelity.
"""
from pathlib import Path
import collections,json,re
ROOT=Path(__file__).resolve().parents[1];FX=ROOT/'Assets/NativeFx'
missing=[];layers=[];commands=collections.Counter();warnings=[]
def walk(e,path):
    for tex in e.get('textures',[]):
        name=tex.get('name');exists=(FX/'ZA/textures'/(name+'.png')).exists()
        if not exists:missing.append(str(path)+': '+name)
        supported=not tex.get('sphere_map') and tex.get('slot',0)<=2
        layers.append(dict(source='ZA',asset=str(path.relative_to(FX)),emitter=e['name'],texture=name,slot=tex.get('slot'),exists=exists,bound=supported))
        if not supported:warnings.append(dict(asset=str(path.relative_to(FX)),emitter=e['name'],reason='Native lighting lookup / texture slot beyond supported masks',texture=name))
    for child in e.get('children',[]):walk(child,path)
za=json.loads((FX/'ZA/index.json').read_text())['moves']
for move,folder in za.items():
    path=FX/'ZA'/folder/'move.json';d=json.loads(path.read_text())
    for obj in d.get('objects',[]):
        if obj.get('set') and not (path.parent/obj['set']).exists():missing.append(str(path)+': '+obj['set'])
        for cmd in obj.get('clips',[]):commands['ZA:'+cmd['cmd']]+=1
    for path in (FX/'ZA'/folder).glob('*.json'):
        if path.name=='move.json':continue
        for group in json.loads(path.read_text()).get('sets',[]):
            for e in group.get('emitters',[]):walk(e,path)
bdsp=list((FX/'BDSP').glob('MOVE_*/move.json'))
for path in bdsp:
    for group in json.loads(path.read_text()).get('groups',[]):
        for cmd in group.get('cmds',[]):
            commands['BDSP:'+cmd['cmd']]+=1
            if cmd['cmd'] in ('ParticleCreate','ModelCreate'):
                name=cmd['args'].get('file')
                if name and not (FX/'BDSP/prefabs'/(str(name)+'.json')).exists():missing.append(str(path)+': '+str(name))
for path in (FX/'BDSP/prefabs').glob('*.json'):
    for node in json.loads(path.read_text()).get('nodes',[]):
        mat=node.get('r',{}).get('mat',{})
        for slot in ('tex0','tex1'):
            if not mat.get(slot):continue
            name=mat[slot]['name'];exists=(FX/'BDSP/textures'/(name+'.png')).exists()
            if not exists:missing.append(str(path)+': '+name)
            layers.append(dict(source='BDSP',asset=str(path.relative_to(FX)),emitter=node['name'],texture=name,slot=slot,exists=exists,bound=True))
report=dict(scope={'ZA':len(za),'BDSP':len(bdsp),'Arceus/SV/Sword':'Bodies use shared ZA effects; their separate native effect engines are not imported.'},missing=missing,layerCount=len(layers),layers=layers,unsupportedLightingLookups=warnings,commands=dict(sorted(commands.items())),limitations=['Texture binding does not establish native combiner/refraction/material fidelity.','Source camera tracks, native deformation and full state machines remain incomplete.','Manually compare release, travel, contact and cleanup; use F2 layers and Esc pause.'])
(ROOT/'Docs/effect-layer-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('layers','commands','unsupportedLightingLookups')},indent=2));print('Unsupported lighting lookups:',len(warnings))
raise SystemExit(bool(missing))
