"""Schema-less FlatBuffers reader: fields are addressed by their index in the .fbs table."""
import struct

class T:
    __slots__ = ('b', 'p', 'vt', 'vlen')

    def __init__(self, b, p=None):
        if p is None:
            p = struct.unpack_from('<I', b, 0)[0]
        self.b, self.p = b, p
        self.vt = p - struct.unpack_from('<i', b, p)[0]
        self.vlen = struct.unpack_from('<H', b, self.vt)[0]

    def _off(self, i):
        o = 4 + 2 * i
        return struct.unpack_from('<H', self.b, self.vt + o)[0] if o < self.vlen else 0

    def has(self, i):
        return self._off(i) != 0

    def scalar(self, i, fmt='I', default=0):
        o = self._off(i)
        return struct.unpack_from('<' + fmt, self.b, self.p + o)[0] if o else default

    def struct(self, i, fmt):
        o = self._off(i)
        return struct.unpack_from('<' + fmt, self.b, self.p + o) if o else None

    def _ref(self, i):
        o = self._off(i)
        if not o:
            return None
        a = self.p + o
        return a + struct.unpack_from('<I', self.b, a)[0]

    def table(self, i):
        a = self._ref(i)
        return None if a is None else T(self.b, a)

    def string(self, i, default=''):
        a = self._ref(i)
        if a is None:
            return default
        n = struct.unpack_from('<I', self.b, a)[0]
        return self.b[a + 4:a + 4 + n].decode('utf-8', 'replace')

    def _vec(self, i):
        a = self._ref(i)
        if a is None:
            return 0, 0
        return struct.unpack_from('<I', self.b, a)[0], a + 4

    def tables(self, i):
        n, a = self._vec(i)
        return [T(self.b, a + 4 * k + struct.unpack_from('<I', self.b, a + 4 * k)[0]) for k in range(n)]

    def strings(self, i):
        n, a = self._vec(i)
        out = []
        for k in range(n):
            s = a + 4 * k + struct.unpack_from('<I', self.b, a + 4 * k)[0]
            ln = struct.unpack_from('<I', self.b, s)[0]
            out.append(self.b[s + 4:s + 4 + ln].decode('utf-8', 'replace'))
        return out

    def bytes(self, i):
        n, a = self._vec(i)
        return self.b[a:a + n]

    def scalars(self, i, fmt):
        n, a = self._vec(i)
        return list(struct.unpack_from(f'<{n}{fmt}', self.b, a))

    def structs(self, i, fmt):
        n, a = self._vec(i)
        sz = struct.calcsize('<' + fmt)
        return [struct.unpack_from('<' + fmt, self.b, a + k * sz) for k in range(n)]
