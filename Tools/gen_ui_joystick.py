# -*- coding: utf-8 -*-
"""程序化生成屏幕虚拟摇杆素材（底圈 + 摇杆头）。

为什么需要：
    FarmScene 里玩家移动只读 `Input.GetAxisRaw("Horizontal"/"Vertical")`，
    手机上没有键盘，安卓包里**完全无法移动**。虚拟摇杆是触屏移动的输入源，
    这个脚本只负责出素材（逻辑在 Assets/Scripts/Input/VirtualJoystick.cs）。

设计取舍：
    1. 底圈做成**空心环**。项目硬边规范禁止半透明（luminous_pixel 顶部第 1 条），
       而实心圆盘会在 1080 高的屏幕上遮掉一大块农场画面 —— 空心是唯一既能
       看清又不挡视野的做法。
    2. 配色全部取自 luminous_pixel.PAL（木纹族），与 v2_ui_button_wood 同源，
       摇杆因此天然像「同一套 UI」。
    3. 尺寸与 Inspector 里的显示尺寸 1:1（base 288 / knob 128），
       Point 过滤下不会出现非整数缩放的毛边。

运行：python Tools/gen_ui_joystick.py
      python Tools/gen_ui_joystick.py --verify   # 只校验已有产物，不写文件
输出：Assets/Art/V2/UI/v2_ui_joystick_base.png   288x288（空心环）
      Assets/Art/V2/UI/v2_ui_joystick_knob.png   128x128（实心木纹圆盘）
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from luminous_pixel import rgba, new_canvas, paint_mask

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets/Art/V2/UI')
DOCS = os.path.join(ROOT, 'Docs')
GRASS = os.path.join(ROOT, 'Assets/Art/V2/Tiles/fill_grass_a.png')

# 与 GameSceneBuilder.BuildFarmHud() 的常量一一对应，改一边必须改另一边
REF_W, REF_H = 1920, 1080          # Canvas 参考分辨率
AREA, BASE, KNOB, INSET = 480, 288, 128, 40
GESTURE_BAR = 50                   # 安卓手势条 / 虚拟导航栏大致占用的底部高度（UI 像素）

BASE_SIZE = 288      # 底圈画布边长（= Inspector 里的显示尺寸，1:1）
KNOB_SIZE = 128      # 摇杆头画布边长
RING_W = 18          # 底圈环宽（含内外描边）

BASE_NAME = 'v2_ui_joystick_base.png'
KNOB_NAME = 'v2_ui_joystick_knob.png'


def disc_mask(w, h, r, cx=None, cy=None):
    """硬边圆盘掩码（半径 r，含边界；r<=0 返回全 False）。"""
    yy, xx = np.mgrid[0:h, 0:w]
    cx = (w - 1) / 2.0 if cx is None else cx
    cy = (h - 1) / 2.0 if cy is None else cy
    d2 = (xx - cx) ** 2 + (yy - cy) ** 2
    return d2 <= (r + 0.5) ** 2


def _clear(im, mask):
    """把掩码内像素置为全透明（用于挖空底圈）。"""
    px = im.load()
    ys, xs = np.where(mask)
    for y, x in zip(ys, xs):
        px[int(x), int(y)] = (0, 0, 0, 0)


def make_base():
    """空心环底圈：外描边 → 木框 → 木纹环体 → 左上高光 / 右下暗部 → 内缘深色唇边。"""
    w = h = BASE_SIZE
    R = BASE_SIZE / 2.0 - 1.0
    cx = cy = (w - 1) / 2.0
    hole_r = R - RING_W

    im = new_canvas(w, h)
    ys, xs = np.mgrid[0:h, 0:w]
    diag = (xs - cx) + (ys - cy)          # <0 = 左上（光源方向）

    paint_mask(im, disc_mask(w, h, R), rgba('outline'))
    paint_mask(im, disc_mask(w, h, R - 3), rgba('frame'))
    band_body = disc_mask(w, h, R - 6)
    paint_mask(im, band_body, rgba('wood'))
    # 环面上的方向性明暗：沿左上 45° 高光，右下压暗
    paint_mask(im, band_body & (diag < -R * 0.45), rgba('wood_hi'))
    paint_mask(im, band_body & (diag > R * 0.45), rgba('wood_dark'))

    # 挖空中心 —— 底圈唯一的不透明区域就是这一圈环
    _clear(im, disc_mask(w, h, hole_r))

    # 内缘唇边：贴着镂空口的一圈暗色，让环有厚度感
    lip = disc_mask(w, h, hole_r + 3) & ~disc_mask(w, h, hole_r)
    paint_mask(im, lip, rgba('outline_2'))
    lip_lo = lip & (diag > R * 0.3)
    paint_mask(im, lip_lo, rgba('outline'))

    # 四点定位刻度：上下左右各一小段奶油色，提示「这是可推动的方向盘」
    tick = 6
    for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        for t in range(tick):
            r = R - 10 - t
            x = int(round(cx + dx * r))
            y = int(round(cy + dy * r))
            if 0 <= x < w and 0 <= y < h and im.getpixel((x, y))[3] > 0:
                im.putpixel((x, y), rgba('cream'))
    return im


def make_knob():
    """实心木纹圆盘：外描边 → 木框 → 木纹体 → 左上高光 / 右下暗部 → 中心奶油点。"""
    w = h = KNOB_SIZE
    R = KNOB_SIZE / 2.0 - 1.0
    cx = cy = (w - 1) / 2.0

    im = new_canvas(w, h)
    ys, xs = np.mgrid[0:h, 0:w]
    diag = (xs - cx) + (ys - cy)

    paint_mask(im, disc_mask(w, h, R), rgba('outline'))
    paint_mask(im, disc_mask(w, h, R - 3), rgba('frame'))
    body = disc_mask(w, h, R - 6)
    paint_mask(im, body, rgba('wood'))
    paint_mask(im, body & (diag < -R * 0.5), rgba('wood_hi'))
    paint_mask(im, body & (diag > R * 0.5), rgba('wood_dark'))

    # 中心奶油点 + 一圈深色边，抬头可见「抓手」
    dot = disc_mask(w, h, R * 0.26)
    paint_mask(im, dot, rgba('outline_2'))
    paint_mask(im, disc_mask(w, h, R * 0.26 - 2), rgba('cream'))
    return im


def verify():
    """校验已有产物：尺寸、硬边（alpha 只有 0/255）、以及关键位置的几何形态。

    用几何探针而不是面积比例 —— 圆盘外接矩形的四角本来就该是透明的，
    比例判据会把正常的圆盘判成「漏空」。
    """
    ok = True
    for name, size, is_ring in ((BASE_NAME, (BASE_SIZE, BASE_SIZE), True),
                                (KNOB_NAME, (KNOB_SIZE, KNOB_SIZE), False)):
        p = os.path.join(OUT, name)
        if not os.path.exists(p):
            print(f'[FAIL] 缺文件 {name}')
            ok = False
            continue

        im = Image.open(p).convert('RGBA')
        w, h = im.size
        a = np.array(im)[:, :, 3]
        partial = int(((a > 0) & (a < 255)).sum())
        opaque = int((a == 255).sum())

        cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
        R = w / 2.0 - 1.0
        at = lambda x, y: im.getpixel((int(round(x)), int(round(y))))[3]

        probes = [('圆心', at(cx, cy), is_ring is False),
                  ('左上角', at(2, 2), False)]
        if is_ring:
            probes.append(('环体中点', at(cx + (R - RING_W / 2.0), cy), True))
            probes.append(('镂空内点', at(cx + (R - RING_W - 6), cy), False))
        else:
            probes.append(('盘体边缘内', at(cx + (R - 4), cy), True))

        bad = [n for n, alpha, want_opaque in probes if (alpha > 0) != want_opaque]
        good = (partial == 0) and (w, h) == size and not bad
        if not good:
            ok = False

        print(f'[{"OK " if good else "FAIL"}] {name:26s} {w}x{h}  不透明 {opaque:6d}  '
              f'半透明 {partial}  探针 {"、".join(n for n, _, _ in probes)}'
              + (f'  ← 不符: {"、".join(bad)}' if bad else ''))

    print('\n结论：' + ('全部通过（硬边、尺寸、几何形态均符合预期）' if ok else '存在问题，见上'))
    return ok


# ---------------------------------------------------------------- 预览图

def _pixel_text(img, xy, text, scale=2, color=(255, 255, 255, 255)):
    """用 PIL 默认位图字体画字再最近邻放大 —— 与像素素材风格一致，且不依赖外部字体。"""
    font = ImageFont.load_default()
    tmp = Image.new('RGBA', (len(text) * 7 + 4, 15), (0, 0, 0, 0))
    ImageDraw.Draw(tmp).text((1, 1), text, font=font, fill=color)
    tmp = tmp.resize((tmp.size[0] * scale, tmp.size[1] * scale), Image.NEAREST)
    img.alpha_composite(tmp, (int(xy[0]), int(xy[1])))


def make_states_preview():
    """三态对照：居中 / 推到右上极限 / 推到左下极限。"""
    base = Image.open(os.path.join(OUT, BASE_NAME)).convert('RGBA')
    knob = Image.open(os.path.join(OUT, KNOB_NAME)).convert('RGBA')
    travel = BASE / 2 - KNOB / 2                      # 80
    pad = 20
    img = Image.new('RGBA', (BASE * 3 + pad * 4, BASE + pad * 2 + 30), (120, 152, 92, 255))
    for i, (dx, dy) in enumerate([(0, 0), (travel * 0.707, -travel * 0.707),
                                  (-travel * 0.707, travel * 0.707)]):
        ox, oy = pad + i * (BASE + pad), pad
        img.alpha_composite(base, (ox, oy))
        img.alpha_composite(knob, (int(ox + BASE / 2 - KNOB / 2 + dx),
                                   int(oy + BASE / 2 - KNOB / 2 + dy)))
    _pixel_text(img, (pad, img.size[1] - 24),
                'center       up-right max      down-left max', 2)
    return img.convert('RGB')


def make_layout_preview():
    """把摇杆按 BuildFarmHud 的真实参数摆到 1920x1080 画布左下角，并叠加参考线。

    用途：不开编辑器就能判断「位置偏不偏、大小合不合适、会不会被系统手势条压住」。
    """
    base = Image.open(os.path.join(OUT, BASE_NAME)).convert('RGBA')
    knob = Image.open(os.path.join(OUT, KNOB_NAME)).convert('RGBA')

    if os.path.exists(GRASS):                          # 用真实草地无缝块平铺，观感接近游戏内
        g = Image.open(GRASS).convert('RGBA')
        gw = g.size[0]
        canvas = Image.new('RGBA', (REF_W, REF_H), (0, 0, 0, 255))
        for y in range(0, REF_H, gw):
            for x in range(0, REF_W, gw):
                canvas.alpha_composite(g, (x, y))
    else:
        canvas = Image.new('RGBA', (REF_W, REF_H), (74, 140, 58, 255))

    # 屏幕坐标：左下角向内 INSET + AREA/2；Canvas 的 y 轴向上、图像 y 轴向下，要翻过来
    cx = INSET + AREA / 2
    cy = REF_H - (INSET + AREA / 2)

    canvas.alpha_composite(base, (int(cx - BASE / 2), int(cy - BASE / 2)))
    canvas.alpha_composite(knob, (int(cx - KNOB / 2), int(cy - KNOB / 2)))

    guide = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(guide)

    x0, y0, x1, y1 = cx - AREA / 2, cy - AREA / 2, cx + AREA / 2, cy + AREA / 2
    for x in range(int(x0), int(x1), 16):              # 触摸热区：红色虚线框
        d.rectangle([x, y0, x + 8, y0], fill=(230, 60, 60, 210))
        d.rectangle([x, y1, x + 8, y1], fill=(230, 60, 60, 210))
    for y in range(int(y0), int(y1), 16):
        d.rectangle([x0, y, x0, y + 8], fill=(230, 60, 60, 210))
        d.rectangle([x1, y, x1, y + 8], fill=(230, 60, 60, 210))

    # 系统手势条 / 虚拟导航栏大致占用的底部区域
    d.rectangle([0, REF_H - GESTURE_BAR, REF_W, REF_H], fill=(60, 120, 230, 95))

    canvas = Image.alpha_composite(canvas, guide)
    _pixel_text(canvas, (cx - 130, cy - 244), 'touch area 480x480', 2, (255, 218, 218, 255))
    _pixel_text(canvas, (cx - 104, cy + 168), 'base d288 / knob d128', 2)
    _pixel_text(canvas, (12, REF_H - GESTURE_BAR - 32), 'system gesture nav ~50', 2,
                (206, 228, 255, 255))
    _pixel_text(canvas, (REF_W - 300, 14), 'reference canvas 1920x1080', 2)
    return canvas.convert('RGB')


def main():
    if '--verify' in sys.argv:
        sys.exit(0 if verify() else 1)

    os.makedirs(OUT, exist_ok=True)
    for name, im in ((BASE_NAME, make_base()), (KNOB_NAME, make_knob())):
        p = os.path.join(OUT, name)
        im.save(p)
        print(f'生成 {os.path.relpath(p, ROOT).replace(os.sep, "/")}  {im.size[0]}x{im.size[1]}')

    if '--no-preview' not in sys.argv:
        os.makedirs(DOCS, exist_ok=True)
        for name, im in (('preview_joystick.png', make_states_preview()),
                         ('preview_joystick_layout.png', make_layout_preview())):
            p = os.path.join(DOCS, name)
            im.save(p)
            print(f'生成 {os.path.relpath(p, ROOT).replace(os.sep, "/")}  {im.size[0]}x{im.size[1]}')

    print()
    verify()


if __name__ == '__main__':
    main()
