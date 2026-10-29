#!/usr/bin/env python3
"""
LuminousSSS 打铁玩法像素精灵生成器
风格：16-bit SNES/GBA 硬边像素，无抗锯齿，星露谷/牧场物语调色板。
输出 RGBA PNG，尺寸均为整数像素，便于 Unity 2D 像素完美导入。
"""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "Forge")
OUT = os.path.abspath(OUT)
os.makedirs(OUT, exist_ok=True)

# 调色板（星露谷暖调）
PAL = {
    "iron_dark":   (60, 64, 74, 255),
    "iron_mid":    (96, 102, 116, 255),
    "iron_light":  (140, 148, 164, 255),
    "iron_hi":     (190, 198, 214, 255),
    "wood_dark":   (92, 60, 36, 255),
    "wood_mid":    (138, 92, 56, 255),
    "wood_light":  (180, 128, 80, 255),
    "ground_shadow": (0, 0, 0, 70),
}

def new_canvas(w, h):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)

def save(img, name):
    p = os.path.join(OUT, name)
    img.save(p, "PNG")
    print(f"  -> {name}  {img.size}")

# ---------------------------------------------------------------
# 1. 铁砧 anvil.png  64x48
# ---------------------------------------------------------------
def draw_anvil():
    W, H = 64, 48
    img, d = new_canvas(W, H)
    # 地面阴影
    d.rectangle([8, 42, 56, 45], fill=PAL["ground_shadow"])
    # 底座（梯形）
    d.rectangle([20, 36, 44, 42], fill=PAL["iron_dark"])
    d.rectangle([22, 34, 42, 36], fill=PAL["iron_mid"])
    # 砧腰（收窄）
    d.rectangle([26, 28, 38, 34], fill=PAL["iron_mid"])
    # 砧面（顶部宽台）
    d.rectangle([12, 22, 52, 28], fill=PAL["iron_mid"])
    # 砧面高光（顶部亮线）
    d.rectangle([14, 22, 50, 23], fill=PAL["iron_light"])
    # 砧面左端尖角（horn）
    d.polygon([(12, 22), (4, 25), (12, 28)], fill=PAL["iron_mid"])
    d.polygon([(12, 22), (7, 24), (12, 23)], fill=PAL["iron_light"])
    # 右端方尾
    d.rectangle([50, 23, 56, 27], fill=PAL["iron_dark"])
    # 底部暗边
    d.rectangle([20, 41, 44, 42], fill=PAL["iron_dark"])
    # 高光点
    d.point([(25, 24), (30, 24), (35, 24)], fill=PAL["iron_hi"])
    save(img, "anvil.png")

# ---------------------------------------------------------------
# 2. 剑坯 sword.png —— 4 个加热阶段
#    冷灰 → 暗红 → 橙红 → 亮黄白
#    尺寸 56x12，水平放在砧面上
# ---------------------------------------------------------------
HEAT_STAGES = [
    # name, blade_main, blade_edge, blade_glow
    ("sword_cold",  (150, 155, 165, 255), (190, 195, 205, 255), None),
    ("sword_hot1",  (160,  60,  40, 255), (220, 100,  60, 255), (180,  40,  20, 90)),
    ("sword_hot2",  (230, 110,  40, 255), (255, 170,  70, 255), (255,  90,  20, 140)),
    ("sword_hot3",  (255, 210, 100, 255), (255, 245, 180, 255), (255, 180,  40, 200)),
]

def draw_sword():
    W, H = 56, 12
    for name, main, edge, glow in HEAT_STAGES:
        img, d = new_canvas(W, H)
        # 辉光背景
        if glow is not None:
            for r in range(6, 0, -1):
                g = list(glow); g[3] = int(glow[3] * (1 - r/7) * 0.4)
                d.rectangle([2-r, 3-r, W-3+r, H-4+r], fill=tuple(g))
        # 剑柄（左侧）
        d.rectangle([0, 5, 4, 7], fill=PAL["wood_dark"])          # 柄
        d.rectangle([4, 4, 6, 8], fill=PAL["iron_hi"])           # 护手
        # 剑身（主体）
        d.rectangle([6, 5, W-8, 7], fill=main)
        # 剑刃高光
        d.rectangle([6, 5, W-8, 5], fill=edge)
        # 剑尖（右侧三角）
        d.polygon([(W-8, 5), (W-1, 6), (W-8, 7)], fill=edge)
        save(img, f"{name}.png")

