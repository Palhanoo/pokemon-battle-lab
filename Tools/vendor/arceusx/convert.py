"""Convert a Legends: Arceus model (trmdl + trmsh/trmbf + trskl + trmtr + bntx + tranm) to a .glb.

Usage: convert.py <pack.gfpak> [<more packs>...] --model NAME.trmdl --out OUT.glb [--anims all|none|a,b,c]
Files are looked up by name across all given packs (the player is split over several packs).
"""
import argparse, io, json, math, os, re, struct, sys
import numpy as np
from PIL import Image

sys.path.insert(0, __import__('os').path.dirname(__file__))
from gfpak import GFPak
from fb import T
import bntx

# ---------------------------------------------------------------- file lookup

class Files:
    def __init__(self, packs):
        self.packs = [GFPak(p) for p in packs]
        self.names = {}
        for pk in self.packs:  # recover real names from references inside the pack's files
            for i in range(len(pk)):
                for m in re.findall(rb'[\w.-]{3,}\.(?:tr[a-z]{2,8}|bntx)\b', pk.get(i)):
                    self.names.setdefault(m.decode(), None)

    def get(self, name):
        for pk in self.packs:
            b = pk.by_name(name)
            if b is not None:
                return b
        return None

    def all_named(self, suffix):
        return sorted(n for n in self.names if n.endswith(suffix) and self.get(n) is not None)

# ---------------------------------------------------------------- math

def euler_xyz_to_quat(x, y, z):
    """Blender-style XYZ Euler (R = Rz*Ry*Rx) -> quaternion (x, y, z, w)."""
    cx, sx = math.cos(x / 2), math.sin(x / 2)
    cy, sy = math.cos(y / 2), math.sin(y / 2)
    cz, sz = math.cos(z / 2), math.sin(z / 2)
    w = cz * cy * cx + sz * sy * sx
    qx = cz * cy * sx - sz * sy * cx
    qy = cz * sy * cx + sz * cy * sx
    qz = sz * cy * cx - cz * sy * sx
    return [qx, qy, qz, w]

def quat_to_mat(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])

def trs(t, q, s):
    m = np.eye(4)
    m[:3, :3] = quat_to_mat(q) * np.array(s)
    m[:3, 3] = t
    return m

def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)

def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)

def unpack_quat(x, y, z):
    pack = (z << 32) | (y << 16) | x
    vals = [(pack >> s & 0x7FFF) * (math.pi / 2 / 0x7FFF) - math.pi / 4 for s in (3, 18, 33)]
    vals.insert(pack & 3, math.sqrt(max(1.0 - sum(v * v for v in vals), 0.0)))
    if pack & 4:
        vals = [-v for v in vals]
    return vals  # x, y, z, w

# ---------------------------------------------------------------- glb builder

