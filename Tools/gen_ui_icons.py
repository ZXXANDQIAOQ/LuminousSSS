"""程序化生成登录界面缺失的左右切换箭头。

素材集里没有方向箭头，文字符号（◀ ▶）是矢量渲染，和像素风不搭。
这里按素材同款色板（浅米 + 深棕描边）画一对 64x64 的像素箭头。

运行：python Tools/gen_ui_icons.py
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets/Art/V2/UI')

FILL = (255, 243, 208, 255)
EDGE = (90, 46, 8, 255)
SIZE = 64


def arrow(direction):
    im = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    tip_x, back_x = 10, 54
    top_y, bot_y = 10, 54
    mid_y = 32

    outline = [(tip_x, mid_y), (back_x, top_y), (back_x, bot_y)]
    d.line(outline + [outline[0]], fill=EDGE, width=8, joint='curve')
    d.polygon([(tip_x + 4, mid_y), (back_x - 4, top_y + 6), (back_x - 4, bot_y - 6)], fill=FILL)

    if direction == 'right':
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    return im


for name, dr in [('v2_ui_icon_arrow_left.png', 'left'), ('v2_ui_icon_arrow_right.png', 'right')]:
    p = os.path.join(OUT, name)
    arrow(dr).save(p)
    print('生成', os.path.relpath(p, ROOT))
