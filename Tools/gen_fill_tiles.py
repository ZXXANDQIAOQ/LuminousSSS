# -*- coding: utf-8 -*-
"""
从 V2 瓦片内部裁纹理、镜像拼接成无缝填充瓦片。

背景：tile001 素材集是「边缘块」风格 —— 每格自带描边/栅栏线/苗行，
直接平铺会出现网格接缝。游戏地面需要无缝填充块，这里取若干瓦片的
内部区域（避开边缘），用镜像四拼（edges 镜像相接，天然无缝）生成。

输出（Assets/Art/V2/Tiles/，PPU 跟随目录 = 64）：
  fill_grass_a/b.png   草地填充
  fill_dirt_a/b.png    土块填充
"""
import os
from PIL import Image, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets/Art/V2/Tiles")

# (输出名, 源瓦片, 裁剪框 left,top,right,bottom)
# 裁剪 32x32 -> 镜像四拼成 64x64；裁 48x48 -> 先缩到 32 再四拼
JOBS = [
    ("fill_grass_a", "tile_r02c02", (6, 6, 38, 38)),
    ("fill_grass_b", "tile_r02c04", (20, 16, 52, 48)),
    ("fill_dirt_a", "tile_r04c16", (18, 8, 50, 40)),
    ("fill_dirt_b", "tile_r03c16", (22, 20, 54, 52)),
    ("fill_sand", "tile_r07c15", (10, 10, 42, 42)),
]


def mirror_quad(patch):
    """左上 patch + 三个镜像拼成 2x 尺寸的无缝块。"""
    w, h = patch.size
    out = Image.new("RGBA", (w * 2, h * 2))
    out.paste(patch, (0, 0))
    out.paste(ImageOps.mirror(patch), (w, 0))
    out.paste(ImageOps.flip(patch), (0, h))
    out.paste(ImageOps.flip(ImageOps.mirror(patch)), (w, h))
    return out


def main():
    for out_name, src, box in JOBS:
        im = Image.open(os.path.join(TILES, src + ".png")).convert("RGBA")
        patch = im.crop(box)
        if patch.size != (32, 32):
            patch = patch.resize((32, 32), Image.LANCZOS)
        tile = mirror_quad(patch)
        p = os.path.join(TILES, out_name + ".png")
        tile.save(p)
        print("saved", p)


if __name__ == "__main__":
    main()
