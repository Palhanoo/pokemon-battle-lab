"""Convert Pokémon Sword/Shield models (gfbmdl + gfbanm + bntx in a .gfpak) to .glb with named clips.

Usage: python -m hoenngen.sword OUT_DIR NAME:DEX [NAME:DEX ...]
Writes OUT_DIR/NAME.glb and OUT_DIR/NAME.clips.json for each species.
"""
import json
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'arceusx'))
import bntx  # noqa: E402
from convert import GLB, euler_xyz_to_quat, lin_to_srgb, track_keys, trs  # noqa: E402
from fb import T  # noqa: E402
from gfpak import GFPak  # noqa: E402

from hoenngen.species import pick_clips  # noqa: E402

ROMFS = os.environ.get('SWORD_ROMFS', '/mnt/d/pokemon/sword-work/romfs')
POKEMON_DIR = os.path.join(ROMFS, 'bin', 'archive', 'pokemon')
SCALE = 0.01  # model units are centimetres

# Sword clip -> the clip name pick_clips looks for. First listed source that exists wins.
CLIP_MAP = [
    ('defaultwait01_loop', ['kw01_wait01', 'fi01_wait01', 'fi01_wait02']),  # calm camp/field idle
    ('battlewait01_loop', ['ba10_waitA01']),
    ('walk01_loop', ['fi20_walk01']),
    ('run01_loop', ['fi21_run01']),
    ('notice01', ['cm10_kwwait_bawait01']),  # calm idle -> battle stance
    ('glad01', ['kw32_happyA01', 'kw32_happyB01', 'kw32_happyC01']),
    ('hate01', ['kw30_hate01']),
    ('roar01', ['ba02_roar01']),
    ('sleep01_loop', ['kw21_sleepA01']),
    ('rest01_loop', ['kw20_drowseB01']),
    ('rest01_start', ['kw20_drowseA01']),
    ('rest01_end', ['kw20_drowseC01']),
    ('eat01_start', ['kw50_eatA01']),
    ('eat01_loop', ['kw50_eatB01']),
    ('eat01_end', ['kw50_eatC01']),
    ('attack01', ['ba20_buturi01']),          # physical move
    ('rangeattack01', ['ba21_tokusyu01']),    # special move
    ('damage01', ['ba30_damageS01']),
    ('down01_start', ['ba41_down01']),        # faint
    ('land01', ['ba01_landA01', 'ba01_land01']),
]
SKIP_CLIPS = {'gloop01', 'eye01', 'mouth01'}  # no bone tracks (timeline / face texture swaps)

# gfbmdl vertex attribute data types: (numpy dtype, normalised)
VFMT = {0: ('<f4', False), 1: ('<f2', False), 2: ('i1', True), 3: ('u1', False), 4: ('<i2', True),
        5: ('<u2', False), 6: ('<i4', False), 7: ('<u4', False), 8: ('u1', True)}
POSITION, NORMAL, UV0, GROUP_IDX, GROUP_WEIGHT = 0, 1, 3, 11, 12
WRAP_REPEAT, WRAP_CLAMP, WRAP_MIRROR = 0, 1, 2


def pack_path(dex, form=0):
    base=os.path.join(POKEMON_DIR, f'pm{dex:04d}_{form:02d}.gfpak')
    return base if os.path.exists(base) else os.path.join(POKEMON_DIR, f'pm{dex:04d}_{form:02d}_00.gfpak')


def clip_files(pak, prefix):
    """Animation file names referenced in the pack (gfpak only stores name hashes)."""
    names = set()
    pat = re.compile(rb'(' + re.escape(prefix.encode()) + rb'_\w+?)\.gfbanm')
    for i in range(len(pak)):
        names.update(m.decode() for m in pat.findall(pak.get(i)))
    return {n[len(prefix) + 1:]: n + '.gfbanm' for n in sorted(names) if pak.by_name(n + '.gfbanm') is not None}


