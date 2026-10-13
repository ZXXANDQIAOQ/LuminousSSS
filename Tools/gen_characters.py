"""角色素材处理：
1) 把白底 JPEG 立绘抠成透明 PNG（登录界面用）
2) 生成 3 个正俯视 chibi 小像素人（场景内玩家用，32x48）
"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = r"E:/Document/GameCode/LuminousSSS/LuminousSSS"
REF = os.path.join(ROOT, "Docs/Refs")
OUT = os.path.join(ROOT, "Assets/Art")

MAGENTA = (255, 0, 255)
JOBS = [
    ("char_001_farmer.jpg", "portrait_001_farmer.png"),
    ("char_002_rain_maiden.jpg", "portrait_002_rain_maiden.png"),
    ("char_003_adventurer.jpg", "portrait_003_adventurer.png"),
]


def cutout(src_path, dst_path, target_h=760):
    im = Image.open(src_path).convert("RGB")
    w, h = im.size
    # 四角泛洪填成洋红（保护角色内部的白色区域）
    for seed in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]:
        ImageDraw.floodfill(im, seed, MAGENTA, thresh=55)

    # 接近洋红的像素 -> 透明
    diff = ImageChops.difference(im, Image.new("RGB", im.size, MAGENTA)).convert("L")
    mask = diff.point(lambda v: 0 if v < 40 else 255)
    im = im.convert("RGBA")
    im.putalpha(mask)
    im = im.filter(ImageFilter.MinFilter(3))  # 腐蚀一圈，去掉 JPEG 白边

    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)
    # 按高度等比缩放
    ratio = target_h / im.height
    im = im.resize((max(1, int(im.width * ratio)), target_h), Image.LANCZOS)
    os.makedirs(os.path.dirname(dst_path), exist_ok=True)
    im.save(dst_path)
    return im.size


# ------------------------------------------------------------ 小像素人
def chibi(skin, hair, shirt, shirt_d, pants, shoe, accent=None):
    """32x48 正俯视 chibi，正面站立 idle。"""
    w, h = 32, 48
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    dark = (0, 0, 0, 255)
    hair_d = tuple(max(0, c - 40) for c in hair[:3]) + (255,)
    skin_d = tuple(max(0, c - 40) for c in skin[:3]) + (255,)
    skin_l = tuple(min(255, c + 25) for c in skin[:3]) + (255,)

    # 影子
    d.ellipse([8, 43, 24, 47], fill=(0, 0, 0, 60))
    # 腿
    d.rectangle([11, 34, 15, 43], fill=pants)
    d.rectangle([17, 34, 21, 43], fill=pants)
    d.rectangle([15, 34, 17, 43], fill=tuple(max(0, c - 35) for c in pants[:3]) + (255,))
    # 鞋
    d.rectangle([10, 42, 16, 45], fill=shoe)
    d.rectangle([16, 42, 22, 45], fill=shoe)
    # 身体
    d.rectangle([9, 22, 23, 36], fill=shirt)
    d.rectangle([9, 22, 12, 36], fill=tuple(min(255, c + 25) for c in shirt[:3]) + (255,))
    d.rectangle([20, 22, 23, 36], fill=shirt_d)
    # 手臂
    d.rectangle([6, 23, 9, 33], fill=shirt_d)
    d.rectangle([23, 23, 26, 33], fill=shirt_d)
    d.rectangle([6, 31, 9, 35], fill=skin)
    d.rectangle([23, 31, 26, 35], fill=skin)
    # 脖子
    d.rectangle([14, 20, 18, 23], fill=skin_d)
    # 头
    d.ellipse([8, 5, 24, 23], fill=skin)
    d.ellipse([8, 5, 14, 23], fill=skin_l)
    # 头发
    d.ellipse([7, 3, 25, 14], fill=hair)
    d.rectangle([7, 9, 9, 18], fill=hair)
    d.rectangle([23, 9, 25, 18], fill=hair)
    d.ellipse([7, 3, 16, 10], fill=tuple(min(255, c + 30) for c in hair[:3]) + (255,))
    # 眼睛
    d.rectangle([11, 14, 13, 16], fill=dark)
    d.rectangle([19, 14, 21, 16], fill=dark)
    d.point((12, 14), fill=(255, 255, 255, 255))
    d.point((20, 14), fill=(255, 255, 255, 255))
    # 嘴
    d.rectangle([15, 19, 17, 19], fill=skin_d)
    # 可选配饰（雨女：整顶黄雨帽）
    if accent:
        d.ellipse([5, 1, 27, 15], fill=accent)
        d.rectangle([3, 8, 29, 11], fill=accent)
        d.rectangle([4, 8, 28, 9], fill=tuple(min(255, c + 35) for c in accent[:3]) + (255,))
        d.ellipse([8, 3, 18, 8], fill=tuple(min(255, c + 30) for c in accent[:3]) + (255,))
    return im


CHIBI_JOBS = [
    ("char_001_farmer.png", dict(skin=(240, 200, 165, 255), hair=(122, 82, 48, 255),
                                 shirt=(72, 134, 74, 255), shirt_d=(52, 104, 56, 255),
                                 pants=(62, 96, 168, 255), shoe=(110, 74, 42, 255))),
    ("char_002_rain_maiden.png", dict(skin=(245, 212, 180, 255), hair=(38, 34, 40, 255),
                                      shirt=(232, 178, 46, 255), shirt_d=(198, 148, 32, 255),
                                      pants=(58, 56, 62, 255), shoe=(96, 70, 46, 255),
                                      accent=(236, 188, 52, 255))),
    ("char_003_adventurer.png", dict(skin=(242, 204, 172, 255), hair=(232, 128, 44, 255),
                                     shirt=(58, 104, 176, 255), shirt_d=(42, 78, 138, 255),
                                     pants=(96, 112, 88, 255), shoe=(238, 238, 234, 255))),
]


def main():
    for src, dst in JOBS:
        p = os.path.join(REF, src)
        if not os.path.exists(p):
            print("MISSING", p)
            continue
        size = cutout(p, os.path.join(OUT, "Portraits", dst))
        print("portrait", dst, size)

    for name, kw in CHIBI_JOBS:
        im = chibi(**kw)
        d = os.path.join(OUT, "Characters")
        os.makedirs(d, exist_ok=True)
        im.save(os.path.join(d, name))
        print("chibi", name, im.size)


if __name__ == "__main__":
    main()
