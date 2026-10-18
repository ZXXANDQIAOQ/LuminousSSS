# -*- coding: utf-8 -*-
"""生成「温馨小洋楼」室内素材（Assets/Art/V2/Interior，PPU 64 -> 1 tile = 64px）。

风格锚点：与 V2 农场素材一致 —— 暖木色 + 深棕硬描边 + 左上 45° 高光，
alpha 只有 0/255（Point 过滤下不会出毛边）。

素材分两类：
  1. 可平铺瓦片（64x64）：木地板 / 瓷砖 / 墙纸 / 侧墙木
  2. 家具（64 的整数倍）：底部对齐摆放，构建器按 sprite 实际高度吊到地面上

重新生成：python Tools/gen_house_interior.py
预览图：  Docs/preview_v2/interior_sheet.png
"""
import os
import sys

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   'Assets', 'Art', 'V2', 'Interior')
SHEET = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                     'Docs', 'preview_v2', 'interior_sheet.png')

# ---------------------------------------------------------------- 调色板
C = {
    'outline':      (0x38, 0x22, 0x10),
    'outline_soft': (0x5A, 0x38, 0x1C),

    'wood':         (0xC0, 0x8A, 0x4E),
    'wood_hi':      (0xDE, 0xAA, 0x6A),
    'wood_dark':    (0x94, 0x62, 0x2E),
    'wood_deep':    (0x5E, 0x3A, 0x16),
    'plank_seam':   (0x7A, 0x4C, 0x22),

    # 地板专用（比家具木色低饱和一档，铺满一屏才不会「满眼橙」）
    'floor_wood':    (0xA8, 0x7A, 0x4C),
    'floor_wood_hi': (0xB4, 0x87, 0x58),
    'floor_wood_lo': (0xA2, 0x74, 0x48),
    'floor_wood_lo_hi': (0xAE, 0x80, 0x52),
    'floor_seam':    (0x89, 0x5E, 0x37),
    'floor_grain':   (0x99, 0x6C, 0x40),

    'wall':         (0xEC, 0xD8, 0xB0),
    'wall_lo':      (0xD6, 0xBC, 0x90),
    'wall_stripe':  (0xE2, 0xCA, 0x9E),
    'wall_shadow':  (0xB8, 0x98, 0x6E),

    'cream':        (0xFF, 0xF3, 0xD0),
    'paper':        (0xF4, 0xE8, 0xCE),

    'terra':        (0xB5, 0x5B, 0x42),
    'terra_hi':     (0xD4, 0x7C, 0x5E),
    'terra_dk':     (0x86, 0x3C, 0x2A),

    'sage':         (0x7C, 0x9B, 0x6A),
    'sage_hi':      (0x9C, 0xBB, 0x82),
    'sage_dk':      (0x50, 0x6B, 0x44),

    'wine':         (0x8E, 0x3B, 0x3B),
    'wine_dk':      (0x64, 0x26, 0x26),

    'gold':         (0xE2, 0xAC, 0x48),
    'gold_hi':      (0xF6, 0xD2, 0x84),
    'gold_dk':      (0xB0, 0x7C, 0x24),

    'glass':        (0xA8, 0xC8, 0xD8),
    'glass_hi':     (0xD2, 0xEA, 0xF4),
    'glass_dk':     (0x74, 0x9A, 0xB0),

    'flame':        (0xF0, 0x94, 0x2C),
    'flame_hi':     (0xFF, 0xDC, 0x7A),

    'stone':        (0xA6, 0x9C, 0x90),
    'stone_hi':     (0xC6, 0xBE, 0xB2),
    'stone_dk':     (0x76, 0x6C, 0x62),

    'white':        (0xFF, 0xFF, 0xFF),
    'shadow':       (0x00, 0x00, 0x00, 0),
}


def rgba(name, a=255):
    c = C[name]
    return (c[0], c[1], c[2], a)


# ---------------------------------------------------------------- 画布与基元

def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def fill(im, x0, y0, x1, y1, col):
    """左闭右开矩形填充。"""
    h, w = im.shape[:2]
    x0, x1 = max(0, int(x0)), min(w, int(x1))
    y0, y1 = max(0, int(y0)), min(h, int(y1))
    if x0 >= x1 or y0 >= y1:
        return
    im[y0:y1, x0:x1] = col


def border(im, x0, y0, x1, y1, col, t=1):
    """矩形描边（左闭右开，向内 t 像素）。"""
    fill(im, x0, y0, x1, y0 + t, col)
    fill(im, x0, y1 - t, x1, y1, col)
    fill(im, x0, y0, x0 + t, y1, col)
    fill(im, x1 - t, y0, x1, y1, col)


