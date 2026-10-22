"""Convert Pokémon Brilliant Diamond (Unity asset bundles) models to .glb with named clips.

Usage: python -m hoenngen.bdsp OUT_DIR NAME:DEX [NAME:DEX ...]
Writes OUT_DIR/NAME.glb and OUT_DIR/NAME.clips.json for each species.

A species is a battle prefab bundle (transforms, skinned renderer, materials) plus shared bundles with
its mesh, textures and Mecanim clips. Unity is left-handed; everything is mirrored on X into glTF space.
Model units are already metres at the Pokédex size.
"""
import json
import math
import os
import struct
import sys
import zlib

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'arceusx'))
from convert import GLB, lin_to_srgb  # noqa: E402

from hoenngen.species import pick_clips  # noqa: E402
from hoenngen.sword import _sample, clip_names  # noqa: E402

ROMFS = os.environ.get('BDSP_ROMFS', '/mnt/d/pokemon/bdsp-work/romfs')
AA = os.path.join(ROMFS, 'Data', 'StreamingAssets', 'AssetAssistant')
PM_DIR = os.path.join(AA, 'Pokemon Database', 'pokemons')
FPS = 30
MIRROR = np.diag([-1.0, 1.0, 1.0, 1.0])


def model_base(dex):
    """'pmDDDD_FF' of the species' default form (form 00, or the lowest numbered one BDSP ships)."""
    common = os.path.join(PM_DIR, 'common')
    forms = sorted(f[7:9] for f in os.listdir(common)
                   if f.startswith(f'pm{dex:04d}_') and len(f) == 9 and os.path.exists(os.path.join(PM_DIR, 'battle', f + '_00')))
    if not forms:
        raise FileNotFoundError(f'BDSP has no model for dex {dex}')
    return f'pm{dex:04d}_{"00" if "00" in forms else forms[0]}'


def bundle_paths(base):
    cands = [('battle', base + '_00'), ('common', base + '_00'), ('common', base),
             ('battle/animations', base), ('field/animations', base)]
    return [p for p in (os.path.join(PM_DIR, a, b) for a, b in cands) if os.path.exists(p)]


def load(base):
    import UnityPy
    env = UnityPy.load(*bundle_paths(base))
    return {o.path_id: o for o in env.objects}

# ---------------------------------------------------------------- math


def unity_trs(t, q, s):
    x, y, z, w = q
    r = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    m = np.eye(4)
    m[:3, :3] = r * np.array(s)
    m[:3, 3] = t
    return m


def flip_pos(v):
    v = np.array(v, np.float32)
    v[..., 0] *= -1
    return v


def flip_quat(q):
    q = np.array(q, np.float32)
    q[..., 1] *= -1
    q[..., 2] *= -1
    return q


def euler_to_quat(e):
    """Unity Euler degrees (Z, then X, then Y) -> quaternion (x, y, z, w), arrays of shape (n, 3)."""
    x, y, z = (np.radians(e[:, i]) / 2 for i in range(3))
    cx, sx, cy, sy, cz, sz = np.cos(x), np.sin(x), np.cos(y), np.sin(y), np.cos(z), np.sin(z)
    return np.stack([cy * sx * cz + sy * cx * sz, sy * cx * cz - cy * sx * sz,
                     cy * cx * sz - sy * sx * cz, cy * cx * cz + sy * sx * sz], -1)


def v3(d):
    return [d['x'], d['y'], d['z']]


def v4(d):
    return [d['x'], d['y'], d['z'], d['w']]

# ---------------------------------------------------------------- hierarchy


