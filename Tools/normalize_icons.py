# -*- coding: utf-8 -*-
"""道具图标画布归一 + 规格校验

问题：图标画布尺寸从 27x21 到 225x146 不等，放进同一个背包格会大小乱跳。
      （旧图标是「裁到内容边界」的，画布=内容；新图标是「固定画布 + 内容居中留白」。）

做法：统一画布尺寸，并把**内容长边**缩放到同一目标值 ——
      显示时无论 UI 用 preserveAspect 还是拉伸，视觉大小都一致。
      这与行走帧归一（normalize_walk_frames.py）是同一思路的两种应用：
      行走帧统一「画布+基线」，图标统一「画布+内容长边」。

用法：
    python3 normalize_icons.py <src_dir> [--canvas 128x128] [--content 96]
                                [--out <dst_dir>] [--check] [--no-upscale]

    --check       只校验，不输出（退出码 0 = 全合规 / 3 = 有不合规）
    --no-upscale  内容长边已 <= 目标时不放大（用于场景物件，保持像素密度）

示例（背包图标，统一 128x128 画布 / 内容长边 96）：
    python3 normalize_icons.py Assets/Art/V2/Items --canvas 128x128 --content 96

示例（场景作物，统一 64x64 画布且不放大）：
    python3 normalize_icons.py Assets/Art/V2/Items --prefix v2_item_crop_growth_ \\
        --canvas 64x64 --content 56 --no-upscale
"""
import argparse
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.stderr.write('缺少依赖 Pillow：pip install Pillow\n')
    sys.exit(2)

EXIT_OK, EXIT_ARGS, EXIT_IO, EXIT_FAIL = 0, 1, 2, 3


def parse_size(s):
    w, h = s.lower().split('x')
    return int(w), int(h)


def bbox_of(im):
    return im.convert('RGBA').getchannel('A').point(
        lambda v: 255 if v > 0 else 0).getbbox()


def fit_content(im, content, no_upscale):
    """裁到内容 -> 等比缩放到长边 = content（no_upscale 时不放大）。"""
    box = bbox_of(im)
    if box is None:
        return None, '整图全透明'
    c = im.crop(box)
    long_side = max(c.size)
    if long_side == 0:
        return None, '内容为空'
    target = content
    if no_upscale and long_side <= content:
        target = long_side
    k = target / long_side
    if abs(k - 1.0) > 1e-6:
        # 最近邻：像素风缩放的唯一正确选择（双线性/双三次会引入中间色，破坏硬边）
        c = c.resize((max(1, round(c.width * k)), max(1, round(c.height * k))), Image.NEAREST)
    return c, None


def norm_one(path, cw, ch, content, no_upscale, dst=None):
    im = Image.open(path).convert('RGBA')
    c, err = fit_content(im, content, no_upscale)
    if err:
        return None, None, err
    if c.width > cw or c.height > ch:
        return None, None, '内容 %dx%d 放不进画布 %dx%d' % (c.width, c.height, cw, ch)
    canvas = Image.new('RGBA', (cw, ch), (0, 0, 0, 0))
    x, y = (cw - c.width) // 2, (ch - c.height) // 2
    canvas.alpha_composite(c, (x, y))
    if dst:
        canvas.save(dst)
    return canvas, c.size, None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('--canvas', default='128x128')
    ap.add_argument('--content', type=int, default=96, help='内容长边目标像素')
    ap.add_argument('--prefix', default='', help='只处理文件名以此开头的素材')
    ap.add_argument('--only', default='', help='只处理这些名字（不含扩展名），逗号分隔')
    ap.add_argument('--exclude', default='', help='跳过这些名字（不含扩展名），逗号分隔')
    ap.add_argument('--out', default=None)
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--no-upscale', action='store_true')
    a = ap.parse_args(argv)

    try:
        cw, ch = parse_size(a.canvas)
    except Exception:
        sys.stderr.write('--canvas 格式应为 WxH，如 128x128\n')
        return EXIT_ARGS

    if not os.path.isdir(a.src):
        sys.stderr.write('目录不存在：%s\n' % a.src)
        return EXIT_IO

    only = set(x.strip() for x in a.only.split(',') if x.strip())
    excl = set(x.strip() for x in a.exclude.split(',') if x.strip())

    def picked(f):
        stem = os.path.splitext(f)[0]
        if not f.lower().endswith('.png') or not f.startswith(a.prefix):
            return False
        if only and stem not in only:
            return False
        if stem in excl:
            return False
        return True

    files = sorted(f for f in os.listdir(a.src) if picked(f))
    if not files:
        sys.stderr.write('目录下没有匹配的 png：%s（prefix=%r only=%d exclude=%d）\n'
                         % (a.src, a.prefix, len(only), len(excl)))
        return EXIT_IO

    if a.out and not a.check:
        os.makedirs(a.out, exist_ok=True)

    print('=' * 84)
    print('图标归一｜画布 %dx%d ｜内容长边 %d%s' % (
        cw, ch, a.content, '（不放大）' if a.no_upscale else ''))
    print('=' * 84)
    print('%-34s %-12s %-12s %-8s %s' % ('文件', '原画布', '原内容', '缩放', '结果'))

    bad = 0
    for fn in files:
        p = os.path.join(a.src, fn)
        im = Image.open(p).convert('RGBA')
        box = bbox_of(im)
        ow, oh = im.size
        obw, obh = (box[2] - box[0], box[3] - box[1]) if box else (0, 0)
        dst = None if a.check else (os.path.join(a.out, fn) if a.out else None)
        canvas, csize, err = norm_one(p, cw, ch, a.content, a.no_upscale, dst)
        if err:
            bad += 1
            print('%-34s %-12s %-12s %-8s ❌ %s' % (fn, '%dx%d' % (ow, oh),
                                                  '%dx%d' % (obw, obh), '-', err))
            continue
        k = max(csize) / max(obw, obh) if max(obw, obh) else 1.0
        ok = (canvas.size == (cw, ch)) and (abs(max(csize) - a.content) <= 1 or a.no_upscale)
        if not ok:
            bad += 1
        print('%-34s %-12s %-12s %-8s %s' % (
            fn, '%dx%d' % (ow, oh), '%dx%d' % (obw, obh), 'x%.2f' % k,
            '✅ %dx%d' % canvas.size if ok else '❌ 内容 %dx%d' % csize))

    print('-' * 84)
    if bad == 0:
        print('全部合规：%d 张统一到 %dx%d，内容长边 %d' % (len(files), cw, ch, a.content))
        if a.out and not a.check:
            print('输出目录：%s' % a.out)
        return EXIT_OK
    print('不合规 %d / %d' % (bad, len(files)))
    return EXIT_FAIL


if __name__ == '__main__':
    sys.exit(main())
