"""Export Brilliant Diamond battle move effects for the Gen-3 and Gen-4 moves Violet has no effect for.

A BDSP move is a sequence (Battle/btlv/waza/sequence/ewNNN, a SequenceFile MonoBehaviour: groups of
frame-ranged commands) that creates Unity particle prefabs (Effects/effect/prefab/battle/<name>) and
moves/scales them relative to the battlers. Since BDSP is a Unity game, the prefabs' ParticleSystem
modules are written out almost verbatim for Assets/Scripts/Battle/BdspFx.cs to rebuild 1:1.

Many moves Sword/Shield dropped are empty sequences in BDSP; those borrow the closest move that has an
effect (SOURCE). Output: <out>/<MOVE>/move.json, <out>/prefabs/<prefab>.json, <out>/textures/*.png,
<out>/index.json. Sequence frames are 30 fps; sequence distances are centimetres.

  python -m hoenngen.bdspfx --out /mnt/d/www/HoennVR/Assets/Generated/MoveFx/bdsp
"""
import argparse
import hashlib
import json
import math
import os
import re
import shutil
from pathlib import Path

ROMFS = os.environ.get('BDSP_ROMFS', '/mnt/d/pokemon/bdsp-work/romfs')
AA = Path(ROMFS) / 'Data' / 'StreamingAssets' / 'AssetAssistant'
SEQ_DIR = AA / 'Battle' / 'btlv' / 'waza' / 'sequence'
PREFAB_DIR = AA / 'Effects' / 'effect' / 'prefab' / 'battle'
COMMON = AA / 'Effects' / 'effect_common'
FPS = 30.0

# Move -> BDSP move number whose effect plays it (its own when BDSP has one).
SOURCE = {
    'KARATE_CHOP': 2, 'DOUBLE_SLAP': 1, 'COMET_PUNCH': 5, 'RAZOR_WIND': 314, 'JUMP_KICK': 136, 'ROLLING_KICK': 25,
    'TWINEEDLE': 42, 'SONIC_BOOM': 48, 'SUBMISSION': 66, 'DRAGON_RAGE': 225, 'MEDITATE': 347, 'RAGE': 279,
    'BARRIER': 115, 'BIDE': 117, 'MIRROR_MOVE': 383, 'EGG_BOMB': 121, 'BONE_CLUB': 155, 'CLAMP': 11,
    'SKULL_BASH': 130, 'SPIKE_CANNON': 42, 'CONSTRICT': 35, 'KINESIS': 134, 'BARRAGE': 121, 'LOVELY_KISS': 142,
    'BUBBLE': 61, 'DIZZY_PUNCH': 5, 'FLASH': 148, 'PSYWAVE': 60, 'BONEMERANG': 155, 'HYPER_FANG': 158,
    'SHARPEN': 468, 'SPIDER_WEB': 81, 'MIND_READER': 170, 'NIGHTMARE': 171, 'FAINT_ATTACK': 389, 'OCTAZOOKA': 190,
    'FORESIGHT': 170, 'RETURN': 34, 'FRUSTRATION': 263, 'MAGNITUDE': 89, 'PURSUIT': 372, 'VITAL_THROW': 233,
    'HIDDEN_POWER': 237, 'HAIL': 181, 'SMELLING_SALT': 395, 'NATURE_POWER': 129, 'ASSIST': 383, 'MAGIC_COAT': 277,
    'REVENGE': 279, 'REFRESH': 215, 'GRUDGE': 288, 'SNATCH': 168, 'SECRET_POWER': 21, 'CAMOUFLAGE': 160,
    'MUD_SPORT': 189, 'ICE_BALL': 205, 'NEEDLE_ARM': 42, 'AROMATHERAPY': 312, 'ODOR_SLEUTH': 170, 'SILVER_WIND': 16,
    'GRASS_WHISTLE': 47, 'SIGNAL_BEAM': 60, 'SKY_UPPERCUT': 5, 'WATER_SPORT': 55,
    # Bind is a squeezing model and Substitute a doll model in BDSP: Wrap's coil and Acid Armor's puff stand in.
    'BIND': 35, 'SUBSTITUTE': 133,
    # Gen-4 moves Violet dropped play BDSP's own effect.
    'MIRACLE_EYE': 357, 'WAKE_UP_SLAP': 358, 'NATURAL_GIFT': 363, 'EMBARGO': 373, 'PSYCHO_SHIFT': 375,
    'TRUMP_CARD': 376, 'HEAL_BLOCK': 377, 'WRING_OUT': 378, 'LUCKY_CHANT': 381, 'ME_FIRST': 382, 'PUNISHMENT': 386,
    'MUD_BOMB': 426, 'MIRROR_SHOT': 429, 'ROCK_CLIMB': 431, 'MAGNET_BOMB': 443, 'CAPTIVATE': 445, 'CHATTER': 448,
    'HEAL_ORDER': 456, 'OMINOUS_WIND': 466,
}

