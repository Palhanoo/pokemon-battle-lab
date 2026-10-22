"""Where model files come from: Arceus gfpak archives or the Violet / Legends Z-A packed filesystems."""
import os
import posixpath
from functools import lru_cache

from trpfs import TRPFS
from convert import Files

HOME = os.path.expanduser('~')
ARCEUS_ROMFS = f'{HOME}/pokemon/arceus-work/romfs/bin'
ARCEUS_POKEMON = f'{ARCEUS_ROMFS}/archive/pokemon'
VIOLET_ARC = f'{HOME}/pokemon/violet-work/romfs4/arc'
SV_HASHLIST = f'{HOME}/pokemon/refs/PokeDocs/SV/Hashlists/FileSystem/hashes_inside_fd.txt'
ZA_BASE = f'{HOME}/pokemon/za-work/romfs/arc'
ZA_UPDATE = f'{HOME}/pokemon/za-work/romfs-update/arc'
ZA_HASHLIST = f'{HOME}/pokemon/refs/PokeDocs/ZA/Hashlists/FileSystem/hashes_inside_fd.txt'


@lru_cache(maxsize=1)
def open_violet():
    return TRPFS(VIOLET_ARC, SV_HASHLIST)


@lru_cache(maxsize=2)
def open_za(update=False):
    """Legends Z-A: the base romfs, or the merged v2.0.x update for models only it ships."""
    from za_trpfs import ZATRPFS
    return ZATRPFS(ZA_UPDATE if update else ZA_BASE, ZA_HASHLIST)


class VioletDir:
    """Files of one Violet folder, addressed by bare file name like the Arceus packs."""

    def __init__(self, fs, dir_path):
        self.fs, self.dir = fs, dir_path.rstrip('/')
        self.names = {p.rsplit('/', 1)[1]: p for p in fs.find(self.dir + '/', '')}

    def get(self, name):
        if '/' in name:  # materials point at shared textures like ../../tr_share_tex/x/x.bntx
            return self.fs.get(posixpath.normpath(f'{self.dir}/{name}'))
        path = self.names.get(name) or f'{self.dir}/{name}'
        return self.fs.get(path)

    def all_named(self, suffix):
        return sorted(n for n in self.names if n.endswith(suffix))


class Sources:
    """Chain several sources; first hit wins (a model folder plus a shared texture folder)."""

    def __init__(self, *srcs):
        self.srcs = srcs

    def get(self, name):
        for s in self.srcs:
            b = s.get(name)
            if b is not None:
                return b
        return None

    def all_named(self, suffix):
        return sorted({n for s in self.srcs for n in s.all_named(suffix)})


def arceus_pack(path):
    return Files([path])
