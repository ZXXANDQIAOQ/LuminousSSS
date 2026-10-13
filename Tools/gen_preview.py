"""按设计文档的农场布局拼一张预览图，用于确认素材尺寸与配色。"""
import os
from PIL import Image

ROOT = r"E:/Document/GameCode/LuminousSSS/LuminousSSS"
ART = os.path.join(ROOT, "Assets/Art")
T = 32  # tile 尺寸


def art(rel):
    return Image.open(os.path.join(ART, rel)).convert("RGBA")


def main():
    W, H = 40 * T, 24 * T          # 40x24 tiles = 1280x768
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    grass, dirt, path, wall = (art("Tiles/tile_grass.png"), art("Tiles/tile_dirt.png"),
                               art("Tiles/tile_path.png"), art("Tiles/tile_stonewall.png"))

    # 底色全草
    for y in range(0, H, T):
        for x in range(0, W, T):
            canvas.paste(grass, (x, y), grass)
    # 中央土黄空地
    for ty in range(8, 20):
        for tx in range(6, 26):
            canvas.paste(dirt, (tx * T, ty * T), dirt)
    # 土路：木屋门口 -> 底部出口（2 tile 宽）
    for ty in range(11, 24):
        canvas.paste(path, (31 * T, ty * T), path)
        canvas.paste(path, (32 * T, ty * T), path)
    for tx in range(27, 32):
        canvas.paste(path, (tx * T, 11 * T), path)
    # 顶部石墙两排
    for ty in (0, 1):
        for tx in range(0, 40):
            canvas.paste(wall, (tx * T, ty * T), wall)

    def put(rel, tx, ty):
        im = art(rel)
        canvas.paste(im, (int(tx * T), int(ty * T)), im)

    # 木屋（右上）
    put("Buildings/building_house.png", 25.5, 3.0)
    # 顶部灌木带
    for tx in (8, 11, 14, 17, 20, 36):
        put("Props/prop_bush.png", tx, 2.0)
    # 左侧：阔叶树 / 石头 / 树桩
    for tx, ty in [(0.2, 4.5), (0.6, 10.0), (2.4, 15.5), (0.8, 19.5)]:
        put("Props/prop_tree_broadleaf.png", tx, ty)
    put("Props/prop_rock_small.png", 2.2, 7.4)
    put("Props/prop_rock_large.png", 1.8, 12.6)
    put("Props/prop_rock_large.png", 3.4, 21.0)
    put("Props/prop_stump.png", 4.6, 9.6)
    put("Props/prop_stump.png", 4.2, 17.6)
    # 右侧：松树 / 树桩
    for tx, ty in [(37.4, 3.2), (37.6, 10.0), (37.0, 16.4)]:
        put("Props/prop_tree_pine.png", tx, ty)
    put("Props/prop_stump.png", 35.4, 9.0)
    put("Props/prop_stump.png", 36.0, 14.0)
    # 底部：横放木头 / 石头 / 树桩 / 杂草
    put("Props/prop_log.png", 8.0, 21.4)
    put("Props/prop_rock_small.png", 12.6, 22.0)
    put("Props/prop_stump.png", 15.6, 21.6)
    put("Props/prop_stump.png", 22.6, 21.8)
    put("Props/prop_rock_small.png", 20.2, 22.2)
    put("Props/prop_rock_large.png", 25.2, 21.4)
    for tx, ty in [(7.0, 9.2), (10.0, 19.4), (18.0, 9.2), (24.0, 19.0), (33.5, 20.0)]:
        put("Props/prop_grass_tuft.png", tx, ty)

    canvas.convert("RGB").save(os.path.join(ROOT, "Docs/preview_farm_layout.png"))
    print("saved", canvas.size)


if __name__ == "__main__":
    main()
