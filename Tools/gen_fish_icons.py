# -*- coding: utf-8 -*-
"""LuminousSSS 鱼类道具图标 v2（v0.23 钓鱼玩法）—— 鳜鱼 / 湖主重绘

为什么有这个脚本
----------------
v0.21 的鳜鱼、湖主由 gen_items.py 的 fish_mandarin()/fish_legend() 生成，
和河鲈/锦鲤并排时有三个问题：腹部是一块内嵌深色椭圆（读作「洞」）、明暗
只有 2~3 档、斑点用近黑圆点（读作穿孔）。本脚本按河鲈/锦鲤的**实际像素
骨架**重画这两张；河鲈/锦鲤两张不动。

!! 注意 !!
gen_items.py 结尾仍会调用旧的 fish_mandarin()/fish_legend()，
直接重跑 gen_items.py 会把这两张覆盖回旧版。要重出这两张请只跑本脚本；
旧的 fish_mandarin()/fish_legend() 建议从 gen_items.py 删掉（本次未动该文件）。

几何基线（逐像素等于 gen_items.py fish() 的取值，四张图完全共用）
--------------------------------------------------------------
    身体   ell(58,66,42,26) | poly([(58,40),(64,66),(58,92)])
    尾鳍   poly([(16,66),(2,40),(30,66),(2,92)])
    背鳍   poly([(56,42),(74,26),(84,44)])
    眼睛   white ell(90,58,9,9) + coal ell(92,58,5,5) + white ell(93,56,2,2)
    描边   dilate(union,2)，颜色 #2E1A0C（从原版取色，不是纯黑）
→ union bbox x 2..100 / y 26..92，出图不透明 bbox x 0..102 / y 24..94。
湖主的「更贵」只改尾鳍分叉角度、背鳍轮廓、金鳍/金边/金侧线，
不越出上述 bbox —— 背包格子里四张并排不会跳。

画法（每个档位都是硬边色块，无过渡色）
--------------------------------------
家族共用：主体色 / 亮部（背上缘大面积透镜）/ 暗部（贴下缘月牙）/ 描边。
鳜鱼 ★3：+ 鞍状暗斑 2 档（斑块落在亮部上提亮一档，落在主体色上压暗一档）
         + 腹部浅带（贴下缘中段）+ 侧腹 2px 亮点。
湖主 ★5：+ 背脊月光边 + 下缘最深一档 + 流金侧线（含内嵌暗影、断 2px）
         + 鳃盖线 + 侧鳞亮点 + 金背鳍（放射鳍条）+ 金边尾鳍。

两条踩出来的规则：
  ① 深色块必须与轮廓相连。孤岛状深色块在这个尺寸上 100% 被读成「洞」；
  ② 深色块不下探到身体后 1/3，那块留给暗部与腹带，否则鱼会碎成花斑。

重跑
----
    cd /workspace/LuminousSSS
    python3 Tools/gen_fish_icons.py                    # 只出这两张图标（覆盖同路径）
    python3 Tools/gen_fish_icons.py --preview /tmp/p.png   # 另出四鱼并排对比图

对比预览需要河鲈/锦鲤原图，按顺序查找：
    Assets/Art/V2/Items/v2_item_fish_blue.png
    $FISH_REF_DIR/v2_item_fish_blue.png / $FISH_REF_DIR/fish_blue.png
（--refs 可改目录，默认 /tmp）
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image, ImageDraw
import luminous_pixel as lp
from luminous_pixel import dilate, new_canvas, paint_mask

S = 128

# ---------------------------------------------------------------- 调色板（本地扩展，不改库）
PAL = dict(lp.PAL)
PAL.update({
    # 鳜鱼：金琥珀梯度（黄褐底，不是灰橄榄；也不是米黄，米黄会读成「土豆」）
    'mand':        (0xCF, 0x94, 0x26),   # 主体色（金琥珀，不是灰褐）
    'mand_hi':     (0xEF, 0xBE, 0x55),   # 亮部（背上缘大面积）
    'mand_crest':  (0xFA, 0xD9, 0x8E),   # 侧腹鳞光（只做 2px 亮点）
    'mand_lo':     (0x96, 0x62, 0x0F),   # 暗部（贴下缘的月牙，比描边亮两档）
    'mand_belly':  (0xF7, 0xE3, 0xB4),   # 腹部浅带
    'mand_fin':    (0x7A, 0x42, 0x12),   # 鳍（暖褐，比身体暗一档）
    'mand_fin_hi': (0x9E, 0x60, 0x22),   # 鳍缘
    'mand_blot':   (0x63, 0x38, 0x06),   # 斑块（落在主体色上）
    'mand_blot_hi': (0x8A, 0x52, 0x10),  # 斑块（落在亮部上，跟着提亮一档）
    # 湖主：紫金
    'leg':         (0x5A, 0x2E, 0x92),   # 主体色
    'leg_hi':      (0x94, 0x55, 0xD8),   # 亮部
    'leg_crest':   (0xC9, 0xA6, 0xF0),   # 背脊高光（月光边）
    'leg_lo':      (0x38, 0x1D, 0x66),   # 暗部
    'leg_deep':    (0x28, 0x12, 0x48),   # 下缘最深一档（水下投影）
    'leg_fin':     (0xED, 0xB9, 0x3C),   # 金鳍
    'leg_fin_hi':  (0xFF, 0xD9, 0x6E),   # 鳍缘金
    'leg_fin_lo':  (0x8A, 0x5E, 0x02),   # 鳍条
    'leg_gold':    (0xF5, 0xC8, 0x4A),   # 流金侧线
})


def rgba(name, a=255):
    r, g, b = PAL[name]
    return (r, g, b, a)


def ell(cx, cy, rx, ry):
    yy, xx = np.mgrid[0:S, 0:S]
    return ((xx - cx) / float(rx)) ** 2 + ((yy - cy) / float(ry)) ** 2 <= 1.0


def poly(pts):
    img = Image.new('L', (S, S), 0)
    ImageDraw.Draw(img).polygon(pts, fill=255)
    return np.array(img) > 0


def py(x, y):
    m = np.zeros((S, S), bool)
    if 0 <= x < S and 0 <= y < S:
        m[y, x] = True
    return m


def line_mask(pts, w=1):
    img = Image.new('L', (S, S), 0)
    ImageDraw.Draw(img).line(pts, fill=255, width=w)
    return np.array(img) > 0


def rim(m, t=3):
    """贴轮廓的内环带：绕形体走一圈，厚度 t。用它代替内嵌椭圆，
    深色才不会变成浮在身上的「洞」。"""
    return m & ~lp.erode(m, t)


YY = np.mgrid[0:S, 0:S][0]


def upper(m, y0=66):
    return m & (YY < y0)


def lower(m, y0=66):
    return m & (YY > y0)


# ---------------------------------------------------------------- 公共骨架（与原版逐像素一致）
BODY = ell(58, 66, 42, 26) | poly([(58, 40), (64, 66), (58, 92)])
TAIL = poly([(16, 66), (2, 40), (30, 66), (2, 92)])
FIN = poly([(56, 42), (74, 26), (84, 44)])

# 湖主：尾鳍双叶张得更开、分叉更深（上下尖 y34/90，根 25/56..76，bbox 不变），
# 背鳍改成双叶（顶点仍取 (66,26)，保证上沿仍是 26 → 描边后仍是 24）。
TAIL_L = poly([(25, 56), (2, 34), (18, 66), (2, 90), (25, 76)])
FIN_L = poly([(54, 43), (60, 32), (66, 26), (72, 32), (76, 30), (82, 42), (84, 49)])

# 明暗档（沿用原版的「透镜」切法：椭圆 ∩ 身体，边界跟着体形弯，不是直线切）
DOME_HI = ell(52, 56, 30, 12)      # 主亮部，同原版
DOME_LO = ell(64, 82, 30, 10)      # 下缘暗部，同原版


def outline_pass(im, shapes, t=2):
    u = shapes[0]
    for s in shapes[1:]:
        u = u | s
    paint_mask(im, dilate(u, t), rgba('outline'))


def eye(im):
    """眼皮/瞳孔/高光：四张图完全同一套，作为家族记号。"""
    paint_mask(im, ell(90, 58, 9, 9), rgba('white'))
    paint_mask(im, ell(92, 58, 5, 5), rgba('coal'))
    paint_mask(im, ell(93, 56, 2, 2), rgba('white'))


# ---------------------------------------------------------------- 鳜鱼 ★★★
BLOTCHES = [
    # 不规则鞍状斑：从背缘挂下来 8~12px，边缘带缺口，长短不齐。
    # 两条硬规则（都是踩坑踩出来的）：
    #   ① 斑块必须连到背缘 —— 孤岛状圆点才读成「洞」，连轮廓的色块读作花纹；
    #   ② 斑块不下探到身体后 1/3 —— 那块留给下缘暗部与腹部浅带，
    #      保住家族「浅上深下」的体积读法，否则整条鱼会碎成花斑。
    poly([(26, 47), (33, 43), (39, 47), (38, 53), (33, 55), (28, 54)]),
    poly([(43, 42), (51, 41), (56, 46), (54, 53), (48, 52), (45, 49)]),
    poly([(59, 44), (67, 42), (72, 47), (70, 54), (64, 52)]),
    poly([(73, 49), (79, 48), (83, 54), (78, 57), (74, 54)]),
]


def fish_mandarin():
    im = new_canvas(S, S)
    outline_pass(im, [BODY, TAIL, FIN])

    paint_mask(im, TAIL, rgba('mand_fin'))
    paint_mask(im, BODY, rgba('mand'))

    # ① 主亮部（同原版透镜形）
    paint_mask(im, BODY & DOME_HI, rgba('mand_hi'))

    # ② 鞍状斑：不规则多边形（带缺口、长短不一），按所在明暗档换色 ——
    #    落在亮部上的偏亮，落在主体色上的偏暗，斑块因此「贴着体色深浅变化」。
    #    下缘暗部在这之后覆盖，斑块不会贴到描边。
    blots = np.zeros((S, S), bool)
    for b in BLOTCHES:
        blots = blots | b
    blots = blots & BODY
    hi_zone = BODY & DOME_HI
    paint_mask(im, blots & hi_zone, rgba('mand_blot_hi'))
    paint_mask(im, blots & ~hi_zone, rgba('mand_blot'))

    # ③ 下缘暗部（贴下缘的月牙，不是内嵌椭圆）④ 腹部浅带（沿下缘中段走，不绕成项圈）
    paint_mask(im, BODY & DOME_LO, rgba('mand_lo'))
    paint_mask(im, BODY & ell(64, 97, 27, 9), rgba('mand_belly'))

    paint_mask(im, FIN, rgba('mand_fin'))
    paint_mask(im, FIN & rim(FIN, 1), rgba('mand_fin_hi'))

    # 侧腹鳞光：2px 亮点（不是暗点 —— 暗点在这个尺寸上一定被读成洞）
    for x, y in ((35, 58), (46, 56), (58, 59), (67, 62), (43, 66), (55, 70)):
        paint_mask(im, (py(x, y) | py(x, y + 1)) & BODY & ~rim(BODY, 3),
                   rgba('mand_crest'))
    eye(im)
    return im


# ---------------------------------------------------------------- 湖主 ★★★★★
def fish_legend():
    im = new_canvas(S, S)
    outline_pass(im, [BODY, TAIL_L, FIN_L])

    # 尾鳍：深紫鳍面 + 1px 金边 + 深金鳍条。金只走「边」不走「面」——
    # 大面积金色会把传说感变成廉价感，金边才是镶嵌感。
    paint_mask(im, TAIL_L, rgba('leg_lo'))
    paint_mask(im, TAIL_L & rim(TAIL_L, 1), rgba('leg_fin'))
    for b in ((4, 38), (4, 88)):
        paint_mask(im, TAIL_L & line_mask([(21, 66), b], 1), rgba('leg_fin_lo'))

    # 身体 5 档：主体 / 亮部 / 背脊月光边 / 暗部 / 下缘最深
    paint_mask(im, BODY, rgba('leg'))
    paint_mask(im, BODY & DOME_HI, rgba('leg_hi'))
    paint_mask(im, upper(BODY) & rim(BODY, 2), rgba('leg_crest'))
    paint_mask(im, BODY & DOME_LO, rgba('leg_lo'))
    paint_mask(im, lower(BODY) & rim(BODY, 3), rgba('leg_deep'))

    # 鳃盖线：1px，只给湖主（细节密度最高的一档）
    gill = line_mask([(79, 49), (77, 53), (76, 57), (76, 61), (78, 66)])
    paint_mask(im, gill & BODY, rgba('leg_deep'))

    # 流金侧线：1px，从鳃盖线走到尾柄，沿体侧中线微微下沉。
    # 两端必须落在结构上（鳃 / 尾柄），悬空的横线会读成划痕。
    line = np.zeros((S, S), bool)
    under = np.zeros((S, S), bool)
    for x in range(31, 77):
        t = (x - 31) / 45.0
        y = int(round(63.0 + 2.4 * (2 * t - 1) ** 2))
        line |= py(x, y)
        if 0.22 < t < 0.78:
            under |= py(x, y + 1)
    line = line & BODY
    under = under & BODY & ~line
    # 断两处 1px：让金线读作「鳞光」，而不是一道划痕
    for x in (43, 62):
        line[:, x] = False
        under[:, x] = False
    paint_mask(im, under, rgba('leg_deep'))
    paint_mask(im, line, rgba('leg_gold'))

    # 侧鳞：2px 竖点，稀疏（缩到 64px 时是一层微光，不是噪点）
    for x, y in ((33, 53), (45, 48), (57, 47), (68, 53), (40, 72), (55, 71)):
        paint_mask(im, (py(x, y) | py(x, y + 1)) & BODY & ~line & ~rim(BODY, 5),
                   rgba('leg_crest'))

    # 背鳍：金面 + 放射状深金鳍条（顶点仍是 y=26，上沿描边位置不变）
    paint_mask(im, FIN_L, rgba('leg_fin'))
    rays = np.zeros((S, S), bool)
    for tip in ((58, 32), (66, 27), (73, 31), (81, 41)):
        rays |= line_mask([(66, 45), tip])
    paint_mask(im, FIN_L & rays, rgba('leg_fin_lo'))
    paint_mask(im, FIN_L & rim(FIN_L, 1), rgba('leg_fin_hi'))

    eye(im)
    return im


# ---------------------------------------------------------------- 输出与校验
def save(im, path):
    im.save(path)
    print('  %-46s %dx%d' % (os.path.basename(path), im.size[0], im.size[1]))


def stats(im):
    a = np.array(im.convert('RGBA'))
    m = a[:, :, 3] > 0
    ys, xs = np.where(m)
    return dict(bbox=(xs.min(), xs.max(), ys.min(), ys.max()),
                w=xs.max() - xs.min() + 1,
                h=ys.max() - ys.min() + 1,
                alpha=sorted(set(np.unique(a[:, :, 3]).tolist())),
                colors=len(set(map(tuple, a[m].reshape(-1, 4).tolist()))))


def verify(path):
    im = Image.open(path).convert('RGBA')
    s = stats(im)
    assert im.size == (128, 128), 'canvas must be 128x128'
    assert s['alpha'] == [0, 255], 'non-hard-edge alpha in %s: %s' % (path, s['alpha'])
    assert s['bbox'] == (0, 102, 24, 94), 'bbox drift in %s: %s' % (path, s['bbox'])
    return s


def preview(paths, out, scale=2, bg=(0x1E, 0x22, 0x30)):
    n = len(paths)
    sheet = Image.new('RGBA', (S * n, S), bg + (255,))
    for i, p in enumerate(paths):
        sheet.alpha_composite(Image.open(p).convert('RGBA'), (i * S, 0))
    sheet = sheet.resize((S * n * scale, S * scale), Image.NEAREST)
    sheet.convert('RGB').save(out)
    print('  preview -> %s (%dx%d)' % (out, sheet.size[0], sheet.size[1]))


REFS = ('v2_item_fish_blue.png', 'v2_item_fish_gold.png')


def find_refs(refdir):
    here = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        'Assets', 'Art', 'V2', 'Items')
    out = []
    for name in REFS:
        for cand in (os.path.join(here, name), os.path.join(refdir, name),
                     os.path.join(refdir, name.replace('v2_item_', ''))):
            if os.path.exists(cand):
                out.append(cand)
                break
        else:
            out.append(None)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'Assets', 'Art', 'V2', 'Items'))
    ap.add_argument('--preview', default='')
    ap.add_argument('--refs', default=os.environ.get('FISH_REF_DIR', '/tmp'))
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    made = []
    for fn, name in ((fish_mandarin, 'v2_item_fish_mandarin'),
                     (fish_legend, 'v2_item_fish_legend')):
        p = os.path.join(args.out, name + '.png')
        save(fn(), p)
        s = verify(p)
        print('      bbox=%s %dx%d alpha=%s colors=%d'
              % (s['bbox'], s['w'], s['h'], s['alpha'], s['colors']))
        made.append(p)

    if args.preview:
        refs = find_refs(args.refs)
        missing = [n for n, p in zip(REFS, refs) if p is None]
        if missing:
            print('  ! 缺少对比参照 %s（--refs 指定目录）' % missing)
        else:
            preview(refs + made, args.preview)


if __name__ == '__main__':
    main()
