# -*- coding: utf-8 -*-
"""
小洋楼室内布局预览（不依赖引擎，按 GameSceneBuilder.BuildHouseScene 的同一份坐标渲染）。

用途：改布局时先看效果，确认后再落到 C# —— 沙箱/CI 里没有团结引擎，靠这张图自检。

坐标约定与构建器一致：
  - 房间 26 x 14 格，1 格 = 64 素材像素
  - 后墙：y=12 墙纸 / y=13 墙顶木；左右侧墙 x=0 / x=25
  - 家具底部对齐 groundY，按 (14 - groundY) 做 Y 排序
  - 挂饰/贴墙家具用「顶部贴墙顶」：groundY = 14 - 精灵高度

输出：Docs/preview_v2/18_house_1f_layout.png / 19_house_2f_layout.png
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IN = os.path.join(ROOT, 'Assets/Art/V2/Interior')
OUT = os.path.join(ROOT, 'Docs/preview_v2')

ROOM_W, ROOM_H, WALL_ROWS = 26, 14, 2
PX = 64                      # 1 格 = 64 px（与素材 1:1）
W, H = ROOM_W * PX, ROOM_H * PX

# ---------------------------------------------------------------- 布局数据
# (文件名, x中心, groundY, 碰撞宽, 碰撞高)；groundY 为「精灵底部」所在的世界 y
FLOOR_WOOD = ('v2_in_floor_wood_a', 'v2_in_floor_wood_b')
FLOOR_TILE = ('v2_in_floor_tile',)

# 1F：左侧客厅（木地板）+ 右侧餐厨（瓷砖）
F1 = dict(
    tile_zone=[(1, 14, FLOOR_WOOD), (14, 25, FLOOR_TILE)],
    props=[
        # 贴后墙（顶部贴 y=14）
        ('v2_in_bookshelf',    2.5, 11.0, 1.8, 0.7),
        ('v2_in_fireplace',    6.5, 11.0, 2.6, 0.8),
        ('v2_in_counter',     19.5, 12.0, 2.8, 0.8),
        ('v2_in_stairs_up',   22.5, 11.0, 2.6, 1.0),
        # 墙饰
        ('v2_in_clock',        4.25, 12.5, 0, 0),
        ('v2_in_window',      10.5, 12.0, 0, 0),
        ('v2_in_painting',    13.5, 12.5, 0, 0),
        ('v2_in_window',      16.5, 12.0, 0, 0),
        # 客厅
        ('v2_in_rug_red',      6.5,  3.2, 0, 0),
        ('v2_in_sofa',         6.5,  8.6, 3.8, 0.9),
        ('v2_in_coffee_table', 6.5,  5.6, 2.6, 0.8),
        ('v2_in_armchair',    10.4,  7.0, 1.8, 0.7),
        ('v2_in_floor_lamp',   3.2,  8.6, 0.6, 0.5),
        ('v2_in_plant',        1.7, 10.6, 1.0, 0.5),
        ('v2_in_plant',        1.7,  2.0, 1.0, 0.5),
        # 门厅
        ('v2_in_door',        13.0,  0.0, 1.8, 0.6),
        ('v2_in_plant',       11.4,  2.2, 1.0, 0.5),
        ('v2_in_plant',       15.2,  1.6, 1.0, 0.5),
        # 餐厨
        ('v2_in_rug_red',     19.0,  2.4, 0, 0),
        ('v2_in_dining_table',19.0,  5.8, 3.6, 0.8),
        ('v2_in_chair',       16.4,  4.9, 0.7, 0.5),
        ('v2_in_chair',       16.4,  7.2, 0.7, 0.5),
        ('v2_in_chair',       21.6,  4.9, 0.7, 0.5),
        ('v2_in_chair',       21.6,  7.2, 0.7, 0.5),
        ('v2_in_table_lamp',  19.0,  8.2, 0, 0),
        ('v2_in_counter',     23.0,  3.0, 2.8, 0.8),
        ('v2_in_plant',       24.2, 10.6, 1.0, 0.5),
    ],
)

# 2F：左侧卧室 + 右侧书房（同木地板，用家具围合分区）
F2 = dict(
    tile_zone=[(1, 25, FLOOR_WOOD)],
    props=[
        ('v2_in_nightstand',   2.6, 12.5, 1.2, 0.5),
        ('v2_in_bed',          5.5,  8.5, 3.6, 0.9),
        ('v2_in_wardrobe',    10.3, 10.5, 2.6, 0.8),
        ('v2_in_bookshelf',   14.0, 11.0, 1.8, 0.7),
        ('v2_in_desk',        17.8, 12.0, 2.8, 0.7),
        ('v2_in_stairs_down', 22.5, 10.0, 2.6, 1.0),
        ('v2_in_clock',       12.2, 12.5, 0, 0),
        ('v2_in_rug_red',      6.5,  2.6, 0, 0),
        ('v2_in_chair',       17.8, 10.0, 0.7, 0.5),
        ('v2_in_chair',        8.7,  7.6, 0.7, 0.5),
        ('v2_in_armchair',    14.6,  4.6, 1.8, 0.7),
        ('v2_in_floor_lamp',  16.4,  3.6, 0.6, 0.5),
        ('v2_in_plant',        1.7, 10.4, 1.0, 0.5),
        ('v2_in_plant',        1.7,  2.0, 1.0, 0.5),
        ('v2_in_plant',       24.2,  2.4, 1.0, 0.5),
    ],
)


def img(name):
    return Image.open(os.path.join(IN, name + '.png')).convert('RGBA')


_tile_cache = {}


def tile_of(name):
    if name not in _tile_cache:
        _tile_cache[name] = img(name).resize((PX, PX), Image.NEAREST)
    return _tile_cache[name]


def draw(room, title):
    canvas = Image.new('RGBA', (W, H), (18, 14, 10, 255))

    # 地板（按区）
    for x0, x1, names in room['tile_zone']:
        for x in range(x0, x1):
            for y in range(0, ROOM_H - WALL_ROWS):
                if names is FLOOR_TILE:
                    t = tile_of(names[0])
                else:
                    t = tile_of(names[(x * 7 + y * 13) % len(names)])
                canvas.alpha_composite(t, (x * PX, H - (y + 1) * PX))

    # 后墙：墙纸（y=12）+ 墙顶木（y=13）
    for x in range(ROOM_W):
        canvas.alpha_composite(tile_of('v2_in_wall_paper'), (x * PX, H - (ROOM_H - WALL_ROWS + 1) * PX))
        canvas.alpha_composite(tile_of('v2_in_wall_wood'), (x * PX, 0))

    # 左右侧墙
    for y in range(ROOM_H):
        canvas.alpha_composite(tile_of('v2_in_wall_wood'), (0, H - (y + 1) * PX))
        canvas.alpha_composite(tile_of('v2_in_wall_wood'), ((ROOM_W - 1) * PX, H - (y + 1) * PX))

    # 绘制顺序：地毯（地面装饰）最先，其余按 groundY 从大到小
    # （引擎里由 sortingOrder 表达：地毯 orderOverride=-50，家具按 (14-groundY)*10）
    def draw_key(p):
        is_rug = 'rug' in p[0]
        return (0 if is_rug else 1, -p[2])

    for name, cx, gy, _cw, _ch in sorted(room['props'], key=draw_key):
        im = img(name)
        x = round(cx * PX - im.width / 2)
        y = H - round(gy * PX) - im.height
        canvas.alpha_composite(im, (x, y))

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, title)
    canvas.convert('RGB').save(path)
    print('saved', path, canvas.size)
    return canvas


if __name__ == '__main__':
    draw(F1, '18_house_1f_layout.png')
    draw(F2, '19_house_2f_layout.png')