def clip_names(sources):
    """Sword clip short names -> output clip names (mapped ones renamed, the rest kept)."""
    out, used = {}, set()
    for dst, cands in CLIP_MAP:
        src = next((c for c in cands if c in sources and c not in used), None)
        if src:
            out[src] = dst
            used.add(src)
    for s in sources:
        if s not in out and s not in SKIP_CLIPS:
            out[s] = s
    return out

# ---------------------------------------------------------------- model parsing


class Model:
    def __init__(self, data):
        self.t = T(data)
        t = self.t
        self.textures = t.strings(2)
        self.materials = t.tables(6)
        self.groups = t.tables(7)
        self.shapes = t.tables(8)
        self.bones = t.tables(9)

    def skin_bones(self):
        """Vertex group indices address the bones that carry no rigid flag, in order."""
        return [i for i, b in enumerate(self.bones) if not b.has(10)]


def decode_shape(shape):
    attrs = [(a.scalar(0), a.scalar(1), a.scalar(2)) for a in shape.tables(1)]
    stride = sum(np.dtype(VFMT[f][0]).itemsize * c for _, f, c in attrs)
    raw = shape.bytes(2)
    n = len(raw) // stride
    rows = np.frombuffer(raw, np.uint8)[:n * stride].reshape(n, stride)
    out, off = {}, 0
    for typ, f, c in attrs:
        dt, norm = VFMT[f]
        size = np.dtype(dt).itemsize * c
        v = rows[:, off:off + size].copy().view(dt).reshape(n, c)
        if norm:
            v = v.astype(np.float32) / np.iinfo(np.dtype(dt)).max
        out.setdefault(typ, v)
        off += size
    return out

# ---------------------------------------------------------------- materials


def _wrap(t, mode):
    if mode == WRAP_MIRROR:
        t = np.mod(t, 2.0)
        return np.where(t > 1.0, 2.0 - t, t)
    if mode == WRAP_REPEAT:
        return np.mod(t, 1.0)
    return np.clip(t, 0.0, 1.0)


def _sample(img, h, w, scale, base, wrap):
    """Sample img over a glTF-space [0,1]^2 grid; the game's UVs have a bottom-left origin."""
    ih, iw = img.shape[:2]
    r, c = np.mgrid[0:h, 0:w]
    u = (c + 0.5) / w
    v = 1.0 - (r + 0.5) / h
    tu = _wrap(u * scale[0] + base[0], wrap[0])
    tv = _wrap(v * scale[1] + base[1], wrap[1])
    return img[((1.0 - tv) * ih).astype(int).clip(0, ih - 1), (tu * iw).astype(int).clip(0, iw - 1)]


def _period_ok(scale, wrap):
    """True when the texture pattern repeats with period 1 in mesh UV space, so it can be baked into one tile."""
    k = abs(scale) * (0.5 if wrap == WRAP_MIRROR else 1.0)
    return k > 0 and abs(k - round(k)) < 1e-4 and round(k) >= 1


