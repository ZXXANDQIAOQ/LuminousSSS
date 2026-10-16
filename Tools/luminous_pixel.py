# -*- coding: utf-8 -*-
"""LuminousSSS 像素素材绘制库

设计约束（全部来自对 Assets/Art/V2 现有素材的实测）：
  1. 硬边像素：alpha 只有 0 或 255，不做抗锯齿 —— 现有素材 22%-69% 的
     半透明边缘是黑底抠图的残留，在 Point 过滤下会变成毛边。
  2. 配色锚点取自现有素材实测主色。
  3. UI 正投影视角；光源统一左上 45°。
  4. 画布尺寸一律取 2 的整数倍。
"""

import numpy as np
from PIL import Image

# ---------------------------------------------------------------- 调色板（实测来源标注）

PAL = {
    # 木纹按钮 v2_ui_button_wood.png 实测主色 #BA5E11
    'wood':        (0xBA, 0x5E, 0x11),
    'wood_light':  (0xD2, 0x74, 0x22),
    'wood_hi':     (0xE8, 0x9A, 0x42),
    'wood_dark':   (0x8A, 0x42, 0x0A),
    'wood_deep':   (0x5C, 0x2A, 0x06),
    # 羊皮纸 v2_ui_dialog_panel.png 实测主色 #DDA85C / #D49E56
    'parch':       (0xDD, 0xA8, 0x5C),
    'parch_light': (0xEF, 0xC9, 0x8C),
    'parch_dark':  (0xC9, 0x8F, 0x4F),
    'parch_deep':  (0x8C, 0x5A, 0x24),
    # 木格底 v2_ui_slot_empty.png 实测主色 #C18B4D
    'slot':        (0xC1, 0x8B, 0x4D),
    'slot_light':  (0xD8, 0xA4, 0x66),
    'slot_dark':   (0x96, 0x66, 0x30),
    # 统一描边（v2_nature_crate 实测暗部 #311416 / #4D221F）
    'outline':     (0x2E, 0x1A, 0x0C),
    'outline_2':   (0x4A, 0x26, 0x08),
    # 文字（Docs/LuminousSSS-UI规格 §2.4）
    'cream':       (0xFF, 0xF3, 0xD0),
    # 血量 v2_ui_heart.png 实测主色 #ED0006
    'hp':          (0xED, 0x00, 0x06),
    'hp_light':    (0xFF, 0x4A, 0x42),
    'hp_dark':     (0xA8, 0x00, 0x06),
    'hp_deep':     (0x6E, 0x00, 0x04),
    'hp_empty':    (0x4E, 0x38, 0x2A),
    'hp_empty_hi': (0x6B, 0x52, 0x40),
    # 体力 v2_ui_stamina_bar.png 实测主色 #FFEE00
    'stamina':     (0xFF, 0xEE, 0x00),
    'stamina_hi':  (0xFF, 0xFB, 0x8A),
    'stamina_dark':(0xC9, 0xA8, 0x00),
    'stamina_deep':(0x8A, 0x72, 0x00),
    'slot_well':   (0x53, 0x33, 0x1A),   # 格子内凹底色
    'slot_well_hi':(0x6B, 0x45, 0x24),
    # 面板木框（比羊皮纸暗一档，保证框与纸的明暗对比）
    'frame':       (0x7A, 0x40, 0x14),
    'frame_hi':    (0xA6, 0x60, 0x24),
    'frame_lo':    (0x4C, 0x25, 0x09),
    # 通用
    'white':       (0xFF, 0xFF, 0xFF),
    'gold':        (0xFA, 0xE1, 0x0A),
    'gold_dark':   (0xDB, 0x9D, 0x06),
    'gold_deep':   (0x8A, 0x5E, 0x02),
    'plank':       (0xC8, 0x92, 0x4E),
    'plank_dark':  (0x8A, 0x5B, 0x28),
    'plank_deep':  (0x5A, 0x36, 0x14),

    # ---- 道具图标用色 ----
    # 石头（对齐 v2_nature_rock.png 实测主色 #7C7471）
    'stone':       (0x7C, 0x74, 0x71),
    'stone_hi':    (0x9E, 0x97, 0x94),
    'stone_dark':  (0x54, 0x4E, 0x4C),
    # 叶绿（对齐 v2_nature_bush 的深绿族）
    'leaf':        (0x4A, 0x8C, 0x3A),
    'leaf_hi':     (0x74, 0xBA, 0x4E),
    'leaf_dark':   (0x2C, 0x5C, 0x22),
    # 纸袋
    'paper':       (0xD8, 0xC0, 0x92),
    'paper_hi':    (0xEE, 0xDC, 0xB4),
    'paper_dark':  (0xA8, 0x8E, 0x60),
    'paper_deep':  (0x74, 0x5E, 0x38),
    # 作物
    'potato':      (0xB8, 0x8A, 0x4E),
    'potato_hi':   (0xD4, 0xAA, 0x6E),
    'potato_dark': (0x86, 0x5E, 0x2C),
    'corn':        (0xF2, 0xC4, 0x2A),
    'corn_hi':     (0xFF, 0xE4, 0x6A),
    'corn_dark':   (0xC0, 0x94, 0x10),
    'pumpkin':     (0xE0, 0x78, 0x1A),
    'pumpkin_hi':  (0xF8, 0xA0, 0x3C),
    'pumpkin_dark':(0xA8, 0x54, 0x0C),
    'berry_red':   (0xE0, 0x2A, 0x2A),
    'berry_red_hi':(0xFF, 0x64, 0x5A),
    'berry_red_dark':(0xA8, 0x14, 0x14),
    # 矿物
    'coal':        (0x32, 0x32, 0x38),
    'coal_hi':     (0x56, 0x56, 0x60),
    'coal_dark':   (0x18, 0x18, 0x1E),
    'gem':         (0x4A, 0x8C, 0xE0),
    'gem_hi':      (0x9E, 0xC8, 0xF8),
    'gem_dark':    (0x22, 0x52, 0x9A),
    # 采集品
    'shroom':      (0xD8, 0x3A, 0x2A),
    'shroom_hi':   (0xF2, 0x6A, 0x56),
    'shroom_dark': (0xA0, 0x22, 0x18),
    'berry':       (0x7A, 0x3A, 0xB8),
    'berry_hi':    (0xA8, 0x6A, 0xE0),
    'berry_dark':  (0x54, 0x24, 0x82),
    'honey':       (0xE8, 0xA8, 0x1A),
    'honey_hi':    (0xFF, 0xD0, 0x5A),
    'honey_dark':  (0xB2, 0x76, 0x08),
    'glass':       (0xC8, 0xE0, 0xE8),
    'glass_hi':    (0xEE, 0xF6, 0xFA),
    # 鱼
    'fish':        (0x4A, 0x9A, 0xC8),
    'fish_hi':     (0x86, 0xC8, 0xE8),
    'fish_dark':   (0x2A, 0x66, 0x8E),
}

