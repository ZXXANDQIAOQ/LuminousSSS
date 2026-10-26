# -*- coding: utf-8 -*-
"""铁匠铺实体道具：铁砧 + 锻炉（v0.21）。

为什么单独一个脚本而不是塞进 gen_village_props.py：
    这两件是「打铁玩法的一部分」——铁砧是锤子落点（火花从砧面迸出、震屏对齐），
    炉子是入炉加热的火源（面板打开期间持续冒余烬）。它们的位置与 BlacksmithActor
    的站位是一起调的，放在角色生成器旁边更好维护。

尺寸（PPU 64，1 格 = 64px）
    铁砧 v2_build_anvil.png  64x52   底部对齐，砧面在距底 26~38px 处
    锻炉 v2_build_forge.png  64x96   底部对齐，炉口中心约在距底 30px、横向居中

用法
    python3 Tools/gen_smithy.py
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from luminous_pixel import new_canvas, paint_mask, round_rect_mask, dilate  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   'Assets/Art/V2/Village')

C = {
    'outline':   (0x1E, 0x18, 0x14),
    'iron':      (0x86, 0x8E, 0x9A),
    'iron_hi':   (0xD2, 0xD8, 0xE0),
    'iron_lo':   (0x4A, 0x50, 0x5C),
    'iron_deep': (0x2E, 0x32, 0x3C),
    'stone':     (0x8C, 0x84, 0x78),
    'stone_hi':  (0xB4, 0xAC, 0x9C),
    'stone_lo':  (0x5E, 0x58, 0x4E),
    'ember':     (0xE8, 0x52, 0x14),
    'flame':     (0xFF, 0x8A, 0x28),
    'flame_hi':  (0xFF, 0xD8, 0x6A),
    'soot':      (0x24, 0x1C, 0x18),
    'wood':      (0x7A, 0x4A, 0x22),
}


def _c(rgb, a=255):
    return (rgb[0], rgb[1], rgb[2], a)


def ell(w, h, cx, cy, rx, ry):
    yy, xx = np.mgrid[0:h, 0:w]
    return ((xx - cx) / float(max(0.5, rx))) ** 2 + ((yy - cy) / float(max(0.5, ry))) ** 2 <= 1.0


def poly(w, h, pts):
    layer = Image.new('L', (w, h), 0)
    ImageDraw.Draw(layer).polygon([(float(x), float(y)) for x, y in pts], fill=255)
    return np.array(layer) > 0


def outline(im, width=2):
    solid = np.array(im)[:, :, 3] > 0
    out = im.copy()
    paint_mask(out, dilate(solid, width) & ~solid, _c(C['outline']))
    return out


def build_anvil():
    """铁砧：宽砧面 + 右侧尖角 + 细腰 + 梯形底座。"""
    W, H = 64, 52
    im = new_canvas(W, H)

    def rr(x0, y0, x1, y1, r):
        return round_rect_mask(W, H, x0, y0, x1, y1, r)

    # 底座（梯形，上窄下宽）
    base = poly(W, H, [(16, 50), (48, 50), (41, 34), (23, 34)])
    paint_mask(im, base, _c(C['iron_lo']))
    paint_mask(im, base & ell(W, H, 26, 42, 14, 10), _c(C['iron']))      # 左侧受光
    paint_mask(im, base & ell(W, H, 30, 51, 18, 3), _c(C['iron_deep']))  # 只压落地那一线

    # 腰
    waist = rr(26, 30, 40, 38, 2)
    paint_mask(im, waist, _c(C['iron']))
    paint_mask(im, waist & ell(W, H, 28, 34, 5, 6), _c(C['iron_hi']))

    # 砧面：主体 + 右侧尖角
    face = rr(6, 12, 56, 26, 2)
    horn = poly(W, H, [(56, 12), (63, 19), (56, 26)])
    paint_mask(im, face | horn, _c(C['iron']))
    # 顶面高光（左上 45° 光）
    paint_mask(im, (face | horn) & ell(W, H, 26, 13, 30, 7), _c(C['iron_hi']))
    # 底部暗面
    paint_mask(im, (face | horn) & ell(W, H, 32, 26, 30, 6), _c(C['iron_lo']))
    # 砧面上的小凹坑与划痕
    paint_mask(im, ell(W, H, 22, 18, 3, 2), _c(C['iron_lo']))
    paint_mask(im, ell(W, H, 40, 20, 2, 1.5), _c(C['iron_lo']))

    # 与底座之间的连接阴影
    paint_mask(im, rr(24, 26, 42, 32, 2), _c(C['iron_deep']))

    return outline(im)


def build_forge():
    """锻炉：石砌炉体 + 烟囱 + 炉膛火光 + 台前木炭。"""
    W, H = 64, 96
    im = new_canvas(W, H)

    def rr(x0, y0, x1, y1, r):
        return round_rect_mask(W, H, x0, y0, x1, y1, r)

    # 烟囱
    paint_mask(im, rr(24, 4, 40, 30, 2), _c(C['stone_lo']))
    paint_mask(im, rr(24, 4, 32, 30, 2), _c(C['stone']))
    paint_mask(im, rr(23, 2, 41, 8, 2), _c(C['stone']))

    # 炉体
    body = rr(8, 24, 56, 94, 6)
    paint_mask(im, body, _c(C['stone']))
    paint_mask(im, body & ell(W, H, 24, 40, 20, 22), _c(C['stone_hi']))
    paint_mask(im, body & ell(W, H, 48, 78, 16, 20), _c(C['stone_lo']))
    # 砖缝
    for y in (44, 62, 80):
        paint_mask(im, rr(10, y, 54, y + 1, 0) & body, _c(C['stone_lo']))

    # 炉膛（拱形黑洞）
    mouth = rr(18, 40, 46, 80, 5) | ell(W, H, 32, 42, 14, 8)
    paint_mask(im, mouth, _c(C['soot']))

    # 火焰：外焰橙、内焰黄
    flame = poly(W, H, [(24, 78), (40, 78), (38, 60), (32, 48), (26, 60)])
    paint_mask(im, flame, _c(C['flame']))
    inner = poly(W, H, [(28, 78), (36, 78), (35, 64), (32, 56), (29, 64)])
    paint_mask(im, inner, _c(C['flame_hi']))
    paint_mask(im, ell(W, H, 32, 78, 8, 3), _c(C['ember']))

    # 炉口两侧的炭
    paint_mask(im, ell(W, H, 20, 80, 4, 2), _c(C['ember']))
    paint_mask(im, ell(W, H, 44, 80, 4, 2), _c(C['ember']))

    # 台前（底部）阴影与地面接触面
    paint_mask(im, rr(6, 90, 58, 95, 3), _c(C['stone_lo']))

    return outline(im)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    a = build_anvil()
    a.save(os.path.join(OUT, 'v2_build_anvil.png'), optimize=True)
    f = build_forge()
    f.save(os.path.join(OUT, 'v2_build_forge.png'), optimize=True)
    print('生成：v2_build_anvil.png %s / v2_build_forge.png %s' % (a.size, f.size))
