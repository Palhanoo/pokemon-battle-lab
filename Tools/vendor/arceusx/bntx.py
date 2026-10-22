"""BNTX -> RGBA numpy image (mip 0, first face). Header layout per BNTX-Extractor."""
import struct
import numpy as np
import texture2ddecoder as t2d

BLK = {0x1a: 8, 0x1b: 16, 0x1c: 16, 0x1d: 8, 0x1e: 16, 0x1f: 16, 0x20: 16}  # BCn bytes per 4x4 block
UNCOMP = {0x0b: 4, 0x02: 1, 0x07: 2, 0x09: 2}
DECODE = {0x1a: t2d.decode_bc1, 0x1c: t2d.decode_bc3, 0x1d: t2d.decode_bc4,
          0x1e: t2d.decode_bc5, 0x1f: t2d.decode_bc6, 0x20: t2d.decode_bc7}

def _rup(x, y):
    return ((x - 1) | (y - 1)) + 1

def deswizzle(data, w, h, bpp, block_height):
    """Tegra X1 block-linear -> linear, vectorised. w/h are in blocks (or pixels for uncompressed)."""
    gobs_w = (w * bpp + 63) // 64
    y, xb = np.mgrid[0:h, 0:w * bpp]
    gob = ((y // (8 * block_height)) * 512 * block_height * gobs_w
           + (xb // 64) * 512 * block_height + (y % (8 * block_height) // 8) * 512)
    addr = gob + ((xb % 64) // 32) * 256 + ((y % 8) // 2) * 64 + ((xb % 32) // 16) * 32 + (y % 2) * 16 + (xb % 16)
    src = np.frombuffer(data, np.uint8)
    need = int(addr.max()) + 1
    if src.size < need:
        src = np.concatenate([src, np.zeros(need - src.size, np.uint8)])
    return src[addr.ravel()].tobytes()

def count(buf):
    return struct.unpack_from('<i', buf, 0x24)[0]

def name(buf, index=0):
    info_ptr = struct.unpack_from('<q', buf, 0x28)[0]
    p = struct.unpack_from('<q', buf, info_ptr + 8 * index)[0]
    a = struct.unpack_from('<4siq2b3H3I5i6I4i3q', buf, p)[26]
    n = struct.unpack_from('<H', buf, a)[0]
    return buf[a + 2:a + 2 + n].decode('utf-8', 'replace')

def read(buf, index=0):
    assert buf[:4] == b'BNTX'
    info_ptr = struct.unpack_from('<q', buf, 0x20 + 8)[0]
    p = struct.unpack_from('<q', buf, info_ptr + 8 * index)[0]
    (_, _, _, tile, _, _, _, nmips, _, fmt, _, w, h, _, nfaces, size_range, *_rest) = \
        struct.unpack_from('<4siq2b3H3I5i6I4i3q', buf, p)
    f = struct.unpack_from('<4siq2b3H3I5i6I4i3q', buf, p)
    image_size, align, comp_sel, ptrs = f[22], f[23], f[24], f[28]
    data_addr = struct.unpack_from('<q', buf, ptrs)[0]
    data = buf[data_addr:data_addr + image_size]
    kind = fmt >> 8
    bh = 1 << size_range
    if kind in BLK:
        bw_, bh_ = (w + 3) // 4, (h + 3) // 4
        lin = deswizzle(data, bw_, bh_, BLK[kind], bh) if tile else data
        bgra = DECODE[kind](lin, bw_ * 4, bh_ * 4) if kind != 0x1f else DECODE[kind](lin, bw_ * 4, bh_ * 4)
        img = np.frombuffer(bgra, np.uint8).reshape(bh_ * 4, bw_ * 4, 4)[:h, :w, [2, 1, 0, 3]]
    elif kind in UNCOMP:
        bpp = UNCOMP[kind]
        lin = deswizzle(data, w, h, bpp, bh) if tile else data
        raw = np.frombuffer(lin, np.uint8)[:w * h * bpp].reshape(h, w, bpp)
        img = np.zeros((h, w, 4), np.uint8); img[..., 3] = 255
        img[..., :bpp] = raw
    else:
        raise ValueError(f'unsupported BNTX format {fmt:#x}')
    # component selector: byte i (low->high) picks the source for output R,G,B,A
    src = [img[..., k] for k in range(4)]
    out = np.empty_like(img)
    for i in range(4):
        s = (comp_sel >> (8 * i)) & 0xff
        out[..., i] = 0 if s == 0 else 255 if s == 1 else src[s - 2]
    return out, fmt