# ---------------------------------------------------------------
# 3. 锤子 hammer.png —— 3 帧动作
#    frame1: 举高（斜上）
#    frame2: 下挥（水平）
#    frame3: 命中（下压，带冲击位移）
#    尺寸 32x32
# ---------------------------------------------------------------
def draw_hammer_frame(pose):
    W, H = 32, 32
    img, d = new_canvas(W, H)

    if pose == 1:  # 举高：柄从右下指向左上，锤头在左上
        # 木柄（斜）
        for i in range(14):
            d.rectangle([6+i, 22-i, 8+i, 24-i], fill=PAL["wood_mid"])
        d.rectangle([7, 23, 8, 23], fill=PAL["wood_light"])
        # 锤头（左上）
        d.rectangle([0, 6, 12, 12], fill=PAL["iron_mid"])
        d.rectangle([0, 6, 12, 7], fill=PAL["iron_light"])
        d.rectangle([0, 11, 12, 12], fill=PAL["iron_dark"])
    elif pose == 2:  # 下挥中：水平向右，锤头在右
        # 木柄（横）
        for i in range(14):
            d.rectangle([2+i, 15, 4+i, 17], fill=PAL["wood_mid"])
        d.rectangle([2, 15, 16, 15], fill=PAL["wood_light"])
        # 锤头（右）
        d.rectangle([18, 11, 30, 21], fill=PAL["iron_mid"])
        d.rectangle([18, 11, 30, 12], fill=PAL["iron_light"])
        d.rectangle([18, 20, 30, 21], fill=PAL["iron_dark"])
    else:  # pose 3 命中：锤头下压，接触剑面
        # 木柄（竖）
        d.rectangle([14, 2, 16, 14], fill=PAL["wood_mid"])
        d.rectangle([14, 2, 14, 14], fill=PAL["wood_light"])
        # 锤头（底部压扁）
        d.rectangle([8, 14, 22, 22], fill=PAL["iron_mid"])
        d.rectangle([8, 14, 22, 15], fill=PAL["iron_light"])
        d.rectangle([8, 21, 22, 22], fill=PAL["iron_dark"])
        # 命中冲击短线
        d.point([(5, 22), (25, 22), (10, 24), (20, 24)], fill=(255, 230, 150, 255))

    save(img, f"hammer_f{pose}.png")

# ---------------------------------------------------------------
# 4. 火花 spark.png  8x8 像素星
# ---------------------------------------------------------------
def draw_spark():
    W = H = 8
    img, d = new_canvas(W, H)
    # 十字星
    d.point([(3, 0), (4, 0), (3, 7), (4, 7)], fill=(255, 200, 80, 255))
    d.point([(0, 3), (0, 4), (7, 3), (7, 4)], fill=(255, 200, 80, 255))
    d.rectangle([3, 3, 4, 4], fill=(255, 245, 200, 255))
    # 斜向小芒
    d.point([(2, 2), (5, 5), (5, 2), (2, 5)], fill=(255, 170, 60, 255))
    save(img, "spark.png")