class Prefab:
    def __init__(self, objs, base):
        self.objs = objs
        self.name = base + '_00'
        tree = {}
        for pid, o in objs.items():
            if o.type.name == 'GameObject':
                tree[pid] = o.read_typetree()
        root_go = next(pid for pid, g in tree.items() if g['m_Name'] == self.name)
        self.go = tree
        self.comps = {pid: [c['component']['m_PathID'] for c in g['m_Component']] for pid, g in tree.items()}
        root_tr = next(c for c in self.comps[root_go] if c in objs and objs[c].type.name == 'Transform')
        self.nodes, self.index, self.parent, self.paths = [], {}, [], []
        self._walk(root_tr, -1, '')

    def _walk(self, tr_id, parent, path):
        t = self.objs[tr_id].read_typetree()
        name = self.go[t['m_GameObject']['m_PathID']]['m_Name']
        i = len(self.nodes)
        self.index[tr_id] = i
        self.nodes.append({'tr': t, 'go': t['m_GameObject']['m_PathID'], 'name': name})
        self.parent.append(parent)
        self.paths.append(path)
        for c in t['m_Children']:
            self._walk(c['m_PathID'], i, (path + '/' if path else '') + self.go[self.objs[c['m_PathID']].read_typetree()['m_GameObject']['m_PathID']]['m_Name'])

    def components(self, node, kind):
        return [self.objs[c] for c in self.comps[self.nodes[node]['go']] if c in self.objs and self.objs[c].type.name == kind]

    def world(self):
        out = []
        for i, n in enumerate(self.nodes):
            t = n['tr']
            m = unity_trs(v3(t['m_LocalPosition']), v4(t['m_LocalRotation']), v3(t['m_LocalScale']))
            out.append(out[self.parent[i]] @ m if self.parent[i] >= 0 else m)
        return out

# ---------------------------------------------------------------- materials