# Sequence node index -> effect locator name (PokemonPrefabInfo._locators order).
LOCATORS = {0: 'Origin', 1: 'Waist', 2: 'Spine1', 3: 'Spine2', 5: 'Head', 6: 'LFinger', 7: 'Jaw',
            8: 'EffAttack01_01', 9: 'EffAttack01_02', 10: 'EffAttack01_03', 11: 'EffAttack01_04',
            12: 'EffAttack02_01', 15: 'EffCenter01', 17: 'EffEye01', 18: 'EffEye02', 25: 'EffFoot01',
            26: 'EffFoot02', 27: 'EffFoot03', 28: 'EffFoot04', 33: 'EffFront01', 34: 'EffGcloud01',
            35: 'EffHand01', 36: 'EffHand02', 37: 'EffHand03', 38: 'EffHand04', 41: 'EffHeadCenter01',
            44: 'EffHorn01', 45: 'EffHorn02', 51: 'EffMouth01', 57: 'EffOverHead01', 58: 'EffShoot01_01',
            59: 'EffShoot02_01', 61: 'EffTail01'}

# Commands the runtime understands; the rest (camera, screen, sound, battler motion) stay out.
PARTICLE_CMDS = {'ParticleCreate', 'ParticleMoveRelativePoke', 'ParticleMovePosition', 'ParticleMoveSpecialPos',
                 'ParticleScale', 'ParticleRotate', 'ParticleRotateOffset', 'ParticleRotatePoke', 'ParticleFollowPoke',
                 'ParticleFollowModel', 'DprParticleFollowModel', 'ParticleMoveRelativeModel',
                 'DprParticleMoveRelativeModel', 'ParticleStop'}
MODEL_CMDS = {'ModelCreate', 'ModelMoveRelativePoke', 'ModelMovePosition', 'ModelMoveSpecialPos', 'ModelDelete',
              'DprModelFollowModel'}
SKIP_PREFAB = re.compile(r'_cam_|_bg_|_BG|concentrated|gamen')  # screen-space focus lines and backdrops: wrong in VR


def _r(v, n=4):
    if isinstance(v, (list, tuple)):
        return [_r(x, n) for x in v]
    v = float(v)
    return round(v, n) if math.isfinite(v) else 0.0  # a few gradients carry NaN channels


def _num(s):
    try:
        return float(s)
    except ValueError:
        return s


def _cmd(cmd, start, end, **args):
    return {'cmd': cmd, 'start': start, 'end': end, 'args': args}


def _poke(node, trg):
    return dict(node=node, trg=trg, pos=[0.0, 0.0, 0.0], isRot=1, isRotPos=1, rate=100)


# Moves whose effects BDSP ships but whose sequences it left empty (cut from Sword/Shield): simple stagings.
SYNTH = {
    117: [{'name': 'attacker', 'grp': 0, 'cmds': [_cmd('PokemonAttackMotion', 0, 0, motion=31, trg=0)]},
          {'name': 'ew117_at_start', 'grp': 1, 'cmds': [
              _cmd('ParticleCreate', 0, 60, file='ew117/ew117_at_start.ptcl'),
              _cmd('ParticleMoveRelativePoke', 0, 0, **_poke(0, 0))]},
          {'name': 'defender', 'grp': 2, 'cmds': [_cmd('HitBack', 40, 40, trg=1)]}],
    121: [{'name': 'attacker', 'grp': 0, 'cmds': [_cmd('PokemonAttackMotion', 0, 0, motion=33, trg=0)]},
          {'name': 'ew121_throw', 'grp': 1, 'cmds': [
              _cmd('ParticleCreate', 2, 22, file='ew121/ew121_throw.ptcl'),
              _cmd('ParticleMoveRelativePoke', 2, 2, **_poke(15, 0)),
              _cmd('ParticleRotatePoke', 2, 2, dirPoke=1, node=15, ofs=0.0),
              _cmd('ParticleMoveRelativePoke', 3, 20, **_poke(15, 1))]},
          {'name': 'ew121_hit', 'grp': 2, 'cmds': [
              _cmd('ParticleCreate', 20, 60, file='ew121/ew121_hit.ptcl'),
              _cmd('ParticleMoveRelativePoke', 20, 20, **_poke(15, 1))]},
          {'name': 'defender', 'grp': 3, 'cmds': [_cmd('HitBack', 20, 20, trg=1)]}],
}


