# -*- coding: utf-8 -*-
"""行走帧一致性校验（自动化《第二批需求》验收第 ⑧ 条）

背景：项目已栽过一次 —— 农夫 24 帧里，[up] 方向 6 帧凭空多了一顶黄草帽，
其余 18 帧没有。这类「特征漂移」靠肉眼并排看 24 张很累且容易漏，
本脚本把它变成可复现的判定。

它查四件事
    ① 画布 / 脚底基线 / 水平居中（几何）
    ② 硬边（半透明像素 = 0）
    ③ **跨帧调色板漂移**：某帧出现了该角色绝大多数帧都没有的显著色块
       —— 黄草帽就是这么被逮出来的
    ④ 逐方向汇总，指出「哪个方向的头部配色与其余方向最不一致」（离群方向）

用法
    # 只校验几何与硬边
    python3 check_walk_consistency.py Assets/Art/V2/Characters

    # 完整校验（含调色板漂移）
    python3 check_walk_consistency.py Assets/Art/V2/Characters --canvas 64x112 --baseline 109

    # 从严：把「显著色块」门槛降到 0.8%，并把离群方向阈值收紧
    python3 check_walk_consistency.py <dir> --min-share 0.008 --outlier 0.6

退出码
    0 全部通过 ／ 1 参数错 ／ 2 目录或文件问题 ／ 3 检出问题
"""

import argparse
import os
import re
import sys
from collections import Counter, defaultdict

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

try:
    import numpy as np
    from PIL import Image
except ImportError:
    sys.stderr.write('缺少依赖 Pillow / numpy\n')
    sys.exit(2)

EXIT_OK, EXIT_ARGS, EXIT_IO, EXIT_FAIL = 0, 1, 2, 3

# 兼容两种命名：
#   现行代码用的        v2_walk_<方向>_<nn>.png          （无角色段，GameSceneBuilder 写死这种）
#   第二批需求要求的    v2_walk_<角色>_<方向>_<nn>.png
NAME_RE = re.compile(
    r'^v2_walk_(?:(?P<who>[a-z0-9]+)_)?(?P<dir>down|up|left|right)_(?P<n>\d{2})\.png$')
DIRS = ('down', 'up', 'left', 'right')


def hexc(c):
    return '#%02X%02X%02X' % (int(c[0]), int(c[1]), int(c[2]))


