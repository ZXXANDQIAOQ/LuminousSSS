"""生成 HUD / 背包面板用的布袋图标（v2_ui_icon_bag.png）。

素材集里有齿轮 / 心 / 电池 / 问号图标，唯独没有背包。HUD 右上角要放一个
「打开背包」的按钮，用文字太占地方，所以这里补一张 64x64 硬边像素图标，
色板与 gen_ui_icons.py 一致（深棕描边 + 暖色主体）。

画法：逐像素判定（不用抗锯齿），保证 alpha 只有 0 / 255 ——
项目有 verify_hardedge.py 卡「半透明像素占比 < 2%」。

运行：python Tools/gen_ui_bag_icon.py
"""
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets/Art/V2/UI')

W = H = 64
CX = 31.5

EDGE = (90, 46, 8, 255)        # 描边
BODY = (206, 152, 84, 255)     # 袋身
BODY_HL = (226, 176, 108, 255)  # 左侧高光
BODY_SH = (170, 118, 62, 255)  # 右侧阴影
FLAP = (168, 116, 60, 255)     # 翻盖
STRAP = (120, 76, 32, 255)     # 扣带
BUCKLE = (233, 190, 86, 255)   # 金属扣
STITCH = (255, 243, 208, 255)  # 缝线
CLEAR = (0, 0, 0, 0)

BODY_TOP, BODY_BOT = 20, 57
HANDLE_CY, HANDLE_R = 21.0, 8.5
HANDLE_THICK = 3.2


def half_width(y):
    """袋身在某一行的半宽：上窄下宽（梯形），底部再加圆角收口。"""
    t = (y - BODY_TOP) / float(BODY_BOT - BODY_TOP)
    half = 17.5 + 3.5 * t
    if y > BODY_BOT - 6:                       # 底部圆角 r=6
        dy = y - (BODY_BOT - 6)
        half -= 6 - math.sqrt(max(0.0, 36 - dy * dy))
    return half


def in_body(x, y, inset=0.0):
    if y < BODY_TOP or y > BODY_BOT:
        return False
    return abs(x - CX) <= half_width(y) - inset


def in_handle(x, y):
    """提手：以 (CX, HANDLE_CY) 为圆心的上半环。"""
    if y > HANDLE_CY + HANDLE_THICK:
        return False
    d = math.hypot(x - CX, y - HANDLE_CY)
    return HANDLE_R - HANDLE_THICK <= d <= HANDLE_R


def in_flap(x, y):
    return BODY_TOP <= y <= 33 and in_body(x, y, inset=2.0)


def pixel(x, y):
    # ---- 提手（画在袋身之前，袋身会盖住它的下沿）
    if in_handle(x, y):
        return EDGE

    if not in_body(x, y):
        return CLEAR

    # ---- 描边：身体最外 2px
    if not in_body(x, y, inset=2.0):
        return EDGE

    # ---- 翻盖区（比袋身深一档 + 下沿一条缝线）
    if in_flap(x, y):
        if y == 33:
            return EDGE
        if y == 32:
            # 缝线：每 3px 一点，断断续续才像针脚
            return STITCH if (x % 3) == 0 else FLAP
        return FLAP if x > CX else (FLAP[0] + 16, FLAP[1] + 10, FLAP[2] + 8, 255)

    # ---- 扣带 + 金属扣
    if 29 <= x <= 34 and 33 <= y <= 45:
        if 29 <= x <= 34 and 40 <= y <= 45:
            if x in (29, 34) or y in (40, 45):
                return EDGE
            return BUCKLE
        return EDGE if x in (29, 34) else STRAP

    # ---- 袋身明暗：左侧高光、右侧阴影，中间正常
    if x < CX - 5:
        return BODY_HL
    if x > CX + 6:
        return BODY_SH
    return BODY


img = Image.new('RGBA', (W, H), CLEAR)
px = img.load()
for y in range(H):
    for x in range(W):
        px[x, y] = pixel(x, y)

path = os.path.join(OUT, 'v2_ui_icon_bag.png')
img.save(path)
print('生成', os.path.relpath(path, ROOT), img.size)