class MaterialBaker:
    def __init__(self, pak, model, glb):
        self.pak, self.model, self.glb, self.cache = pak, model, glb, {}

    def tex(self, name):
        if name not in self.cache:
            b = self.pak.by_name(name + '.bntx')
            self.cache[name] = None if b is None else bntx.read(b)[0]
        return self.cache[name]

    def build(self, mt, uv_range):
        name = mt.string(0)
        tex = {}
        for tm in mt.tables(11):
            st = tm.table(2)
            wrap = (st.scalar(1), st.scalar(2)) if st else (0, 0)
            tex[tm.string(0)] = (self.model.textures[tm.scalar(1, 'i')], wrap)
        sw = {s.string(0): bool(s.scalar(1, 'B')) for s in mt.tables(12)}
        val = {s.string(0): s.scalar(1, 'f') for s in mt.tables(13)}
        common = mt.table(20)
        csw = {s.string(0): bool(s.scalar(1, 'B')) for s in common.tables(0)} if common else {}
        col = {s.string(0): s.struct(1, '3f') for s in mt.tables(14)}

        layers = []  # (image, scale, base, wrap)
        cname, cwrap = tex.get('Col0Tex', (None, (0, 0)))
        cimg = self.tex(cname) if cname and sw.get('useColorTex', True) else None
        if cimg is not None:
            layers.append((cimg, (val.get('ColorUVScaleU', 1), val.get('ColorUVScaleV', 1)),
                           (val.get('ColorBaseU', 0) + val.get('ColorUVTranslateU', 0),
                            val.get('ColorBaseV', 0) + val.get('ColorUVTranslateV', 0)), cwrap))
        lname, lwrap = tex.get('LyCol0Tex', (None, (0, 0)))
        limg = self.tex(lname) if lname and sw.get('Layer1Enable') else None
        if limg is not None and cimg is not None:
            layers.append((limg, (val.get('Layer1UVScaleU', 1), val.get('Layer1UVScaleV', 1)),
                           (val.get('Layer1BaseU', 0) + val.get('Layer1UVTranslateU', 0),
                            val.get('Layer1BaseV', 0) + val.get('Layer1UVTranslateV', 0)), lwrap))

        if not layers:
            c = np.array(col.get('ConstantColor0', (1, 1, 1)), np.float32)
            rgba = np.concatenate([lin_to_srgb(c) * 255, [255]]).round().astype(np.uint8)
            rgba = np.broadcast_to(rgba, (4, 4, 4)).copy()
        else:
            inside = [uv_range is None or (uv_range[0][k] >= -1e-3 and uv_range[1][k] <= 1 + 1e-3) for k in (0, 1)]
            for img, sc, _, wr in layers:
                if not all(inside[k] or _period_ok(sc[k], wr[k]) for k in (0, 1)):
                    print(f'  warning: {name}: texture does not tile in mesh space, bake may seam', file=sys.stderr)
            h = max(int(img.shape[0] * max(1.0, abs(sc[1]))) for img, sc, _, _ in layers)
            w = max(int(img.shape[1] * max(1.0, abs(sc[0]))) for img, sc, _, _ in layers)
            h, w = min(h, 2048), min(w, 2048)
            base = _sample(layers[0][0], h, w, *layers[0][1:]).astype(np.float32)
            if len(layers) > 1:
                # Iris layer shows through where the eye texture is transparent.
                lay = _sample(layers[1][0], h, w, *layers[1][1:]).astype(np.float32)
                a = base[..., 3:4] / 255
                base = np.concatenate([base[..., :3] * a + lay[..., :3] * (1 - a), np.full_like(a, 255)], -1)
            rgba = base.round().clip(0, 255).astype(np.uint8)

        mat = {'name': name, 'pbrMetallicRoughness': {
            'baseColorTexture': {'index': self.glb.image(rgba, name + '_col')},
            'metallicFactor': 0.0, 'roughnessFactor': 0.75}}
        holes = float(np.mean(rgba[..., 3] < 128)) > 0.02
        if csw.get('TextureAlphaTestEnable') or csw.get('DiscardEnable') or holes:
            mat['alphaMode'], mat['alphaCutoff'] = 'MASK', 0.5
            mat['doubleSided'] = True
        return mat

# ---------------------------------------------------------------- animation


def add_animation(glb, name, data, node_by_name):
    a = T(data)
    info = a.table(0)
    rate = max(info.scalar(2), 1) if info else 30
    sk = a.table(1)
    if sk is None:
        return False
    out = {'name': name, 'channels': [], 'samplers': []}
    for t in sk.tables(0):
        bone = t.string(0)
        node = node_by_name.get(bone)
        if node is None:
            continue
        for path, ts, vs, rot in (('scale', 1, 2, False), ('rotation', 3, 4, True), ('translation', 5, 6, False)):
            if path == 'translation' and bone.lower() == 'origin':
                continue  # root motion stays with the game object
            k = track_keys(t, ts, vs, rot)
            if k is None:
                continue
            frames, vals = k
            vals = np.array(vals, np.float32)
            if path == 'translation':
                vals = vals * SCALE
            inp = glb.accessor(np.array(frames, np.float32) / rate, 'SCALAR', 5126, minmax=True)
            outp = glb.accessor(vals, 'VEC4' if rot else 'VEC3', 5126)
            out['samplers'].append({'input': inp, 'output': outp, 'interpolation': 'LINEAR'})
            out['channels'].append({'sampler': len(out['samplers']) - 1, 'target': {'node': node, 'path': path}})
    if not out['channels']:
        return False
    glb.j['animations'].append(out)
    return True