class GLB:
    def __init__(self):
        self.j = {'asset': {'version': '2.0', 'generator': 'arceusx'}, 'scene': 0, 'scenes': [{'nodes': []}],
                  'nodes': [], 'meshes': [], 'materials': [], 'textures': [], 'images': [], 'samplers': [{}],
                  'accessors': [], 'bufferViews': [], 'skins': [], 'animations': []}
        self.bin = bytearray()

    def view(self, data, target=None):
        while len(self.bin) % 4:
            self.bin.append(0)
        v = {'buffer': 0, 'byteOffset': len(self.bin), 'byteLength': len(data)}
        if target:
            v['target'] = target
        self.bin += data
        self.j['bufferViews'].append(v)
        return len(self.j['bufferViews']) - 1

    def accessor(self, arr, typ, ctype, target=None, minmax=False, normalized=False):
        arr = np.ascontiguousarray(arr)
        a = {'bufferView': self.view(arr.tobytes(), target), 'componentType': ctype,
             'count': int(arr.shape[0]), 'type': typ}
        if normalized:
            a['normalized'] = True
        if minmax:
            flat = arr.reshape(arr.shape[0], -1)
            a['min'] = [float(v) for v in flat.min(0)]
            a['max'] = [float(v) for v in flat.max(0)]
        self.j['accessors'].append(a)
        return len(self.j['accessors']) - 1

    def image(self, rgba, name):
        buf = io.BytesIO()
        Image.fromarray(rgba).save(buf, 'PNG', optimize=False, compress_level=6)
        self.j['images'].append({'bufferView': self.view(buf.getvalue()), 'mimeType': 'image/png', 'name': name})
        self.j['textures'].append({'source': len(self.j['images']) - 1, 'sampler': 0})
        return len(self.j['textures']) - 1

    def data(self):
        for k in [k for k, v in self.j.items() if v == [] and k not in ('nodes',)]:
            del self.j[k]
        while len(self.bin) % 4:
            self.bin.append(0)
        self.j['buffers'] = [{'byteLength': len(self.bin)}]
        js = json.dumps(self.j, separators=(',', ':')).encode()
        js += b' ' * (-len(js) % 4)
        return (struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(self.bin))
                + struct.pack('<II', len(js), 0x4E4F534A) + js
                + struct.pack('<II', len(self.bin), 0x004E4942) + bytes(self.bin))

    def write(self, path):
        data = self.data()
        try:  # identical bytes stay untouched so Unity does not reimport them
            if os.path.getsize(path) == len(data) and open(path, 'rb').read() == data:
                return
        except OSError:
            pass
        with open(path, 'wb') as f:
            f.write(data)

# ---------------------------------------------------------------- vertex decoding

FMT = {51: ('<f4', 3, False), 43: ('<f2', 4, False), 48: ('<f4', 2, False), 54: ('<f4', 4, False),
       22: ('u1', 4, False), 20: ('u1', 4, True), 39: ('<u2', 4, True), 36: ('<u4', 1, False), 37: ('<i4', 1, False)}
SEM = {1: 'POSITION', 2: 'NORMAL', 3: 'TANGENT', 4: 'BINORMAL', 5: 'COLOR', 6: 'TEXCOORD', 7: 'JOINTS', 8: 'WEIGHTS'}

def decode_vertices(shape, vbufs):
    attrs = {}
    for layout, buf in zip(shape.tables(3), vbufs):
        stride = layout.tables(1)[0].scalar(0)
        data = np.frombuffer(buf, np.uint8)
        n = len(buf) // stride
        rows = data[:n * stride].reshape(n, stride)
        for e in layout.tables(0):
            sem, idx, fmt, off = e.scalar(1), e.scalar(2), e.scalar(3), e.scalar(4)
            dt, cnt, norm = FMT[fmt]
            size = np.dtype(dt).itemsize * cnt
            v = rows[:, off:off + size].copy().view(dt).reshape(n, cnt).astype(np.float32)
            if norm:
                v /= np.iinfo(np.dtype(dt)).max
            attrs[(SEM.get(sem, str(sem)), idx)] = v
    return attrs

# ---------------------------------------------------------------- materials

# Engine-only helper geometry: ground decal volumes and shadow casters, not visible surfaces.
PROXY_SHADERS = {'Decal', 'Shadow'}

# Z-A closed-lid shell skinned to the eyelid bones; it sits over the open eye in every pose and the game only
# shows it to blink or sleep, so the open eye is kept.
EYELID_SHELL = '_eyelid_mesh'

def tex_by_binding(mp):
    return {tp.string(0): tp.string(1) for tp in mp.tables(2)}

def params(mp):
    f4 = {f.string(0): f.struct(1, '4f') for f in mp.tables(7)}
    f1 = {f.string(0): f.scalar(1, 'f') for f in mp.tables(4)}
    sh = {v.string(0): v.string(1) for s in mp.tables(1) for v in s.tables(1)}
    return f4, f1, sh

def _wrap(t, mode):
    if mode in (6, 7):  # mirror
        t = t % 2.0
        return np.where(t > 1.0, 2.0 - t, t)
    return t % 1.0  # repeat; clamp too, since UVs sit in per-material UDIM tiles