def box(im, x0, y0, x1, y1, body='wood', hi='wood_hi', dark='wood_dark',
        outline='outline', ow=2, top_hi=True):
    """带描边 / 左上高光 / 右下阴影的箱体，家具的基本积木。"""
    fill(im, x0, y0, x1, y1, rgba(body))
    if top_hi:
        fill(im, x0 + ow, y0 + ow, x1 - ow, y0 + ow + 2, rgba(hi))
        fill(im, x0 + ow, y0 + ow, x0 + ow + 2, y1 - ow, rgba(hi))
    if dark:
        fill(im, x0 + ow, y1 - ow - 3, x1 - ow, y1 - ow, rgba(dark))
        fill(im, x1 - ow - 3, y0 + ow, x1 - ow, y1 - ow, rgba(dark))
    if outline:
        border(im, x0, y0, x1, y1, rgba(outline), ow)


def hlines(im, x0, x1, ys, col, t=1):
    for y in ys:
        fill(im, x0, y, x1, y + t, col)


def vlines(im, y0, y1, xs, col, t=1):
    for x in xs:
        fill(im, x, y0, x + t, y1, col)


def rounded(im, x0, y0, x1, y1, r, col):
    """近似圆角矩形填充（只在四角挖掉 r 个像素）。"""
    fill(im, x0, y0, x1, y1, col)
    for i in range(r):
        cut = r - i
        fill(im, x0, y0 + i, x0 + cut, y0 + i + 1, (0, 0, 0, 0))
        fill(im, x1 - cut, y0 + i, x1, y0 + i + 1, (0, 0, 0, 0))
        fill(im, x0, y1 - i - 1, x0 + cut, y1 - i, (0, 0, 0, 0))
        fill(im, x1 - cut, y1 - i - 1, x1, y1 - i, (0, 0, 0, 0))