# ---------------------------------------------------------------- conversion


def convert_pack(pak_path, model_name, out_glb, anims=True):
    """Convert one gfbmdl inside a pack. Returns the list of clip names written."""
    pak = GFPak(pak_path)
    data = pak.by_name(model_name)
    if data is None:
        raise FileNotFoundError(f'{model_name} not in {pak_path}')
    model = Model(data)
    glb = GLB()
    glb.j['asset']['generator'] = 'hoenngen.sword'

    # skeleton
    parents, node_by_name = [], {}
    for i, b in enumerate(model.bones):
        t = np.array(b.struct(7, '3f') or (0, 0, 0)) * SCALE
        r = b.struct(6, '3f') or (0, 0, 0)
        s = b.struct(5, '3f') or (1, 1, 1)
        glb.j['nodes'].append({'name': b.string(0), 'translation': [float(x) for x in t],
                               'rotation': euler_xyz_to_quat(*r), 'scale': list(s)})
        p = b.scalar(2, 'i', 0)
        parents.append(p if 0 <= p < len(model.bones) and p != i else -1)
        node_by_name.setdefault(b.string(0), i)
    for i, p in enumerate(parents):
        if p >= 0:
            glb.j['nodes'][p].setdefault('children', []).append(i)
    worlds = []
    for i, nd in enumerate(glb.j['nodes']):
        m = trs(nd['translation'], nd['rotation'], nd['scale'])
        worlds.append(worlds[parents[i]] @ m if parents[i] >= 0 else m)  # parents precede children
    roots = [i for i, p in enumerate(parents) if p < 0]

    joints = model.skin_bones()
    ibm = np.array([np.linalg.inv(worlds[j]).T for j in joints], np.float32)
    glb.j['skins'].append({'joints': joints, 'inverseBindMatrices': glb.accessor(ibm, 'MAT4', 5126),
                           'skeleton': roots[0]})

    # geometry
    shape_bone = {g.scalar(1): g.scalar(0) for g in model.groups}
    decoded = [decode_shape(s) for s in model.shapes]
    uv_ranges = {}
    for si, shape in enumerate(model.shapes):
        uv = decoded[si].get(UV0)
        for pg in shape.tables(0):
            if uv is None:
                continue
            ix = np.array(pg.scalars(1, 'H'))
            if len(ix):
                lo, hi = uv[ix, :2].min(0), uv[ix, :2].max(0)
                mi = pg.scalar(0)
                if mi in uv_ranges:
                    lo, hi = np.minimum(lo, uv_ranges[mi][0]), np.maximum(hi, uv_ranges[mi][1])
                uv_ranges[mi] = (lo, hi)
    baker = MaterialBaker(pak, model, glb)
    mat_index = {}
    for mi, mt in enumerate(model.materials):
        glb.j['materials'].append(baker.build(mt, uv_ranges.get(mi)))
        mat_index[mi] = len(glb.j['materials']) - 1

    mesh_nodes = []
    for si, shape in enumerate(model.shapes):
        at = decoded[si]
        pos = at[POSITION][:, :3].astype(np.float32) * SCALE
        n = len(pos)
        attrs = {'POSITION': glb.accessor(pos, 'VEC3', 5126, 34962, minmax=True)}
        if NORMAL in at:
            nrm = at[NORMAL][:, :3].astype(np.float32)
            nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-8)
            attrs['NORMAL'] = glb.accessor(nrm, 'VEC3', 5126, 34962)
        if UV0 in at:
            uv = at[UV0][:, :2].astype(np.float32).copy()
            uv[:, 1] = 1.0 - uv[:, 1]
            attrs['TEXCOORD_0'] = glb.accessor(uv, 'VEC2', 5126, 34962)
        if GROUP_IDX in at and GROUP_WEIGHT in at:
            j = at[GROUP_IDX][:, :4].astype(np.uint16)
            w = at[GROUP_WEIGHT][:, :4].astype(np.float32)
            j[j >= len(joints)] = 0
        else:  # rigid shape: follow the group's bone
            bone = shape_bone.get(si, 0)
            j = np.zeros((n, 4), np.uint16)
            j[:, 0] = joints.index(bone) if bone in joints else 0
            w = np.tile(np.array([1, 0, 0, 0], np.float32), (n, 1))
        s = w.sum(1, keepdims=True)
        w = np.where(s > 0, w / np.maximum(s, 1e-8), np.array([1, 0, 0, 0], np.float32))
        attrs['JOINTS_0'] = glb.accessor(j, 'VEC4', 5123, 34962)
        attrs['WEIGHTS_0'] = glb.accessor(w.astype(np.float32), 'VEC4', 5126, 34962)
        prims = []
        for pg in shape.tables(0):
            ix = np.array(pg.scalars(1, 'H'), np.uint32)
            if not len(ix):
                continue
            p = {'attributes': attrs, 'indices': glb.accessor(ix, 'SCALAR', 5125, 34963), 'mode': 4}
            if pg.scalar(0) in mat_index:
                p['material'] = mat_index[pg.scalar(0)]
            prims.append(p)
        nm = model.bones[shape_bone[si]].string(0) if si in shape_bone else f'shape{si}'
        glb.j['meshes'].append({'name': nm, 'primitives': prims})
        glb.j['nodes'].append({'name': nm, 'mesh': len(glb.j['meshes']) - 1, 'skin': 0})
        mesh_nodes.append(len(glb.j['nodes']) - 1)

    root_name = model_name.rsplit('.', 1)[0]
    glb.j['nodes'].append({'name': root_name, 'children': roots + mesh_nodes})
    glb.j['scenes'][0]['nodes'] = [len(glb.j['nodes']) - 1]

    written = []
    if anims:
        files = clip_files(pak, root_name)
        for src, dst in clip_names(files).items():
            if add_animation(glb, dst, pak.by_name(files[src]), node_by_name):
                written.append(dst)
    os.makedirs(os.path.dirname(os.path.abspath(out_glb)), exist_ok=True)
    glb.write(out_glb)
    return written