class Baker:
    def __init__(self, objs, glb):
        self.objs, self.glb, self.cache = objs, glb, {}

    def tex(self, pid):
        if pid not in self.cache:
            o = self.objs.get(pid)
            if o is None:
                self.cache[pid] = None
            else:
                t = o.read()
                ts = t.m_TextureSettings
                self.cache[pid] = (np.array(t.image.convert('RGBA')), (ts.m_WrapU, ts.m_WrapV), t.m_Name)
        return self.cache[pid]

    def build(self, mat_obj, uv_range):
        t = mat_obj.read_typetree()
        name = t['m_Name']
        sp = t['m_SavedProperties']
        texs = {k: v for k, v in sp['m_TexEnvs']}
        fl = {k: v for k, v in sp['m_Floats']}
        cols = {k: v for k, v in sp['m_Colors']}
        if texs.get('_Mask0Tex', {}).get('m_Texture', {}).get('m_PathID'):
            return self.flame(name, texs, fl)
        layers = []
        for slot, pre, enable in (('_Col0Tex', 'Color', None), ('_L1Col0Tex', 'Layer1', '_Layer1Enable')):
            te = texs.get(slot)
            if te is None or not te['m_Texture']['m_PathID'] or (enable and not fl.get(enable)):
                continue
            tx = self.tex(te['m_Texture']['m_PathID'])
            if tx is None:
                continue
            img, wrap, _ = tx
            sc = (te['m_Scale']['x'], te['m_Scale']['y'])
            off = (te['m_Offset']['x'] + fl.get(f'_{pre}BaseU', 0), te['m_Offset']['y'] + fl.get(f'_{pre}BaseV', 0))
            layers.append((img, sc, off, wrap))
        if not layers:
            c = cols.get('_ConstantColor0') or cols.get('_Color') or {'r': 1, 'g': 1, 'b': 1}
            rgb = lin_to_srgb(np.array([c['r'], c['g'], c['b']], np.float32)) * 255
            rgba = np.broadcast_to(np.concatenate([rgb, [255]]).round().astype(np.uint8), (4, 4, 4)).copy()
        else:
            h = max(int(img.shape[0] * max(1.0, abs(sc[1]))) for img, sc, _, _ in layers)
            w = max(int(img.shape[1] * max(1.0, abs(sc[0]))) for img, sc, _, _ in layers)
            h, w = min(h, 2048), min(w, 2048)
            base = _sample(layers[0][0], h, w, *layers[0][1:]).astype(np.float32)
            lay = None
            if len(layers) > 1:
                lay = _sample(layers[1][0], h, w, *layers[1][1:]).astype(np.float32)
                # the layer shows where the base is transparent, or everywhere over a black base (lava bodies)
                override = fl.get('_Layer1OverLerpValue', 0) >= 0.5 and float(base[..., :3].mean()) < 20
                a = np.zeros_like(base[..., 3:4]) if override else base[..., 3:4] / 255
                base = np.concatenate([base[..., :3] * a + lay[..., :3] * (1 - a), np.full_like(a, 255)], -1)
            glow = self.glow(texs, fl, base, lay, h, w)
            rgba = base.round().clip(0, 255).astype(np.uint8)
        kw = t.get('m_ShaderKeywords') or ''
        mat = {'name': name, 'pbrMetallicRoughness': {
            'baseColorTexture': {'index': self.glb.image(rgba, name + '_col')},
            'metallicFactor': 0.0, 'roughnessFactor': 0.75}}
        if layers and glow is not None:
            mat['emissiveTexture'] = {'index': self.glb.image(glow, name + '_emi')}
            mat['emissiveFactor'] = [1.0, 1.0, 1.0]
        holes = float(np.mean(rgba[..., 3] < 128)) > 0.02
        if '_ALPHATEST_ON' in kw or fl.get('_AlphaTestEnable') or holes:
            mat['alphaMode'], mat['alphaCutoff'] = 'MASK', 0.5
            mat['doubleSided'] = True
        elif fl.get('_CullMode', 2) == 0:
            mat['doubleSided'] = True
        return mat

    def glow(self, texs, fl, base, lay, h, w):
        """Self-lit parts (lures, tail orbs, lava): the emission mask's red channel lights them. Where the
        colour texture is left black for the shader to fill, the layer colour or a warm glow stands in.
        Edits base in place; returns the emissive RGB image or None."""
        te = texs.get('_EmissionMaskTex')
        if not fl.get('_EmissionMaskUse') or te is None or not te['m_Texture']['m_PathID']:
            return None
        tx = self.tex(te['m_Texture']['m_PathID'])
        if tx is None:
            return None
        m = _sample(tx[0], h, w, (te['m_Scale']['x'], te['m_Scale']['y']), (0, 0), tx[1])[..., :1].astype(np.float32) / 255
        if float(m.mean()) < 0.01:
            return None
        dark = (base[..., :3].max(-1, keepdims=True) < 20) & (m > 0.5)
        fill = lay[..., :3] if lay is not None else np.broadcast_to(np.array([255, 235, 120], np.float32), base[..., :3].shape)
        base[..., :3] = np.where(dark, fill, base[..., :3])
        return (base[..., :3] * m * 0.8).round().clip(0, 255).astype(np.uint8)

    def core_colours(self):
        """Fill colours of the model's flame/smoke core material: (base, layer, lerp mask)."""
        for o in self.objs.values():
            if o.type.name != 'Material':
                continue
            t = o.read_typetree()
            sp = t['m_SavedProperties']
            cols = {k: v for k, v in sp['m_Colors']}
            texs = {k: v for k, v in sp['m_TexEnvs']}
            if '_BaseColor' in cols and '_LayerColor' in cols:
                lerp = texs.get('_LerpTex', {}).get('m_Texture', {}).get('m_PathID')
                return ([cols['_BaseColor'][c] for c in 'rgb'], [cols['_LayerColor'][c] for c in 'rgb'],
                        self.tex(lerp) if lerp else None)
        return [1.0, 0.45, 0.1], [1.0, 0.85, 0.2], None

    def flame(self, name, texs, fl):
        """Stencil-masked flames and smoke: the mask shapes become a glowing cut-out in the core's colours."""
        base, layer, lerp = self.core_colours()
        alpha = None
        for i in (0, 1):
            te = texs.get(f'_Mask{i}Tex')
            tx = self.tex(te['m_Texture']['m_PathID']) if te and te['m_Texture']['m_PathID'] else None
            if tx is None:
                continue
            img, wrap, _ = tx
            sc = (fl.get(f'_Mask{i}UVScaleU', 1.0), fl.get(f'_Mask{i}UVScaleV', 1.0))
            off = (fl.get(f'_Mask{i}UVTranslateU', 0.0), fl.get(f'_Mask{i}UVTranslateV', 0.0))
            m = _sample(img, 256, 256, sc, off, wrap)[..., 0].astype(np.float32) / 255
            alpha = m if alpha is None else np.clip(alpha + m, 0, 1)
        if alpha is None:
            alpha = np.ones((256, 256), np.float32)
        if lerp is not None:
            k = _sample(lerp[0], 256, 256, (1, 1), (0, 0), lerp[1])[..., :1].astype(np.float32) / 255
        else:
            k = np.linspace(0, 1, 256, dtype=np.float32)[:, None, None] * np.ones((1, 256, 1), np.float32)
        col = np.array(base, np.float32) * (1 - k) + np.array(layer, np.float32) * k
        rgba = np.concatenate([lin_to_srgb(col) * 255, alpha[..., None] * 255], -1).round().clip(0, 255).astype(np.uint8)
        tex = self.glb.image(rgba, name + '_col')
        return {'name': name, 'pbrMetallicRoughness': {'baseColorTexture': {'index': tex}, 'metallicFactor': 0.0,
                                                       'roughnessFactor': 1.0},
                'emissiveTexture': {'index': tex}, 'emissiveFactor': [1.0, 1.0, 1.0],
                'alphaMode': 'MASK', 'alphaCutoff': 0.5, 'doubleSided': True}

