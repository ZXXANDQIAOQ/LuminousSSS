# -*- coding: utf-8 -*-
"""生成村口集市的场景配件（Assets/Art/V2/Village，PPU 64 -> 1 tile = 64px）。

集市不能只有一排现成房子：摊子、水井、路灯才是"有人在这生活"的信号。
这四张图与 V2 农场素材同一套色阶（暖木 + 深棕描边 + 左上高光），
alpha 只有 0/255（Point 过滤下不出毛边）。

产出（文件名 -> 世界尺寸）：
  v2_build_stall.png  2.0 x 2.0 格  集市摊位（红白条纹雨棚 + 台面 + 三样货）
  v2_build_well.png   1.5 x 1.5 格  村口石砌水井
  v2_build_lamp.png   0.5 x 2.0 格  木杆路灯（暖黄灯罩）
  v2_build_fence.png  1.0 x 0.6 格  木栅栏（可连续平铺）

重新生成：python Tools/gen_village_props.py
预览：    Docs/preview_v2/village_sheet.png
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'V2', 'Village')
SHEET = os.path.join(ROOT, 'Docs', 'preview_v2', 'village_sheet.png')

C = {
    'outline':   (0x38, 0x22, 0x10),
    'outline_s': (0x5A, 0x38, 0x1C),

    'wood':      (0xC0, 0x8A, 0x4E),
    'wood_hi':   (0xDE, 0xAA, 0x6A),
    'wood_dark': (0x94, 0x62, 0x2E),
    'wood_deep': (0x5E, 0x3A, 0x16),

    'red':       (0xB5, 0x4A, 0x3C),
    'red_hi':    (0xD4, 0x6A, 0x56),
    'red_dk':    (0x84, 0x32, 0x28),
    'cream':     (0xF2, 0xE2, 0xC0),
    'cream_hi':  (0xFF, 0xF4, 0xDA),

    'stone':     (0xA6, 0x9C, 0x90),
    'stone_hi':  (0xC6, 0xBE, 0xB2),
    'stone_dk':  (0x76, 0x6C, 0x62),

    'water':     (0x2A, 0x50, 0x5C),
    'water_hi':  (0x45, 0x7C, 0x86),

    'gold':      (0xE2, 0xAC, 0x48),
    'gold_hi':   (0xF6, 0xD2, 0x84),
    'gold_dk':   (0xB0, 0x7C, 0x24),

    'pumpkin':   (0xD8, 0x86, 0x2E),
    'leaf':      (0x5E, 0x8F, 0x42),
    'leaf_hi':   (0x82, 0xB2, 0x5C),
    'tomato':    (0xC0, 0x3E, 0x34),
}


def rgba(name, a=255):
    c = C[name]
    return (c[0], c[1], c[2], a)


def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def fill(im, x0, y0, x1, y1, col):
    h, w = im.shape[:2]
    x0, x1 = max(0, int(x0)), min(w, int(x1))
    y0, y1 = max(0, int(y0)), min(h, int(y1))
    if x0 < x1 and y0 < y1:
        im[y0:y1, x0:x1] = col


def disc(im, cx, cy, rx, ry, col):
    h, w = im.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    m = ((xx + 0.5 - cx) / max(rx, 0.5)) ** 2 + ((yy + 0.5 - cy) / max(ry, 0.5)) ** 2 <= 1.0
    im[m] = col


def box(im, x0, y0, x1, y1, base, hi=None, lo=None, outline=None):
    """描边 + 左上高光 + 右下阴影的矩形（左闭右开）。"""
    fill(im, x0, y0, x1, y1, rgba(base))
    if hi:
        fill(im, x0, y0, x1 - 1, y0 + 1, rgba(hi))
        fill(im, x0, y0, x0 + 1, y1 - 1, rgba(hi))
    if lo:
        fill(im, x0 + 1, y1 - 1, x1, y1, rgba(lo))
        fill(im, x1 - 1, y0 + 1, x1, y1, rgba(lo))
    if outline:
        fill(im, x0, y0, x1, y0 + 1, rgba(outline))
        fill(im, x0, y1 - 1, x1, y1, rgba(outline))
        fill(im, x0, y0, x0 + 1, y1, rgba(outline))
        fill(im, x1 - 1, y0, x1, y1, rgba(outline))


# ---------------------------------------------------------------- 摊位（128x128）
def build_stall():
    im = canvas(128, 128)

    # ---- 四根立柱（先画，让雨棚压在上面）
    for x in (16, 104):
        box(im, x, 44, x + 9, 118, 'wood_dark', 'wood', 'wood_deep', 'outline')

    # ---- 台面（两层木板 + 前沿阴影）
    box(im, 10, 76, 118, 100, 'wood', 'wood_hi', 'wood_dark', 'outline')
    fill(im, 10, 88, 118, 90, rgba('wood_dark'))
    fill(im, 10, 96, 118, 99, rgba('wood_deep'))

    # ---- 台面下的木箱（货仓感）
    box(im, 22, 100, 58, 120, 'wood_dark', 'wood', 'wood_deep', 'outline')
    box(im, 66, 104, 96, 120, 'wood_dark', 'wood', 'wood_deep', 'outline')
    fill(im, 22, 109, 58, 110, rgba('wood_deep'))
    fill(im, 66, 111, 96, 112, rgba('wood_deep'))

    # ---- 雨棚：红白条纹 + 底边波浪
    top, bottom = 10, 46
    fill(im, 6, top, 122, bottom, rgba('cream'))
    # 斜条纹：每 16px 一条红（用 x 偏移让条纹斜着走）
    for i in range(-8, 14):
        x0 = i * 16
        for y in range(top, bottom):
            off = int((y - top) * 0.35)
            xa = x0 + off
            fill(im, xa, y, xa + 9, y + 1, rgba('red'))
    # 条纹高光/暗边，做出布料的厚度
    fill(im, 6, top, 122, top + 2, rgba('cream_hi'))
    fill(im, 6, bottom - 3, 122, bottom, rgba('red_dk'))

    # 雨棚底边的三角齿（遮阳棚的波浪边）
    for x in range(6, 122, 16):
        for k in range(6):
            fill(im, x + k, bottom, x + 16 - k, bottom + 1, rgba('cream'))

    # 雨棚描边
    fill(im, 6, top - 1, 122, top, rgba('outline'))
    fill(im, 5, top, 6, bottom, rgba('outline'))
    fill(im, 122, top, 123, bottom, rgba('outline'))

    # ---- 台面上的货：南瓜 / 青菜 / 番茄
    disc(im, 38, 70, 11, 9, rgba('pumpkin'))
    disc(im, 36, 67, 5, 4, rgba('gold_hi'))
    fill(im, 37, 58, 40, 63, rgba('leaf'))          # 南瓜梗
    disc(im, 66, 71, 10, 8, rgba('leaf'))
    disc(im, 64, 68, 5, 4, rgba('leaf_hi'))
    for cx in (86, 96):
        disc(im, cx, 72, 6, 6, rgba('tomato'))
        disc(im, cx - 2, 70, 2, 2, rgba('red_hi'))

    # 货品描边（压一圈深色，避免和台面糊在一起）
    for (cx, cy, rx, ry, col) in ((38, 70, 11, 9, 'pumpkin'), (66, 71, 10, 8, 'leaf')):
        hh, ww = im.shape[:2]
        yy, xx = np.mgrid[0:hh, 0:ww]
        ring = ((((xx + 0.5 - cx) / rx) ** 2 + ((yy + 0.5 - cy) / ry) ** 2 <= 1.0) &
                ((((xx + 0.5 - cx) / (rx - 1.2)) ** 2 + ((yy + 0.5 - cy) / (ry - 1.2)) ** 2) > 1.0))
        im[ring] = rgba('outline')

    return im


# ---------------------------------------------------------------- 水井（96x96）
def build_well():
    im = canvas(96, 96)

    # ---- 井身：石砌圆柱
    box(im, 14, 46, 82, 92, 'stone', 'stone_hi', 'stone_dk', 'outline')
    # 石块横缝 + 竖缝（错缝砌法）
    for y in (58, 70, 82):
        fill(im, 15, y, 81, y + 1, rgba('stone_dk'))
    for i, y in enumerate((46, 58, 70, 82)):
        for x in range(24 + (i % 2) * 12, 80, 24):
            fill(im, x, y, x + 1, y + 12, rgba('stone_dk'))

    # ---- 井口（深色椭圆）+ 内壁高光
    disc(im, 48, 46, 34, 12, rgba('outline'))
    disc(im, 48, 46, 31, 9, rgba('water'))
    disc(im, 48, 47, 24, 6, rgba('water_hi'))
    disc(im, 48, 46, 18, 4, rgba('water'))

    # ---- 木架 + 小屋顶
    box(im, 17, 20, 24, 48, 'wood_dark', 'wood', 'wood_deep', 'outline')
    box(im, 72, 20, 79, 48, 'wood_dark', 'wood', 'wood_deep', 'outline')

    # 三角屋顶
    for k in range(16):
        fill(im, 10 + k, 4 + k, 86 - k, 6 + k, rgba('red'))
    fill(im, 10, 4, 86, 6, rgba('red_hi'))
    fill(im, 8, 19, 88, 22, rgba('red_dk'))
    fill(im, 8, 18, 88, 19, rgba('outline'))

    # 绳子 + 木桶
    fill(im, 46, 22, 48, 46, rgba('wood_deep'))
    box(im, 40, 44, 56, 60, 'wood_dark', 'wood', 'wood_deep', 'outline')
    fill(im, 40, 50, 56, 52, rgba('wood_deep'))

    return im


# ---------------------------------------------------------------- 路灯（32x128）
def build_lamp():
    im = canvas(32, 128)

    # 灯柱
    box(im, 14, 30, 20, 126, 'wood_dark', 'wood', 'wood_deep', 'outline')
    # 底座
    box(im, 9, 116, 25, 127, 'wood_dark', 'wood', 'wood_deep', 'outline')
    # 横臂
    box(im, 12, 28, 22, 33, 'wood_dark', 'wood', 'wood_deep', 'outline')

    # 灯罩：上窄下宽的梯形 + 暖黄玻璃
    for k in range(6):
        fill(im, 8 + k // 2, 14 + k, 24 - k // 2, 15 + k, rgba('outline'))
    box(im, 7, 20, 25, 36, 'gold_dk', 'gold', 'outline_s', 'outline')
    fill(im, 9, 22, 23, 34, rgba('gold'))
    fill(im, 10, 23, 22, 33, rgba('gold_hi'))
    fill(im, 12, 25, 20, 31, rgba('cream_hi'))
    # 顶盖
    fill(im, 6, 12, 26, 15, rgba('outline'))
    fill(im, 8, 10, 24, 13, rgba('wood_deep'))
    # 底部挂钩
    fill(im, 14, 35, 18, 39, rgba('outline_s'))

    return im


# ---------------------------------------------------------------- 栅栏（64x40）
def build_fence():
    im = canvas(64, 40)

    # 两根横杆
    box(im, 0, 12, 64, 20, 'wood', 'wood_hi', 'wood_dark', 'outline')
    box(im, 0, 26, 64, 34, 'wood', 'wood_hi', 'wood_dark', 'outline')
    # 立柱
    for x in (6, 34):
        box(im, x, 4, x + 10, 40, 'wood_dark', 'wood', 'wood_deep', 'outline')

    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    items = [('v2_build_stall', build_stall()),
             ('v2_build_well', build_well()),
             ('v2_build_lamp', build_lamp()),
             ('v2_build_fence', build_fence())]

    for name, im in items:
        p = os.path.join(OUT, name + '.png')
        Image.fromarray(im, 'RGBA').save(p)
        print('saved', p, im.shape[1], 'x', im.shape[0])

    # 预览：贴草地色排一行（间距 24px），缩放 2 倍看得清细节
    gap, scale = 24, 2
    tw = sum(im.shape[1] for _, im in items) + gap * (len(items) + 1)
    th = max(im.shape[0] for _, im in items) + gap * 2
    sheet = Image.new('RGBA', (tw, th), (0x4A, 0x8C, 0x3A, 255))
    x = gap
    for _, im in items:
        sheet.paste(Image.fromarray(im, 'RGBA'), (x, th - gap - im.shape[0]), Image.fromarray(im, 'RGBA'))
        x += im.shape[1] + gap
    sheet = sheet.resize((tw * scale, th * scale), Image.NEAREST)
    sheet.save(SHEET)
    print('saved', SHEET)


if __name__ == '__main__':
    main()