C = PAL


def rgba(name, a=255):
    r, g, b = PAL[name]
    return (r, g, b, a)


# ---------------------------------------------------------------- 画布与硬边


def new_canvas(w, h):
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def harden(im, thresh=128):
    """alpha 二值化：像素资源必须硬边。"""
    r, g, b, a = im.split()
    a = a.point(lambda v: 255 if v >= thresh else 0)
    out = Image.merge('RGBA', (r, g, b, a))
    px = out.load()
    for y in range(out.size[1]):
        for x in range(out.size[0]):
            if px[x, y][3] == 0:
                px[x, y] = (0, 0, 0, 0)
    return out


def put(im, x, y, color):
    if 0 <= x < im.size[0] and 0 <= y < im.size[1]:
        im.putpixel((x, y), color if len(color) == 4 else color + (255,))


def rect(im, x0, y0, x1, y1, color):
    c = color if len(color) == 4 else color + (255,)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(im, x, y, c)


def hline(im, x0, x1, y, color):
    rect(im, x0, y, x1, y, color)


def vline(im, x, y0, y1, color):
    rect(im, x, y0, x, y1, color)


# ---------------------------------------------------------------- 掩码形态学


def dilate(m, r=1):
    out = m.copy()
    for _ in range(r):
        p = np.pad(out, 1, constant_values=False)
        out = (p[0:-2, 0:-2] | p[0:-2, 1:-1] | p[0:-2, 2:] |
               p[1:-1, 0:-2] | p[1:-1, 1:-1] | p[1:-1, 2:] |
               p[2:, 0:-2] | p[2:, 1:-1] | p[2:, 2:])
    return out


def erode(m, r=1):
    return ~dilate(~m, r)


def paint_mask(im, mask, color, off=(0, 0)):
    c = color if len(color) == 4 else color + (255,)
    ox, oy = off
    h, w = mask.shape
    for y in range(h):
        for x in range(w):
            if mask[y, x]:
                put(im, ox + x, oy + y, c)


def mask_to_image(mask, color):
    h, w = mask.shape
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    paint_mask(im, mask, color)
    return im


def place(dst, src, x, y):
    """把 src 贴到 dst（仅不透明像素覆盖）。"""
    p = src.load()
    for yy in range(src.size[1]):
        for xx in range(src.size[0]):
            c = p[xx, yy]
            if c[3] > 0:
                put(dst, x + xx, y + yy, c)


# ---------------------------------------------------------------- 心形（参数化 + 硬边化）


