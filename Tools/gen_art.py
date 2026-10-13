"""LuminousSSS 像素素材生成器 —— 地形 Tile / 场景物件 / 建筑。
风格：正俯视、硬边像素、限定调色板（遵守设计文档第二节）。
"""
import math
import os
import random
from PIL import Image, ImageDraw

ROOT = r"E:/Document/GameCode/LuminousSSS/LuminousSSS/Assets/Art"
PAL = {
    "grass_d": (58, 111, 46, 255),
    "grass": (74, 140, 58, 255),
    "grass_l": (92, 163, 74, 255),
    "grass_xl": (111, 184, 92, 255),
    "dirt_d": (184, 134, 58, 255),
    "dirt": (212, 160, 74, 255),
    "dirt_l": (226, 178, 98, 255),
    "dirt_xl": (238, 194, 124, 255),
    "path_d": (172, 134, 82, 255),
    "path": (201, 161, 100, 255),
    "path_l": (219, 181, 122, 255),
    "trunk_d": (82, 54, 25, 255),
    "trunk": (107, 74, 42, 255),
    "trunk_l": (140, 100, 60, 255),
    "leaf_d": (42, 96, 38, 255),
    "leaf": (63, 140, 54, 255),
    "leaf_l": (92, 170, 72, 255),
    "leaf_xl": (124, 198, 96, 255),
    "pine_d": (28, 82, 36, 255),
    "pine": (44, 118, 54, 255),
    "pine_l": (64, 148, 72, 255),
    "rock_d": (107, 107, 107, 255),
    "rock": (138, 138, 138, 255),
    "rock_l": (170, 170, 170, 255),
    "rock_xl": (200, 200, 200, 255),
    "wall_d": (116, 108, 96, 255),
    "wall": (150, 142, 128, 255),
    "wall_l": (180, 172, 156, 255),
    "roof_d": (152, 46, 36, 255),
    "roof": (176, 58, 46, 255),
    "roof_l": (196, 74, 56, 255),
    "wood_d": (168, 124, 66, 255),
    "wood": (200, 154, 91, 255),
    "wood_l": (219, 179, 124, 255),
    "door_d": (92, 58, 30, 255),
    "door": (139, 90, 43, 255),
    "win_d": (52, 108, 156, 255),
    "win": (86, 158, 206, 255),
    "win_l": (166, 216, 242, 255),
    "gry_d": (90, 90, 90, 255),
    "gry": (122, 122, 122, 255),
    "gry_l": (156, 156, 156, 255),
    "shade": (0, 0, 0, 55),
}


def blank(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def speckle(d, w, h, seed, colors, n=110, block=1):
    rnd = random.Random(seed)
    for _ in range(n):
        x = rnd.randrange(w - block + 1)
        y = rnd.randrange(h - block + 1)
        c = rnd.choice(colors)
        if block == 1:
            d.point((x, y), fill=c)
        else:
            d.rectangle([x, y, x + block - 1, y + block - 1], fill=c)


def lump(d, cx, cy, r, color, lobes=7, spread=0.60, seed=0):
    """用多个圆叠成圆润的团块，用来画 Q 版树冠。"""
    rnd = random.Random(seed)
    for i in range(lobes):
        ang = i * (2 * math.pi / lobes) + rnd.uniform(-0.15, 0.15)
        ox = math.cos(ang) * r * spread
        oy = math.sin(ang) * r * spread * 0.85
        rr = r * 0.62
        d.ellipse([cx + ox - rr, cy + oy - rr, cx + ox + rr, cy + oy + rr], fill=color)
    d.ellipse([cx - r * 0.82, cy - r * 0.82, cx + r * 0.82, cy + r * 0.82], fill=color)


def save(im, rel):
    p = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    im.save(p)


# ---------------------------------------------------------------- 地形 Tiles
def tile_grass():
    im = Image.new("RGBA", (32, 32), PAL["grass"])
    d = ImageDraw.Draw(im)
    speckle(d, 32, 32, 1001, [PAL["grass_d"], PAL["grass_l"], PAL["grass_xl"]], 150)
    speckle(d, 32, 32, 1002, [PAL["grass_l"], PAL["grass_xl"]], 18, 2)
    speckle(d, 32, 32, 1003, [PAL["grass_d"]], 14, 2)
    return im


def tile_dirt():
    im = Image.new("RGBA", (32, 32), PAL["dirt"])
    d = ImageDraw.Draw(im)
    speckle(d, 32, 32, 2001, [PAL["dirt_d"], PAL["dirt_l"], PAL["dirt_xl"]], 150)
    speckle(d, 32, 32, 2002, [PAL["dirt_l"], PAL["dirt_xl"]], 20, 2)
    speckle(d, 32, 32, 2003, [PAL["dirt_d"]], 16, 2)
    return im


def tile_path():
    im = Image.new("RGBA", (32, 32), PAL["path"])
    d = ImageDraw.Draw(im)
    speckle(d, 32, 32, 3001, [PAL["path_d"], PAL["path_l"]], 140)
    speckle(d, 32, 32, 3002, [PAL["path_l"]], 16, 2)
    rnd = random.Random(3003)
    for _ in range(5):
        x, y = rnd.randrange(27), rnd.randrange(27)
        d.rectangle([x, y, x + 1, y + 1], fill=PAL["rock_l"])
    return im


def tile_stonewall():
    im = blank(32, 32)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 31, 31], fill=PAL["wall"])
    rnd = random.Random(4001)
    for i, y in enumerate([0, 8, 16, 24]):
        offset = 0 if i % 2 == 0 else 8
        x = -offset
        while x < 32:
            bw = rnd.choice([10, 12, 14])
            d.rectangle([x, y, x + bw - 1, y + 7], fill=rnd.choice([PAL["wall"], PAL["wall_l"]]))
            d.line([(x, y + 7), (x + bw - 1, y + 7)], fill=PAL["wall_d"])
            d.line([(x + bw - 1, y), (x + bw - 1, y + 7)], fill=PAL["wall_d"])
            x += bw
    speckle(d, 32, 32, 4002, [PAL["wall_d"], PAL["wall_l"]], 60)
    return im