def sequence(num):
    """Groups of the move's sequence: [{name, grp, cmds: [{cmd, start, end, args}]}] (active commands only)."""
    import UnityPy
    if num in SYNTH:
        return SYNTH[num]
    path = SEQ_DIR / f'ew{num:03d}'
    if not path.exists():
        return []
    for o in UnityPy.load(str(path)).objects:
        if o.type.name != 'MonoBehaviour':
            continue
        out = []
        for g in o.read_typetree()['_groupData']:
            cmds = []
            for c in sorted(g['Commands'], key=lambda c: c['StartFrame']):
                if not c['IsActive']:
                    continue
                m = c['Macro']
                args = {v['Name']: [_num(x) for x in v['Values']] for v in m['Values']}
                args = {k: (v[0] if len(v) == 1 else v) for k, v in args.items()}
                cmds.append({'cmd': m['Name'], 'start': c['StartFrame'], 'end': c['EndFrame'], 'args': args})
            out.append({'name': g['Name'], 'grp': g['GrpNo'], 'cmds': cmds})
        return out
    return []


def timing(groups):
    """Attack motion and damage frames (seconds) from the battler commands."""
    attack = hit = None
    for g in groups:
        for c in g['cmds']:
            a = c['args']
            if c['cmd'] == 'PokemonAttackMotion' and attack is None:
                attack = c['start'] / FPS
            if (c['cmd'] == 'HitBack' or (c['cmd'] == 'PokemonMotion' and a.get('trg') == 1 and a.get('motion') == 16)
                    or c['cmd'] == 'PokemonShaderCol' and a.get('trg') == 1):
                hit = c['start'] / FPS if hit is None else min(hit, c['start'] / FPS)
    if hit is None:
        for g in groups:
            for c in g['cmds']:
                if c['cmd'] == 'GaugeDamage':
                    hit = c['start'] / FPS if hit is None else min(hit, c['start'] / FPS)
    return attack, hit


def effect_groups(groups):
    """Groups that create a particle prefab or a (stand-in) model, with only the commands that move them."""
    out = []
    for g in groups:
        kinds = {c['cmd'] for c in g['cmds']}
        if 'ParticleCreate' in kinds:
            keep = PARTICLE_CMDS
        elif 'ModelCreate' in kinds:
            keep = MODEL_CMDS
        else:
            continue
        cmds = [dict(c, start=_r(c['start'] / FPS), end=_r(c['end'] / FPS)) for c in g['cmds'] if c['cmd'] in keep]
        for c in cmds:
            if c['cmd'] == 'ParticleCreate':
                c['args']['file'] = str(c['args']['file']).split('/')[-1].rsplit('.', 1)[0]
        out.append({'name': g['name'], 'grp': g['grp'], 'kind': 'particle' if keep is PARTICLE_CMDS else 'model',
                    'cmds': cmds})
    return out

# ---------------------------------------------------------------- prefabs


def curve(c):
    """MinMaxCurve -> a number (constant) or {s, k, m, max, min} with keys [t, v, in, out]."""
    st = c['minMaxState']
    if st == 0:
        return _r(c['scalar'])

    def keys(ac):
        return [[_r(k['time']), _r(k['value']), _r(k['inSlope']), _r(k['outSlope'])] for k in ac['m_Curve']]
    out = {'s': st, 'k': _r(c['scalar']), 'm': _r(c['minScalar'])}
    if st in (1, 2):
        out['max'] = keys(c['maxCurve'])
    if st == 2:
        out['min'] = keys(c['minCurve'])
    return out


def _grad(g):
    return {'c': [[_r(g[f'ctime{i}'] / 65535.0)] + _r([g[f'key{i}'][ch] for ch in 'rgb']) for i in range(g['m_NumColorKeys'])],
            'a': [[_r(g[f'atime{i}'] / 65535.0), _r(g[f'key{i}']['a'])] for i in range(g['m_NumAlphaKeys'])],
            'mode': g.get('m_Mode', 0)}


def gradient(c):
    """MinMaxGradient: s 0 colour, 1 gradient, 2 two colours, 3 two gradients, 4 random colour."""
    st = c['minMaxState']
    out = {'s': st}
    if st in (0, 2):
        out['max'] = _r([c['maxColor'][ch] for ch in 'rgba'])
    if st == 2:
        out['min'] = _r([c['minColor'][ch] for ch in 'rgba'])
    if st in (1, 3, 4):
        out['gmax'] = _grad(c['maxGradient'])
    if st == 3:
        out['gmin'] = _grad(c['minGradient'])
    return out


def _v3(d):
    return _r([d['x'], d['y'], d['z']])


