# -*- coding: utf-8 -*-
"""LuminousSSS 道具图标产出（对应 采集/素材需求清单 v0.7 第四节 ITEM-01 ~ ITEM-08）

统一画布 128×128（项目 PPU=64 → 显示 2 tile；背包内按 64 或 48 显示）。
所有图标共用同一套家族规则：外描边 2px、光源左上 45°、留白率约 72%。
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image, ImageDraw
from luminous_pixel import *

import os as _os
OUT = _os.path.join(_os.environ.get('LUMINOUS_OUT', 'Assets/Art/V2'), 'Items')
os.makedirs(OUT, exist_ok=True)
S = 128


def save(im, name):
    im.save('%s/%s.png' % (OUT, name))
    print('  %-30s %dx%d' % (name, im.size[0], im.size[1]))


def ell(cx, cy, rx, ry):
    yy, xx = np.mgrid[0:S, 0:S]
    return ((xx - cx) / float(rx)) ** 2 + ((yy - cy) / float(ry)) ** 2 <= 1.0


def poly(pts):
    img = Image.new('L', (S, S), 0)
    ImageDraw.Draw(img).polygon(pts, fill=255)
    return np.array(img) > 0


def build(layers, outline_t=2):
    """layers: [(掩码, 颜色名), ...] 由底到顶；每层各自带描边，层间自然分隔。"""
    im = new_canvas(S, S)
    for m, _ in layers:
        paint_mask(im, dilate(m, outline_t), rgba('outline'))
    for m, c in layers:
        paint_mask(im, m, rgba(c))
    return im


def shade(im, mask, hi_key, lo_key, amt=0.42):
    """沿轮廓的月牙明暗（光源左上 45°）。直线分割会显得像被斜切一刀。"""
    ys, xs = np.where(mask)
    if not len(xs):
        return
    cx = (xs.min() + xs.max()) / 2.0
    cy = (ys.min() + ys.max()) / 2.0
    span = max(1.0, (xs.max() - xs.min()))
    t = max(3, int(span * amt * 0.42))
    band = erode(mask, 1) & ~erode(mask, t)
    yy, xx = np.mgrid[0:S, 0:S]
    d = (xx - cx) * 0.5 + (yy - cy)
    paint_mask(im, band & (d < -span * 0.10), rgba(hi_key))
    paint_mask(im, band & (d > span * 0.12), rgba(lo_key))


# ============================================================ 种子袋 ×3（ITEM-01）
BODY = (round_rect_mask(S, S, 34, 42, 93, 110, 10) | ell(63, 108, 30, 12))
FOLD = round_rect_mask(S, S, 26, 26, 101, 52, 7)
TIE = round_rect_mask(S, S, 30, 50, 97, 60, 3)

SEEDS = {
    'carrot': ('pumpkin', 'pumpkin_hi', 'pumpkin_dark'),   # 胡萝卜橙
    'tomato': ('hp', 'hp_light', 'hp_dark'),               # 番茄红
    'radish': ('cream', 'white', 'parch_dark'),            # 萝卜白
}
for key, (base, hi, lo) in SEEDS.items():
    im = build([(BODY, 'paper'), (FOLD, 'paper_hi'), (TIE, 'paper_dark')])
    shade(im, BODY, 'paper_hi', 'paper_dark', amt=0.30)
    shade(im, FOLD, 'paper_hi', 'paper_dark', amt=0.24)
    # 袋面标记：一颗种子
    mark = ell(63, 84, 16, 18)
    paint_mask(im, dilate(mark, 2), rgba('outline'))
    paint_mask(im, mark, rgba(base))
    shade(im, mark, hi, lo, amt=0.34)
    # 袋口缝线
    for x in range(42, 88, 9):
        rect(im, x, 62, x + 4, 64, rgba('paper_deep'))
    save(im, 'v2_item_seed_%s' % key)

# ============================================================ 石头堆（ITEM-02）
im = new_canvas(S, S)
rocks = [poly([(18, 108), (14, 92), (26, 78), (46, 74), (58, 84), (56, 104), (44, 112), (26, 112)]),
         poly([(52, 110), (50, 92), (62, 76), (84, 72), (100, 82), (104, 100), (94, 112), (68, 114)]),
         poly([(34, 80), (32, 64), (44, 52), (62, 50), (74, 60), (74, 76), (60, 84), (44, 84)])]
for i, m in enumerate(rocks):
    paint_mask(im, dilate(m, 2), rgba('outline'))
for i, m in enumerate(rocks):
    paint_mask(im, m, rgba('stone'))
    shade(im, m, 'stone_hi', 'stone_dark', amt=0.20)
save(im, 'v2_item_stone_pile')

# ============================================================ 木板（ITEM-03）
im = new_canvas(S, S)
for i, y in enumerate((34, 58, 82)):
    m = round_rect_mask(S, S, 16 + i * 3, y, 111 - i * 3, y + 22, 4)
    paint_mask(im, dilate(m, 2), rgba('outline'))
    paint_mask(im, m, rgba('plank'))
    band = np.zeros_like(m)
    band[y:y + 4, :] = True
    paint_mask(im, m & band, rgba('wood_hi'))
    band2 = np.zeros_like(m)
    band2[y + 18:y + 22, :] = True
    paint_mask(im, m & band2, rgba('plank_dark'))
    for x in range(24, 106, 26):
        put(im, x, y + 11, rgba('plank_deep'))
save(im, 'v2_item_plank')

# ============================================================ 金矿 / 煤 / 宝石（ITEM-05, ITEM-02）
def ore(vein_base, vein_hi, vein_lo, rock_key='stone'):
    im = new_canvas(S, S)
    m = poly([(20, 106), (16, 82), (30, 62), (56, 52), (86, 56), (104, 74),
              (108, 98), (94, 114), (52, 118), (28, 114)])
    paint_mask(im, dilate(m, 2), rgba('outline'))
    paint_mask(im, m, rgba(rock_key))
    shade(im, m, 'stone_hi', 'stone_dark', amt=0.18)
    for (cx, cy, r) in ((48, 78, 12), (76, 92, 14), (68, 66, 9), (38, 100, 8)):
        v = ell(cx, cy, r, r * 0.85)
        paint_mask(im, dilate(v, 1), rgba('outline'))
        paint_mask(im, v, rgba(vein_base))
        paint_mask(im, ell(cx - r // 3, cy - r // 3, r // 2, r // 3), rgba(vein_hi))
        paint_mask(im, ell(cx + r // 3, cy + r // 3, r // 3, r // 4), rgba(vein_lo))
    return im


save(ore('gold', 'gold', 'gold_deep'), 'v2_item_gold_ore')
save(ore('coal', 'coal_hi', 'coal_dark', rock_key='stone_dark'), 'v2_item_coal')

im = new_canvas(S, S)
gem = poly([(64, 18), (100, 52), (64, 114), (28, 52)])
paint_mask(im, dilate(gem, 2), rgba('outline'))
paint_mask(im, gem, rgba('gem'))
facet = poly([(64, 18), (100, 52), (64, 52)])
paint_mask(im, facet, rgba('gem_hi'))
facet2 = poly([(64, 52), (64, 114), (28, 52)])
paint_mask(im, facet2, rgba('gem_dark'))
line = poly([(64, 18), (56, 52), (64, 114), (72, 52)])
paint_mask(im, line & gem, rgba('gem_hi'))
save(im, 'v2_item_gem')

# ============================================================ 蘑菇 / 浆果 / 蜂蜜（ITEM-06）
im = new_canvas(S, S)
stalk = poly([(52, 62), (76, 62), (80, 110), (48, 110)]) | ell(64, 108, 17, 8)
cap = poly([(18, 66), (22, 44), (44, 28), (64, 24), (84, 28), (106, 44), (110, 66), (18, 66)])
paint_mask(im, dilate(stalk | cap, 2), rgba('outline'))
paint_mask(im, stalk, rgba('cream'))
shade(im, stalk, 'white', 'parch_dark', amt=0.42)
paint_mask(im, cap, rgba('shroom'))
shade(im, cap, 'shroom_hi', 'shroom_dark', amt=0.30)
for (cx, cy, r) in ((44, 46, 7), (76, 42, 6), (62, 58, 5)):
    paint_mask(im, ell(cx, cy, r, r), rgba('cream'))
save(im, 'v2_item_mushroom')

im = new_canvas(S, S)
leaf = ell(84, 40, 22, 12) & poly([(64, 46), (108, 28), (110, 52)])
berrys = [ell(44, 76, 20, 20), ell(74, 82, 18, 18), ell(58, 52, 16, 16)]
paint_mask(im, dilate(leaf, 2), rgba('outline'))
for b in berrys:
    paint_mask(im, dilate(b, 2), rgba('outline'))
paint_mask(im, leaf, rgba('leaf'))
paint_mask(im, leaf & ell(80, 36, 16, 6), rgba('leaf_hi'))
for b in berrys:
    paint_mask(im, b, rgba('berry'))
    paint_mask(im, ell(int(np.where(b)[1].mean()) - 5, int(np.where(b)[0].mean()) - 5, 6, 5), rgba('berry_hi'))
    paint_mask(im, ell(int(np.where(b)[1].mean()) + 6, int(np.where(b)[0].mean()) + 7, 7, 6), rgba('berry_dark'))
save(im, 'v2_item_berry')

im = new_canvas(S, S)
jar = round_rect_mask(S, S, 32, 52, 95, 112, 10)
paint_mask(im, dilate(jar, 2), rgba('outline'))
paint_mask(im, jar, rgba('glass'))
shade(im, jar, 'glass_hi', 'parch_dark', amt=0.22)
paint_mask(im, round_rect_mask(S, S, 34, 66, 93, 110, 8), rgba('honey'))
paint_mask(im, round_rect_mask(S, S, 34, 66, 93, 84, 8), rgba('honey_hi'))
paint_mask(im, round_rect_mask(S, S, 34, 102, 93, 110, 8), rgba('honey_dark'))
lid = round_rect_mask(S, S, 26, 34, 101, 54, 5)
paint_mask(im, dilate(lid, 2), rgba('outline'))
paint_mask(im, lid, rgba('parch_deep'))
paint_mask(im, round_rect_mask(S, S, 28, 36, 99, 47, 4), rgba('parch'))
paint_mask(im, round_rect_mask(S, S, 28, 36, 99, 40, 3), rgba('parch_light'))
# 玻璃高光（左侧竖条）
paint_mask(im, round_rect_mask(S, S, 38, 70, 48, 100, 4), rgba('glass_hi'))
paint_mask(im, round_rect_mask(S, S, 51, 74, 56, 92, 2), rgba('glass_hi'))
save(im, 'v2_item_honey')

# ============================================================ 鱼 ×2（ITEM-07）
def fish(body_key, hi_key, lo_key, fin_key):
    im = new_canvas(S, S)
    body = ell(58, 66, 42, 26) | poly([(58, 40), (64, 66), (58, 92)])
    tail = poly([(16, 66), (2, 40), (30, 66), (2, 92)])
    fin = poly([(56, 42), (74, 26), (84, 44)])
    paint_mask(im, dilate(body | tail | fin, 2), rgba('outline'))
    paint_mask(im, tail, rgba(fin_key))
    paint_mask(im, body, rgba(body_key))
    paint_mask(im, body & ell(52, 56, 30, 12), rgba(hi_key))
    paint_mask(im, body & ell(64, 82, 30, 10), rgba(lo_key))
    paint_mask(im, fin, rgba(fin_key))
    paint_mask(im, ell(90, 58, 9, 9), rgba('white'))
    paint_mask(im, ell(92, 58, 5, 5), rgba('coal'))
    paint_mask(im, ell(93, 56, 2, 2), rgba('white'))
    return im


save(fish('fish', 'fish_hi', 'fish_dark', 'fish_dark'), 'v2_item_fish_blue')
save(fish('gold', 'gold', 'gold_deep', 'pumpkin_dark'), 'v2_item_fish_gold')

print('\n完成（道具 %d 个）' % len([f for f in os.listdir(OUT) if f.endswith('.png')]))
