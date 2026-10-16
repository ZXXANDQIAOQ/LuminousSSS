# -*- coding: utf-8 -*-
"""新素材 vs 现有素材：硬边质量对比"""
import os
from PIL import Image

import sys
NEW = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/V2'
OLD = sys.argv[2] if len(sys.argv) > 2 else NEW

def semi_ratio(path):
    im = Image.open(path).convert('RGBA')
    h = im.getchannel('A').histogram()
    return sum(h[1:255]) / float(im.size[0] * im.size[1])

def stat(d, label):
    files = []
    for root, _, fs in os.walk(d):
        for f in fs:
            if f.endswith('.png'):
                files.append(os.path.join(root, f))
    if not files:
        return None
    ratios = [semi_ratio(p) for p in files]
    worst = max(ratios)
    bad = sum(1 for r in ratios if r > 0.02)
    return len(files), sum(ratios) / len(ratios), worst, bad

print('=' * 84)
print('硬边质量检查（半透明像素占比；像素资源应 = 0%）')
print('=' * 84)
print('%-40s %-7s %-11s %-11s %s' % ('素材组', '数量', '平均占比', '最高占比', '超标(>2%)'))
for d, label in ((os.path.join(NEW, 'UI'), 'UI'), (os.path.join(NEW, 'Items'), '道具'),
                 (os.path.join(NEW, 'FX'), '特效'),
                 (os.path.join(NEW, 'Characters'), '角色帧'), (os.path.join(NEW, 'Portraits'), '立绘'),
                 (os.path.join(NEW, 'Nature'), '自然元素'),
                 (os.path.join(NEW, 'Tiles'), '瓦片'), (os.path.join(NEW, 'Buildings'), '建筑')):
    r = stat(d, label)
    if r is None:
        continue
    n, avg, worst, bad = r
    print('%-40s %-7d %-11s %-11s %d' % (label, n, '%.1f%%' % (avg * 100), '%.1f%%' % (worst * 100), bad))
