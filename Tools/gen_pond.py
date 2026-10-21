# -*- coding: utf-8 -*-
"""生成农场右侧的池塘（Assets/Art/V2/Nature/v2_nature_pond.png，PPU 64）。

为什么整块池塘画成一张大图，而不是用 Tilemap 拼水面瓦片：
  瓦片拼不规则水岸需要 16 方向过渡（或 47 张 autotile），否则岸线是方块台阶。
  池塘是个固定大小的独立景观，一张 10x6 格（640x384）的图能直接画出
  波浪岸线、泡沫圈、浅滩渐变和睡莲，构建器只要按「底部对齐 + 碰撞盒」摆一次，
  后续替换美术也只换这一个 png。

构图（世界 10 x 6 格，图片坐标 y 向下）：
  外圈 深泥描边 -> 沙岸 -> 湿泥 -> 泡沫 -> 水面（中心深、近岸浅）
  水面上加横向波纹 + 高光点 + 3 片睡莲

重新生成：python Tools/gen_pond.py
预览：    Docs/preview_v2/pond_preview.png
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_NATURE = os.path.join(ROOT, 'Assets', 'Art', 'V2', 'Nature')
PREVIEW = os.path.join(ROOT, 'Docs', 'preview_v2')

TILE = 64
GRID_W, GRID_H = 10, 6   # 农场右侧可用宽度只有 x 28..39，10x6 刚好放下且两侧留出草地
W, H = GRID_W * TILE, GRID_H * TILE

# ---- 调色板（与 V2 农场素材同一套暖调；水面偏青绿，冷得克制，不抢草地）
BANK_OUTLINE = (0x7A, 0x5A, 0x30)
BANK_SAND = (0xC9, 0xAC, 0x72)
BANK_SAND_HI = (0xDD, 0xC2, 0x8C)
BANK_WET = (0x9A, 0x7C, 0x4A)

WATER_DEEP = (0x2A, 0x5F, 0x6E)     # 池心
WATER_MID = (0x34, 0x74, 0x7F)
WATER_EDGE = (0x45, 0x8B, 0x92)     # 近岸浅水
FOAM = (0x9E, 0xD2, 0xCE)
FOAM_HI = (0xC8, 0xEA, 0xE4)
WAVE_HI = (0x62, 0xA8, 0xAD)

LILY = (0x4E, 0x8A, 0x45)
LILY_HI = (0x6D, 0xAB, 0x5C)
LILY_SHADOW = (0x33, 0x5F, 0x30)
LILY_FLOWER = (0xF0, 0xA8, 0xC0)


def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def put(im, mask, col):
    """把布尔 mask 区域涂成 col（不透明，alpha=255）。"""
    im[mask] = (col[0], col[1], col[2], 255)


def lerp(a, b, t):
    return (int(a[0] + (b[0] - a[0]) * t),
            int(a[1] + (b[1] - a[1]) * t),
            int(a[2] + (b[2] - a[2]) * t))


def build_pond():
    im = canvas(W, H)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

    cx, cy = W / 2.0, H / 2.0
    rx, ry = W * 0.48, H * 0.46

    # 归一化椭圆距离；用两段正弦扰动把岸线打得不规则（否则是个死板椭圆）
    nx = (xx - cx) / rx
    ny = (yy - cy) / ry
    theta = np.arctan2(ny, nx)
    wobble = 1.0 + 0.045 * np.sin(theta * 7.0) + 0.028 * np.sin(theta * 13.0 + 1.1)
    d = np.sqrt(nx * nx + ny * ny) * wobble

    # ---- 分层（从外到内）
    put(im, d <= 1.00, BANK_OUTLINE)          # 最外描边
    put(im, d <= 0.965, BANK_SAND)
    put(im, d <= 0.90, BANK_SAND)             # 沙岸主体
    put(im, d <= 0.875, BANK_WET)             # 湿泥（贴水那一圈深色）
    put(im, d <= 0.845, FOAM)                 # 泡沫圈
    put(im, d <= 0.825, FOAM_HI)

    # ---- 水面：越靠中心越深，但做成 5 档阶梯而不是平滑渐变（像素风要"能数出色阶"）
    water = d <= 0.815
    t = np.clip(d / 0.815, 0.0, 1.0)          # 0=池心 1=近岸
    steps = np.minimum((t * 5).astype(np.int32), 4)
    shades = [
        WATER_DEEP,
        lerp(WATER_DEEP, WATER_MID, 0.45),
        WATER_MID,
        lerp(WATER_MID, WATER_EDGE, 0.55),
        WATER_EDGE,
    ]
    for i, col in enumerate(shades):
        put(im, water & (steps == i), col)

    # ---- 沙岸高光：让"土"有点颗粒感（按坐标哈希撒点，确定性）
    rng = np.random.default_rng(20261021)
    for _ in range(420):
        x = int(rng.integers(0, W))
        y = int(rng.integers(0, H))
        if 0.875 < d[y, x] <= 0.965:
            im[y, x] = (BANK_SAND_HI[0], BANK_SAND_HI[1], BANK_SAND_HI[2], 255)

    # ---- 横向波纹：稀疏细线（约每 37px 一条）。
    # 频率是第一版的三分之一 —— 0.55 rad/px 会在 448px 里塞进 40 条，
    # 密到看着像百叶窗；稀疏 + 扭曲才像水面。
    phase = yy * 0.17 + np.sin(xx * 0.018) * 2.2
    s_phase = np.sin(phase)
    band = (s_phase > 0.90) & (s_phase <= 0.97)
    put(im, water & band & (t > 0.15), WAVE_HI)

    # 亮线下面压一条暗线，读起来是"波峰+波谷"而不是单纯白条纹
    band2 = (s_phase > 0.80) & (s_phase <= 0.90)
    for i, col in enumerate(shades):
        m = water & band2 & (steps == i) & (t > 0.15)
        im[m] = (max(0, col[0] - 12), max(0, col[1] - 10), max(0, col[2] - 8), 255)

    # ---- 高光点（水面反光，稀疏小点）
    for _ in range(120):
        x = int(rng.integers(0, W))
        y = int(rng.integers(0, H))
        if water[y, x] and t[y, x] < 0.75:
            im[y, x] = (FOAM_HI[0], FOAM_HI[1], FOAM_HI[2], 255)

    # ---- 睡莲（3 片荷叶 + 1 朵花）—— 有水生植物的信号，比纯水面更好认
    for (lx, ly, r) in ((int(W * 0.30), int(H * 0.42), 26),
                        (int(W * 0.36), int(H * 0.55), 20),
                        (int(W * 0.68), int(H * 0.58), 24)):
        yy2, xx2 = np.mgrid[0:H, 0:W]
        disc = (xx2 - lx) ** 2 + (yy2 - ly) ** 2 <= r * r
        # 切一个缺口，荷叶才不是纯圆
        notch = (xx2 > lx) & (np.abs(yy2 - ly) < r * 0.18)
        leaf = disc & ~notch & water
        put(im, leaf, LILY)
        # 左上高光半环
        hi = leaf & (((xx2 - lx + r * 0.25) ** 2 + (yy2 - ly + r * 0.3) ** 2) <= (r * 0.55) ** 2)
        put(im, hi, LILY_HI)
        # 右下压暗
        dk = leaf & (((xx2 - lx - r * 0.45) ** 2 + (yy2 - ly - r * 0.45) ** 2) <= (r * 0.45) ** 2)
        put(im, dk, LILY_SHADOW)

    # 一朵睡莲花（在最大那片叶子上）
    fx, fy = int(W * 0.30) + 6, int(H * 0.42) - 10
    yy3, xx3 = np.mgrid[0:H, 0:W]
    petal = (np.abs(xx3 - fx) / 7.0) ** 2 + (np.abs(yy3 - fy) / 5.0) ** 2 <= 1.0
    put(im, petal & water, LILY_FLOWER)

    return im


def main():
    os.makedirs(OUT_NATURE, exist_ok=True)
    im = build_pond()

    path = os.path.join(OUT_NATURE, 'v2_nature_pond.png')
    Image.fromarray(im, 'RGBA').save(path)
    print('saved', path, im.shape[1], 'x', im.shape[0])

    # 预览：放大 1x（本来就够大），并贴在草地色上，模拟实际铺在农场里的观感
    os.makedirs(PREVIEW, exist_ok=True)
    sheet = Image.new('RGBA', (W + 64, H + 64), (0x4A, 0x8C, 0x3A, 255))
    sheet.paste(Image.fromarray(im, 'RGBA'), (32, 32), Image.fromarray(im, 'RGBA'))
    prev = os.path.join(PREVIEW, 'pond_preview.png')
    sheet.save(prev)
    print('saved', prev)


if __name__ == '__main__':
    main()
