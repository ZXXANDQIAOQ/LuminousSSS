# -*- coding: utf-8 -*-
"""LuminousSSS 特效贴图产出（对应 采集/素材需求清单 v0.7 第八节 FX-01 ~ FX-05）

规范：透明底 PNG、硬边像素、序列帧等分对齐、四周留 4px 发光留白不切边，
      交付按帧序命名，可直接挂 Unity Sprite 动画或 Particle System。
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image
from luminous_pixel import *

import os as _os
OUT = _os.path.join(_os.environ.get('LUMINOUS_OUT', 'Assets/Art/V2'), 'FX')
os.makedirs(OUT, exist_ok=True)


def save(im, name):
    im.save('%s/%s.png' % (OUT, name))
    print('  %-30s %dx%d' % (name, im.size[0], im.size[1]))


def ell_m(n, cx, cy, rx, ry, ang=0.0):
    yy, xx = np.mgrid[0:n, 0:n].astype(float)
    a = np.radians(ang)
    xr = (xx - cx) * np.cos(a) + (yy - cy) * np.sin(a)
    yr = -(xx - cx) * np.sin(a) + (yy - cy) * np.cos(a)
    return (xr / float(max(0.5, rx))) ** 2 + (yr / float(max(0.5, ry))) ** 2 <= 1.0


def star_m(n, reach, arm, core=0):
    """四角闪光星：中心 + 上下左右渐尖。"""
    m = np.zeros((n, n), bool)
    c = n // 2
    for i in range(reach):
        t = i / float(max(1, reach - 1))
        h = max(1, int(round((1.0 - t) * arm)))
        for (y, x) in ((c - i, None), (c + i, None)):
            if 0 <= y < n:
                m[y, max(0, c - h):min(n, c + h + 1)] = True
        for (x, y) in ((c - i, None), (c + i, None)):
            if 0 <= x < n:
                m[max(0, c - h):min(n, c + h + 1), x] = True
    if core > 0:
        m |= ell_m(n, c, c, core, core)
    return m


# ============================================================ FX-02 尘土（3 帧，32×32）
for i in range(3):
    n = 32
    im = new_canvas(n, n)
    r = 2 + i * 4
    for (cx, cy, s) in ((16, 21, 1.0), (16 - r, 19, 0.8), (16 + r, 19, 0.8),
                        (16 - r // 2 - 2, 25, 0.6), (16 + r // 2 + 2, 25, 0.6)):
        rr = max(1, int(round(3 * s)))
        m = ell_m(n, cx, cy, rr, max(1, rr - 1))
        paint_mask(im, dilate(m, 1), rgba('outline_2'))
        paint_mask(im, m, rgba('plank_dark' if i < 2 else 'plank'))
    save(im, 'v2_fx_dust_%02d' % (i + 1))

# ============================================================ FX-03 采集闪光（4 帧，48×48）
for i in range(4):
    n = 48
    im = new_canvas(n, n)
    reach, arm, core = (9, 16, 22, 14)[i], (2, 3, 4, 2)[i], (6, 9, 5, 0)[i]
    m = star_m(n, reach, arm, core)
    if i == 3:
        for (cx, cy, r) in ((13, 16, 2), (35, 31, 2), (24, 39, 2), (35, 11, 1), (12, 34, 1)):
            m |= ell_m(n, cx, cy, r, r)
    paint_mask(im, dilate(m, 1), rgba('outline_2'))
    paint_mask(im, m, rgba('gold'))
    if core > 0:
        paint_mask(im, m & ell_m(n, 24, 24, max(1, core - 2), max(1, core - 2)), rgba('white'))
    save(im, 'v2_fx_spark_%02d' % (i + 1))

# ============================================================ FX-01 树叶飘落（4 帧，32×32）
for i in range(4):
    n = 32
    im = new_canvas(n, n)
    ang = i * 45
    cx = 15 + (i % 2) * 2
    cy = 14 + (i // 2) * 3
    leaf = ell_m(n, cx, cy, 9, 5, ang)
    leaf |= ell_m(n, cx + 8 * np.cos(np.radians(ang)), cy + 8 * np.sin(np.radians(ang)), 4, 2, ang)
    leaf |= ell_m(n, cx - 8 * np.cos(np.radians(ang)), cy - 8 * np.sin(np.radians(ang)), 3, 2, ang)
    paint_mask(im, dilate(leaf, 1), rgba('leaf_dark'))
    paint_mask(im, leaf, rgba('leaf' if i % 2 == 0 else 'corn_dark'))
    vein = ell_m(n, cx, cy, 7, 1, ang)
    paint_mask(im, leaf & vein, rgba('leaf_hi'))
    if i % 2 == 1:
        paint_mask(im, leaf & ell_m(n, cx - 3, cy - 3, 3, 2, ang), rgba('corn_hi'))
    save(im, 'v2_fx_leaf_%02d' % (i + 1))

# ============================================================ FX-04 水花（3 帧，48×48）
for i in range(3):
    n = 48
    im = new_canvas(n, n)
    k = i + 1
    splash = np.zeros((n, n), bool)
    for j, ang in enumerate((205, 228, 252, 288, 312, 335)):
        d = 5 + k * 6
        a = np.radians(ang + (j - 2.5) * 3)
        splash |= ell_m(n, 24 + d * np.cos(a), 30 + d * np.sin(a), max(1, 3 - i), max(1, 3 - i))
    if i < 2:
        splash |= ell_m(n, 24, 30, 11 - i * 4, 3)
    paint_mask(im, dilate(splash, 1), rgba('fish_dark'))
    paint_mask(im, splash, rgba('fish_hi' if i < 2 else 'glass'))
    paint_mask(im, splash & ell_m(n, 24, 28, 6, 2), rgba('white'))
    save(im, 'v2_fx_splash_%02d' % (i + 1))

# ============================================================ FX-05 建造 / 耕地选择框（64×64）
n, seg, t, inset = 64, 18, 6, 2
a, b = inset, n - 1 - inset
corners = [
    (a, a, a + seg, a + t - 1), (a, a, a + t - 1, a + seg),
    (b - seg, a, b, a + t - 1), (b - t + 1, a, b, a + seg),
    (a, b - t + 1, a + seg, b), (a, b - seg, a + t - 1, b),
    (b - seg, b - t + 1, b, b), (b - t + 1, b - seg, b, b),
]
im = new_canvas(n, n)
for (x0, y0, x1, y1) in corners:
    rect(im, x0, y0, x1, y1, rgba('outline'))
for (x0, y0, x1, y1) in corners:
    rect(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, rgba('stamina'))
    rect(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, rgba('stamina'))
save(im, 'v2_fx_select_cursor')

print('\n完成')