# ---------------------------------------------------------------- Mecanim clips


def _streamed_keys(words):
    """StreamedClip -> {curve index: [(time, a, b, c, d)]}; value at t is ((a*dt + b)*dt + c)*dt + d."""
    raw = struct.pack(f'<{len(words)}I', *words)
    keys, off = {}, 0
    while off + 8 <= len(raw):
        time, n = struct.unpack_from('<fi', raw, off)
        off += 8
        for _ in range(n):
            idx, a, b, c, d = struct.unpack_from('<i4f', raw, off)
            off += 20
            if math.isfinite(time):
                keys.setdefault(idx, []).append((time, a, b, c, d))
    return keys


def _eval_streamed(keys, times):
    k = np.array(keys, np.float64)
    i = np.clip(np.searchsorted(k[:, 0], times, side='right') - 1, 0, len(k) - 1)
    dt = np.maximum(times - k[i, 0], 0)
    return ((k[i, 1] * dt + k[i, 2]) * dt + k[i, 3]) * dt + k[i, 4]


def clip_curves(clip):
    """Sample every curve of a Mecanim clip at FPS. Returns (times, [n_curves][n_times])."""
    mc = clip['m_MuscleClip']
    data = mc['m_Clip']['data']
    start, stop = mc['m_StartTime'], mc['m_StopTime']
    n = max(int(round((stop - start) * FPS)) + 1, 2)
    times = start + np.arange(n) / FPS
    times[-1] = min(times[-1], stop) if n > 2 else stop
    sc = data['m_StreamedClip']
    dense = data['m_DenseClip']
    const = data['m_ConstantClip']['data']
    n_str, n_den = sc['curveCount'], dense['m_CurveCount']
    curves = [None] * (n_str + n_den + len(const))
    for idx, ks in _streamed_keys(sc['data']).items():
        if 0 <= idx < n_str:
            curves[idx] = _eval_streamed(ks, times)
    if n_den:
        arr = np.array(dense['m_SampleArray'], np.float64).reshape(dense['m_FrameCount'], n_den)
        ft = dense['m_BeginTime'] + np.arange(dense['m_FrameCount']) / dense['m_SampleRate']
        for c in range(n_den):
            curves[n_str + c] = np.interp(times, ft, arr[:, c])
    for c, v in enumerate(const):
        curves[n_str + n_den + c] = np.full(n, v)
    return times - start, curves


DIMS = {1: 3, 2: 4, 3: 3, 4: 3}  # position, quaternion, scale, euler


