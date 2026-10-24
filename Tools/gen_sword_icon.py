#!/usr/bin/env python3
"""生成铁匠铺的剑图标（v2_item_sword.png）。
风格：64×64 硬边像素，铁灰剑身 + 棕色剑柄 + 金色护手。
"""
import os, sys
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   '..', 'Assets', 'Art', 'V2', 'Items', 'v2_item_sword.png')
OUT = os.path.abspath(OUT)

def main():
    W = H = 64
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    # 调色板
    blade_light = (200, 210, 220, 255)   # 剑身亮面
    blade_dark  = (140, 150, 165, 255)   # 剑身暗面
    blade_edge  = (90, 95, 110, 255)     # 剑身描边
    guard_gold  = (220, 180, 60, 255)    # 护手
    guard_dark  = (170, 130, 30, 255)
    handle_brown = (120, 80, 40, 255)    # 剑柄
    handle_dark  = (80, 50, 25, 255)

    # 剑身：从右上到左下斜着画（对角线）
    # 剑尖在 (52, 12)，剑根在 (18, 46)
    for i in range(28):
        x = 52 - i
        y = 12 + i
        # 剑身宽度 4 像素
        for w in range(4):
            px = x + w
            py = y + w
            if 0 <= px < W and 0 <= py < H:
                if w == 0:
                    d.point((px, py), blade_edge)
                elif w == 3:
                    d.point((px, py), blade_edge)
                else:
                    d.point((px, py), blade_light if w == 1 else blade_dark)

    # 剑尖（三角形收尖）
    for i in range(4):
        for w in range(4 - i):
            px = 52 - i + w
            py = 12 + i + w
            if 0 <= px < W and 0 <= py < H:
                d.point((px, py), blade_light)

    # 护手：垂直于剑身的短横条
    for i in range(8):
        x = 14 + i
        y = 44 + i
        for w in range(6):
            px = x + w - 2
            py = y - w + 2
            if 0 <= px < W and 0 <= py < H:
                d.point((px, py), guard_gold if (i + w) % 2 == 0 else guard_dark)

    # 剑柄：从护手往左下
    for i in range(12):
        x = 16 - i
        y = 46 + i
        for w in range(3):
            px = x + w
            py = y + w
            if 0 <= px < W and 0 <= py < H:
                d.point((px, py), handle_brown if w == 1 else handle_dark)

    # 剑柄末端圆头
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            px = 4 + dx
            py = 58 + dy
            if 0 <= px < W and 0 <= py < H:
                d.point((px, py), handle_brown)

    im.save(OUT)
    print(f'已生成 {OUT}')

if __name__ == '__main__':
    main()