# ---------------------------------------------------------------
# 5. 熔炉火焰 flame_f1..f4.png  20x28，4 帧循环
# ---------------------------------------------------------------
def draw_flame(frame):
    W, H = 20, 28
    img, d = new_canvas(W, H)
    # 外焰（橙）
    outer = [
        [(6, 26), (4, 18), (7, 12), (5, 6), (10, 0), (13, 6), (11, 12), (14, 18), (12, 26)],
        [(5, 26), (3, 17), (8, 11), (6, 5), (10, 2), (14, 5), (12, 11), (15, 17), (13, 26)],
        [(7, 26), (5, 19), (8, 13), (4, 8), (10, 1), (15, 8), (12, 13), (14, 19), (11, 26)],
        [(6, 26), (4, 18), (9, 12), (7, 4), (11, 1), (14, 4), (11, 12), (15, 18), (12, 26)],
    ][frame-1]
    d.polygon(outer, fill=(255, 120, 30, 255))
    # 内焰（黄）
    inner = [
        [(8, 26), (7, 19), (9, 14), (8, 9), (10, 6), (12, 9), (11, 14), (13, 19), (12, 26)],
        [(8, 26), (6, 18), (10, 13), (9, 8), (10, 5), (12, 8), (11, 13), (13, 18), (11, 26)],
        [(9, 26), (7, 19), (9, 14), (7, 10), (10, 7), (13, 10), (11, 14), (12, 19), (11, 26)],
        [(8, 26), (7, 19), (10, 13), (9, 7), (11, 5), (12, 7), (11, 13), (13, 19), (11, 26)],
    ][frame-1]
    d.polygon(inner, fill=(255, 210, 80, 255))
    # 焰心（白）
    d.rectangle([9, 18, 11, 24], fill=(255, 245, 200, 255))
    save(img, f"flame_f{frame}.png")

# ---------------------------------------------------------------
# 6. 断剑 sword_broken.png  56x12
# ---------------------------------------------------------------
def draw_sword_broken():
    W, H = 56, 12
    img, d = new_canvas(W, H)
    # 辉光
    d.rectangle([2, 3, W-3, H-4], fill=(255, 80, 30, 60))
    # 剑柄
    d.rectangle([0, 5, 4, 7], fill=PAL["wood_dark"])
    d.rectangle([4, 4, 6, 8], fill=PAL["iron_hi"])
    # 剑身（两段，中间缺口）
    d.rectangle([6, 5, 28, 7], fill=(150, 60, 40, 255))
    d.rectangle([6, 5, 28, 5], fill=(200, 100, 70, 255))
    d.rectangle([34, 5, 48, 7], fill=(150, 60, 40, 255))
    d.rectangle([34, 5, 48, 5], fill=(200, 100, 70, 255))
    # 缺口锯齿
    d.point([(29, 5), (30, 6), (31, 5), (32, 7), (33, 6)], fill=(80, 30, 20, 255))
    save(img, "sword_broken.png")

# ---------------------------------------------------------------
# 7. 锻炉（背景） furnace.png  80x60
# ---------------------------------------------------------------
def draw_furnace():
    W, H = 80, 60
    img, d = new_canvas(W, H)
    # 地面阴影
    d.rectangle([6, 54, 74, 57], fill=PAL["ground_shadow"])
    # 炉体（石砖）
    d.rectangle([10, 20, 70, 54], fill=(110, 90, 80, 255))
    d.rectangle([10, 20, 70, 22], fill=(140, 115, 100, 255))
    # 砖缝
    for y in range(26, 54, 6):
        d.line([(10, y), (70, y)], fill=(80, 65, 58, 255))
    for y in range(22, 54, 12):
        d.line([(30, y), (30, y+6)], fill=(80, 65, 58, 255))
        d.line([(50, y+6), (50, y+12)], fill=(80, 65, 58, 255))
    # 炉口（黑）
    d.rectangle([26, 32, 54, 52], fill=(20, 15, 12, 255))
    # 炉口高光边
    d.rectangle([26, 32, 54, 33], fill=(160, 130, 110, 255))
    # 炉口火光（橙色渐变条）
    d.rectangle([28, 44, 52, 51], fill=(255, 120, 30, 255))
    d.rectangle([30, 47, 50, 51], fill=(255, 200, 80, 255))
    # 烟囱
    d.rectangle([58, 4, 68, 20], fill=(110, 90, 80, 255))
    d.rectangle([56, 2, 70, 5], fill=(140, 115, 100, 255))
    save(img, "furnace.png")

if __name__ == "__main__":
    print("生成打铁像素精灵到", OUT)
    draw_anvil()
    draw_sword()
    for p in (1, 2, 3): draw_hammer_frame(p)
    draw_spark()
    for f in (1, 2, 3, 4): draw_flame(f)
    draw_sword_broken()
    draw_furnace()
    print("完成。")