def clip_value(clip, path_hash, attribute, component, default):
    """First-frame value of one bound transform channel, or default when the clip doesn't animate it."""
    _, curves = clip_curves(clip)
    ci = 0
    for b in clip['m_ClipBindingConstant']['genericBindings']:
        dim = DIMS.get(b['attribute'], 1) if b['typeID'] == 4 else 1
        if b['typeID'] == 4 and b['path'] == path_hash and b['attribute'] == attribute:
            v = curves[ci + component] if ci + component < len(curves) else None
            return float(v[0]) if v is not None else default
        ci += dim
    return default


def add_clip(glb, name, clip, node_of_hash, root_nodes):
    times, curves = clip_curves(clip)
    out = {'name': name, 'channels': [], 'samplers': []}
    inp = None
    ci = 0
    for b in clip['m_ClipBindingConstant']['genericBindings']:
        dim = DIMS.get(b['attribute'], 1) if b['typeID'] == 4 else 1
        vals = curves[ci:ci + dim]
        ci += dim
        node = node_of_hash.get(b['path'])
        if b['typeID'] != 4 or node is None or any(v is None for v in vals) or len(vals) < dim:
            continue
        v = np.stack(vals, -1)
        if b['attribute'] == 1:
            if node in root_nodes:
                continue  # root motion stays with the game object
            path, v, typ = 'translation', flip_pos(v), 'VEC3'
        elif b['attribute'] in (2, 4):
            q = euler_to_quat(v) if b['attribute'] == 4 else v
            q = q / np.maximum(np.linalg.norm(q, axis=1, keepdims=True), 1e-8)
            path, v, typ = 'rotation', flip_quat(q), 'VEC4'
        elif b['attribute'] == 3:
            path, typ = 'scale', 'VEC3'
        else:
            continue
        if inp is None:
            inp = glb.accessor(times.astype(np.float32), 'SCALAR', 5126, minmax=True)
        outp = glb.accessor(np.ascontiguousarray(v, np.float32), typ, 5126)
        out['samplers'].append({'input': inp, 'output': outp, 'interpolation': 'LINEAR'})
        out['channels'].append({'sampler': len(out['samplers']) - 1, 'target': {'node': node, 'path': path}})
    if not out['channels']:
        return False
    glb.j['animations'].append(out)
    return True

# ---------------------------------------------------------------- conversion


def _mesh_arrays(mesh):
    from UnityPy.helpers.MeshHelper import MeshHandler
    h = MeshHandler(mesh)
    h.process()
    pos = np.array(h.m_Vertices, np.float32).reshape(-1, 3)
    nrm = np.array(h.m_Normals, np.float32).reshape(len(pos), -1)[:, :3] if h.m_Normals else None
    uv = np.array(h.m_UV0, np.float32).reshape(len(pos), 2) if h.m_UV0 else None
    bi = np.array(h.m_BoneIndices, np.int64).reshape(len(pos), -1)[:, :4] if h.m_BoneIndices else None
    bw = np.array(h.m_BoneWeights, np.float32).reshape(len(pos), -1)[:, :4] if h.m_BoneWeights else None
    return pos, nrm, uv, bi, bw, h.get_triangles()


