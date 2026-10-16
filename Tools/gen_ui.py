# -*- coding: utf-8 -*-
"""LuminousSSS UI 素材产出（对应 采集/素材需求清单 v0.7 第六节 UI-01 ~ UI-11）

全部为程序化生成：尺寸精确、边界可算、可重跑、无 AI 残字风险。
"""
import os
import sys
import math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image
from luminous_pixel import *

import os as _os
OUT = _os.path.join(_os.environ.get('LUMINOUS_OUT', 'Assets/Art/V2'), 'UI')
os.makedirs(OUT, exist_ok=True)


def save(im, name):
    im.save('%s/%s.png' % (OUT, name))
    print('  %-34s %dx%d' % (name, im.size[0], im.size[1]))


# ============================================================ UI-01 血量三态
for st in ('full', 'half', 'empty'):
    save(heart_icon((48, 48), st), 'v2_ui_heart_%s' % st)

# ============================================================ UI-02 体力条
save(bevel_bar(128, 20, 'slot_well_hi', 'outline_2', 'slot_well', 'outline'),
     'v2_ui_stamina_bg')
save(bevel_bar(128, 20, 'stamina', 'stamina_hi', 'stamina_dark', 'stamina_deep'),
     'v2_ui_stamina_fill')

# ============================================================ 背包格子
slot = nine_slice_panel(96, 96, radius=10, frame=4, fill='slot', framed=False,
                        inner_well='slot_well', inner_pad=14)
save(slot, 'v2_ui_slot_empty')

sel = slot.copy()
# 选中态：外圈亮黄描边（2px，圆角与 slot 对齐）
ring = round_rect_mask(96, 96, 0, 0, 95, 95, 10) & ~round_rect_mask(96, 96, 2, 2, 93, 93, 8)
paint_mask(sel, ring, rgba('stamina'))
ring2 = round_rect_mask(96, 96, 2, 2, 93, 93, 8) & ~round_rect_mask(96, 96, 3, 3, 92, 92, 7)
paint_mask(sel, ring2, rgba('stamina_dark'))
save(sel, 'v2_ui_slot_selected')

# ============================================================ UI-03 背包主面板
save(nine_slice_panel(256, 256, radius=14, frame=12, fill='parch', rivet=True),
     'v2_ui_panel_inventory')

# ============================================================ UI-04 tooltip 底板
save(nine_slice_panel(256, 192, radius=12, frame=10, fill='parch'),
     'v2_ui_tooltip_panel')

# ============================================================ UI-05 对话气泡 + 尖角 + 头像框
save(nine_slice_panel(256, 160, radius=22, frame=10, fill='parch'),
     'v2_ui_dialog_bubble')

tail = new_canvas(40, 20)
tmask = np.zeros((20, 40), bool)
for i in range(20):
    half = int(round((20 - i) * 0.9))
    tmask[i, 20 - half:20 + half] = True
paint_mask(tail, dilate(tmask, 1), rgba('outline'))
paint_mask(tail, tmask, rgba('parch'))
sh = np.zeros_like(tmask)
sh[:, 26:34] = True
paint_mask(tail, tmask & sh, rgba('parch_dark'))
save(tail, 'v2_ui_bubble_tail')


def ring_mask(w, h, x0, y0, x1, y1, r, t):
    return (round_rect_mask(w, h, x0, y0, x1, y1, r) &
            ~round_rect_mask(w, h, x0 + t, y0 + t, x1 - t, y1 - t, max(0, r - t)))


pf = new_canvas(96, 96)
paint_mask(pf, ring_mask(96, 96, 0, 0, 95, 95, 47, 2), rgba('outline'))
paint_mask(pf, ring_mask(96, 96, 2, 2, 93, 93, 45, 2), rgba('wood_dark'))
paint_mask(pf, ring_mask(96, 96, 4, 4, 91, 91, 43, 2), rgba('wood'))
paint_mask(pf, ring_mask(96, 96, 6, 6, 89, 89, 41, 3), rgba('wood_light'))
paint_mask(pf, ring_mask(96, 96, 9, 9, 86, 86, 38, 1), rgba('wood_deep'))
save(pf, 'v2_ui_portrait_frame')

