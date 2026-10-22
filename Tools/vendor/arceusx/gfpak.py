"""GFLXPACK reader (layout per pkNX GFPack.cs)."""
import struct, zlib

def fnv1a64(s):
    h = 0xCBF29CE484222645
    for b in s.encode():
        h = ((h ^ b) * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return h

class GFPak:
    def __init__(self, path):
        d = self.raw = open(path, 'rb').read()
        magic, ver, reloc, nfiles, nfolders = struct.unpack_from('<QIIii', d, 0)
        assert magic == 0x4B434150584C4647, path
        ptr_files, ptr_paths = struct.unpack_from('<qq', d, 0x18)
        folder_ptrs = struct.unpack_from(f'<{nfolders}q', d, 0x28)
        self.path_hashes = list(struct.unpack_from(f'<{nfiles}Q', d, ptr_paths))
        self.name_index = {}  # filename hash -> file index
        self.folders = []
        for fp in folder_ptrs:
            fh, cnt = struct.unpack_from('<Qi', d, fp)
            files = [struct.unpack_from('<Qi', d, fp + 0x10 + i * 0x10) for i in range(cnt)]
            self.folders.append((fh, files))
            for h, idx in files:
                self.name_index[h] = idx
        self.entries = [struct.unpack_from('<HBxiiiii', d, ptr_files + i * 0x18) for i in range(nfiles)]

    def __len__(self):
        return len(self.entries)

    def comp_type(self, i):
        return self.entries[i][1]

    def get(self, i):
        _, typ, dsize, csize, _, off, _ = self.entries[i]
        c = self.raw[off:off + csize]
        if typ == 0:
            return c
        if typ == 1:
            return zlib.decompress(c)
        if typ == 2:
            import lz4.block
            return lz4.block.decompress(c, uncompressed_size=dsize)
        import ooz
        return ooz.decompress(c, dsize)

    def by_name(self, name):
        i = self.name_index.get(fnv1a64(name))
        return None if i is None else self.get(i)
