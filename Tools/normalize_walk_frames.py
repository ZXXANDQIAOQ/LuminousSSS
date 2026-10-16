# -*- coding: utf-8 -*-
"""行走帧画布归一 + 基线校验

用途：把尺寸各异的行走帧统一到固定画布，脚底落在同一条基线。
      —— 这是像素游戏「逐帧播放时角色抖动」的根因修复手段。

用法：
    python3 normalize_walk_frames.py <src_dir> [--out <dst_dir>] [--canvas 64x112]
                                     [--baseline 109] [--check]

    --check  只校验，不输出（退出码 0 = 全合规 / 3 = 有不合规）
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


def bbox_of(im):
    return im.convert('RGBA').getchannel('A').point(
        lambda v: 255 if v > 0 else 0).getbbox()


def norm_one(path, cw, ch, baseline, dst=None):
    """把单帧放到 cw×ch 画布：水平按主体中心居中，底边贴到 baseline。"""
    im = Image.open(path).convert('RGBA')
    box = bbox_of(im)
    if box is None:
        return None, '整图全透明'
    content = im.crop(box)
    bw, bh = content.size
    if bh > baseline + 1:
        return None, '主体高 %d > 基线 %d，画布装不下（需要加高画布或缩放主体）' % (bh, baseline + 1)
    canvas = Image.new('RGBA', (cw, ch), (0, 0, 0, 0))
    x = (cw - bw) // 2
    y = baseline - bh + 1
    canvas.alpha_composite(content, (x, y))
    if dst:
        canvas.save(dst)
    return canvas, None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('--out', default=None)
    ap.add_argument('--canvas', default='64x112')
    ap.add_argument('--baseline', type=int, default=109,
                    help='脚底基线（从画布顶部数，0-indexed），默认 109 = 112 高画布底部留 2px')
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args(argv)

    try:
        cw, ch = [int(v) for v in a.canvas.lower().split('x')]
    except Exception:
        sys.stderr.write('--canvas 格式应为 WxH\n')
        return EXIT_ARGS
    if not os.path.isdir(a.src):
        sys.stderr.write('目录不存在：%s\n' % a.src)
        return EXIT_IO

    files = sorted(f for f in os.listdir(a.src)
                   if f.startswith('v2_walk_') and f.endswith('.png'))
    if not files:
        sys.stderr.write('未找到 v2_walk_*.png\n')
        return EXIT_IO

    if a.out and not a.check:
        os.makedirs(a.out, exist_ok=True)

    print('画布 %dx%d ｜ 脚底基线 y=%d（底部留 %d px）' % (cw, ch, a.baseline, ch - 1 - a.baseline))
    print()
    print('%-26s %-11s %-11s %-9s %-9s %s' % ('文件', '原画布', '主体', '底边y', '中线偏移', '结论'))

    problems = []
    for fn in files:
        p = os.path.join(a.src, fn)
        src = Image.open(p)
        box = bbox_of(src)
        if box is None:
            problems.append('%s: 整图全透明' % fn)
            continue
        x0, y0, x1, y1 = box
        bw, bh = x1 - x0, y1 - y0
        # 归一前：主体底边相对画布底边的位置 + 主体中线相对画布中心
        old_bottom_off = src.size[1] - y1
        old_cx_off = ((x0 + x1) / 2.0) - src.size[0] / 2.0
        dst = None if a.check else os.path.join(a.out, fn)
        res, err = norm_one(p, cw, ch, a.baseline, dst)
        if err:
            problems.append('%s: %s' % (fn, err))
            print('%-26s %-11s %-11s %-9s %-9s %s' % (
                fn, '%dx%d' % src.size, '%dx%d' % (bw, bh), old_bottom_off,
                '%+.1f' % old_cx_off, '✗ ' + err))
            continue
        # 归一后校验
        nb = bbox_of(res)
        new_bottom = nb[3] - 1
        new_cx = (nb[0] + nb[2]) / 2.0
        okb = (new_bottom == a.baseline)
        okc = abs(new_cx - cw / 2.0) <= 1.0
        flag = '✅ 基线对齐' if (okb and okc) else '✗ 基线/居中不达标'
        if not (okb and okc):
            problems.append('%s: 归一后底边 y=%d（应 %d）、中线偏移 %.1f' % (fn, new_bottom, a.baseline, new_cx - cw / 2.0))
        print('%-26s %-11s %-11s %-9d %-9s %s' % (
            fn, '%dx%d' % src.size, '%dx%d' % (bw, bh), old_bottom_off,
            '%+.1f' % old_cx_off, flag))

    print()
    if problems:
        print('不合规 %d 项：' % len(problems))
        for m in problems:
            print('  ! %s' % m)
        return EXIT_FAIL
    print('全部合规：%d 帧统一到 %dx%d，脚底同在 y=%d' % (len(files), cw, ch, a.baseline))
    if a.out and not a.check:
        print('输出目录：%s' % a.out)
    return EXIT_OK


if __name__ == '__main__':
    sys.exit(main())
