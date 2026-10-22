"""Validated Z-A asset view: clean base packs, then update-only assets.

Some local v2 pack entries are malformed. Bound their declared sizes before
native decompression; never pass corrupt lengths to ooz.
"""
from functools import lru_cache
from sources import open_za

class ZaAssets:
    def __init__(self):
        self.base,self.update=open_za(False),open_za(True)
    def get(self,path):
        try:
            data=self.base.get(path)
            if data is not None:return data
        except (KeyError,ValueError,IndexError,TypeError):pass
        from gfpak import fnv1a64
        import struct
        h=fnv1a64(path);fs=self.update
        if h not in fs.file_pack:return None
        try:
            pack=fs._pack(fs.file_pack[h])
            entry=pack.get(h) or (next(iter(pack.values())) if len(pack)==1 else None)
            if entry is None:return None
            compression,size,data=entry.scalar(1,'b',-1),entry.scalar(3,'Q'),entry.bytes(4)
            if compression==-1:return data
            if not 0<size<256<<20 or len(data)>size+4096:raise ValueError('Corrupt Z-A update asset '+path)
            return fs._get_from({h:entry},h)
        except struct.error as e:raise ValueError('Malformed Z-A update asset '+path) from e
    def find(self,prefix,suffix):
        return sorted(set(self.base.find(prefix,suffix))|set(self.update.find(prefix,suffix)))

@lru_cache(maxsize=1)
def open_assets():return ZaAssets()
