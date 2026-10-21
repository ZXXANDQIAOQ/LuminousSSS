# -*- coding: utf-8 -*-
"""
生成「耕地」填充瓦片（砍树后铺在树原位的开垦地面）。

为什么从 fill_dirt_a 派生、而不是另画一张：
  土块瓦片是从 tiled 素材集镜像拼出来的，它的调色、噪点密度、明度阶梯
  和草地/沙土是同一套。新画一张很难对上这套色阶，平铺时接缝会露出色差。
  派生做法只做「压暗 + 偏暖 + 加断续犁沟」，既保住原纹理，又能一眼区分
  「没开垦的土」和「已经犁过的田」。

犁沟画法（关键，试过两版才对）：
  - 沟宽 1px、压暗 0.58，**沿 x 断续**（每行随机跳过约 18% 的像素）。
    连续 2px 黑线会读成「木板缝/地砖缝」，断续才像耙齿划过的犁沟。
  - 沟下方 1px 提亮 1.10，是受光的垄背；不画这会显得整片发闷。
  - 沟的位置固定（y=8 / y=40），保证相邻格横向能接上；断续点每格不同，
    所以平铺看不出重复。

输出（Assets/Art/V2/Tiles/，该目录 PPU=64，由 PixelArtImporter 自动设置）：
  fill_tilled_a.png  耕地
  fill_tilled_b.png  耕地变体（整体略亮、断续点不同）
预览：
  Docs/preview_v2/farmland_preview.png
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets/Art/V2/Tiles")
PREVIEW = os.path.join(ROOT, "Docs/preview_v2")

SRC = "fill_dirt_a"          # 派生基底
SIZE = 64

# 犁沟行（图像坐标 y，从上往下）
FURROW_ROWS = (8, 40)

# 两个变体：(整体压暗, 偏红, 偏绿, 偏蓝, 沟深系数, 断续跳过率)
VARIANTS = {
    "fill_tilled_a": (0.70, 1.07, 0.92, 0.78, 0.58, 0.18),
    "fill_tilled_b": (0.73, 1.05, 0.94, 0.81, 0.62, 0.24),
}


def build(src_arr, darken, kr, kg, kb, furrow_k, skip, seed):
    """返回处理后的 uint8 RGBA 数组。"""
    arr = src_arr.astype(np.float32).copy()
    rgb = arr[:, :, :3]
    rng = np.random.default_rng(seed)

    # 1) 整体压暗 + 偏暖：翻开的土比表面干土更深、更红
    rgb[:, :, 0] *= darken * kr
    rgb[:, :, 1] *= darken * kg
    rgb[:, :, 2] *= darken * kb

    # 2) 断续犁沟：1px 沟 + 下方 1px 垄背受光
    for y0 in FURROW_ROWS:
        y = y0 % SIZE
        mask = rng.random(SIZE) >= skip          # False 的位置留白 = 沟断续
        rgb[y, mask, :] *= furrow_k
        rgb[(y + 1) % SIZE, mask, :] *= 1.10

    # 3) 沟里撒几粒碎石/土块，打散规律感
    for y0 in FURROW_ROWS:
        for _ in range(6):
            x = int(rng.integers(0, SIZE))
            y = (y0 + int(rng.integers(0, 2))) % SIZE
            rgb[y, x, :] *= 0.82
            rgb[y, (x + 1) % SIZE, :] *= 0.90

    out = np.clip(arr, 0, 255).astype(np.uint8)
    out[:, :, 3] = 255          # 硬边像素：alpha 只有 0/255，不留半透明
    return out


def main():
    src_path = os.path.join(TILES, SRC + ".png")
    src_im = Image.open(src_path).convert("RGBA")
    if src_im.size != (SIZE, SIZE):
        src_im = src_im.resize((SIZE, SIZE), Image.NEAREST)
    src_arr = np.array(src_im)

    made = []
    for i, (name, params) in enumerate(VARIANTS.items()):
        arr = build(src_arr, *params, seed=20261018 + i)
        im = Image.fromarray(arr, "RGBA")
        p = os.path.join(TILES, name + ".png")
        im.save(p)
        made.append(im)
        print("saved", p)

    # 预览：2 变体各平铺 3x2，模拟游戏里棋盘混铺的效果
    os.makedirs(PREVIEW, exist_ok=True)
    cols, rows, scale = 6, 3, 4
    sheet = Image.new("RGBA", (SIZE * cols, SIZE * rows), (0, 0, 0, 255))
    for gy in range(rows):
        for gx in range(cols):
            im = made[(gx + gy) & 1]          # 相邻格用不同变体
            sheet.paste(im, (gx * SIZE, gy * SIZE))
    sheet = sheet.resize((SIZE * cols * scale, SIZE * rows * scale), Image.NEAREST)
    sheet_path = os.path.join(PREVIEW, "farmland_preview.png")
    sheet.save(sheet_path)
    print("saved", sheet_path)

    # 对比图：土块底 + 棋盘开垦出 4 格耕地，直观看出「砍完和没砍」的差别。
    # 开垦格按 (x+y) 奇偶取变体，与 GameSceneBuilder 里树的梅花型种植一致。
    dirt = [Image.open(os.path.join(TILES, n + ".png")).convert("RGBA")
            for n in ("fill_dirt_a", "fill_dirt_b")]
    ccols, crows = 5, 3
    cmp_sheet = Image.new("RGBA", (SIZE * ccols, SIZE * crows), (0, 0, 0, 255))
    for gy in range(crows):
        for gx in range(ccols):
            tilled = (gx + gy) % 2 == 0 and gx not in (0, ccols - 1)
            if tilled:
                cmp_sheet.paste(made[(gx + gy) & 1], (gx * SIZE, gy * SIZE))
            else:
                cmp_sheet.paste(dirt[(gx + gy) & 1], (gx * SIZE, gy * SIZE))
    cmp_sheet = cmp_sheet.resize((SIZE * ccols * scale, SIZE * crows * scale), Image.NEAREST)
    cmp_path = os.path.join(PREVIEW, "farmland_compare.png")
    cmp_sheet.save(cmp_path)
    print("saved", cmp_path)


if __name__ == "__main__":
    main()