# ============================================================ UI-06 数值底板
save(nine_slice_panel(192, 64, radius=10, frame=6, fill='slot', framed=False,
                      rivet=False), 'v2_ui_currency_plate')

# ============================================================ UI-07 开关
for state in ('on', 'off'):
    w, h = 80, 40
    im = new_canvas(w, h)
    track = round_rect_mask(w, h, 0, 0, w - 1, h - 1, h // 2 - 1)
    paint_mask(im, track, rgba('outline'))
    t1 = round_rect_mask(w, h, 2, 2, w - 3, h - 3, h // 2 - 3)
    paint_mask(im, t1, rgba('wood_dark'))
    t2 = round_rect_mask(w, h, 4, 4, w - 5, h - 5, h // 2 - 5)
    paint_mask(im, t2, rgba('wood_deep'))
    well = round_rect_mask(w, h, 7, 7, w - 8, h - 8, h // 2 - 8)
    paint_mask(im, well, rgba('outline'))
    if state == 'on':
        glow = round_rect_mask(w, h, 8, 8, w - 9, h - 9, h // 2 - 9)
        paint_mask(im, glow, rgba('stamina_deep'))
        paint_mask(im, erode(glow, 2), rgba('stamina_dark'))
    # 圆钮
    r = h // 2 - 5
    cx = w - h // 2 if state == 'on' else h // 2
    cy = h // 2
    knob = round_rect_mask(w, h, cx - r, cy - r, cx + r, cy + r, r)
    paint_mask(im, knob, rgba('outline'))
    ki = round_rect_mask(w, h, cx - r + 2, cy - r + 2, cx + r - 2, cy + r - 2, r - 2)
    paint_mask(im, ki, rgba('slot'))
    kh = round_rect_mask(w, h, cx - r + 3, cy - r + 3, cx + r - 5, cy + r - 5, r - 3)
    paint_mask(im, ki & kh, rgba('slot_light'))
    save(im, 'v2_ui_switch_%s' % state)

# ============================================================ UI-08 转场遮罩
w = h = 256
im = new_canvas(w, h)
rect(im, 0, 0, w - 1, h - 1, (0x14, 0x0E, 0x09, 255))
rng = np.random.RandomState(20261016)
for _ in range(220):
    x = rng.randint(0, w)
    y = rng.randint(0, h)
    put(im, x, y, (0x1E, 0x15, 0x0E, 255))
save(im, 'v2_ui_transition')

# ============================================================ UI-09 加载图标 6 帧
n, cx, cy = 64, 31.5, 31.5
yy, xx = np.mgrid[0:n, 0:n]
d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
ring_m = (d >= n / 2 - 5) & (d <= n / 2 - 2)
ang = (np.degrees(np.arctan2(yy - cy, xx - cx)) + 360.0) % 360.0
for i in range(6):
    im = new_canvas(n, n)
    paint_mask(im, ring_m, rgba('outline'))
    start = i * 60.0
    rel = (ang - start) % 360.0
    head = ring_m & (rel <= 60)
    mid = ring_m & (rel > 60) & (rel <= 150)
    tail = ring_m & (rel > 150) & (rel <= 260)
    paint_mask(im, tail, rgba('wood_dark'))
    paint_mask(im, mid, rgba('gold_dark'))
    paint_mask(im, head, rgba('gold'))
    lead = ring_m & (rel <= 18)
    paint_mask(im, lead, rgba('cream'))
    save(im, 'v2_ui_load_%02d' % (i + 1))

# ============================================================ 方向箭头：不产出
# 现有 v2_ui_icon_arrow_left / _right 已是干净的纯三角（深棕描边 + 米白填充），
# 本轮曾按同源旋转生成 up / down，但对比后判定：
#   1) 暂无使用场景（项目只有角色切换用左右箭头）；
#   2) 项目原版造型优于自绘版本。
# 故不产出、也不覆盖，保持仓库现有一致。日后若要做列表/滚动 UI，再按
# 「以 v2_ui_icon_arrow_left 为基准做无损 90° 转置」的方式补齐，可保证同族一致。

print('\n完成')