def sample(img, h, w, scale=(1, 1), offset=(0, 0), wrap=(0, 0)):
    """Nearest-sample img on an h*w grid of UVs transformed by scale/offset and the sampler's wrap modes."""
    ih, iw = img.shape[:2]
    v, u = np.mgrid[0:h, 0:w]
    u = _wrap((u + 0.5) / w * scale[0] + offset[0], wrap[0])
    v = _wrap((v + 0.5) / h * scale[1] + offset[1], wrap[1])
    return img[(v * ih).astype(int).clip(0, ih - 1), (u * iw).astype(int).clip(0, iw - 1)]

class MaterialBaker:
    def __init__(self, files, glb, tints=None, guess_cutouts=True):
        self.files, self.glb, self.cache, self.guess_cutouts = files, glb, {}, guess_cutouts
        self.tints = tints or {}  # (material, layer) -> linear rgb, for colours the game sets at runtime

    def tex(self, name):
        if name not in self.cache:
            b = self.files.get(name)
            self.cache[name] = None if b is None else bntx.read(b)[0]
        return self.cache[name]

    @staticmethod
    def hidden(mp):
        """Eyelid shells (Z-A, EyelidType Lower/Upper): a dark layer the shader only reveals when an expression
        closes the eye. Drawn solid it z-fights the open eye as black patches, so like proxies they are left out."""
        sh = params(mp)[2]
        if 'IkCharacter' not in {s.string(0) for s in mp.tables(1)}:
            return False  # Scarlet/Violet 'Eye' materials with EyelidType ARE the eye
        return sh.get('EyelidType', 'None') != 'None' and any(b.endswith('EyelidColorMap') for b in tex_by_binding(mp))

    def build(self, mp):
        name = mp.string(0)
        texs = tex_by_binding(mp)
        slots = {tp.string(0): tp.scalar(2) for tp in mp.tables(2)}
        samplers = [(sm.scalar(9), sm.scalar(10)) for sm in mp.tables(3)]
        wrap = lambda b: samplers[slots[b]] if b in slots and slots[b] < len(samplers) else (0, 0)
        f4, f1, sh = params(mp)
        alb = self.tex(texs['BaseColorMap']) if 'BaseColorMap' in texs else None
        lym = self.tex(texs['LayerMaskMap']) if 'LayerMaskMap' in texs else None
        layers = [f4.get(f'BaseColorLayer{i}') for i in range(1, 5)]
        has_layers = lym is not None and any(layers)
        sc = f4.get('UVScaleOffset', (1, 1, 0, 0))
        hw = [(t.shape[0] * max(1, round(sc[1])), t.shape[1] * max(1, round(sc[0]))) for t in (alb, lym) if t is not None]
        nrm_t = self.tex(texs['NormalMap']) if 'NormalMap' in texs else None
        if nrm_t is not None:
            hw.append(nrm_t.shape[:2])
        h, w = max(hw) if hw else (4, 4)
        h, w = min(h, 2048), min(w, 2048)

        if alb is not None:
            a = sample(alb, h, w, sc[:2], sc[2:], wrap('BaseColorMap')).astype(np.float32) / 255
        else:
            base = f4.get('BaseColor', (1, 1, 1, 1))
            a = np.ones((h, w, 4), np.float32) * np.array([*lin_to_srgb(np.array(base[:3])), 1.0])
        col = srgb_to_lin(a[..., :3])
        alpha = a[..., 3]
        if not has_layers and 'BaseColor' in f4 and alb is not None:
            col = col * np.array(f4['BaseColor'][:3], np.float32)  # flat tint some shaders (gems, fresnel) use
        if has_layers:
            m = sample(lym, h, w, sc[:2], sc[2:], wrap('LayerMaskMap')).astype(np.float32) / 255
            for i, lc in enumerate(layers):
                if lc is None:
                    continue
                lc = self.tints.get((name, i + 1), lc)
                k = (m[..., i] * f1.get(f'LayerMaskScale{i + 1}', 1.0)).clip(0, 1)[..., None]
                col = col * ((1 - k) + np.array(lc[:3], np.float32) * k)  # layers tint the albedo
        if 'AOMap' in texs and self.tex(texs['AOMap']) is not None:
            ao = sample(self.tex(texs['AOMap']), h, w).astype(np.float32)[..., 0:1] / 255
            col *= 0.35 + 0.65 * ao  # bake a soft AO so it reads without a custom shader
        # Pokémon eyes draw their shine from a separate mask the shader adds in; bake it in as a white catchlight.
        if 'eye' in name.lower() and 'BaseColorMap' in texs and self.tex(texs['BaseColorMap'].replace('_alb', '_msk')) is not None:
            hl = sample(self.tex(texs['BaseColorMap'].replace('_alb', '_msk')), h, w).astype(np.float32)[..., 0:1] / 255
            col = col * (1 - hl) + hl
        rgba = np.concatenate([lin_to_srgb(col) * 255, alpha[..., None] * 255], -1).round().astype(np.uint8)

        mat = {'name': name, 'pbrMetallicRoughness': {
            'baseColorTexture': {'index': self.glb.image(rgba, name + '_col')},
            'metallicFactor': float(f1.get('Metallic', 0.0)),
            'roughnessFactor': float(max(f1.get('Roughness', 0.7), 0.65))}}
        nrm = self.tex(texs['NormalMap']) if 'NormalMap' in texs else None
        if nrm is not None and sh.get('EnableNormalMap', 'True') == 'True':
            xy = nrm[..., :2].astype(np.float32) / 255 * 2 - 1
            z = np.sqrt(np.clip(1 - (xy ** 2).sum(-1), 0, 1))
            n = np.concatenate([xy, z[..., None]], -1) * 0.5 + 0.5
            n8 = np.concatenate([(n * 255).round(), np.full(z.shape + (1,), 255)], -1).astype(np.uint8)
            mat['normalTexture'] = {'index': self.glb.image(n8, name + '_nrm')}
        # Eyes: the lens is a glossy shell over the iris; drawn solid it hides the eye, so it stays a faint sheen.
        lens = 'lens' in name.lower()
        # Lashes, brows and other cut-outs carry their shape in the alpha even when the shader doesn't flag it.
        # Pokémon flag their own cut-outs; an opaque body's alpha can hold other data (Wurmple's is half zero).
        cutout = self.guess_cutouts and float(np.mean(alpha < 0.5)) > 0.05
        if lens:
            mat['alphaMode'] = 'BLEND'
            mat['pbrMetallicRoughness']['baseColorFactor'] = [1, 1, 1, 0.12]
        elif sh.get('EnableAlphaTest') == 'True' or (cutout and mp.string(15) != 'Transparent'):
            mat['alphaMode'], mat['alphaCutoff'] = 'MASK', 0.5
        elif mp.string(15) == 'Transparent':
            mat['alphaMode'] = 'BLEND'
        if mat.get('alphaMode') in ('MASK', 'BLEND'):
            mat['doubleSided'] = True
        return mat

