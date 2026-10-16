# -*- coding: utf-8 -*-
"""《第二批需求》2D 存量素材质量修复

覆盖：
  2D-02  v2_nature_grass_tuft        重画（半透明 48.5%）
  2D-03  v2_nature_flower_white/pink/yellow   重画（半透明 29~31%）
  2D-04  v2_portrait_001/002/003     硬边化（不重画，仅 alpha 二值化）
  2D-05  v2_building_shop_striped    抹掉橱窗上的伪英文残字（"Clis" / "Chotes"）
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image
from luminous_pixel import new_canvas, paint_mask, dilate, erode

SRC = '../fetch/v2'
OUT = 'out/fix2d'
os.makedirs(OUT, exist_ok=True)


def save(im, name):
    p = os.path.join(OUT, name + '.png')
    im.save(p)
    a = np.array(im.convert('RGBA'))
    h = np.bincount(a[:, :, 3].ravel(), minlength=256)
    print('  %-36s %-9s 半透明 %.2f%%' % (name, '%dx%d' % a.shape[1::-1],
                                          h[1:255].sum() * 100.0 / a[:, :, 3].size))
    return p


# ============================================================ 2D-02 草丛（19×14）
def grass_tuft(w=19, h=14):
    im = new_canvas(w, h)
    yy, xx = np.mgrid[0:h, 0:w]
    blades = [
        # (x0,y0, x1,y1, 半宽)
        (4, 13, 2, 5, 1.6), (4, 13, 6, 3, 1.6),
        (9, 13, 9, 1, 1.8), (9, 13, 12, 5, 1.5),
        (11, 13, 15, 4, 1.6), (13, 13, 17, 8, 1.4),
    ]
    m = np.zeros((h, w), bool)
    for (x0, y0, x1, y1, hw) in blades:
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for k in range(n + 1):
            t = k / float(n)
            cx = x0 + (x1 - x0) * t
            cy = y0 + (y1 - y0) * t
            half = max(0.5, hw * (1.0 - 0.45 * t))
            m |= (np.abs(xx - cx) <= half) & (np.abs(yy - cy) <= half)
    paint_mask(im, m, (0x24, 0x74, 0x22, 255))
    upper = m & (yy < 7)
    paint_mask(im, upper, (0x3E, 0xA0, 0x32, 255))
    paint_mask(im, upper & (xx < 9), (0x54, 0xC0, 0x40, 255))
    lower = m & (yy >= 11)
    paint_mask(im, lower, (0x16, 0x50, 0x16, 255))
    return im


save(grass_tuft(), 'v2_nature_grass_tuft')


# ============================================================ 2D-03 花（40×56）
def flower(petal, petal_hi, petal_lo, core=(0xF0, 0xA8, 0x20),
           core_hi=(0xFF, 0xD0, 0x60), core_lo=(0xB8, 0x6C, 0x08),
           w=40, h=56):
    im = new_canvas(w, h)
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    CX, CY = 20.0, 18.0

    def ell(cx, cy, rx, ry):
        return ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1.0

    # 5 片花瓣（逐片描边，保证瓣与瓣之间有分隔）
    petals = np.zeros((h, w), bool)
    petal_list = []
    for k in range(5):
        ang = np.radians(90 + k * 72)
        pm = ell(CX + 9.6 * np.cos(ang), CY - 9.6 * np.sin(ang), 9.2, 8.8)
        petal_list.append(pm)
        petals |= pm
    # 茎
    stem = (np.abs(xx - CX) <= 1.5) & (yy >= CY + 6) & (yy <= h - 1)
    # 两片叶
    leaf1 = ell(CX - 8, 39, 8.5, 4.5)
    leaf2 = ell(CX + 8, 44, 8.5, 4.5)

    # 茎叶（先画，让花瓣压在上面）
    green = stem | leaf1 | leaf2
    paint_mask(im, dilate(green, 1) & ~green, (0x14, 0x40, 0x12, 255))
    paint_mask(im, green, (0x2E, 0x8C, 0x28, 255))
    paint_mask(im, green & ell(CX - 3, 38, 14, 8), (0x4C, 0xB4, 0x3A, 255))
    paint_mask(im, green & ell(CX + 9, 46, 10, 6), (0x18, 0x58, 0x16, 255))
    # 花瓣：逐片 描边 -> 本体 -> 明暗
    for pm in petal_list:
        paint_mask(im, dilate(pm, 1) & ~pm, (0x7A, 0x20, 0x50, 255))
    for pm in petal_list:
        paint_mask(im, pm, petal + (255,))
        paint_mask(im, pm & ell(CX - 4, CY - 4, 13, 13), petal_hi + (255,))
        paint_mask(im, pm & ell(CX + 6, CY + 7, 13, 11), petal_lo + (255,))

    # 花心
    core_m = ell(CX, CY, 6, 6)
    paint_mask(im, dilate(core_m, 1) & ~core_m, core_lo + (255,))
    paint_mask(im, core_m, core + (255,))
    paint_mask(im, core_m & ell(CX - 1.5, CY - 1.5, 3, 3), core_hi + (255,))
    # 花心颗粒
    for (dx, dy) in ((-2, -2), (2, -1), (-1, 2), (2, 2)):
        paint_mask(im, ell(CX + dx, CY + dy, 1, 1), core_lo + (255,))
    return im


save(flower((0xF4, 0xF4, 0xF8), (0xFF, 0xFF, 0xFF), (0xC8, 0xCC, 0xDC)),
     'v2_nature_flower_white')
save(flower((0xE8, 0x48, 0x9C), (0xFF, 0x86, 0xC8), (0xA8, 0x1C, 0x6C), core=(0xE0, 0x90, 0x18)),
     'v2_nature_flower_pink')
save(flower((0xF0, 0xB8, 0x1C), (0xFF, 0xDC, 0x58), (0xB8, 0x78, 0x08), core=(0xC8, 0x70, 0x10)),
     'v2_nature_flower_yellow')


# ============================================================ 2D-04 立绘硬边化
def harden_alpha(path, out_name, thresh=128):
    im = Image.open(path).convert('RGBA')
    a = np.array(im)
    al = a[:, :, 3]
    a[:, :, 3] = np.where(al >= thresh, 255, 0)
    a[:, :, :3][a[:, :, 3] == 0] = 0
    return save(Image.fromarray(a), out_name)


for n in ('v2_portrait_001_farmer', 'v2_portrait_002_rain_maiden', 'v2_portrait_003_adventurer'):
    harden_alpha(os.path.join(SRC, n + '.png'), n)


# ============================================================ 2D-05 商店橱窗去伪英文
def clean_shop(path, out_name):
    """抹掉橱窗玻璃上的伪英文。

    文字是「白色浮雕 + 浅蓝描边」，正好落在橱窗玻璃上。
    做法：对每一列，先在带内上下各找一个确实属于玻璃的像素（b-r 明显偏蓝），
    线性插出该列的玻璃基准色；再把「与基准色偏差过大」的像素判为文字并替换。
    这样雨篷垂齿、窗内货物都不会被误伤。
    """
    im = Image.open(path).convert('RGBA')
    a = np.array(im).astype(np.float64)
    h, w = a.shape[:2]
    BAND = (152, 180)
    windows = [(33, 92), (127, 186)]
    fixed = 0
    for (x0, x1) in windows:
        for x in range(x0, x1 + 1):
            def is_glass(y):
                return a[y, x, 3] > 0 and (a[y, x, 2] - a[y, x, 0]) > 55
            ytop = next((y for y in range(BAND[0], BAND[1]) if is_glass(y)), None)
            ybot = next((y for y in range(BAND[1], BAND[0], -1) if is_glass(y)), None)
            if ytop is None or ybot is None or ybot - ytop < 6:
                continue
            c0, c1 = a[ytop, x, :3], a[ybot, x, :3]
            for y in range(ytop + 1, ybot):
                if a[y, x, 3] <= 0:
                    continue
                t = (y - ytop) / float(ybot - ytop)
                expect = c0 * (1 - t) + c1 * t
                if np.abs(a[y, x, :3] - expect).max() > 22:
                    a[y, x, :3] = expect
                    fixed += 1
    # 轻度纵向中值平滑，抹掉残余"污渍"
    for (x0, x1) in windows:
        for x in range(x0, x1 + 1):
            col = a[BAND[0]:BAND[1], x, :3]
            if col.shape[0] < 3:
                continue
            med = np.median(np.stack([np.roll(col, 1, 0), col, np.roll(col, -1, 0)]), axis=0)
            a[BAND[0] + 1:BAND[1] - 1, x, :3] = med[1:-1]
    print('  商店橱窗：按偏差判定替换文字像素 %d 个' % fixed)
    return save(Image.fromarray(a.astype(np.uint8)), out_name)


clean_shop(os.path.join(SRC, 'v2_building_shop_striped.png'), 'v2_building_shop_striped')
print('\n完成 -> %s' % OUT)
