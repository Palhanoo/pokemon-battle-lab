"""Pokemon Legends Z-A packed filesystem reader: arceusx TRPFS plus tolerance for the v2.0.x update.

In a patched data.trpfs (as extracted by hactool from the v2.0.x BKTR) the first and last 16 bytes of
many packs (and the first 16 of the file table) are garbled. Packs hold exactly one file, so the
head is restored from the two known FlatBuffer layouts and a missing hash falls back to that
single entry. The unpatched base romfs needs none of this.
"""
import trpfs as _t
from fb import T as _T

HEAD_A = bytes.fromhex('10000000000000000800' '0c00' '04000800')  # root 0x10, table at 0x10
HEAD_B = bytes.fromhex('0c000000' '08000c00' '04000800' '08000000')  # root 0xc, table at 0xc


def _fix_T(b, p=None):
    if p is None and len(b) > 20:
        r = int.from_bytes(b[:4], 'little')
        if not (r < 0x40 and r % 4 == 0):  # garbled root offset
            b = (HEAD_A if b[16:20] == b'\x08\x00\x00\x00' else HEAD_B) + b[16:]
    return _T(b, p)


_t.T = _fix_T


class ZATRPFS(_t.TRPFS):
    def get(self, path):
        h = _t.fnv1a64(path)
        if h not in self.file_pack:
            return None
        pack = self._pack(self.file_pack[h])
        if h not in pack and len(pack) == 1:
            pack = {h: next(iter(pack.values()))}
            self._cache = {k: pack for k in self._cache}
        return super().get(path) if h in self._pack(self.file_pack[h]) else self._get_from(pack, h)

    def _get_from(self, pack, h):
        e = pack[h]
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