def convert_model(dex, out_glb, anims=True):
    """Convert the species' default-form battle model. Returns the list of clip names written."""
    base = model_base(dex)
    objs = load(base)
    pf = Prefab(objs, base)
    glb = GLB()
    glb.j['asset']['generator'] = 'hoenngen.bdsp'
    for i, n in enumerate(pf.nodes):
        t = n['tr']
        nd = {'name': n['name'], 'translation': [float(x) for x in flip_pos(v3(t['m_LocalPosition']))],
              'rotation': [float(x) for x in flip_quat(v4(t['m_LocalRotation']))],
              'scale': [float(x) for x in v3(t['m_LocalScale'])]}
        glb.j['nodes'].append(nd)
    for i, p in enumerate(pf.parent):
        if p >= 0:
            glb.j['nodes'][p].setdefault('children', []).append(i)

    node_of_hash = {zlib.crc32(p.encode()): i for i, p in enumerate(pf.paths) if p}
    prefix = pf.name + '_'
    clips = {}
    for o in objs.values():
        if o.type.name == 'AnimationClip':
            nm = o.peek_name()
            if nm.startswith(prefix):
                clips.setdefault(nm[len(prefix):], o)
    names = clip_names(clips)
    idle = next((src for dst in ('defaultwait01_loop', 'battlewait01_loop') for src, d in names.items() if d == dst), None)
    idle_clip = clips[idle].read_typetree() if idle else None

    baker = Baker(objs, glb)
    # CusAnimVis_<renderer> nodes switch optional parts (vines, curled-up body, long tongue): an x of 0 hides
    # the part, -1 or 1 shows it. The idle clip's value wins over the prefab's.
    hidden = set()
    for i, n in enumerate(pf.nodes):
        if n['name'].startswith('CusAnimVis_'):
            x = n['tr']['m_LocalPosition']['x']
            if idle_clip is not None:
                x = clip_value(idle_clip, zlib.crc32(pf.paths[i].encode()), 1, 0, x)
            if abs(x * 100) < 0.5:
                hidden.add(n['name'][len('CusAnimVis_'):])
    body_shader = main_shader(objs)
    active = []
    for i, n in enumerate(pf.nodes):
        active.append(bool(pf.go[n['go']].get('m_IsActive', True)) and (pf.parent[i] < 0 or active[pf.parent[i]]))
    for ni in range(len(pf.nodes)):
        for smr in pf.components(ni, 'SkinnedMeshRenderer'):
            st = smr.read_typetree()
            if not st.get('m_Enabled', 1) or not active[ni] or pf.nodes[ni]['name'] in hidden:
                continue
            mesh_obj = objs.get(st['m_Mesh']['m_PathID'])
            if mesh_obj is None:
                continue
            mesh = mesh_obj.read()
            pos, nrm, uv, bi, bw, tris = _mesh_arrays(mesh)
            bones = [pf.index.get(b['m_PathID'], 0) for b in st['m_Bones']]
            ibm = np.array([(MIRROR @ np.array(bp_matrix(bp)) @ MIRROR).T for bp in mesh.m_BindPose], np.float32)
            if not bones or len(ibm) != len(bones):
                bones = bones or [0]
                w = pf.world()
                ibm = np.array([(MIRROR @ np.linalg.inv(w[b]) @ MIRROR).T for b in bones], np.float32)
            glb.j['skins'].append({'joints': bones, 'inverseBindMatrices': glb.accessor(ibm, 'MAT4', 5126)})
            attrs = _attrs(glb, pos, nrm, uv, bi, bw, len(bones))
            prims = []
            for si, tri in enumerate(tris):
                if not tri:
                    continue
                ix = np.array(tri, np.uint32)[:, [0, 2, 1]].reshape(-1)  # mirrored, so flip winding
                mats = st['m_Materials']
                prim = {'attributes': attrs, 'indices': glb.accessor(ix, 'SCALAR', 5125, 34963), 'mode': 4}
                mo = objs.get(mats[min(si, len(mats) - 1)]['m_PathID']) if mats else None
                if mo is None or mo.type.name != 'Material':
                    continue  # depth-only shells
                if not textured(mo, body_shader):
                    continue  # flame/smoke cores show only through their masks, which are drawn instead
                if mo is not None:
                    rng = None
                    if uv is not None:
                        u = uv[np.unique(ix)]
                        rng = (u.min(0), u.max(0))
                    glb.j['materials'].append(baker.build(mo, rng))
                    prim['material'] = len(glb.j['materials']) - 1
                prims.append(prim)
            if not prims:
                continue
            glb.j['meshes'].append({'name': pf.nodes[ni]['name'], 'primitives': prims})
            glb.j['nodes'][ni]['mesh'] = len(glb.j['meshes']) - 1
            glb.j['nodes'][ni]['skin'] = len(glb.j['skins']) - 1
    glb.j['scenes'][0]['nodes'] = [0]

    written = []
    if anims:
        roots = {i for i, n in enumerate(pf.nodes) if n['name'].lower() == 'origin'}
        for src, dst in names.items():
            if add_clip(glb, dst, clips[src].read_typetree(), node_of_hash, roots):
                written.append(dst)
    os.makedirs(os.path.dirname(os.path.abspath(out_glb)), exist_ok=True)
    glb.write(out_glb)
    return written