# ---------------------------------------------------------------- animation

def track_keys(t, typ_slot, val_slot, rot):
    """Returns (frames, values) for a union track field, or None."""
    kind = t.scalar(typ_slot, 'B', 0)
    tr = t.table(val_slot)
    if not kind or tr is None:
        return None
    if rot:
        if kind == 1:
            co = [tr.struct(0, '3H')]
            frames = [0]
        else:
            co = tr.structs(0 if kind == 2 else 1, '3H')
            frames = list(range(len(co))) if kind == 2 else (tr.scalars(0, 'H' if kind == 3 else 'B'))
        vals = [unpack_quat(*c) for c in co]
        for i in range(1, len(vals)):  # keep hemisphere continuity for slerp
            if sum(a * b for a, b in zip(vals[i], vals[i - 1])) < 0:
                vals[i] = [-v for v in vals[i]]
    else:
        if kind == 1:
            vals, frames = [tr.struct(0, '3f')], [0]
        else:
            vals = tr.structs(0 if kind == 2 else 1, '3f')
            frames = list(range(len(vals))) if kind == 2 else tr.scalars(0, 'H' if kind == 3 else 'B')
    if not vals:
        return None
    return frames[:len(vals)], vals

def add_animation(glb, name, data, node_by_name, keep_root_motion=False):
    a = T(data)
    info = a.table(0)
    rate = max(info.scalar(2), 1) if info else 30
    out = {'name': name, 'channels': [], 'samplers': []}
    for t in a.table(1).tables(0):
        bone = t.string(0)
        node = node_by_name.get(bone)
        if node is None:
            continue
        for path, ts, vs, rot in (('scale', 1, 2, False), ('rotation', 3, 4, True), ('translation', 5, 6, False)):
            if path == 'translation' and bone.lower() == 'origin' and not keep_root_motion:
                continue
            k = track_keys(t, ts, vs, rot)
            if k is None:
                continue
            frames, vals = k
            # Keep one key per frame (the last): glTF needs strictly increasing times.
            frames = np.asarray(frames)
            keep = np.append(frames[1:] != frames[:-1], True) if len(frames) else np.zeros(0, bool)
            if not keep.all():
                frames = frames[keep]; vals = [v for v, kp in zip(vals, keep) if kp]
            times = np.array(frames, np.float32) / rate
            inp = glb.accessor(times, 'SCALAR', 5126, minmax=True)
            outp = glb.accessor(np.array(vals, np.float32), 'VEC4' if rot else 'VEC3', 5126)
            out['samplers'].append({'input': inp, 'output': outp, 'interpolation': 'LINEAR'})
            out['channels'].append({'sampler': len(out['samplers']) - 1, 'target': {'node': node, 'path': path}})
    if out['channels']:
        glb.j['animations'].append(out)

