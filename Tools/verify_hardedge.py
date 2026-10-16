# -*- coding: utf-8 -*-
"""硬边质量校验（像素资源 alpha 必须只有 0 / 255）

修复记录（v0.9.1）：
    旧版只把 sys.argv[1] 当「根目录」，再拼出 <根>/UI、<根>/Items……
    一旦传入多个目录（如 Assets/Art/V2/UI Assets/Art/V2/Items），
    实际会去找 Assets/Art/V2/UI/UI 这种不存在的路径，
    而旧版对「目录不存在」是静默 continue —— 于是输出整个为空、也不报错。
    本版改为：显式接收任意多个目录；目录不存在会明确报错；
    并对超阈值的文件逐张列出，便于退回重做。

用法
    python3 verify_hardedge.py                        # 默认扫 Assets/Art/V2 下全部子目录
    python3 verify_hardedge.py <dir> [<dir> ...]      # 指定任意多个目录
    python3 verify_hardedge.py <dir> --threshold 0    # 阈值收紧到 0（严格硬边）
    python3 verify_hardedge.py <dir> --list           # 逐张列出，不只给汇总

退出码
    0 全部合规 ／ 1 参数错 ／ 2 目录问题 ／ 3 有文件超阈值
"""

import argparse
import os
import sys

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

try:
    from PIL import Image
except ImportError:
    sys.stderr.write('缺少依赖 Pillow：pip install Pillow\n')
    sys.exit(2)

EXIT_OK, EXIT_ARGS, EXIT_IO, EXIT_FAIL = 0, 1, 2, 3

# 默认扫描顺序（相对于 --root）
DEFAULT_SUBDIRS = ('UI', 'Items', 'FX', 'Characters', 'Portraits',
                   'Nature', 'Buildings', 'Tiles')


def semi_ratio(path):
    """半透明像素（0 < alpha < 255）占比。"""
    with Image.open(path) as im:
        im = im.convert('RGBA')
        h = im.getchannel('A').histogram()
        return sum(h[1:255]) / float(im.size[0] * im.size[1])


def scan(d):
    files = []
    for root, _, fs in os.walk(d):
        for f in sorted(fs):
            if f.lower().endswith('.png'):
                files.append(os.path.join(root, f))
    return sorted(files)


def main(argv=None):
    ap = argparse.ArgumentParser(add_help=True)
    ap.add_argument('dirs', nargs='*', help='要校验的目录（可多个）')
    ap.add_argument('--root', default='Assets/Art/V2',
                    help='不传 dirs 时，扫描该根目录下的标准子目录（默认 Assets/Art/V2）')
    ap.add_argument('--threshold', type=float, default=0.02,
                    help='半透明占比阈值，默认 0.02（2%%，与验收标准一致）；0 表示严格')
    ap.add_argument('--list', action='store_true', help='逐张列出每个文件')
    a = ap.parse_args(argv)

    if not (0.0 <= a.threshold <= 1.0):
        sys.stderr.write('--threshold 取值范围 0-1\n')
        return EXIT_ARGS

    # ---- 决定要扫哪些目录（关键修复点：不再静默跳过）----
    targets = []          # (显示名, 路径)
    if a.dirs:
        for d in a.dirs:
            if not os.path.isdir(d):
                sys.stderr.write('目录不存在：%s\n' % d)
                return EXIT_IO
            targets.append((os.path.basename(d.rstrip('/')) or d, d))
    else:
        if not os.path.isdir(a.root):
            sys.stderr.write(
                '根目录不存在：%s\n'
                '请在仓库根目录执行，或用 --root / 直接传目录。\n' % a.root)
            return EXIT_IO
        for sub in DEFAULT_SUBDIRS:
            d = os.path.join(a.root, sub)
            if os.path.isdir(d):
                targets.append((sub, d))
        # 根目录下直接放的 png 也算一组
        if any(f.lower().endswith('.png') for f in os.listdir(a.root)):
            targets.append(('(根目录直接放的图)', a.root))
        if not targets:
            sys.stderr.write('%s 下没有可校验的子目录\n' % a.root)
            return EXIT_IO

    print('=' * 96)
    print('硬边质量校验 ｜ 判据：alpha 只有 0 或 255；半透明像素占比阈值 %.1f%%' % (a.threshold * 100))
    print('扫描目录：%s' % '、'.join(n for n, _ in targets))
    print('=' * 96)
    print('%-24s %-7s %-12s %-12s %s' % ('素材组', '数量', '平均占比', '最高占比', '超标张数'))

    violators = []
    total = 0
    for name, d in targets:
        files = scan(d)
        if not files:
            print('%-24s %-7s %s' % (name, 0, '（该目录下没有 png）'))
            continue
        rows = []
        for p in files:
            try:
                rows.append((p, semi_ratio(p)))
            except Exception as exc:
                sys.stderr.write('无法读取 %s：%s\n' % (p, exc))
                return EXIT_IO
        total += len(rows)
        ratios = [r for _, r in rows]
        bad = [(p, r) for p, r in rows if r > a.threshold]
        violators.extend((name, p, r) for p, r in bad)
        print('%-24s %-7d %-12s %-12s %d' % (
            name, len(rows),
            '%.2f%%' % (sum(ratios) / len(ratios) * 100),
            '%.2f%%' % (max(ratios) * 100), len(bad)))
        if a.list:
            for p, r in rows:
                print('    %-56s %6.2f%%%s' % (
                    os.path.basename(p), r * 100, '   ← 超标' if r > a.threshold else ''))

    print()
    print('合计 %d 张' % total)
    if violators:
        print('超标 %d 张（需硬边化或重画）：' % len(violators))
        for name, p, r in sorted(violators, key=lambda x: -x[2]):
            print('  ! [%s] %-46s %6.2f%%' % (name, os.path.basename(p), r * 100))
        print()
        print('处理建议：alpha 二值化即可硬边化，但会略微削掉边缘细节；')
        print('若素材本身是柔边绘制（非像素风），二值化后会发毛，应重画。')
        return EXIT_FAIL

    print('全部合规：%d 张均为硬边（半透明占比 ≤ %.1f%%）' % (total, a.threshold * 100))
    return EXIT_OK


if __name__ == '__main__':
    sys.exit(main())