def main_shader(objs):
    """Shader used by most of the model's textured materials (the Pokémon body shader)."""
    from collections import Counter
    c = Counter()
    for o in objs.values():
        if o.type.name == 'Material':
            t = o.read_typetree()
            if any(k == '_Col0Tex' and v['m_Texture']['m_PathID'] for k, v in t['m_SavedProperties']['m_TexEnvs']):
                c[t['m_Shader']['m_PathID']] += 1
    return c.most_common(1)[0][0] if c else None


def textured(mat_obj, body_shader):
    t = mat_obj.read_typetree()
    has = {k for k, v in t['m_SavedProperties']['m_TexEnvs'] if v['m_Texture']['m_PathID']}
    return bool(has & {'_Col0Tex', '_Mask0Tex'}) or t['m_Shader']['m_PathID'] == body_shader


def bp_matrix(bp):
    """Unity Matrix4x4 (eRC fields) -> nested rows."""
    if hasattr(bp, 'e00'):
        return [[getattr(bp, f'e{r}{c}') for c in range(4)] for r in range(4)]
    return [[bp[f'e{r}{c}'] for c in range(4)] for r in range(4)]


def _attrs(glb, pos, nrm, uv, bi, bw, n_joints):
    attrs = {'POSITION': glb.accessor(flip_pos(pos), 'VEC3', 5126, 34962, minmax=True)}
    if nrm is not None:
        n = flip_pos(nrm)
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-8)
        attrs['NORMAL'] = glb.accessor(n, 'VEC3', 5126, 34962)
    if uv is not None:
        u = uv.copy()
        u[:, 1] = 1.0 - u[:, 1]
        attrs['TEXCOORD_0'] = glb.accessor(u, 'VEC2', 5126, 34962)
    if bi is None or bw is None:
        bi = np.zeros((len(pos), 4), np.int64)
        bw = np.tile(np.array([1, 0, 0, 0], np.float32), (len(pos), 1))
    pad = 4 - bi.shape[1]
    if pad > 0:  # fewer than four influences per vertex
        bi = np.pad(bi, ((0, 0), (0, pad)))
        bw = np.pad(bw, ((0, 0), (0, max(0, 4 - bw.shape[1]))))
    j = bi.clip(0, n_joints - 1).astype(np.uint16)
    w = bw.copy()
    w[bi >= n_joints] = 0
    s = w.sum(1, keepdims=True)
    w = np.where(s > 0, w / np.maximum(s, 1e-8), np.array([1, 0, 0, 0], np.float32))
    attrs['JOINTS_0'] = glb.accessor(np.ascontiguousarray(j), 'VEC4', 5123, 34962)
    attrs['WEIGHTS_0'] = glb.accessor(w.astype(np.float32), 'VEC4', 5126, 34962)
    return attrs


def has_model(dex):
    try:
        model_base(dex)
        return True
    except FileNotFoundError:
        return False


def convert_species(dex: int, out_glb: str, fly: str = None) -> dict:
    """Write the species' default-form model to out_glb; returns pick_clips roles for its clips."""
    return pick_clips(convert_model(dex, out_glb), fly)


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    out_dir = argv[0] if argv else '/mnt/d/www/hoenn-scratch/bdsp'
    todo = [(a.split(':')[0], int(a.split(':')[1])) for a in argv[1:]]
    os.makedirs(out_dir, exist_ok=True)
    for name, dex in todo:
        roles = convert_species(dex, os.path.join(out_dir, f'{name}.glb'))
        json.dump(roles, open(os.path.join(out_dir, f'{name}.clips.json'), 'w'))
        missing = [k for k, v in roles.items() if v is None and k != 'fly']
        print(f'{name}: ok' + (f' (no clip for {", ".join(missing)})' if missing else ''))


if __name__ == '__main__':
    main()