# ---------------------------------------------------------------- main

def add_skeleton(glb, sk, node_by_name, rig_nodes, parents):
    """Append a trskl's nodes. Part skeletons hang their roots off an earlier skeleton's bone by name."""
    base = len(glb.j['nodes'])
    rig_off = sk.scalar(4)
    tnodes = sk.tables(1)
    for i, n in enumerate(tnodes):
        tf = n.table(1)
        s, r, t = tf.struct(0, '3f'), tf.struct(1, '3f'), tf.struct(2, '3f')
        glb.j['nodes'].append({'name': n.string(0), 'translation': list(t),
                               'rotation': euler_xyz_to_quat(*r), 'scale': list(s)})
        p = n.scalar(4, 'i', -1)
        if 0 <= p < len(tnodes) and p != i:
            parents.append(base + p)
        else:
            parents.append(node_by_name.get(n.string(6), -1) if n.string(6) else -1)
        rig = n.scalar(5, 'i', -1)
        if rig >= 0:
            rig_nodes.setdefault(rig + rig_off, base + i)
    for i, n in enumerate(tnodes):
        node_by_name.setdefault(n.string(0), base + i)

def clip_name(tranm):
    """A Pokémon clip's name without its model and id: pm0723_00_00_00000_defaultwait01_loop.tranm -> defaultwait01_loop.
    The 1xxxx ids are the in-water set (the same names again, swimming), so they get a swim_ prefix and never shadow the land clips."""
    m = re.match(r'^.*?_(\d{5})_(.*)\.tranm$', tranm)
    if not m:
        return re.sub(r'^.*?_\d{5}_', '', tranm[:-6])
    return ('swim_' if m.group(1)[0] == '1' else '') + m.group(2)


