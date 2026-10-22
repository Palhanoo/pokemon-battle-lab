"""Scarlet/Violet packed filesystem (data.trpfd + data.trpfs, layout per PokeDocs SV/filesystem)."""
import os, struct, sys
sys.path.insert(0, os.path.dirname(__file__))
from fb import T
from gfpak import fnv1a64

class TRPFS:
    def __init__(self, arc_dir, hashlist=None):
        fd = T(open(os.path.join(arc_dir, 'data.trpfd'), 'rb').read())
        self.fs_path = os.path.join(arc_dir, 'data.trpfs')
        self.file_pack = dict(zip(fd.scalars(0, 'Q'), (fi.scalar(0, 'Q') for fi in fd.tables(2))))
        self.pack_names = fd.strings(1)
        with open(self.fs_path, 'rb') as f:
            magic, off = struct.unpack('<8sQ', f.read(16))
            assert magic == b'ONEPACK\0'
            f.seek(off)
            fs = T(f.read())
        self.pack_offsets = dict(zip(fs.scalars(0, 'Q'), fs.scalars(1, 'Q')))
        offs = sorted(self.pack_offsets.values()) + [off]
        self.pack_end = {a: b for a, b in zip(offs, offs[1:])}
        self.names = {}
        if hashlist:
            for line in open(hashlist, encoding='utf-8'):
                h, _, n = line.strip().partition(' ')
                if h.startswith('0x'):
                    self.names[int(h, 16)] = n
        self._cache = {}

    def _pack(self, idx):
        if idx not in self._cache:
            name = self.pack_names[idx]
            o = self.pack_offsets[fnv1a64(name)]
            with open(self.fs_path, 'rb') as f:
                f.seek(o)
                t = T(f.read(self.pack_end[o] - o))
            self._cache = {idx: dict(zip(t.scalars(0, 'Q'), t.tables(1)))}  # keep one pack hot
        return self._cache[idx]

    def get(self, path):
        h = fnv1a64(path)
        if h not in self.file_pack:
            return None
        e = self._pack(self.file_pack[h]).get(h)
        if e is None:
            return None
        ctype, size, data = e.scalar(1, 'b', -1), e.scalar(3, 'Q'), e.bytes(4)
        if ctype == -1:
            return data
        if ctype == 3:
            import ooz
            return ooz.decompress(data, size)
        if ctype == 1:
            import zlib
            return zlib.decompress(data)
        import lz4.block
        return lz4.block.decompress(data, uncompressed_size=size)

    def find(self, prefix='', suffix=''):
        return sorted(n for h, n in self.names.items() if n.startswith(prefix) and n.endswith(suffix) and h in self.file_pack)
