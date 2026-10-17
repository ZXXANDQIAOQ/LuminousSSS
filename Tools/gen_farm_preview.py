# -*- coding: utf-8 -*-
"""
按 GameSceneBuilder.BuildFarmScene 的同一份坐标渲染农场预览图（自检用）。
坐标系：Unity tile (x,y) 占世界 [x,x+1]x[y,y+1]；预览图 y 轴翻转。
输出：Docs/preview_v2/08_farm_scene_layout.png

v0.12：同步「田地种树（砍伐前田地不可见）+ 空地加密 + HUD 砍伐按钮」。
HUD 按钮/摇杆按 UI 参考分辨率 1920x1080 映射（y 方向缩放 H/1080）。
"""
import math
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
V2 = os.path.join(ROOT, "Assets/Art/V2")
OUT = os.path.join(ROOT, "Docs/preview_v2/08_farm_scene_layout.png")

MAP_W, MAP_H, WALL_ROWS = 40, 24, 2
DIRT = (6, 4, 22, 12)          # x, y, w, h
PATH_LEFT, PATH_TOP = 29, 14
HOUSE_DOOR_X = 30.0

# HUD（v0.12）：与 GameSceneBuilder 常量一致
JOY_INSET, JOY_BASE = 40, 288          # 摇杆：左下 inset 40，底圈 288
CHOP_W, CHOP_H = 360, 120              # 砍伐按钮：360x120
CHOP_INSET_X, CHOP_INSET_Y = 56, 40    # 距右 / 下边缘（40 + 16）

PX = 48                         # 每单位像素（预览缩放）
W, H = MAP_W * PX, MAP_H * PX


def tile(name):
    return Image.open(os.path.join(V2, "Tiles", name + ".png")).convert("RGBA").resize((PX, PX), Image.LANCZOS)


def prop(name):
    p = os.path.join(V2, name + ".png")
    im = Image.open(p).convert("RGBA")
    s = PX / 64.0                # 素材 64px = 1 单位
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


def put_tile(canvas, t, x, y):
    canvas.alpha_composite(t, (x * PX, H - (y + 1) * PX))


def put_prop(canvas, im, cx, ground_y):
    """sprite 底部对齐 groundY，水平中心 cx。"""
    w, h = im.size
    x = round(cx * PX - w / 2)
    y = H - round(ground_y * PX) - h
    canvas.alpha_composite(im, (x, y))


