# -*- coding: utf-8 -*-
"""批量硬边化：把「非硬边」的像素素材边缘做 alpha 二值化 + 邻域去暗边。

背景：本项目大量素材来自 AI 生成 / 黑底抠图 / 缩放，边缘残留大量半透明过渡像素
      （实测：草丛 48.5%、树 8%、房屋 3~7%、道具 2~7%）。贴图是 Point 过滤 + 无压缩，
      这些像素在游戏里就是一圈灰黑毛边。

做法：
  1) alpha 二值化：>= thr 记 255，否则记 0（像素风只允许 0/255）。
  2) 去暗边：原本半透明、被判为不透明的像素，用 3x3 邻域内「原本就完全不透明」的
     像素 RGB 均值覆盖 —— 抠图残留的暗边就来自这些像素自身偏暗的 RGB。

**不要用于刻意柔边的素材**（立绘脚下投影、全屏渐变底图、转场遮罩），
在 SKIP 名单里排除。

用法：
    python3 harden_assets.py <目录或文件> [更多目录...] [--thr 128] [--out DIR] [--check]

    --check  只报告不改写（退出码 0 = 无半透明 / 3 = 仍有超阈值）
"""
import argparse
import os
import sys

try:
    import numpy as np
    from PIL import Image
except ImportError:
    sys.stderr.write('缺少依赖：pip install Pillow numpy\n')
    sys.exit(2)

EXIT_OK, EXIT_ARGS, EXIT_IO, EXIT_FAIL = 0, 1, 2, 3

# 刻意柔边，不做硬边化
SKIP = {
    'v2_ui_portrait_shadow',   # 立绘脚下柔边投影
    'v2_ui_login_bg',          # 全屏渐变底图
    'v2_ui_transition',        # 转场遮罩（需要柔边）
}


def semi_ratio(a):
    return float(((a > 0) & (a < 255)).sum()) / a.size * 100.0


def harden(path, thr, dst=None):
    im = Image.open(path).convert('RGBA')
    arr = np.array(im)
    alpha = arr[:, :, 3]
    before = semi_ratio(alpha)
    if before == 0.0:
        return before, 0.0, False

    new_alpha = np.where(alpha >= thr, 255, 0).astype(np.uint8)

    # 去暗边：对「原本半透明且判为不透明」的像素，用邻域内原本完全不透明的像素 RGB 覆盖
    solid = alpha == 255
    need = (alpha > 0) & (alpha < 255) & (new_alpha == 255)
    rgb = arr[:, :, :3].copy()
    if need.any() and solid.any():
        # 用 3x3 邻域内 solid 像素的均值；没有 solid 邻居的保持原色
        acc = np.zeros_like(rgb, dtype=np.float64)
        cnt = np.zeros(alpha.shape, dtype=np.float64)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                sh = np.roll(np.roll(solid, dy, axis=0), dx, axis=1)
                s_rgb = np.roll(np.roll(rgb, dy, axis=0), dx, axis=1)
                acc += s_rgb * sh[:, :, None]
                cnt += sh
        has = need & (cnt > 0)
        rgb[has] = (acc[has] / cnt[has][:, None]).round().astype(np.uint8)

    out = np.dstack([rgb, new_alpha])
    Image.fromarray(out, 'RGBA').save(dst or path)
    return before, semi_ratio(new_alpha), True


def collect(paths):
    files = []
    for p in paths:
        if os.path.isdir(p):
            for f in sorted(os.listdir(p)):
                if f.lower().endswith('.png'):
                    files.append(os.path.join(p, f))
        elif p.lower().endswith('.png'):
            files.append(p)
    return [f for f in files if os.path.splitext(os.path.basename(f))[0] not in SKIP]


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('paths', nargs='+')
    ap.add_argument('--thr', type=int, default=128)
    ap.add_argument('--out', default=None, help='输出目录（默认就地覆盖）')
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--threshold', type=float, default=2.0, help='判定超标的半透明占比 %%')
    a = ap.parse_args(argv)

    files = collect(a.paths)
    if not files:
        sys.stderr.write('没有找到可处理的 png\n')
        return EXIT_IO

    if a.out and not a.check:
        os.makedirs(a.out, exist_ok=True)

    print('=' * 78)
    print('批量硬边化｜阈值 %d｜%s' % (a.thr, '仅检查' if a.check else ('输出到 ' + a.out if a.out else '就地覆盖')))
    print('=' * 78)

    bad = 0
    changed = 0
    for f in files:
        if a.check:
            im = Image.open(f).convert('RGBA')
            r = semi_ratio(np.array(im)[:, :, 3])
            flag = '✅' if r <= a.threshold else '❌'
            if r > a.threshold:
                bad += 1
                print('%-46s %7.2f%% %s' % (os.path.basename(f), r, flag))
            continue
        dst = os.path.join(a.out, os.path.basename(f)) if a.out else None
        before, after, did = harden(f, a.thr, dst)
        if did:
            changed += 1
            print('%-46s %7.2f%% -> %5.2f%%' % (os.path.basename(f), before, after))

    print('-' * 78)
    if a.check:
        if bad == 0:
            print('全部合规：%d 张均 <= %.1f%%' % (len(files), a.threshold))
            return EXIT_OK
        print('仍超标 %d / %d 张' % (bad, len(files)))
        return EXIT_FAIL
    print('处理 %d 张，其中 %d 张有改动' % (len(files), changed))
    return EXIT_OK


if __name__ == '__main__':
    sys.exit(main())