def convert(files, model_names, out_path, anim_filter, base_skel=None, anim_prefix=None, tints=None, guess_cutouts=True):
    glb = GLB()
    mdls = [T(files.get(m)) for m in model_names]

    # skeleton(s) -> nodes
    node_by_name, rig_nodes, parents = {}, {}, []
    skel_names = ([base_skel] if base_skel else []) + [m.table(2).string(0) for m in mdls if m.table(2)]
    for sn in dict.fromkeys(skel_names):
        if files.get(sn):
            add_skeleton(glb, T(files.get(sn)), node_by_name, rig_nodes, parents)
    # Z-A stores effect sockets separately from the deforming skeleton. Bind
    # their named parents before calculating matrices; never add them as skin joints.
    for model in model_names:
        prefix = model.rsplit('.', 1)[0]
        choices = sorted(n for n in files.all_named('.trskl') if n.startswith(prefix+'_') and n.endswith('_eff.trskl'))
        locator = choices[0] if choices else None
        if locator and files.get(locator):
            add_skeleton(glb, T(files.get(locator)), node_by_name, {}, parents)
    root_nodes = [i for i, p in enumerate(parents) if p < 0]
    for i, p in enumerate(parents):
        if p >= 0:
            glb.j['nodes'][p].setdefault('children', []).append(i)
    local = [trs(nd['translation'], nd['rotation'], nd['scale']) for nd in glb.j['nodes']]
    worlds = [None] * len(local)
    def world(i):
        if worlds[i] is None:
            worlds[i] = (world(parents[i]) if parents[i] >= 0 else np.eye(4)) @ local[i]
        return worlds[i]
    for i in range(len(local)):
        world(i)

    skin = None
    if rig_nodes:
        n_j = max(rig_nodes) + 1
        joints = [rig_nodes.get(k, root_nodes[0]) for k in range(n_j)]
        ibm = np.array([np.linalg.inv(worlds[j]).T for j in joints], np.float32)  # column-major
        glb.j['skins'].append({'joints': joints, 'inverseBindMatrices': glb.accessor(ibm, 'MAT4', 5126),
                               'skeleton': root_nodes[0]})
        skin = 0

    # materials, keyed per model so parts can reuse names like "body"
    baker = MaterialBaker(files, glb, tints, guess_cutouts)
    mat_index, proxies, ik_mats = {}, set(), set()
    for mi, mdl in enumerate(mdls):
        for mname in mdl.strings(3):
            mb = files.get(mname)
            if mb is None:
                continue
            for mp in T(mb).tables(1):
                if 'IkCharacter' in {s.string(0) for s in mp.tables(1)}:
                    ik_mats.add((mi, mp.string(0)))
                if {s.string(0) for s in mp.tables(1)} & PROXY_SHADERS or MaterialBaker.hidden(mp):
                    proxies.add((mi, mp.string(0)))
                    continue
                if (mi, mp.string(0)) not in mat_index:
                    glb.j['materials'].append(baker.build(mp))
                    mat_index[(mi, mp.string(0))] = len(glb.j['materials']) - 1

    # meshes (LOD0 only: first trmsh)
    mesh_roots = []
    for mi, mesh_name in [(mi, [x.string(0) for x in m.tables(1)][0]) for mi, m in enumerate(mdls)]:
        msh = T(files.get(mesh_name))
        buf = T(files.get(msh.string(2)))
        mbufs = buf.tables(1)
        for si, shape in enumerate(msh.tables(1)):
            if (shape.string(12) or '').endswith(EYELID_SHELL) and all((mi, sm.string(3)) in ik_mats for sm in shape.tables(4)):
                continue
            mb = mbufs[si]
            vbufs = [v.bytes(0) for v in mb.tables(1)]
            ibuf = mb.tables(0)[0].bytes(0)
            ifmt = shape.scalar(2)
            idx = np.frombuffer(ibuf, {0: np.uint8, 1: np.uint16, 2: np.uint32}.get(ifmt, np.uint16))
            at = decode_vertices(shape, vbufs)
            pos = at[('POSITION', 0)][:, :3]
            prim_attrs = {'POSITION': glb.accessor(pos.astype(np.float32), 'VEC3', 5126, 34962, minmax=True)}
            if ('NORMAL', 0) in at:
                nrm = at[('NORMAL', 0)][:, :3]
                nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-8)
                prim_attrs['NORMAL'] = glb.accessor(nrm.astype(np.float32), 'VEC3', 5126, 34962)
            for k in (0, 1):
                if ('TEXCOORD', k) in at:
                    uv = at[('TEXCOORD', k)][:, :2].astype(np.float32)
                    uv[:, 1] = 1.0 - uv[:, 1]  # Trinity UVs are bottom-left origin, glTF is top-left
                    prim_attrs[f'TEXCOORD_{k}'] = glb.accessor(uv, 'VEC2', 5126, 34962)
            skinned = skin is not None and ('JOINTS', 0) in at and ('WEIGHTS', 0) in at
            if skinned:
                j = at[('JOINTS', 0)].astype(np.uint16)
                w = at[('WEIGHTS', 0)].astype(np.float32)
                j[j >= len(glb.j['skins'][0]['joints'])] = 0
                s = w.sum(1, keepdims=True)
                w = np.where(s > 0, w / np.maximum(s, 1e-8), np.array([1, 0, 0, 0], np.float32))
                prim_attrs['JOINTS_0'] = glb.accessor(j, 'VEC4', 5123, 34962)
                prim_attrs['WEIGHTS_0'] = glb.accessor(w, 'VEC4', 5126, 34962)
            prims = []
            for sm in shape.tables(4):
                cnt, off, mat = sm.scalar(0), sm.scalar(1), sm.string(3)
                if (mi, mat) in proxies:
                    continue
                ix = idx[off:off + cnt].astype(np.uint32)
                p = {'attributes': prim_attrs, 'indices': glb.accessor(ix, 'SCALAR', 5125, 34963), 'mode': 4}
                if (mi, mat) in mat_index:
                    p['material'] = mat_index[(mi, mat)]
                prims.append(p)
            if not prims:  # every submesh was left out (proxies, eyelid shells): an empty mesh is invalid glTF
                continue
            nm = shape.string(12) or f'{mesh_name[:-6]}_{si}'
            glb.j['meshes'].append({'name': nm, 'primitives': prims})
            node = {'name': nm, 'mesh': len(glb.j['meshes']) - 1}
            if skinned:
                node['skin'] = skin
            glb.j['nodes'].append(node)
            mesh_roots.append(len(glb.j['nodes']) - 1)

    # wrap everything under one root so the importer gives a single prefab
    root = {'name': model_names[0].rsplit('.', 1)[0], 'children': root_nodes + mesh_roots}
    glb.j['nodes'].append(root)
    glb.j['scenes'][0]['nodes'] = [len(glb.j['nodes']) - 1]

    # animations
    if anim_filter != 'none' and node_by_name:
        prefix = anim_prefix or model_names[0].rsplit('.', 1)[0]
        wanted = None if anim_filter == 'all' else anim_filter.split(',')
        for an in files.all_named('.tranm'):
            if not an.startswith(prefix):
                continue
            short = re.sub(r'^\d{5}_', '', an[len(prefix):-6].strip('_')) if anim_prefix else clip_name(an)
            if wanted is not None and short not in wanted:
                continue
            add_animation(glb, short, files.get(an), node_by_name)

    glb.write(out_path)
    pos_all = [np.array(glb.j['accessors'][p['attributes']['POSITION']]['max']) for m in glb.j['meshes'] for p in m['primitives']]
    mins = [np.array(glb.j['accessors'][p['attributes']['POSITION']]['min']) for m in glb.j['meshes'] for p in m['primitives']]
    lo, hi = np.min(mins, 0), np.max(pos_all, 0)
    print(f'{out_path}: {len(glb.j["meshes"])} meshes, {len(glb.j.get("materials", []))} materials, '
          f'{len(glb.j.get("animations", []))} anims, bind-pose bbox {np.round(lo, 3)} .. {np.round(hi, 3)}')

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('packs', nargs='+')
    ap.add_argument('--model', required=True, action='append', help='trmdl name; repeat for multi-part characters')
    ap.add_argument('--base-skel', help='shared skeleton the parts attach to (player characters)')
    ap.add_argument('--anim-prefix', help='tranm name prefix, defaults to the first model name')
    ap.add_argument('--out', required=True)
    ap.add_argument('--anims', default='all')
    ap.add_argument('--tint', action='append', default=[], help='material:layer=r,g,b in sRGB 0-255')
    a = ap.parse_args()
    tints = {}
    for t in a.tint:
        k, v = t.split('=')
        mat, layer = k.rsplit(':', 1)
        tints[(mat, int(layer))] = tuple(srgb_to_lin(np.array([float(c) / 255 for c in v.split(',')])))
    convert(Files(a.packs), a.model, a.out, a.anims, a.base_skel, a.anim_prefix, tints)

if __name__ == '__main__':
    main()