def particle_system(t, index_of):
    m = t['InitialModule']
    ps = {'duration': _r(t['lengthInSec']), 'loop': bool(t['looping']), 'prewarm': bool(t['prewarm']),
          'delay': curve(t['startDelay']), 'speed': _r(t['simulationSpeed']), 'space': t['moveWithTransform'],
          'scaling': t['scalingMode'], 'stop_action': t['stopAction'],
          'main': {'life': curve(m['startLifetime']), 'speed': curve(m['startSpeed']), 'color': gradient(m['startColor']),
                   'size': curve(m['startSize']), 'size_y': curve(m['startSizeY']), 'size_z': curve(m['startSizeZ']),
                   'size3d': bool(m['size3D']), 'rot_x': curve(m['startRotationX']), 'rot_y': curve(m['startRotationY']),
                   'rot': curve(m['startRotation']), 'rot3d': bool(m['rotation3D']),
                   'flip_rot': _r(m['randomizeRotationDirection']), 'max': m['maxNumParticles'],
                   'gravity': curve(m['gravityModifier'])}}
    sh = t['ShapeModule']
    if sh['enabled']:
        ps['shape'] = {'type': sh['type'], 'angle': _r(sh['angle']), 'length': _r(sh['length']),
                       'box_thickness': _v3(sh['boxThickness']), 'radius_thickness': _r(sh['radiusThickness']),
                       'donut': _r(sh['donutRadius']), 'pos': _v3(sh['m_Position']), 'rot': _v3(sh['m_Rotation']),
                       'scale': _v3(sh['m_Scale']), 'radius': _r(sh['radius']['value']), 'radius_mode': sh['radius']['mode'],
                       'radius_spread': _r(sh['radius']['spread']), 'arc': _r(sh['arc']['value']), 'arc_mode': sh['arc']['mode'],
                       'arc_spread': _r(sh['arc']['spread']), 'arc_speed': curve(sh['arc']['speed']),
                       'random_dir': _r(sh['randomDirectionAmount']), 'spherical_dir': _r(sh['sphericalDirectionAmount']),
                       'random_pos': _r(sh['randomPositionAmount']), 'align': bool(sh['alignToDirection'])}
    em = t['EmissionModule']
    if em['enabled']:
        ps['emission'] = {'rate': curve(em['rateOverTime']), 'rate_dist': curve(em['rateOverDistance']),
                          'bursts': [{'t': _r(b['time']), 'count': curve(b['countCurve']), 'cycles': b['cycleCount'],
                                      'interval': _r(b['repeatInterval']), 'p': _r(b['probability'])}
                                     for b in em['m_Bursts'][:em['m_BurstCount']]]}
    else:
        ps['emission'] = None
    md = t['SizeModule']
    if md['enabled']:
        ps['size_life'] = {'x': curve(md['curve']), 'y': curve(md['y']), 'z': curve(md['z']), 'sep': bool(md['separateAxes'])}
    md = t['RotationModule']
    if md['enabled']:
        ps['rot_life'] = {'x': curve(md['x']), 'y': curve(md['y']), 'z': curve(md['curve']), 'sep': bool(md['separateAxes'])}
    md = t['ColorModule']
    if md['enabled']:
        ps['color_life'] = gradient(md['gradient'])
    md = t['UVModule']
    if md['enabled'] and md['mode'] == 0:
        ps['uv'] = {'tiles': [md['tilesX'], md['tilesY']], 'anim': md['animationType'], 'row': md['rowIndex'],
                    'row_mode': md['rowMode'], 'cycles': _r(md['cycles']), 'frame': curve(md['frameOverTime']),
                    'start': curve(md['startFrame']), 'time_mode': md['timeMode'], 'fps': _r(md['fps']),
                    'flip': [_r(md['flipU']), _r(md['flipV'])]}
    md = t['VelocityModule']
    if md['enabled']:
        ps['vel'] = {k: curve(md[k]) for k in ('x', 'y', 'z', 'orbitalX', 'orbitalY', 'orbitalZ', 'orbitalOffsetX',
                                                'orbitalOffsetY', 'orbitalOffsetZ', 'radial', 'speedModifier')}
        ps['vel']['world'] = bool(md['inWorldSpace'])
    md = t['InheritVelocityModule']
    if md['enabled']:
        ps['inherit'] = {'mode': md['m_Mode'], 'curve': curve(md['m_Curve'])}
    md = t['ForceModule']
    if md['enabled']:
        ps['force'] = {'x': curve(md['x']), 'y': curve(md['y']), 'z': curve(md['z']), 'world': bool(md['inWorldSpace']),
                       'random': bool(md['randomizePerFrame'])}
    md = t['ClampVelocityModule']
    if md['enabled']:
        ps['limit'] = {'x': curve(md['x']), 'y': curve(md['y']), 'z': curve(md['z']), 'mag': curve(md['magnitude']),
                       'sep': bool(md['separateAxis']), 'world': bool(md['inWorldSpace']), 'dampen': _r(md['dampen']),
                       'drag': curve(md['drag']), 'drag_size': bool(md['multiplyDragByParticleSize']),
                       'drag_vel': bool(md['multiplyDragByParticleVelocity'])}
    md = t['NoiseModule']
    if md['enabled']:
        ps['noise'] = {'strength': curve(md['strength']), 'strength_y': curve(md['strengthY']),
                       'strength_z': curve(md['strengthZ']), 'sep': bool(md['separateAxes']),
                       'frequency': _r(md['frequency']), 'damping': bool(md['damping']), 'octaves': md['octaves'],
                       'octave_mul': _r(md['octaveMultiplier']), 'octave_scale': _r(md['octaveScale']),
                       'scroll': curve(md['scrollSpeed']), 'pos': curve(md['positionAmount']),
                       'rot': curve(md['rotationAmount']), 'size': curve(md['sizeAmount']), 'quality': md['quality']}
    md = t['SizeBySpeedModule']
    if md['enabled']:
        ps['size_speed'] = {'x': curve(md['curve']), 'y': curve(md['y']), 'z': curve(md['z']),
                            'sep': bool(md['separateAxes']), 'range': [_r(md['range']['x']), _r(md['range']['y'])]}
    md = t['RotationBySpeedModule']
    if md['enabled']:
        ps['rot_speed'] = {'x': curve(md['x']), 'y': curve(md['y']), 'z': curve(md['curve']),
                           'sep': bool(md['separateAxes']), 'range': [_r(md['range']['x']), _r(md['range']['y'])]}
    md = t['SubModule']
    if md['enabled']:
        subs = [{'node': index_of.get(s['emitter']['m_PathID']), 'type': s['type'], 'props': s['properties'],
                 'p': _r(s['emitProbability'])} for s in md['subEmitters']]
        ps['subs'] = [s for s in subs if s['node'] is not None]
    md = t['CustomDataModule']
    if md['enabled']:
        cd = {}
        for i in (0, 1):
            if md[f'mode{i}'] == 2:
                cd[f'c{i}'] = gradient(md[f'color{i}'])
            elif md[f'mode{i}'] == 1:
                cd[f'v{i}'] = [curve(md[f'vector{i}_{k}']) for k in range(md[f'vectorComponentCount{i}'])]
        ps['custom'] = cd
    return ps