def main():
    grass_a, grass_b = tile("fill_grass_a"), tile("fill_grass_b")
    dirt_a, dirt_b = tile("fill_dirt_a"), tile("fill_dirt_b")
    sand, cobble = tile("fill_sand"), tile("tile_r06c05")

    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 255))

    # 草地 + 变体（与 C# 同一哈希）
    for x in range(MAP_W):
        for y in range(MAP_H):
            h = abs(x * 73856093 ^ (y * 19349663)) % 100
            put_tile(canvas, grass_a if h < 62 else grass_b, x, y)

    # 土黄空地
    for x in range(DIRT[0], DIRT[0] + DIRT[2]):
        for y in range(DIRT[1], DIRT[1] + DIRT[3]):
            put_tile(canvas, dirt_a if (x + y) % 2 == 0 else dirt_b, x, y)

    # 土路
    for y in range(PATH_TOP + 1):
        put_tile(canvas, sand, PATH_LEFT, y)
        put_tile(canvas, sand, PATH_LEFT + 1, y)

    # 顶部石墙
    for x in range(MAP_W):
        for r in range(WALL_ROWS):
            put_tile(canvas, cobble, x, MAP_H - 1 - r)

    # 物件（名称, cx, ground_y）—— 与 C# 相同，最后按 groundY 降序画（y 大先画）
    items = [
        ("Buildings/v2_building_house_main", HOUSE_DOOR_X, 14.0),
        ("Nature/v2_nature_crate", 24.4, 14.5),
        ("Nature/v2_nature_barrel", 25.3, 13.2),
    ]
    items += [("Nature/v2_nature_bush", bx, MAP_H - WALL_ROWS - 0.5) for bx in (4, 8, 12, 16, 20, 24, 36)]
    items += [("Nature/v2_nature_tree_broadleaf", x, y) for x, y in ((2.0, 6.0), (2.4, 10.5), (3.0, 15.5), (1.8, 19.2))]
    items += [("Nature/v2_nature_rock", 3.6, 8.4), ("Nature/v2_nature_rock", 3.0, 12.8),
              ("Nature/v2_nature_stump", 5.0, 10.0), ("Nature/v2_nature_stump", 4.4, 18.0)]
    items += [("Nature/v2_nature_tree_pine", x, y) for x, y in ((37.2, 5.0), (37.8, 11.0), (37.2, 17.5))]
    items += [("Nature/v2_nature_log", 9.0, 2.4), ("Nature/v2_nature_rock", 13.6, 2.6),
              ("Nature/v2_nature_stump", 17.0, 2.4), ("Nature/v2_nature_rock", 21.5, 2.8),
              ("Nature/v2_nature_stump", 24.5, 2.5)]
    items.append(("Nature/v2_nature_sign", 31.4, 1.4))
    items += [("Nature/v2_nature_grass_tuft", x, y) for x, y in
              ((4.5, 7.0), (5.0, 12.5), (33.5, 6.0), (35.0, 17.0), (10.0, 2.2), (20.0, 2.6), (31.5, 8.5), (36.5, 3.5))]
    items += [("Nature/v2_nature_flower_pink", 4.0, 9.5), ("Nature/v2_nature_flower_white", 5.2, 16.5),
              ("Nature/v2_nature_flower_yellow", 34.0, 10.0), ("Nature/v2_nature_flower_pink", 32.5, 16.5),
              ("Nature/v2_nature_flower_white", 12.0, 2.8), ("Nature/v2_nature_flower_yellow", 37.0, 8.0)]

    # ---- v0.12 田地种树：梅花型（与 PlantFieldTrees 同一套坐标与哈希）----
    field_trees = []
    for y in range(DIRT[1] + 1, DIRT[1] + DIRT[3] - 1, 2):
        for x in range(DIRT[0] + 1 + (y & 1), DIRT[0] + DIRT[2] - 1, 2):
            is_pine = abs(x * 31 + y * 17) % 10 >= 6
            name = "Nature/v2_nature_tree_pine" if is_pine else "Nature/v2_nature_tree_broadleaf"
            field_trees.append((name, x + 0.5, y + 0.5))
    items += field_trees

    # ---- v0.12 空地加密（与 DecorateOpenGround 同一份坐标）----
    items += [("Nature/v2_nature_tree_broadleaf", x, y) for x, y in
              ((7.0, 18.8), (13.0, 18.5), (19.0, 20.0), (5.5, 19.8))]
    items += [("Nature/v2_nature_tree_pine", x, y) for x, y in
              ((10.5, 20.5), (22.0, 18.5), (36.0, 20.5), (35.5, 18.0), (2.5, 21.3))]
    items += [("Nature/v2_nature_bush", x, y) for x, y in ((5.5, 17.5), (16.0, 20.0), (23.5, 20.0))]
    items.append(("Nature/v2_nature_rock", 9.0, 20.3))
    items += [("Nature/v2_nature_grass_tuft", 12.0, 19.5), ("Nature/v2_nature_grass_tuft", 8.0, 21.8),
              ("Nature/v2_nature_flower_yellow", 17.5, 17.8), ("Nature/v2_nature_flower_white", 21.0, 21.2)]

    # 玩家（出生点标记）
    items.append(("Characters/v2_witch_idle", HOUSE_DOOR_X, 12.5))

    # y 大（靠上）的先画，y 小的后画盖住——与 sortingOrder 公式一致
    for name, cx, gy in sorted(items, key=lambda it: -it[2]):
        put_prop(canvas, prop(name), cx, gy)

    # ---- v0.12 HUD 示意：左下摇杆底圈 + 右下砍伐按钮（按 1920x1080 参考映射）----
    dr = ImageDraw.Draw(canvas, "RGBA")
    sy = H / 1080.0
    # 摇杆底圈（空心环）
    r = JOY_BASE / 2 * sy
    cx, cy = JOY_INSET * sy + r, H - (JOY_INSET * sy + r)
    dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(120, 84, 44, 200), width=6)
    kr = 32 * sy
    dr.ellipse([cx - kr, cy - kr, cx + kr, cy + kr], fill=(150, 108, 58, 220))
    # 砍伐按钮（木纹圆角矩形 + 高亮描边表示可交互态）
    bw, bh = CHOP_W * sy, CHOP_H * sy
    bx, by = W - CHOP_INSET_X * sy - bw, H - CHOP_INSET_Y * sy - bh
    dr.rounded_rectangle([bx, by, bx + bw, by + bh], radius=18 * sy,
                         fill=(146, 102, 52, 235), outline=(94, 62, 26, 255), width=5)
    dr.rounded_rectangle([bx + 8 * sy, by + 8 * sy, bx + bw - 8 * sy, by + bh - 8 * sy],
                         radius=12 * sy, outline=(255, 214, 120, 200), width=3)
    # 按钮中央的斧头示意（两道斜线，避免图内文字）
    ax, ay = bx + bw / 2, by + bh / 2
    dr.line([ax - 26 * sy, ay + 26 * sy, ax + 26 * sy, ay - 26 * sy],
            fill=(255, 236, 190, 255), width=8)
    dr.line([ax + 4 * sy, ay - 26 * sy, ax + 30 * sy, ay - 8 * sy],
            fill=(255, 236, 190, 255), width=6)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.convert("RGB").save(OUT)
    print("saved", OUT, canvas.size, f"（田地种树 {len(field_trees)} 棵）")


if __name__ == "__main__":
    main()