# ---------------------------------------------------------------- 场景物件
def prop_broadleaf_tree():
    """Q 版圆润阔叶树，132x168（约 4x5 tile）。"""
    w, h = 132, 168
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([38, 150, 94, 166], fill=PAL["shade"])
    # 树干
    d.rectangle([56, 96, 76, 156], fill=PAL["trunk"])
    d.rectangle([56, 96, 62, 156], fill=PAL["trunk_l"])
    d.rectangle([70, 96, 76, 156], fill=PAL["trunk_d"])
    d.rectangle([48, 148, 84, 156], fill=PAL["trunk_d"])
    # 树冠：三层叠出圆润轮廓
    lump(d, 66, 74, 60, PAL["leaf_d"], 7, 0.62, 11)
    lump(d, 66, 70, 55, PAL["leaf"], 7, 0.62, 12)
    lump(d, 60, 58, 42, PAL["leaf_l"], 7, 0.60, 13)
    d.ellipse([34, 30, 84, 66], fill=PAL["leaf_xl"])
    d.ellipse([40, 26, 74, 50], fill=(140, 210, 110, 255))
    return im


def prop_pine_tree():
    """深绿松树，104x172（约 3.2x5.4 tile）。"""
    w, h = 104, 172
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([30, 154, 74, 170], fill=PAL["shade"])
    d.rectangle([46, 118, 58, 160], fill=PAL["trunk"])
    d.rectangle([46, 118, 50, 160], fill=PAL["trunk_l"])
    layers = [(44, 24, 76), (40, 62, 112), (36, 98, 146)]
    for half, top, bottom in layers:
        cx = 52
        # 锯齿边缘：逐行按 sin 起伏收窄，做出针叶感
        for y in range(top, bottom + 1):
            t = (y - top) / max(1, bottom - top)
            hw = int(half * t)
            hw = max(1, hw + int(3 * math.sin(y * 0.55)))
            d.line([(cx - hw, y), (cx + hw, y)], fill=PAL["pine"])
            d.line([(cx - hw, y), (cx - hw + max(1, hw // 3), y)], fill=PAL["pine_l"])
            d.line([(cx + hw - max(1, hw // 4), y), (cx + hw, y)], fill=PAL["pine_d"])
        d.line([(cx - half, bottom), (cx + half, bottom)], fill=PAL["pine_d"])
    return im


def prop_rock(size="small"):
    if size == "small":
        w, h = 66, 54
        pts = [(5, 44), (12, 23), (28, 10), (48, 15), (61, 30), (56, 46), (20, 49)]
    else:
        w, h = 92, 72
        pts = [(6, 60), (15, 33), (36, 11), (66, 14), (85, 35), (80, 62), (24, 66)]
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([5, h - 15, w - 5, h - 3], fill=PAL["shade"])
    d.polygon(pts, fill=PAL["rock"])
    k = w / 66.0
    d.polygon([(int(14 * k), int(38)), (int(31 * k), int(17)), (int(53 * k), int(20)), (int(41 * k), int(40))],
              fill=PAL["rock_l"])
    d.polygon([(int(24 * k), int(33)), (int(36 * k), int(21)), (int(48 * k), int(24)), (int(38 * k), int(35))],
              fill=PAL["rock_xl"])
    d.polygon([(w - 20, int(40)), (w - 8, int(33)), (w - 10, h - 10), (22, h - 8), (w - 27, int(50))],
              fill=PAL["rock_d"])
    return im


def prop_stump():
    """树桩，68x56。"""
    w, h = 68, 56
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([8, 44, 62, 55], fill=PAL["shade"])
    d.rectangle([12, 20, 56, 48], fill=PAL["trunk"])
    d.rectangle([12, 20, 19, 48], fill=PAL["trunk_l"])
    d.rectangle([49, 20, 56, 48], fill=PAL["trunk_d"])
    d.ellipse([9, 7, 59, 31], fill=PAL["trunk_l"])
    d.ellipse([16, 11, 52, 27], fill=PAL["trunk"])
    d.ellipse([24, 14, 44, 24], fill=PAL["trunk_l"])
    d.ellipse([30, 16, 38, 21], fill=PAL["trunk_d"])
    return im


def prop_log():
    """横放木头，92x48。"""
    w, h = 92, 48
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([6, 36, 88, 47], fill=PAL["shade"])
    d.rectangle([10, 17, 84, 41], fill=PAL["trunk"])
    d.rectangle([10, 17, 84, 23], fill=PAL["trunk_l"])
    d.rectangle([10, 36, 84, 41], fill=PAL["trunk_d"])
    d.ellipse([74, 17, 92, 41], fill=PAL["trunk_l"])
    d.ellipse([79, 22, 87, 36], fill=PAL["trunk_d"])
    d.ellipse([6, 17, 24, 41], fill=PAL["trunk_l"])
    for x in range(22, 74, 14):
        d.line([(x, 25), (x + 7, 30)], fill=PAL["trunk_d"])
        d.line([(x + 3, 32), (x + 10, 36)], fill=PAL["trunk_d"])
    return im


def prop_bush():
    """灌木丛，104x70。"""
    w, h = 104, 70
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse([12, 54, 92, 68], fill=PAL["shade"])
    lump(d, 52, 42, 34, PAL["leaf_d"], 6, 0.68, 21)
    lump(d, 52, 38, 30, PAL["leaf"], 6, 0.68, 22)
    lump(d, 42, 26, 20, PAL["leaf_l"], 6, 0.62, 23)
    d.ellipse([30, 14, 62, 34], fill=PAL["leaf_xl"])
    return im


def prop_grass_tuft():
    """小杂草，28x26。"""
    im = blank(28, 26)
    d = ImageDraw.Draw(im)
    d.ellipse([7, 20, 21, 25], fill=PAL["shade"])
    for x0, y0, x1, y1 in [(14, 22, 14, 6), (11, 22, 6, 10), (17, 22, 22, 9),
                           (13, 22, 9, 13), (16, 22, 20, 14)]:
        d.line([(x0, y0), (x1, y1)], fill=PAL["leaf_l"])
        d.point((x1, y1), fill=PAL["leaf_xl"])
    return im


# ---------------------------------------------------------------- 建筑
def building_house():
    """木屋：红屋顶 + 石烟囱 + 木墙 + 窗 + 门 + 门廊 + 邮箱。336x300。"""
    w, h = 336, 300
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    d.rectangle([16, 272, 320, 296], fill=PAL["shade"])

    # 烟囱（先画，之后被屋顶遮住下半段）
    d.rectangle([196, 44, 232, 150], fill=PAL["gry"])
    d.rectangle([196, 44, 204, 150], fill=PAL["gry_l"])
    d.rectangle([224, 44, 232, 150], fill=PAL["gry_d"])
    d.rectangle([191, 36, 237, 50], fill=PAL["gry_l"])
    d.rectangle([191, 36, 237, 41], fill=PAL["gry"])
    for i, y in enumerate(range(46, 148, 13)):
        d.line([(197, y), (231, y)], fill=PAL["gry_d"] if i % 2 else PAL["gry_l"])

    # 木屋主体
    bx0, by0, bx1, by1 = 88, 152, 248, 280
    d.rectangle([bx0, by0, bx1, by1], fill=PAL["wood"])
    d.rectangle([bx0, by0, bx0 + 8, by1], fill=PAL["wood_l"])
    d.rectangle([bx1 - 8, by0, bx1, by1], fill=PAL["wood_d"])
    for y in range(by0 + 10, by1 - 4, 14):
        d.line([(bx0, y), (bx1, y)], fill=PAL["wood_d"])
        d.line([(bx0, y + 1), (bx1, y + 1)], fill=PAL["wood_l"])

    # 屋顶（三角，盖住烟囱下半段）
    apex, lx, rx, base = (168, 34), 60, 276, 158
    d.polygon([(lx, base), apex, (rx, base)], fill=PAL["roof"])
    d.polygon([(lx, base), apex, (168, base)], fill=PAL["roof_l"])
    d.polygon([apex, (rx, base), (168, base)], fill=PAL["roof_d"])
    d.line([(lx, base), (rx, base)], fill=PAL["roof_d"])
    d.line([(lx, base), (rx, base)], fill=PAL["roof_d"])
    for i in range(1, 5):
        y = 34 + i * 24
        half = int((y - 34) * ((168 - 60) / (158 - 34)))
        d.line([(168 - half, y), (168 + half, y)], fill=PAL["roof_d"])
        d.line([(168 - half, y + 1), (168 + half, y + 1)], fill=PAL["roof_l"])
    # 屋檐
    d.rectangle([56, 154, 280, 164], fill=PAL["roof_d"])
    d.rectangle([56, 154, 280, 157], fill=PAL["roof_l"])

    # 窗户
    for wx in (100, 200):
        d.rectangle([wx, 186, wx + 40, 224], fill=PAL["wood_d"])
        d.rectangle([wx + 3, 189, wx + 37, 221], fill=PAL["win"])
        d.rectangle([wx + 3, 189, wx + 20, 202], fill=PAL["win_l"])
        d.line([(wx + 20, 189), (wx + 20, 221)], fill=PAL["wood_d"])
        d.line([(wx + 3, 205), (wx + 37, 205)], fill=PAL["wood_d"])

    # 门
    d.rectangle([150, 200, 188, 280], fill=PAL["door_d"])
    d.rectangle([153, 203, 185, 280], fill=PAL["door"])
    d.rectangle([153, 203, 159, 280], fill=PAL["wood_l"])
    d.rectangle([176, 236, 182, 243], fill=PAL["gry_l"])

    # 门廊台阶
    d.rectangle([138, 280, 200, 288], fill=PAL["wood_d"])
    d.rectangle([130, 288, 208, 296], fill=PAL["wood"])
    # 门廊栏杆
    for lx in range(96, 146, 12):
        d.rectangle([lx, 250, lx + 5, 280], fill=PAL["wood_d"])
    for lx in range(192, 242, 12):
        d.rectangle([lx, 250, lx + 5, 280], fill=PAL["wood_d"])
    d.rectangle([92, 246, 150, 252], fill=PAL["wood_l"])
    d.rectangle([190, 246, 244, 252], fill=PAL["wood_l"])

    # 邮箱
    d.rectangle([48, 238, 56, 284], fill=PAL["wood_d"])
    d.rectangle([32, 214, 72, 242], fill=PAL["roof"])
    d.rectangle([32, 214, 72, 221], fill=PAL["roof_l"])
    d.rectangle([38, 225, 54, 235], fill=PAL["wall_l"])
    return im


def main():
    save(tile_grass(), "Tiles/tile_grass.png")
    save(tile_dirt(), "Tiles/tile_dirt.png")
    save(tile_path(), "Tiles/tile_path.png")
    save(tile_stonewall(), "Tiles/tile_stonewall.png")
    save(prop_broadleaf_tree(), "Props/prop_tree_broadleaf.png")
    save(prop_pine_tree(), "Props/prop_tree_pine.png")
    save(prop_rock("small"), "Props/prop_rock_small.png")
    save(prop_rock("large"), "Props/prop_rock_large.png")
    save(prop_stump(), "Props/prop_stump.png")
    save(prop_log(), "Props/prop_log.png")
    save(prop_bush(), "Props/prop_bush.png")
    save(prop_grass_tuft(), "Props/prop_grass_tuft.png")
    save(building_house(), "Buildings/building_house.png")
    print("done")


if __name__ == "__main__":
    main()