# shader keyword -> VioletParticle colour / alpha modes
COLOR_EXPR = {'C0': 0, 'C0MULCOMBORGB': 1, 'C0MULCOMBORGBPLUSC1MULONEMINUSCOMBORGB': 2, 'C0MULCOMBORGBPLUSC1': 3}
ALPHA_EXPR = {'COMBOAMULA0': 0, 'COMBOAMULA0MULA1': 0, 'COMBOAMINUSONEMINUSA0MULTWO': 1,
              'CLAMPCOMBOAMINUSA0MULFOURMULA1': 4, 'COMBOAMINUSA0MULA1': 4}


def material(t, mb, tex):
    """The effect material (Nintendo-style colour combiner) as the parameters VioletParticle takes."""
    kw = set((t.get('m_ShaderKeywords') or '').split())
    sp = t['m_SavedProperties']
    fl = dict(sp['m_Floats'])
    cols = dict(sp['m_Colors'])
    texs = dict(sp['m_TexEnvs'])

    def key(prefix, default):
        return next((k[len(prefix):] for k in kw if k.startswith(prefix)), default)
    cexpr = COLOR_EXPR.get(key('_COLORTEXTUREEXPRESSION_', 'C0MULCOMBORGB'), 1)
    aexpr = ALPHA_EXPR.get(key('_ALPHAEXPRESSION_', 'COMBOAMULA0'), 0)
    if key('_ALPHAEXPRESSION_', '')=='COMBOAMINUSA0MULA1':aexpr=5
    if key('_ALPHAEXPRESSION_', '')=='COMBOAMULA0MULA1':aexpr=6
    out = {'color_mode': cexpr, 'alpha_mode': aexpr, 'src': int(fl.get('_SrcColor', 5)),
           'dst': int(fl.get('_DestColor', 10)), 'cull': int(fl.get('_Cull', 0)),
           'color_scale': _r(fl.get('_ColorScale', 1.0)), 'mesh': '_MESHENABLED_ON' in kw}
    mul = cols.get('_MulColor', {'r': 1, 'g': 1, 'b': 1, 'a': 1})
    if mb is not None:
        m2 = mb.get('_MulColor', {'r': 1, 'g': 1, 'b': 1, 'a': 1})
        mul = {ch: mul[ch] * m2[ch] for ch in 'rgba'}
    out['mul'] = _r([mul[ch] for ch in 'rgba'])
    out['render_state']={'display_side':{0:0,1:2,2:1}.get(int(fl.get('_Cull',0)),0),
        'depth_test':int(fl.get('_ZTest',4))!=0,'depth_func':max(0,int(fl.get('_ZTest',4))-1),
        'depth_mask':int(fl.get('_ZWrite',0)),
        'alpha_test':int(fl.get('_AlphaTest',0))!=0,'alpha_func':max(0,int(fl.get('_AlphaTest',0))-1),
        'alpha_threshold':fl.get('_AlphaRef',0),
        'blend_enable':not(out['src']==1 and out['dst']==0)}
    out['surface']={'soft':1 if '_SOFTPARTICLESENABLED_ON' in kw else 0,
        'soft_range':[fl.get('_SoftParticlesNearFadeDistance',0),fl.get('_SoftParticlesFarFadeDistance',1)],
        'refraction':int(fl.get('_Distortion',0))==1,
        'distortion_mode':int(fl.get('_Distortion',0)),
        'distortion_strength':[fl.get('_DistortionStrengthX',0),fl.get('_DistortionStrengthY',0)],
        'distortion_curve':[[cols.get('_DistortionAnim'+str(i),{}).get('r',i/3),cols.get('_DistortionAnim'+str(i),{}).get('g',1)] for i in range(4)],
        'fresnel':fl.get('_FresnelAlphaType',0),
        'fresnel_range':[cols.get('_FresnelAlphaRef',{}).get('r',0),cols.get('_FresnelAlphaRef',{}).get('g',1)]}

    def channels(prefix,values):return [values[key(f'_{prefix}TEXTURE{i}CHANNEL_',next(iter(values)))] for i in range(3)]
    def blends(prefix):return [{'MUL':0,'PLUS':1,'MINUS':2}.get(key(f'_{prefix}TEXTURE{i}BLENDTYPE_','MUL'),0) for i in (1,2)]+[0]
    out['combiner']={'color_channels':channels('COLOR',{'RGB':0,'ONE':1,'ONEMINUSRGB':2,'ALPHA':3,'ONEMINUSALPHA':4}),
                     'alpha_channels':channels('ALPHA',{'ALPHA':0,'R':1,'ONE':2,'ONEMINUSALPHA':3}),
                     'color_blends':blends('COLOR'),'alpha_blends':blends('ALPHA')}
    for prefix,stem,values in [('COLOR','color',{'RGB':0,'ONE':1,'ONEMINUSRGB':2,'ALPHA':3,'ONEMINUSALPHA':4}),('ALPHA','alpha',{'ALPHA':0,'R':1,'ONE':2,'ONEMINUSALPHA':3,'ONEMINUSR':4})]:
        out['combiner'][stem+'_channels'].append(values[key(f'_{prefix}PRIMITIVECHANNEL_',next(iter(values)))])
        out['combiner'][stem+'_blends'][2]={'MUL':0,'PLUS':1,'MINUS':2}.get(key(f'_{prefix}PRIMITIVEEXPRESSION_','MUL'),0)
    for slot, name in ((0, '_Texture0'), (1, '_Texture1'), (2,'_Texture2')):
        te = texs.get(name)
        if te is None or not te['m_Texture']['m_PathID']:
            continue
        info = tex(te['m_Texture']['m_PathID'])
        if info is None:
            continue
        sc = [te['m_Scale']['x'], te['m_Scale']['y']]
        if mb is not None and slot < len(mb.get('_UvScale0', [])) and (mb['_UvScale0'][slot]['x'] or mb['_UvScale0'][slot]['y']):
            sc = [sc[0] * mb['_UvScale0'][slot]['x'], sc[1] * mb['_UvScale0'][slot]['y']]
        d = {'name': info[0], 'wrap': info[1], 'scale': _r(sc), 'offset': _r([te['m_Offset']['x'], te['m_Offset']['y']])}
        if mb is not None and slot < len(mb.get('_UvScroll0', [])):
            s = mb['_UvScroll0'][slot]
            d['offset'] = _r([d['offset'][0]+s['x'],d['offset'][1]+s['y']])
            speed=mb.get('_UvScroll1',[{}]*3)[slot]
            d['scroll'] = _r([speed.get('x',0),speed.get('y',0)])
        if slot>0 and not any(k.startswith('_MULTITEXTUREMODE_TEX'+str(n)) for k in kw for n in range(slot+1,4)):
            continue
        d['uv_channel']=int(key('_MULTITEXTUREMODE_','TEX1_0').split('_')[-1][slot])
        d['sphere_map']=bool(fl.get(f'_SphereMapEnabled_Tex{slot}',0))
        if mb is not None and slot<len(mb.get('_UvRotation',[])):
            rotation=mb['_UvRotation'][slot]
            d['rotation']=_r(rotation['x']);d['rotation_speed']=_r(rotation['z'])
        if mb is not None and slot<len(mb.get('_UvScale1',[])):
            delta=mb['_UvScale1'][slot];d['scale_speed']=_r([delta['x'],delta['y']])
        out[f'tex{slot}'] = d
    return out


