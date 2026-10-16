# -*- coding: utf-8 -*-
"""2A 试点：农夫四方向行走帧（程序化生成）

设计要点
  · 同一套骨架 + 同一套配色常量 → 24 帧特征由构造保证一致，**不可能出现草帽漂移**
  · 画布 64×112、脚底基线 y=109、主体水平居中 x=32，几何一次到位
  · 整幅硬边（alpha 只有 0/255）
  · 配色取自现有 v2_walk_down_01.png 的实测主色，色号与既有素材对得上
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image
from luminous_pixel import new_canvas, paint_mask, round_rect_mask, dilate

W, H = 64, 112
BASE = 109
CX = 32

C = {
    'outline':  (0x2A, 0x0E, 0x06),
    'hair':     (0x7A, 0x2A, 0x0E),
    'hair_hi':  (0x9E, 0x42, 0x18),
    'hair_lo':  (0x50, 0x18, 0x06),
    'skin':     (0xF0, 0xA0, 0x60),
    'skin_hi':  (0xF8, 0xC4, 0x94),
    'skin_lo':  (0xC4, 0x78, 0x40),
    'shirt':    (0x2E, 0xA8, 0x2A),
    'shirt_hi': (0x48, 0xC8, 0x3C),
    'shirt_lo': (0x14, 0x64, 0x18),
    'bib':      (0x24, 0x40, 0xA8),
    'bib_hi':   (0x3C, 0x5E, 0xCC),
    'bib_lo':   (0x10, 0x24, 0x68),
    'pants':    (0x2A, 0x66, 0xC8),
    'pants_hi': (0x4C, 0x88, 0xE2),
    'pants_lo': (0x14, 0x3C, 0x88),
    'boot':     (0x5A, 0x2A, 0x10),
    'boot_hi':  (0x7C, 0x40, 0x18),
    'boot_lo':  (0x30, 0x14, 0x06),
    'eye_w':    (0xFF, 0xFF, 0xFF),
    'iris':     (0x1B, 0x58, 0xC8),
    'pupil':    (0x10, 0x18, 0x30),
    'metal':    (0xC8, 0xC8, 0xD0),
}


def col(k):
    r, g, b = C[k]
    return (r, g, b, 255)


def ell(cx, cy, rx, ry):
    yy, xx = np.mgrid[0:H, 0:W]
    return ((xx - cx) / float(max(0.5, rx))) ** 2 + ((yy - cy) / float(max(0.5, ry))) ** 2 <= 1.0


def rr(x0, y0, x1, y1, r):
    return round_rect_mask(W, H, x0, y0, x1, y1, r)


# (左腿dx, 左腿抬起, 右腿dx, 右腿抬起, 身体起伏, 左臂dy, 右臂dy)
WALK = [
    (0, 0, 0, 0, 0, 0, 0),
    (0, 1, 0, 0, -1, 0, 1),
    (-2, 0, 2, 0, 0, 1, -1),
    (0, 0, 0, 0, 0, 0, 0),
    (0, 0, 0, 1, -1, 1, 0),
    (2, 0, -2, 0, 0, -1, 1),
]


def leg_masks(dx, lift, side, top, bot, front):
    cx = CX + side * 7 + dx
    up = rr(cx - 6, top, cx + 5, bot - 10, 3)                       # 裤腿
    boot = rr(cx - 7, bot - 11 - lift, cx + 6, bot - 1 - lift, 3)   # 靴身
    sole = ell(cx, bot - 2 - lift, 7, 3)                            # 靴头
    return up, (boot | sole)


def arm_masks(dy, side, ty0):
    cx = CX + side * 15
    sleeve = rr(cx - 4, ty0 + 1, cx + 3, ty0 + 9, 2)                # 短袖（绿）
    fore = rr(cx - 3, ty0 + 9, cx + 2, ty0 + 22 + dy, 2)            # 小臂（肤）
    hand = ell(cx - 0.5, ty0 + 23 + dy, 4, 3)                       # 手
    return sleeve, (fore | hand)


def head_masks(direction):
    if direction == 'down':
        hair = rr(14, 12, 49, 56, 12) | ell(32, 21, 18, 11)
        face = rr(19, 26, 45, 56, 9)
        fringe = (ell(32, 26, 18, 10) | rr(14, 12, 49, 31, 10)) & hair
        return hair, face, fringe
    if direction == 'up':
        return rr(14, 12, 49, 56, 12) | ell(32, 21, 18, 11), None, None
    hair = rr(14, 12, 48, 56, 12)
    face = rr(27, 27, 46, 56, 8)
    return hair, face, None


def draw(direction, fi):
    lx, llift, rx, rlift, bob, ldy, rdy = WALK[fi]
    im = new_canvas(W, H)
    ty0, ty1 = 58 + bob, 86 + bob

    # ---- 腿 + 靴 ----
    lu, lb = leg_masks(lx, llift, -1, 83 + bob, BASE + 1, True)
    ru, rb = leg_masks(rx, rlift, +1, 83 + bob, BASE + 1, True)
    for m, k in ((lu, 'pants'), (lb, 'boot'), (ru, 'pants'), (rb, 'boot')):
        paint_mask(im, m, col(k))
    paint_mask(im, lu & ell(CX - 7 + lx, 88 + bob, 5, 6), col('pants_hi'))
    paint_mask(im, ru & ell(CX + 7 + rx, 88 + bob, 5, 6), col('pants_hi'))
    paint_mask(im, lb & ell(CX - 6 + lx, BASE - 12 - llift, 5, 4), col('boot_hi'))
    paint_mask(im, rb & ell(CX + 6 + rx, BASE - 12 - rlift, 5, 4), col('boot_hi'))

    # ---- 躯干 ----
    body = rr(17, ty0, 46, ty1, 4) if direction in ('down', 'up') else rr(19, ty0, 45, ty1, 4)
    paint_mask(im, body, col('shirt'))
    paint_mask(im, body & ell(26, ty0 + 6, 22, 22), col('shirt_hi'))
    paint_mask(im, body & ell(40, ty1 - 4, 20, 18), col('shirt_lo'))
    bib = rr(22, ty0 + 7, 41, ty1 + 1, 3)
    paint_mask(im, bib, col('bib'))
    paint_mask(im, bib & ell(28, ty0 + 12, 14, 16), col('bib_hi'))
    paint_mask(im, bib & ell(38, ty1 - 8, 12, 12), col('bib_lo'))
    straps = rr(22, ty0 - 1, 27, ty0 + 16, 1) | rr(36, ty0 - 1, 41, ty0 + 16, 1)
    paint_mask(im, straps, col('bib'))
    paint_mask(im, ell(24.5, ty0 + 14, 2.5, 2.5), col('metal'))
    paint_mask(im, ell(38.5, ty0 + 14, 2.5, 2.5), col('metal'))

    # ---- 手臂 ----
    ls, lf = arm_masks(ldy, -1, ty0)
    rs, rf = arm_masks(rdy, +1, ty0)
    if direction in ('down', 'up'):
        for m, k in ((ls, 'shirt'), (lf, 'skin'), (rs, 'shirt'), (rf, 'skin')):
            paint_mask(im, m, col(k))
    else:
        paint_mask(im, lf, col('skin'))
        paint_mask(im, ls, col('shirt'))
        paint_mask(im, lf & ell(CX - 13, ty0 + 20, 4, 6), col('skin_hi'))

    # ---- 头 ----
    hair, face, fringe = head_masks(direction)
    paint_mask(im, hair, col('hair'))
    paint_mask(im, hair & ell(24, 20, 14, 10), col('hair_hi'))
    if face is not None:
        paint_mask(im, face, col('skin'))
        paint_mask(im, face & ell(26, 34, 10, 14), col('skin_hi'))
        paint_mask(im, face & ell(42, 52, 8, 8), col('skin_lo'))
    if fringe is not None:
        paint_mask(im, fringe, col('hair'))
        paint_mask(im, fringe & ell(24, 20, 12, 8), col('hair_hi'))

    # ---- 眼 ----
    if direction == 'down':
        for ex in (-8, 8):
            paint_mask(im, ell(CX + ex, 40, 4, 5), col('eye_w'))
            paint_mask(im, ell(CX + ex, 41, 3, 3.5), col('iris'))
            paint_mask(im, ell(CX + ex, 41, 1.5, 2), col('pupil'))
            paint_mask(im, ell(CX + ex - 1, 39, 1, 1), col('eye_w'))
    elif direction in ('left', 'right'):
        ex = 38 if direction == 'right' else 25
        paint_mask(im, ell(ex, 40, 3, 4), col('eye_w'))
        paint_mask(im, ell(ex, 41, 2.5, 3), col('iris'))
        paint_mask(im, ell(ex, 41, 1.2, 1.6), col('pupil'))

    # ---- 描边（整幅外描边，2px）----
    solid = np.array(im)[:, :, 3] > 0
    out = im.copy()
    paint_mask(out, dilate(solid, 2) & ~solid, col('outline'))
    return out


if __name__ == '__main__':
    os.makedirs('out/farmer', exist_ok=True)
    for d in ('down', 'up', 'left', 'right'):
        for i, im in enumerate([draw(d, k) for k in range(6)], 1):
            im.save('out/farmer/v2_walk_farmer_%s_%02d.png' % (d, i))
    print('已生成 24 帧 -> out/farmer/')