def _raw_heart(n=512):
    yy, xx = np.mgrid[0:n, 0:n].astype(float)
    x = ((xx + 0.5) / n) * 3.0 - 1.5
    y = -(((yy + 0.5) / n) * 3.0 - 1.42)
    return (x * x + y * y - 1) ** 3 - x * x * (y ** 3) <= 0


_HEART_CACHE = {}


def heart_mask(cw, ch):
    key = (cw, ch)
    if key in _HEART_CACHE:
        return _HEART_CACHE[key]
    big = _raw_heart(512)
    ys, xs = np.where(big)
    big = big[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    bh, bw = big.shape
    s = min(cw / float(bw), ch / float(bh))
    tw, th = max(1, int(round(bw * s))), max(1, int(round(bh * s)))
    small = np.array(Image.fromarray((big * 255).astype(np.uint8))
                     .resize((tw, th), Image.LANCZOS)) >= 128
    canvas = np.zeros((ch, cw), bool)
    ox, oy = (cw - tw) // 2, (ch - th) // 2
    canvas[oy:oy + th, ox:ox + tw] = small
    _HEART_CACHE[key] = canvas
    return canvas


def heart_icon(canvas, state):
    """state: full / half / empty。返回贴在画布中心的图标 Image。"""
    w, h = canvas
    m = heart_mask(w - 8, h - 12)          # 留 2px 描边 + 呼吸边
    outline = dilate(m, 2) & ~m
    icon = Image.new('RGBA', (w, h), (0, 0, 0, 0))

    if state == 'full':
        body, dark, hi = 'hp', 'hp_dark', 'hp_light'
    elif state == 'half':
        body, dark, hi = 'hp', 'hp_dark', 'hp_light'
    else:
        body, dark, hi = 'hp_empty', 'hp_empty', None

    paint_mask(icon, outline, rgba('hp_deep'))
    paint_mask(icon, m, rgba(body))

    # 右下暗部：沿右下轮廓的月牙带（不用斜切，避免生硬直角）
    inner = erode(m, 1)
    ys, xs = np.where(m)
    H, W = m.shape
    yy, xx = np.mgrid[0:H, 0:W]
    cy = (ys.min() + ys.max()) / 2.0
    cx = (xs.min() + xs.max()) / 2.0
    span = max(1.0, (xs.max() - xs.min()))
    band = inner & ~erode(m, max(3, int(span * 0.16)))
    shadow = band & ((xx - cx) * 0.5 + (yy - cy) > span * 0.06)
    paint_mask(icon, shadow, rgba(dark))

    if state == 'half':
        # 右半归一为「已失去」，中缝加深色竖线
        wm = np.zeros_like(m)
        wm[:, m.shape[1] // 2:] = True
        paint_mask(icon, m & wm, rgba('hp_empty'))
        seam = m & (np.abs(np.mgrid[0:m.shape[0], 0:m.shape[1]][1]
                           - m.shape[1] // 2) <= 0)
        paint_mask(icon, seam, rgba('hp_deep'))

    # 左上高光（光源左上 45°）；空心态用弱高光，避免白点在暗底上跳出来
    hi_main = 'white' if state != 'empty' else 'hp_empty_hi'
    ys, xs = np.where(m)
    if len(xs):
        hx = xs.min() + max(2, int((xs.max() - xs.min()) * 0.16))
        hy = ys.min() + max(2, int((ys.max() - ys.min()) * 0.16))
        for dy in range(-1, 4):
            for dx in range(-1, 4):
                if (dx, dy) in ((0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
                    if 0 <= hy + dy < h and 0 <= hx + dx < w and m[hy + dy, hx + dx]:
                        put(icon, hx + dx, hy + dy, rgba(hi_main))
        if hi:
            for dy in range(2, 5):
                for dx in range(2, 5):
                    if 0 <= hy + dy < h and 0 <= hx + dx < w and m[hy + dy, hx + dx]:
                        put(icon, hx + dx, hy + dy, rgba(hi))

    return icon


# ---------------------------------------------------------------- 圆角矩形 / 底板


def round_rect_mask(w, h, x0, y0, x1, y1, r):
    """圆角矩形掩码（像素硬边）。到「核心矩形」的距离 <= r 即在内。"""
    yy, xx = np.mgrid[0:h, 0:w]
    inside = (xx >= x0) & (xx <= x1) & (yy >= y0) & (yy <= y1)
    if r <= 0:
        return inside
    cx = np.clip(xx, x0 + r, x1 - r)
    cy = np.clip(yy, y0 + r, y1 - r)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    return inside & (d <= r + 0.5)


def nine_slice_panel(w, h, radius=12, frame=8, fill='parch', framed=True,
                     rivet=False, inner_well=None, inner_pad=0):
    """九宫格底板。层级：外描边 → 木框 → 亮内边 → 主体。

    framed=True（羊皮纸面板）：frame 取「木框外沿距画布边」，建议 >= 8，
      保证 border 切片时圆角 / 描边 / 木框都落在固定区。
    framed=False（木质容器）：单层木底 + 深色凹槽。
    """
    im = new_canvas(w, h)
    outer = round_rect_mask(w, h, 0, 0, w - 1, h - 1, radius)
    paint_mask(im, outer, rgba('outline'))

    if framed:
        wo = round_rect_mask(w, h, 2, 2, w - 3, h - 3, max(0, radius - 2))
        paint_mask(im, wo, rgba('frame'))
        band = np.zeros_like(wo)
        band[2:4, 3:w - 3] = True
        band[2:h - 2, 2:4] = True
        paint_mask(im, wo & band, rgba('frame_hi'))
        band2 = np.zeros_like(wo)
        band2[h - 4:h - 2, 3:w - 3] = True
        band2[2:h - 2, w - 4:w - 2] = True
        paint_mask(im, wo & band2, rgba('frame_lo'))
        edge = round_rect_mask(w, h, frame - 2, frame - 2, w - 1 - (frame - 2),
                               h - 1 - (frame - 2), max(0, radius - frame + 2))
        paint_mask(im, edge, rgba('parch_light'))
        body = round_rect_mask(w, h, frame, frame, w - 1 - frame, h - 1 - frame,
                               max(0, radius - frame))
        paint_mask(im, body, rgba(fill))
        # 羊皮纸略内凹：内侧上/左压暗，下/右提亮
        top_c, left_c, bot_c, right_c = 'parch_dark', 'parch_dark', 'parch_light', 'parch_light'
        inset = frame
    else:
        body = round_rect_mask(w, h, frame, frame, w - 1 - frame, h - 1 - frame,
                               max(0, radius - frame))
        paint_mask(im, body, rgba(fill))
        # 木质容器凸起：上/左提亮，下/右压暗
        top_c, left_c, bot_c, right_c = 'slot_light', 'slot_light', 'slot_dark', 'slot_dark'
        inset = frame

    # 光源左上
    top = np.zeros_like(body)
    top[inset:inset + 2, :] = True
    paint_mask(im, body & top, rgba(top_c))
    bot = np.zeros_like(body)
    bot[h - inset - 3:h - inset - 1, :] = True
    paint_mask(im, body & bot, rgba(bot_c))
    left = np.zeros_like(body)
    left[:, inset:inset + 2] = True
    paint_mask(im, body & left, rgba(left_c))
    right = np.zeros_like(body)
    right[:, w - inset - 3:w - inset - 1] = True
    paint_mask(im, body & right, rgba(right_c))

    if inner_well is not None:
        well = round_rect_mask(w, h, inner_pad, inner_pad, w - 1 - inner_pad,
                               h - 1 - inner_pad, max(0, radius - inner_pad // 2))
        paint_mask(im, well, rgba('outline_2'))
        paint_mask(im, erode(well, 2), rgba(inner_well))
        sh = np.zeros_like(well)
        sh[inner_pad:inner_pad + 2, inner_pad:w - inner_pad] = True
        sh[inner_pad:h - inner_pad, inner_pad:inner_pad + 2] = True
        paint_mask(im, well & sh, rgba('outline'))

    if rivet:
        cy_r = 5
        for (cx, cy) in ((5, cy_r), (w - 6, cy_r), (5, h - 6), (w - 6, h - 6)):
            for dy in range(-3, 4):
                for dx in range(-3, 4):
                    if dx * dx + dy * dy <= 8:
                        put(im, cx + dx, cy + dy, rgba('wood_deep'))
            for dy in range(-1, 2):
                for dx in range(-1, 2):
                    if abs(dx) + abs(dy) <= 1:
                        put(im, cx + dx, cy + dy, rgba('wood_hi'))
    return im


def bevel_bar(w, h, fill, hi, dark, deep, cap=4):
    """横向可拉伸的条（体力/进度），左右各 cap px 固定区。"""
    im = new_canvas(w, h)
    m = round_rect_mask(w, h, 0, 0, w - 1, h - 1, min(3, h // 2 - 1))
    paint_mask(im, m, rgba('outline'))
    body = erode(m, 1)
    paint_mask(im, body, rgba(deep))
    paint_mask(im, erode(body, 1), rgba(dark))
    core = erode(body, 2)
    if core.any():
        paint_mask(im, core, rgba(fill))
        hb = np.zeros_like(core)
        hb[core.shape[0] // 3:core.shape[0] // 3 + 2, :] = True
        paint_mask(im, core & hb, rgba(hi))
    return im