class TextureStore:
    """Writes each texture once into a shared folder; exact greyscale ones as 8-bit grey PNGs."""

    def __init__(self, folder):
        self.folder = Path(folder)
        self.folder.mkdir(parents=True, exist_ok=True)
        self.saved = {}   # (bundle, path id) -> (name, wrap)
        self.hashes = {}  # saved name -> hash

    def save(self, key, obj):
        if key in self.saved:
            return self.saved[key]
        import numpy as np
        from PIL import Image
        t = obj.read()
        img = np.array(t.image.convert('RGBA'))
        grey = bool((img[..., 0] == img[..., 1]).all() and (img[..., 0] == img[..., 2]).all() and (img[..., 0] == img[..., 3]).all())
        px = img[..., 0] if grey else img
        h = hashlib.sha1(px.tobytes() + str(px.shape).encode()).hexdigest()[:10]
        name = t.m_Name
        if self.hashes.get(name, h) != h:
            name = f'{name}_{h[:6]}'
        if name not in self.hashes:
            self.hashes[name] = h
            Image.fromarray(px).save(self.folder / f'{name}.png', optimize=True)
        ts = t.m_TextureSettings
        self.saved[key] = (name, [ts.m_WrapU, ts.m_WrapV])
        return self.saved[key]


_COMMON = None