def shift(im, dx, dy):
    out = np.zeros_like(im)
    h, w = im.shape[:2]
    xs0, xs1 = max(0, dx), min(w, w + dx)
    ys0, ys1 = max(0, dy), min(h, h + dy)
    out[ys0:ys1, xs0:xs1] = im[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
    return out


def add(im, other):
    """按 alpha 叠加（other 的颜色覆盖 im，other 全透明处保留 im）。"""
    m = other[:, :, 3] > 0
    im[m] = other[m]
    return im


def disc(im, cx, cy, r, col):
    """实心圆（按行算半宽），用于钟面 / 圆形按钮，比削角干净。"""
    for dy in range(-r, r + 1):
        dx = int((r * r - dy * dy) ** 0.5)
        fill(im, cx - dx, cy + dy, cx + dx + 1, cy + dy + 1, col)


def ellipse(im, cx, cy, rx, ry, col):
    """实心椭圆（叶片 / 圆角软包）。"""
    for dy in range(-ry, ry + 1):
        k = max(0.0, 1.0 - (dy / float(ry or 1)) ** 2) ** 0.5
        w = int(rx * k)
        fill(im, cx - w, cy + dy, cx + w + 1, cy + dy + 1, col)


def diag(im, x0, y0, n, col, t=3):
    """左上到右下的斜线（玻璃反光用）。"""
    for i in range(n):
        fill(im, x0 + i, y0 + i, x0 + i + t, y0 + i + 1, col)


def tri(im, cx, cy, half, height, col, up=True):
    """实心三角（楼梯方向指示）。cy 是宽底边的 y，up=True 时尖端朝上。"""
    for i in range(height):
        w = int(half * (height - i) / height)
        y = cy - i if up else cy + i
        fill(im, cx - w, y, cx + w + 1, y + 1, col)


def cushion(im, x0, y0, x1, y1, body, hi, dark, r=8):
    """圆角软垫：外描边 + 主体 + 顶面高光 + 底部阴影。"""
    rounded(im, x0, y0, x1, y1, r, rgba(dark))
    rounded(im, x0 + 2, y0 + 2, x1 - 2, y1 - 2, max(1, r - 2), rgba(body))
    fill(im, x0 + 4, y0 + 4, x1 - 4, y0 + 7, rgba(hi))
    fill(im, x0 + 4, y1 - 7, x1 - 4, y1 - 3, rgba(dark))


def harden(im):
    """alpha 只保留 0 / 255，硬边。"""
    a = im[:, :, 3]
    im[:, :, 3] = np.where(a >= 128, 255, 0).astype(np.uint8)
    im[im[:, :, 3] == 0] = (0, 0, 0, 0)
    return im


# ---------------------------------------------------------------- 瓦片

def tile_floor_wood(variant=0):
    """木地板：每格两条 32px 宽板，只有细横缝 + 稀疏短竖线，无缝平铺。

    演进过程（都被实机截图否掉过）：
      v1 4 行 16px 窄板 + 每行两条竖断板 → 横竖缝都密，铺满像砖墙；
      v2 2 行宽板但仍保留每行竖断板 + 高对比缝 → 还是有「砌砖」网格感。
    最终版：横缝 1px 且只用略暗的木色（不用深棕），竖线每半格才一条短的。
    木地板读起来应该是「一整片有方向的浅纹」，不是「一格格方块」。
    """
    im = canvas(64, 64)
    base = 'floor_wood_lo' if variant % 2 else 'floor_wood'
    hi = base + '_hi'

    fill(im, 0, 0, 64, 64, rgba(base))
    # 横缝（每 32px 一条）+ 缝上沿亮线
    for y in (31, 63):
        fill(im, 0, y, 64, y + 1, rgba('floor_seam'))
    for y in (0, 32):
        fill(im, 0, y, 64, y + 1, rgba(hi))
    # 稀疏短竖线：每块板一条，只占半格，且两行错位
    for i, y0 in enumerate((0, 32)):
        x = (16 + variant * 23 + i * 29) % 54 + 5
        fill(im, x, y0 + 7, x + 1, y0 + 26, rgba('floor_seam'))
    # 木纹：沿板方向的两条淡线
    for k in range(2):
        x = (12 + variant * 17 + k * 27) % 52 + 3
        fill(im, x, 12 + k * 9, x + 9, 13 + k * 9, rgba('floor_grain'))
    return im


def tile_floor_tile():
    """厨房 / 玄关的米白地砖：32px 棋盘 + 砖缝。"""
    im = canvas(64, 64)
    fill(im, 0, 0, 64, 64, rgba('paper'))
    for by in range(2):
        for bx in range(2):
            if (bx + by) % 2 == 0:
                fill(im, bx * 32, by * 32, bx * 32 + 32, by * 32 + 32, rgba('wall_lo'))
    hlines(im, 0, 64, [0, 31, 32, 63], rgba('wall_shadow'))
    vlines(im, 0, 64, [0, 31, 32, 63], rgba('wall_shadow'))
    # 左上高光
    for by in range(2):
        for bx in range(2):
            fill(im, bx * 32 + 2, by * 32 + 2, bx * 32 + 30, by * 32 + 4, rgba('white'))
    return im


def tile_wall_paper():
    """后墙：上 3/4 竖条纹墙纸 + 下 1/4 深木护墙板 + 踢脚线。"""
    im = canvas(64, 64)
    fill(im, 0, 0, 64, 64, rgba('wall'))
    for x in range(0, 64, 16):
        fill(im, x + 12, 0, x + 16, 48, rgba('wall_stripe'))
    # 顶部壁带
    fill(im, 0, 0, 64, 6, rgba('wood_dark'))
    fill(im, 0, 0, 64, 3, rgba('wood'))
    # 护墙板
    fill(im, 0, 48, 64, 64, rgba('wood'))
    fill(im, 0, 48, 64, 51, rgba('wood_hi'))
    fill(im, 0, 58, 64, 64, rgba('wood_dark'))
    for x in range(0, 64, 22):
        fill(im, x, 51, x + 2, 58, rgba('wood_deep'))
    return im


def tile_wall_wood():
    """侧墙：深一档的竖木板，让左右墙比后墙退后。"""
    im = canvas(64, 64)
    fill(im, 0, 0, 64, 64, rgba('wood_dark'))
    for x in range(0, 64, 16):
        fill(im, x, 0, x + 13, 64, rgba('wood'))
        fill(im, x + 13, 0, x + 16, 64, rgba('wood_deep'))
        fill(im, x + 1, 0, x + 3, 64, rgba('wood_hi'))
    fill(im, 0, 0, 64, 4, rgba('wood_deep'))
    return im


# ---------------------------------------------------------------- 家具

def rug(w=320, h=256):
    """客厅地毯：酒红底 + 金色双边框 + 菱形花纹，四角流苏。"""
    im = canvas(w, h)
    fill(im, 6, 0, w - 6, h, rgba('wine'))
    fill(im, 10, 4, w - 10, h - 4, rgba('wine_dk'))
    fill(im, 16, 10, w - 16, h - 10, rgba('wine'))
    border(im, 16, 10, w - 16, h - 10, rgba('gold'), 3)
    border(im, 26, 20, w - 26, h - 20, rgba('gold_dk'), 2)
    # 中央菱形花纹
    cx, cy = w // 2, h // 2
    for i in range(0, 54, 6):
        fill(im, cx - i, cy - 3, cx + i, cy + 3, rgba('gold_dk') if i % 12 else rgba('gold'))
    for i in range(0, 40, 6):
        fill(im, cx - 3, cy - i, cx + 3, cy + i, rgba('gold_dk') if i % 12 else rgba('gold'))
    # 四角小菱形
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        px = cx + dx * (w // 2 - 48)
        py = cy + dy * (h // 2 - 48)
        fill(im, px - 8, py - 2, px + 8, py + 2, rgba('gold_dk'))
        fill(im, px - 2, py - 8, px + 2, py + 8, rgba('gold_dk'))
    # 流苏
    for x in range(6, w - 6, 8):
        fill(im, x, 0, x + 3, 5, rgba('cream'))
        fill(im, x, h - 5, x + 3, h, rgba('cream'))
    return harden(im)


def sofa():
    """三人沙发 4x2 tile：陶土红布面 + 米色靠垫。"""
    w, h = 256, 128
    im = canvas(w, h)
    # 靠背
    box(im, 8, 4, w - 8, 56, 'terra', 'terra_hi', 'terra_dk', 'outline', 2)
    fill(im, 14, 6, w - 14, 12, rgba('terra_hi'))
    # 三个靠垫
    for i in range(3):
        x0 = 22 + i * 72
        cushion(im, x0, 16, x0 + 60, 50, 'cream', 'white', 'wall_lo', 8)
    # 坐垫
    box(im, 8, 50, w - 8, 96, 'terra_hi', 'cream', 'terra_dk', 'outline', 2)
    for i in range(3):
        x0 = 24 + i * 72
        fill(im, x0, 56, x0 + 2, 88, rgba('terra_dk'))
    fill(im, 14, 52, w - 14, 58, rgba('cream'))
    # 扶手
    box(im, 0, 30, 26, 112, 'terra', 'terra_hi', 'terra_dk', 'outline', 2)
    box(im, w - 26, 30, w, 112, 'terra', 'terra_hi', 'terra_dk', 'outline', 2)
    # 底座 + 四条短腿
    box(im, 10, 96, w - 10, 118, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    for lx in (22, w - 34):
        box(im, lx, 112, lx + 12, 126, 'wood_deep', 'wood_dark', None, 'outline', 2)
    return harden(im)


def armchair():
    """单人扶手椅 2x2：与沙发同色系，靠背更高。"""
    w = h = 128
    im = canvas(w, h)
    box(im, 6, 6, w - 6, 62, 'sage', 'sage_hi', 'sage_dk', 'outline', 2)
    cushion(im, 20, 16, w - 20, 56, 'cream', 'white', 'wall_lo', 10)
    box(im, 6, 56, w - 6, 100, 'sage_hi', 'cream', 'sage_dk', 'outline', 2)
    box(im, 0, 40, 22, 112, 'sage', 'sage_hi', 'sage_dk', 'outline', 2)
    box(im, w - 22, 40, w, 112, 'sage', 'sage_hi', 'sage_dk', 'outline', 2)
    box(im, 8, 100, w - 8, 120, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    for lx in (18, w - 30):
        box(im, lx, 114, lx + 12, 126, 'wood_deep', 'wood_dark', None, 'outline', 2)
    return harden(im)


def coffee_table():
    """茶几 3x1.5：木面 + 一只陶杯 + 一本书。"""
    w, h = 192, 96
    im = canvas(w, h)
    box(im, 4, 8, w - 4, 34, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    fill(im, 10, 12, w - 10, 16, rgba('wood_hi'))
    for lx in (16, w - 30):
        box(im, lx, 30, lx + 14, h - 6, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    # 杯子
    box(im, 62, 4, 78, 12, 'paper', 'white', 'wall_lo', 'outline', 1)
    fill(im, 66, 2, 74, 6, rgba('wood_deep'))
    # 书
    box(im, 104, 6, 146, 12, 'wine', 'terra_hi', 'wine_dk', 'outline', 1)
    fill(im, 118, 8, 122, 10, rgba('cream'))
    return harden(im)


def dining_table():
    """餐桌 4x2.5：桌布 + 两个盘子 + 蜡烛。"""
    w, h = 256, 160
    im = canvas(w, h)
    box(im, 4, 10, w - 4, 42, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    # 桌布
    box(im, 12, 16, w - 12, 40, 'cream', 'white', 'wall_lo', 'outline_soft', 2)
    # 盘子
    for px in (58, 150):
        fill(im, px, 20, px + 34, 36, rgba('white'))
        fill(im, px + 5, 23, px + 29, 33, rgba('wall_lo'))
        fill(im, px + 10, 26, px + 24, 30, rgba('terra'))
    # 蜡烛
    fill(im, 116, 8, 128, 26, rgba('cream'))
    fill(im, 118, 2, 126, 8, rgba('flame'))
    fill(im, 120, 0, 124, 4, rgba('flame_hi'))
    # 桌腿
    for lx in (20, w - 38):
        box(im, lx, 40, lx + 18, h - 6, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    # 后面两张椅子的椅背（露在桌面上方）
    for cx in (54, 168):
        box(im, cx, 0, cx + 36, 14, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
        fill(im, cx + 6, 4, cx + 30, 8, rgba('wood'))
    return harden(im)


def chair():
    """单椅 1x1.5：木框 + 米色坐垫。"""
    w, h = 64, 96
    im = canvas(w, h)
    box(im, 8, 2, w - 8, 44, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    fill(im, 14, 8, w - 14, 14, rgba('wood_hi'))
    box(im, 6, 42, w - 6, 66, 'cream', 'white', 'wall_lo', 'outline', 2)
    for lx in (8, w - 22):
        box(im, lx, 64, lx + 14, h - 4, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    return harden(im)


def bookshelf():
    """书架 2x3：四层彩色书脊。"""
    w, h = 128, 192
    im = canvas(w, h)
    box(im, 0, 0, w, h, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    fill(im, 8, 8, w - 8, h - 8, rgba('wood_deep'))
    book_cols = ['wine', 'sage', 'gold', 'terra', 'glass', 'cream', 'wine_dk']
    for shelf in range(4):
        y0 = 12 + shelf * 44
        # 层板
        box(im, 8, y0 + 34, w - 8, y0 + 40, 'wood', 'wood_hi', 'wood_dark', 'outline_soft', 1)
        x = 14
        k = shelf
        while x < w - 18:
            bw = 6 + (k * 5) % 7
            bh = 22 + (k * 7) % 11
            col = book_cols[(k + shelf) % len(book_cols)]
            fill(im, x, y0 + 34 - bh, x + bw, y0 + 34, rgba(col))
            fill(im, x, y0 + 34 - bh, x + 2, y0 + 34, rgba('cream'))
            x += bw + 2
            k += 1
    return harden(im)


def fireplace():
    """壁炉 3x3：石砌 + 拱形火口 + 火焰 + 木壁炉台。"""
    w = h = 192
    im = canvas(w, h)
    # 石砌主体
    box(im, 10, 18, w - 10, h - 6, 'stone', 'stone_hi', 'stone_dk', 'outline', 3)
    # 石块纹理
    for row in range(5):
        y = 30 + row * 28
        fill(im, 14, y, w - 14, y + 2, rgba('stone_dk'))
        for k in range(3):
            x = 22 + k * 52 + (row % 2) * 26
            fill(im, x, y - 26, x + 2, y, rgba('stone_dk'))
    # 木壁炉台
    box(im, 2, 6, w - 2, 34, 'wood', 'wood_hi', 'wood_dark', 'outline', 3)
    # 拱形火口
    cx = w // 2
    fill(im, cx - 46, 96, cx + 46, h - 12, rgba('wood_deep'))
    for i in range(20):
        fill(im, cx - 46 + i, 96 - i, cx + 46 - i, 97 - i, rgba('wood_deep'))
    fill(im, cx - 46, 80, cx + 46, 96, rgba('wood_deep'))
    # 火焰
    for i, (fw, fy, col) in enumerate([(34, 120, 'flame'), (22, 108, 'flame_hi'), (12, 96, 'cream')]):
        fill(im, cx - fw // 2, fy, cx + fw // 2, h - 16, rgba(col))
    fill(im, cx - 6, 104, cx + 6, 112, rgba('flame_hi'))
    # 余烬
    fill(im, cx - 30, h - 22, cx + 30, h - 14, rgba('wine_dk'))
    return harden(im)


def counter():
    """厨房操作台 3x2：柜体 + 台面 + 水槽 + 锅。"""
    w, h = 192, 128
    im = canvas(w, h)
    box(im, 4, 8, w - 4, h - 6, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    # 台面
    box(im, 0, 8, w, 30, 'stone_hi', 'white', 'stone', 'outline', 2)
    # 柜门
    for i in range(2):
        x0 = 16 + i * 82
        box(im, x0, 38, x0 + 70, h - 14, 'wood_dark', 'wood', 'wood_deep', 'outline_soft', 2)
        fill(im, x0 + 30, 60, x0 + 40, 68, rgba('gold'))
    # 水槽
    fill(im, 30, 14, 92, 26, rgba('glass_dk'))
    fill(im, 34, 16, 88, 20, rgba('glass'))
    fill(im, 56, 10, 64, 16, rgba('stone'))
    # 锅
    fill(im, 120, 6, 162, 16, rgba('wine_dk'))
    fill(im, 126, 4, 156, 8, rgba('wine'))
    fill(im, 138, 0, 146, 6, rgba('stone_dk'))
    return harden(im)


def window():
    """窗 2x2：木框 + 玻璃 + 十字格 + 暖色窗台。"""
    w = h = 128
    im = canvas(w, h)
    box(im, 4, 4, w - 4, h - 10, 'wood', 'wood_hi', 'wood_dark', 'outline', 3)
    fill(im, 16, 16, w - 16, h - 22, rgba('glass'))
    # 玻璃反光：两条斜光带（不透明浅蓝，硬边下才不糊）
    diag(im, 24, 20, 10, rgba('glass_hi'), 4)
    diag(im, 40, 20, 6, rgba('glass_hi'), 3)
    fill(im, 20, h - 52, w - 20, h - 30, rgba('sage_hi'))
    # 十字格
    fill(im, w // 2 - 3, 16, w // 2 + 3, h - 22, rgba('wood'))
    fill(im, 16, (h - 16) // 2 - 3, w - 16, (h - 16) // 2 + 3, rgba('wood'))
    # 窗台
    box(im, 0, h - 22, w, h - 6, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    return harden(im)


def painting():
    """挂画 2x1.5：金木框 + 田园风景。"""
    w, h = 128, 96
    im = canvas(w, h)
    box(im, 2, 2, w - 2, h - 6, 'gold', 'gold_hi', 'gold_dk', 'outline', 3)
    fill(im, 14, 14, w - 14, h - 20, rgba('glass'))
    # 天空 / 山 / 草地
    fill(im, 14, 14, w - 14, 46, rgba('glass'))
    fill(im, 14, 40, w - 14, 56, rgba('sage_dk'))
    fill(im, 14, 52, w - 14, h - 20, rgba('sage'))
    # 太阳
    fill(im, 82, 22, 96, 36, rgba('gold_hi'))
    fill(im, 86, 26, 92, 32, rgba('gold'))
    # 小房子
    fill(im, 34, 40, 60, 58, rgba('cream'))
    fill(im, 30, 34, 64, 42, rgba('terra'))
    fill(im, 42, 46, 50, 58, rgba('wood_deep'))
    return harden(im)


def plant():
    """盆栽 1.5x2：陶盆 + 五片叶子（椭圆叶簇 + 叶脉）。"""
    w, h = 96, 128
    im = canvas(w, h)
    leaves = [(-18, -40, 17, 13), (18, -42, 17, 13), (0, -58, 19, 14),
              (-11, -33, 15, 11), (12, -30, 15, 11)]
    for dx, dy, rx, ry in leaves:                     # 深色外轮廓
        ellipse(im, w // 2 + dx, 62 + dy, rx + 2, ry + 2, rgba('sage_dk'))
    for dx, dy, rx, ry in leaves:
        ellipse(im, w // 2 + dx, 62 + dy, rx, ry, rgba('sage'))
        ellipse(im, w // 2 + dx - 4, 62 + dy - 4, rx // 2, max(2, ry // 3), rgba('sage_hi'))
        fill(im, w // 2 + dx, 62 + dy - ry + 2, w // 2 + dx + 2, 62 + dy + ry - 2, rgba('sage_dk'))
    fill(im, w // 2 - 3, 56, w // 2 + 3, 86, rgba('wood_deep'))
    # 陶盆
    box(im, 20, 82, w - 20, h - 6, 'terra', 'terra_hi', 'terra_dk', 'outline', 2)
    fill(im, 16, 78, w - 16, 90, rgba('terra_hi'))
    border(im, 16, 78, w - 16, 90, rgba('outline'), 2)
    return harden(im)


def floor_lamp():
    """落地灯 1x2.75：暖黄灯罩 + 细杆 + 圆底座。"""
    w, h = 64, 176
    im = canvas(w, h)
    # 灯罩
    box(im, 6, 4, w - 6, 46, 'gold_hi', 'cream', 'gold_dk', 'outline', 2)
    fill(im, 10, 40, w - 10, 44, rgba('flame'))
    fill(im, 14, 12, 26, 18, rgba('cream'))
    # 杆
    fill(im, w // 2 - 3, 44, w // 2 + 3, h - 20, rgba('wood_deep'))
    fill(im, w // 2 - 3, 44, w // 2 - 1, h - 20, rgba('wood_dark'))
    # 底座
    fill(im, 12, h - 16, w - 12, h - 6, rgba('wood_deep'))
    fill(im, 16, h - 20, w - 16, h - 14, rgba('wood_dark'))
    return harden(im)


def table_lamp():
    """床头台灯 1x1.5。"""
    w, h = 64, 96
    im = canvas(w, h)
    box(im, 12, 2, w - 12, 40, 'gold_hi', 'cream', 'gold_dk', 'outline', 2)
    fill(im, 16, 34, w - 16, 38, rgba('flame'))
    fill(im, w // 2 - 3, 38, w // 2 + 3, 78, rgba('wood_deep'))
    fill(im, 16, 78, w - 16, h - 6, rgba('wood_dark'))
    border(im, 16, 78, w - 16, h - 6, rgba('outline'), 2)
    return harden(im)


def stairs(up=True):
    """楼梯 3x3：踏步 + 扶手；up=上楼（踏步向里收），down=下楼（踏步向外扩）。"""
    w = h = 192
    im = canvas(w, h)
    box(im, 4, 4, w - 4, h - 4, 'wood_dark', 'wood', 'wood_deep', 'outline', 3)
    steps = 6
    for i in range(steps):
        y0 = 12 + i * 28
        inset = (i * 10) if up else ((steps - 1 - i) * 10)
        x0, x1 = 16 + inset, w - 16 - inset
        fill(im, x0, y0, x1, y0 + 24, rgba('wood' if (i % 2 == 0) else 'wood_hi'))
        fill(im, x0, y0, x1, y0 + 4, rgba('wood_hi'))
        fill(im, x0, y0 + 24, x1, y0 + 28, rgba('wood_deep'))
    # 两侧扶手
    for side in (0, 1):
        x = 8 if side == 0 else w - 20
        fill(im, x, 8, x + 12, h - 8, rgba('wood_dark'))
        fill(im, x, 8, x + 4, h - 8, rgba('wood'))
    # 方向箭头：实心三角 + 深色描边（上楼尖朝上 / 下楼尖朝下）
    cx, cy = w // 2, h // 2
    if up:
        tri(im, cx, cy + 22, 24, 28, rgba('wood_deep'), up=True)
        tri(im, cx, cy + 20, 17, 21, rgba('gold_hi'), up=True)
    else:
        tri(im, cx, cy - 22, 24, 28, rgba('wood_deep'), up=False)
        tri(im, cx, cy - 20, 17, 21, rgba('gold_hi'), up=False)
    return harden(im)


def door():
    """室内木门 2x2.5：门框 + 双门板 + 把手。"""
    w, h = 128, 160
    im = canvas(w, h)
    box(im, 0, 0, w, h, 'wood_dark', 'wood', 'wood_deep', 'outline', 3)
    box(im, 12, 12, w - 12, h - 4, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    # 门板
    for i in range(2):
        y0 = 22 + i * 62
        box(im, 24, y0, w - 24, y0 + 50, 'wood_dark', 'wood', 'wood_deep', 'outline_soft', 2)
        fill(im, 30, y0 + 6, w - 30, y0 + 10, rgba('wood'))
    # 把手
    fill(im, w - 40, h // 2 + 10, w - 30, h // 2 + 18, rgba('gold'))
    fill(im, w - 38, h // 2 + 12, w - 32, h // 2 + 16, rgba('gold_hi'))
    return harden(im)


def bed():
    """双人床 4x5：木床头板 + 床垫 + 被子 + 两个枕头。"""
    w, h = 256, 320
    im = canvas(w, h)
    # 床头板
    box(im, 6, 0, w - 6, 54, 'wood', 'wood_hi', 'wood_dark', 'outline', 3)
    fill(im, 20, 10, w - 20, 20, rgba('wood_hi'))
    # 床架
    box(im, 10, 46, w - 10, h - 8, 'wood_dark', 'wood', 'wood_deep', 'outline', 3)
    # 床垫
    box(im, 18, 54, w - 18, h - 22, 'cream', 'white', 'wall_lo', 'outline_soft', 2)
    # 枕头
    for px in (30, 138):
        box(im, px, 62, px + 88, 116, 'paper', 'white', 'wall_lo', 'outline', 2)
        fill(im, px + 10, 68, px + 78, 74, rgba('white'))
    # 被子（陶土红 + 米色翻边）
    box(im, 18, 128, w - 18, h - 30, 'terra', 'terra_hi', 'terra_dk', 'outline', 2)
    fill(im, 18, 128, w - 18, 150, rgba('cream'))
    fill(im, 18, 146, w - 18, 152, rgba('wall_lo'))
    # 被子格纹（细线，别抢戏）
    for x in range(40, w - 30, 46):
        fill(im, x, 156, x + 2, h - 36, rgba('terra_dk'))
    for y in range(176, h - 40, 44):
        fill(im, 24, y, w - 24, y + 2, rgba('terra_dk'))
    # 床尾板
    box(im, 10, h - 34, w - 10, h - 8, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    return harden(im)


def nightstand():
    """床头柜 1.5x1.5：一个抽屉 + 金把手。"""
    w = h = 96
    im = canvas(w, h)
    box(im, 4, 6, w - 4, h - 8, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    box(im, 14, 18, w - 14, 46, 'wood_dark', 'wood', 'wood_deep', 'outline_soft', 2)
    fill(im, w // 2 - 10, 30, w // 2 + 10, 36, rgba('gold'))
    box(im, 14, 52, w - 14, h - 16, 'wood_dark', 'wood', 'wood_deep', 'outline_soft', 2)
    fill(im, w // 2 - 10, 62, w // 2 + 10, 68, rgba('gold'))
    return harden(im)


def wardrobe():
    """衣柜 3x3.5：双开门 + 顶线 + 把手。"""
    w, h = 192, 224
    im = canvas(w, h)
    box(im, 2, 0, w - 2, h - 4, 'wood', 'wood_hi', 'wood_dark', 'outline', 3)
    fill(im, 2, 0, w - 2, 12, rgba('wood_dark'))
    for i in range(2):
        x0 = 14 + i * 84
        box(im, x0, 20, x0 + 76, h - 16, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
        box(im, x0 + 10, 30, x0 + 66, 110, 'wood', 'wood_hi', 'wood_dark', 'outline_soft', 2)
        box(im, x0 + 10, 122, x0 + 66, h - 28, 'wood', 'wood_hi', 'wood_dark', 'outline_soft', 2)
        fill(im, x0 + (60 if i == 0 else 12), 118, x0 + (70 if i == 0 else 22), 132, rgba('gold'))
    return harden(im)


def desk():
    """书桌 3x2：桌面 + 抽屉 + 台灯 + 摊开的书。"""
    w, h = 192, 128
    im = canvas(w, h)
    box(im, 2, 10, w - 2, 40, 'wood', 'wood_hi', 'wood_dark', 'outline', 2)
    # 抽屉
    box(im, 18, 40, w - 18, 74, 'wood_dark', 'wood', 'wood_deep', 'outline', 2)
    for i in range(2):
        x0 = 28 + i * 76
        box(im, x0, 46, x0 + 64, 68, 'wood', 'wood_hi', 'wood_dark', 'outline_soft', 2)
        fill(im, x0 + 26, 54, x0 + 38, 60, rgba('gold'))
    # 桌腿
    for lx in (10, w - 26):
        box(im, lx, 72, lx + 16, h - 8, 'wood_deep', 'wood_dark', None, 'outline', 2)
    # 书
    box(im, 60, 4, 108, 12, 'cream', 'white', 'wall_lo', 'outline', 1)
    fill(im, 82, 2, 86, 12, rgba('wine'))
    # 墨水瓶
    fill(im, 124, 2, 140, 12, rgba('wine_dk'))
    fill(im, 128, 0, 136, 4, rgba('wine'))
    return harden(im)


def clock():
    """挂钟 1.5x1.5：木框 + 奶白表盘 + 指针。"""
    w = h = 96
    im = canvas(w, h)
    box(im, 6, 2, w - 6, h - 8, 'wood', 'wood_hi', 'wood_dark', 'outline', 3)
    cx, cy, r = w // 2, h // 2 - 2, 30
    disc(im, cx, cy, r + 3, rgba('wood_dark'))
    disc(im, cx, cy, r, rgba('paper'))
    disc(im, cx, cy, r - 3, rgba('wall'))
    # 刻度
    for k in range(12):
        ang = k * np.pi / 6
        px = int(cx + np.cos(ang) * (r - 6))
        py = int(cy + np.sin(ang) * (r - 6))
        fill(im, px - 1, py - 1, px + 2, py + 2, rgba('wood_dark'))
    # 指针
    fill(im, cx - 2, cy - 20, cx + 2, cy + 2, rgba('outline'))
    fill(im, cx, cy, cx + 16, cy + 3, rgba('outline'))
    fill(im, cx - 4, cy - 4, cx + 4, cy + 4, rgba('gold'))
    return harden(im)


# ---------------------------------------------------------------- 输出

ASSETS = [
    ('v2_in_floor_wood_a',  tile_floor_wood(0)),
    ('v2_in_floor_wood_b',  tile_floor_wood(1)),
    ('v2_in_floor_tile',    tile_floor_tile()),
    ('v2_in_wall_paper',    tile_wall_paper()),
    ('v2_in_wall_wood',     tile_wall_wood()),
    ('v2_in_rug_red',       rug()),
    ('v2_in_sofa',          sofa()),
    ('v2_in_armchair',      armchair()),
    ('v2_in_coffee_table',  coffee_table()),
    ('v2_in_dining_table',  dining_table()),
    ('v2_in_chair',         chair()),
    ('v2_in_bookshelf',     bookshelf()),
    ('v2_in_fireplace',     fireplace()),
    ('v2_in_counter',       counter()),
    ('v2_in_window',        window()),
    ('v2_in_painting',      painting()),
    ('v2_in_plant',         plant()),
    ('v2_in_floor_lamp',    floor_lamp()),
    ('v2_in_table_lamp',    table_lamp()),
    ('v2_in_stairs_up',     stairs(True)),
    ('v2_in_stairs_down',   stairs(False)),
    ('v2_in_door',          door()),
    ('v2_in_bed',           bed()),
    ('v2_in_nightstand',    nightstand()),
    ('v2_in_wardrobe',      wardrobe()),
    ('v2_in_desk',          desk()),
    ('v2_in_clock',         clock()),
]


def write_sheet(items):
    """拼一张总览图，按格子排列并标注文件名（用默认字体，仅用于自检）。"""
    from PIL import ImageDraw
    cell_w, cell_h = 300, 300
    cols = 6
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell_w, rows * cell_h), (38, 34, 30, 255))
    draw = ImageDraw.Draw(sheet)
    for i, (name, im) in enumerate(items):
        cx, cy = (i % cols) * cell_w, (i // cols) * cell_h
        img = Image.fromarray(im, 'RGBA')
        # 等比缩到 240x240 内并居中
        k = min(240 / img.width, 240 / img.height, 3.0)
        img = img.resize((max(1, int(img.width * k)), max(1, int(img.height * k))), Image.NEAREST)
        sheet.alpha_composite(img, (cx + (cell_w - img.width) // 2, cy + 24 + (240 - img.height) // 2))
        draw.text((cx + 12, cy + 6), name, fill=(240, 230, 200, 255))
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    sheet.save(SHEET)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, im in ASSETS:
        Image.fromarray(harden(im), 'RGBA').save(os.path.join(OUT, name + '.png'))
    write_sheet(ASSETS)
    total = sum(1 for _ in os.listdir(OUT) if _.endswith('.png'))
    print(f'[interior] 已生成 {len(ASSETS)} 张室内素材 -> {OUT}（目录内共 {total} 张 png）')
    print(f'[interior] 总览图 -> {SHEET}')


if __name__ == '__main__':
    sys.exit(main())