def convert_species(dex: int, out_glb: str) -> dict:
    """Write the species' default-form model to out_glb; returns pick_clips roles for its clips."""
    path=pack_path(dex)
    clips = convert_pack(path, os.path.basename(path).replace('.gfpak','.gfbmdl'), out_glb)
    return pick_clips(clips)


DEFAULT_SPECIES = [('ZIGZAGOON', 263), ('LINOONE', 264), ('WAILMER', 320), ('WAILORD', 321),
                   ('ELECTRIKE', 309), ('MANECTRIC', 310), ('MAWILE', 303), ('BALTOY', 343),
                   ('CLAYDOL', 344), ('SOLROCK', 338), ('CORSOLA', 222), ('NATU', 177), ('XATU', 178),
                   ('WOBBUFFET', 202), ('WYNAUT', 360), ('NINCADA', 290), ('SHUCKLE', 213),
                   ('GOLDEEN', 118), ('SEAKING', 119)]


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    out_dir = argv[0] if argv else '/mnt/d/www/hoenn-scratch/sword'
    todo = [(a.split(':')[0], int(a.split(':')[1])) for a in argv[1:]] or DEFAULT_SPECIES
    os.makedirs(out_dir, exist_ok=True)
    for name, dex in todo:
        roles = convert_species(dex, os.path.join(out_dir, f'{name}.glb'))
        json.dump(roles, open(os.path.join(out_dir, f'{name}.clips.json'), 'w'))
        missing = [k for k, v in roles.items() if v is None and k != 'fly']
        print(f'{name}: ok' + (f' (no clip for {", ".join(missing)})' if missing else ''))


if __name__ == '__main__':
    main()