def _common():
    global _COMMON
    if _COMMON is None:
        import UnityPy
        _COMMON = {o.path_id: o for o in UnityPy.load(str(COMMON)).objects if o.type.name == 'Texture2D'}
    return _COMMON


def export_prefab(name, store):
    """One effect prefab -> {name, nodes: [...], meshes: [...]}; nodes are in hierarchy order, parents first."""
    import UnityPy
    env = UnityPy.load(str(PREFAB_DIR / name))
    objs = {o.path_id: o for o in env.objects}
    gos = {pid: o.read_typetree() for pid, o in objs.items() if o.type.name == 'GameObject'}
    comps = {pid: {objs[c['component']['m_PathID']].type.name: c['component']['m_PathID']
                   for c in g['m_Component'] if c['component']['m_PathID'] in objs} for pid, g in gos.items()}
    trs = {pid: o.read_typetree() for pid, o in objs.items() if o.type.name == 'Transform'}
    roots = [pid for pid, t in trs.items() if not t['m_Father']['m_PathID']]
    order, parent = [], {}

    def walk(tid, p):
        parent[tid] = p
        order.append(tid)
        for c in trs[tid]['m_Children']:
            walk(c['m_PathID'], tid)
    for r in roots:
        walk(r, None)
    go_index = {trs[tid]['m_GameObject']['m_PathID']: i for i, tid in enumerate(order)}
    ps_index = {comps[trs[tid]['m_GameObject']['m_PathID']].get('ParticleSystem'): i for i, tid in enumerate(order)}

    def tex(pid):
        o = objs.get(pid)
        if o is not None and o.type.name == 'Texture2D':
            return store.save((name, pid), o)
        o = _common().get(pid)
        return store.save(('common', pid), o) if o is not None else None

    meshes, mesh_index = [], {}
    nodes = []
    for tid in order:
        t = trs[tid]
        gid = t['m_GameObject']['m_PathID']
        g = gos[gid]
        c = comps[gid]
        nd = {'name': g['m_Name'], 'parent': order.index(parent[tid]) if parent[tid] is not None else -1,
              'pos': _v3(t['m_LocalPosition']), 'rot': _r([t['m_LocalRotation'][k] for k in 'xyzw']),
              'scale': _v3(t['m_LocalScale']), 'active': bool(g['m_IsActive'])}
        if 'ParticleSystem' in c:
            nd['ps'] = particle_system(objs[c['ParticleSystem']].read_typetree(), ps_index)
        if 'ParticleSystemRenderer' in c:
            r = objs[c['ParticleSystemRenderer']].read_typetree()
            mb = objs[c['MonoBehaviour']].read_typetree() if 'MonoBehaviour' in c else None
            mats = [objs.get(m['m_PathID']) for m in r['m_Materials']]
            rd = {'enabled': bool(r['m_Enabled']), 'mode': r['m_RenderMode'], 'sort': r['m_SortMode'],
                  'min_size': _r(r['m_MinParticleSize']), 'max_size': _r(r['m_MaxParticleSize']),
                  'cam_vel': _r(r['m_CameraVelocityScale']), 'vel_scale': _r(r['m_VelocityScale']),
                  'len_scale': _r(r['m_LengthScale']), 'fudge': _r(r['m_SortingFudge']),
                  'normal_dir': _r(r['m_NormalDirection']), 'align': r['m_RenderAlignment'],
                  'pivot': _v3(r['m_Pivot']), 'flip': _v3(r['m_Flip']), 'order': r['m_SortingOrder'],
                  'priority': r.get('m_RendererPriority', 0)}
            if mats and mats[0] is not None and mats[0].type.name == 'Material':
                mt = mats[0].read_typetree()
                rd['mat'] = material(mt, mb, tex)
                rd['queue'] = mt.get('m_CustomRenderQueue', -1)
            mid = r['m_Mesh']['m_PathID']
            if mid and mid in objs and objs[mid].type.name == 'Mesh':
                if mid not in mesh_index:
                    mesh_index[mid] = len(meshes)
                    meshes.append(mesh_json(objs[mid].read()))
                rd['mesh'] = mesh_index[mid]
            nd['r'] = rd
        nodes.append(nd)
    return {'name': name, 'nodes': nodes, 'meshes': meshes}