def analyse(path, q=16, alpha_min=128):
    """返回 (画布, 主体bbox, 半透明占比, {量化色: 占比})"""
    with Image.open(path) as im:
        rgba = im.convert('RGBA')
        w, h = rgba.size
        a = np.array(rgba)
    alpha = a[:, :, 3]
    semi = int(((alpha > 0) & (alpha < 255)).sum())
    solid = alpha >= alpha_min
    box = None
    ys, xs = np.where(solid)
    if len(xs):
        box = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))
    total = int(solid.sum())
    colors = {}
    if total:
        qc = (a[:, :, :3][solid] // q) * q
        uniq, cnt = np.unique(qc.reshape(-1, 3), axis=0, return_counts=True)
        colors = {tuple(int(v) for v in u): int(c) / float(total)
                  for u, c in zip(uniq, cnt)}
    return (w, h), box, semi / float(w * h), colors


def main(argv=None):
    ap = argparse.ArgumentParser(add_help=True)
    ap.add_argument('dir')
    ap.add_argument('--canvas', default=None, help='期望画布 WxH，例如 64x112；不传则不做画布判定')
    ap.add_argument('--baseline', type=int, default=None,
                    help='期望脚底基线 y（0-indexed），例如 109；不传则不做基线判定')
    ap.add_argument('--min-share', type=float, default=0.02,
                    help='色块「显著」门槛，占主体像素比例，默认 0.012')
    ap.add_argument('--majority', type=float, default=0.6,
                    help='某色被多少比例的帧共有才算「属于该角色」，默认 0.6')
    ap.add_argument('--alpha-min', type=int, default=128,
                    help='判定「主体像素」的 alpha 下限，默认 128（取中点；纯硬边素材下该值不影响结果）')
    ap.add_argument('--quant', type=int, default=16, help='颜色量化步长，默认 16（越大越粗，越不易被噪色干扰）')
    ap.add_argument('--max-report', type=int, default=6, help='每个角色最多报告几个游离色，默认 6')
    ap.add_argument('--outlier', type=float, default=0.55,
                    help='方向离群判定：某方向与其他方向的调色板重合率低于该值即报警，默认 0.55')
    a = ap.parse_args(argv)

    if not os.path.isdir(a.dir):
        sys.stderr.write('目录不存在：%s\n' % a.dir)
        return EXIT_IO
    exp_canvas = None
    if a.canvas:
        try:
            exp_canvas = tuple(int(v) for v in a.canvas.lower().split('x'))
        except Exception:
            sys.stderr.write('--canvas 格式应为 WxH\n')
            return EXIT_ARGS

    files = sorted(f for f in os.listdir(a.dir) if f.endswith('.png'))
    unparsed = [f for f in files if not NAME_RE.match(f)]
    by_who = defaultdict(list)
    data = {}
    for f in files:
        m = NAME_RE.match(f)
        if not m:
            continue
        who = m.group('who') or '(未标角色)'
        by_who[who].append(f)
        data[f] = (who, m.group('dir'), int(m.group('n')))
    if not data:
        sys.stderr.write('未找到符合 v2_walk_<角色>_<方向>_<01..06>.png 的文件\n')
        if unparsed:
            sys.stderr.write('目录内 png 示例：%s\n' % ', '.join(unparsed[:6]))
        return EXIT_IO

    problems = []
    print('=' * 100)
    print('行走帧一致性校验 ｜ %s' % a.dir)
    print('共 %d 个角色、%d 帧' % (len(by_who), len(data)))
    print('=' * 100)

    metrics = {}
    # ---------------- ① ② 几何与硬边 ----------------
    print('\n【①② 几何与硬边】')
    print('%-30s %-11s %-11s %-9s %-9s %s' % ('文件', '画布', '主体', '底边y', '中线偏移', '半透明'))
    for f in sorted(data):
        who, dr, n = data[f]
        canvas, box, semi, colors = analyse(os.path.join(a.dir, f), a.quant, a.alpha_min)
        metrics[f] = dict(who=who, dir=dr, canvas=canvas, box=box, semi=semi, colors=colors)
        if box is None:
            problems.append('%s：整图全透明' % f)
            print('%-30s %-11s %s' % (f, '%dx%d' % canvas, '全透明'))
            continue
        x0, y0, x1, y1 = box
        bottom = y1
        cx = (x0 + x1) / 2.0
        off = cx - canvas[0] / 2.0
        flags = []
        if exp_canvas and canvas != exp_canvas:
            flags.append('画布≠%dx%d' % exp_canvas)
        if a.baseline is not None and bottom != a.baseline:
            flags.append('底边y=%d≠%d' % (bottom, a.baseline))
        if abs(off) > 1.0:
            flags.append('偏心')
        if semi > 0.02:
            flags.append('非硬边')
        if exp_canvas and canvas != exp_canvas:
            problems.append('%s：画布 %dx%d，应为 %dx%d' % (f, canvas[0], canvas[1], *exp_canvas))
        if a.baseline is not None and bottom != a.baseline:
            problems.append('%s：底边 y=%d，应为 %d（差 %d px，播放时会「踩空」）'
                            % (f, bottom, a.baseline, bottom - a.baseline))
        if abs(off) > 1.0:
            problems.append('%s：主体中线偏移 %+.1f px（应 ≤1）' % (f, off))
        if semi > 0.02:
            problems.append('%s：半透明像素 %.2f%%（应 ≤2%%）' % (f, semi * 100))
        print('%-30s %-11s %-11s %-9d %-9s %s' % (
            f, '%dx%d' % canvas, '%dx%d' % (x1 - x0 + 1, y1 - y0 + 1), bottom, '%+.1f' % off,
            '%.2f%%' % (semi * 100) + ('  ← %s' % '/'.join(flags) if flags else '')))

    if unparsed:
        print('\n未按命名解析（%d 个，未参与校验）：%s' % (len(unparsed), ', '.join(unparsed[:8])))

    # ---------------- ③ 调色板漂移 ----------------
    print('\n【③ 跨帧调色板漂移】（检出「多出来的帽子 / 换掉的衣服」这类特征漂移）')
    for who in sorted(by_who):
        fl = [f for f in sorted(by_who[who]) if f in metrics and metrics[f]['box']]
        if len(fl) < 2:
            print('\n  角色 %s：帧数不足，跳过' % who)
            continue
        # 统计每个显著色块出现在多少帧
        appear = Counter()
        for f in fl:
            for c, share in metrics[f]['colors'].items():
                if share >= a.min_share:
                    appear[c] += 1
        need = max(2, int(len(fl) * a.majority))
        majority = {c for c, n in appear.items() if n >= need}
        print('\n  角色 %s（%d 帧）｜角色基调色 %d 个（被 ≥%d 帧共有）'
              % (who, len(fl), len(majority), need))

        drifted = []
        for f in fl:
            for c, s in metrics[f]['colors'].items():
                if s >= a.min_share and c not in majority:
                    drifted.append((f, metrics[f]['dir'], c, s))
        if drifted:
            # 按色块聚合，避免每帧刷屏
            agg = defaultdict(lambda: {'share': 0.0, 'frames': set(), 'dirs': set()})
            for f, dr, c, s in drifted:
                agg[c]['share'] = max(agg[c]['share'], s)
                agg[c]['frames'].add(f)
                agg[c]['dirs'].add(dr)
            shown = sorted(agg.items(), key=lambda kv: -kv[1]['share'])[:a.max_report]
            for c, info in shown:
                ds = '/'.join(sorted(info['dirs']))
                print('    ⚠ 色 %s 占比最高 %.1f%%，仅出现在 %d 帧、方向 [%s]：'
                      % (hexc(c), info['share'] * 100, len(info['frames']), ds))
                print('       %s' % ', '.join(sorted(info['frames'])[:6]))
                problems.append('角色 %s：色 %s 仅出现在 %s 方向 %d 帧 —— 疑似特征漂移'
                                % (who, hexc(c), ds, len(info['frames'])))
            if len(agg) > a.max_report:
                print('    …另有 %d 个更小的游离色未列出（--max-report 可调）' % (len(agg) - a.max_report))
        else:
            print('    ✅ 未检出游离色块：所有显著色块都为该角色多数帧共有')

    # ---------------- ④ 方向离群 ----------------
    print('\n【④ 方向离群】（哪个方向的整体配色与其余方向最不一致）')
    for who in sorted(by_who):
        fl = [f for f in sorted(by_who[who]) if f in metrics and metrics[f]['box']]
        per_dir = defaultdict(set)
        for f in fl:
            for c, s in metrics[f]['colors'].items():
                if s >= a.min_share:
                    per_dir[metrics[f]['dir']].add(c)
        dirs = [d for d in DIRS if d in per_dir]
        if len(dirs) < 3:
            continue
        scores = {}
        for d in dirs:
            others = set()
            for o in dirs:
                if o != d:
                    others |= per_dir[o]
            inter = len(per_dir[d] & others)
            union = len(per_dir[d] | others)
            scores[d] = inter / float(union) if union else 1.0
        worst = min(scores, key=scores.get)
        line = '  %s：' % who + '  '.join('%s=%.2f' % (d, scores[d]) for d in dirs)
        print(line)
        if scores[worst] < a.outlier:
            print('    ⚠ 方向 [%s] 与其余方向调色板重合仅 %.2f，明显离群 —— 优先人工并排核对'
                  % (worst, scores[worst]))
            problems.append('角色 %s：方向 %s 调色板离群（重合 %.2f），需人工核对'
                            % (who, worst, scores[worst]))

    # ---------------- 结论 ----------------
    print('\n' + '=' * 100)
    if problems:
        print('检出 %d 项（按《第二批需求》验收标准，需退回重做）：' % len(problems))
        for p in problems:
            print('  ! %s' % p)
        print('\n注意：调色板漂移是「疑似」判定 —— 若某方向本就有合法差异')
        print('（例如背面才看得见的披风、只在该方向出现的道具），属误报，请人工确认后放行。')
        return EXIT_FAIL
    print('全部通过：几何 / 硬边 / 跨帧一致性均无异常')
    return EXIT_OK


if __name__ == '__main__':
    sys.exit(main())