def mesh_json(mesh):
    from UnityPy.helpers.MeshHelper import MeshHandler
    h = MeshHandler(mesh)
    h.process()
    tris = [i for sub in h.get_triangles() for tri in sub for i in tri]
    out = {'name': mesh.m_Name, 'v': _r([c for v in h.m_Vertices for c in v[:3]], 5), 'i': tris}
    if h.m_Normals:
        out['n'] = _r([c for v in h.m_Normals for c in v[:3]], 4)
    if h.m_UV0:
        out['uv'] = _r([c for v in h.m_UV0 for c in v[:2]], 5)
    if h.m_UV1:
        out['uv1'] = _r([c for v in h.m_UV1 for c in v[:2]], 5)
    if h.m_Colors:
        # MeshHandler exposes UNorm8 bytes as 0..255; Unity colours are 0..1.
        divisor=255.0 if mesh.m_VertexData.m_Channels[3].format==2 else 1.0
        out['col'] = _r([c/divisor for v in h.m_Colors for c in v[:4]], 5)
    return out

# ---------------------------------------------------------------- moves


def export_all(out, only=None):
    out = Path(out)
    if out.exists():  # keep .meta files so Unity keeps the assets' GUIDs
        for f in out.rglob('*'):
            if f.is_file() and f.suffix != '.meta':
                f.unlink()
    store = TextureStore(out / 'textures')
    (out / 'prefabs').mkdir(parents=True, exist_ok=True)
    index, missing, prefabs = {}, {}, {}
    seqs = {}
    for move, num in SOURCE.items():
        if only and move not in only:
            continue
        if num not in seqs:
            seqs[num] = sequence(num)
        groups = seqs[num]
        attack, hit = timing(groups)
        fx = []
        for g in effect_groups(groups):
            if g['kind'] == 'particle':
                file = next(c['args']['file'] for c in g['cmds'] if c['cmd'] == 'ParticleCreate')
                if SKIP_PREFAB.search(file) or not (PREFAB_DIR / file).exists():
                    continue
                if file not in prefabs:
                    prefabs[file] = export_prefab(file, store)
                    (out / 'prefabs' / f'{file}.json').write_text(json.dumps(prefabs[file], separators=(',', ':')))
            fx.append(g)
        if not any(g['kind'] == 'particle' for g in fx):
            missing[move] = num
            continue
        folder = f'MOVE_{move}'
        (out / folder).mkdir(parents=True, exist_ok=True)
        mv = {'move': f'MOVE_{move}', 'source': f'ew{num:03d}', 'fps': FPS, 'attack': attack, 'hit': hit, 'groups': fx}
        (out / folder / 'move.json').write_text(json.dumps(mv, indent=1))
        index[f'MOVE_{move}'] = folder
    (out / 'index.json').write_text(json.dumps({'moves': index, 'missing': missing, 'locators': LOCATORS,
                                                'prefabs': sorted(prefabs)}, indent=1))
    return index, missing


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('moves', nargs='*', help='MOVE names (without MOVE_); default all')
    ap.add_argument('--out', default=str(Path(__file__).resolve().parents[2] / 'Assets' / 'Generated' / 'MoveFx' / 'bdsp'))
    a = ap.parse_args(argv)
    index, missing = export_all(a.out, set(a.moves) or None)
    print(f'{len(index)} moves exported, without effect: {missing}')


if __name__ == '__main__':
    main()
