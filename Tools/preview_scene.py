# -*- coding: utf-8 -*-
"""把引擎导出的场景 JSON 合成成一张布局预览图。

数据来源：Assets/Editor/_SceneDigest.cs（batchmode 里跑，导出每个瓦片格和
每个 SpriteRenderer 的世界坐标 / 尺寸 / 排序值）。这里只负责按 sortingOrder
叠图 —— 世界坐标与素材路径都是引擎给的真实值，不是手抄的坐标表。

用途：本机 GUI 编辑器起不来（受限环境）+ batchmode 无 GPU，没法截 Game 视图，
用这张图核对「东西摆在哪、有没有互相压住、池塘/摊位/路口的位置对不对」。

用法：
    python Tools/preview_scene.py <digest.json> <地图宽> <地图高> <输出png>
例：
    python Tools/preview_scene.py .workbuddy/digest_village.json 34 20 Docs/preview_v2/scene_village.png
"""
import json
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PX = 32                     # 预览分辨率：1 格 = 32px（素材 64px 缩小一半）


def tint(im, r, g, b):
    arr = np.array(im).astype(np.float32)
    arr[:, :, 0] *= r
    arr[:, :, 1] *= g
    arr[:, :, 2] *= b
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')


def build(json_path, map_w, map_h, out_path):
    with open(json_path, encoding='utf-8') as f:
        objects = json.load(f)['objects']

    W, H = map_w * PX, map_h * PX
    canvas = Image.new('RGBA', (W, H), (74, 140, 58, 255))    # 草地底色，防边缘漏空

    tiles = sorted([o for o in objects if o['k'] == 'tile'], key=lambda o: o['order'])
    sprites = sorted([o for o in objects if o['k'] == 'sprite'], key=lambda o: o['order'])

    cache = {}

    def load(path):
        if path not in cache:
            full = os.path.join(ROOT, path)
            try:
                cache[path] = Image.open(full).convert('RGBA')
            except Exception:
                cache[path] = None
                print('  [warn] 读不到素材', path)
        return cache[path]

    def paste(o, default_w=1.0, default_h=1.0):
        src = load(o['sprite']) if o['sprite'] else None
        if src is None:
            return
        w = o.get('w', default_w)
        h = o.get('h', default_h)
        tw, th = max(1, int(round(w * PX))), max(1, int(round(h * PX)))
        im = src.resize((tw, th), Image.NEAREST)

        if o.get('flipX'):
            im = im.transpose(Image.FLIP_LEFT_RIGHT)

        r, g, b = o.get('r', 1.0), o.get('g', 1.0), o.get('b', 1.0)
        if (r, g, b) != (1.0, 1.0, 1.0):
            im = tint(im, r, g, b)

        px = int(round(o['x'] * PX))
        py = int(round((map_h - o['y'] - h) * PX))
        canvas.paste(im, (px, py), im)

    for o in tiles:
        paste(o)
    for o in sprites:
        paste(o)

    canvas.save(out_path)
    print('saved', out_path, canvas.size, f'(tiles={len(tiles)}, sprites={len(sprites)})')


def main():
    if len(sys.argv) != 5:
        print(__doc__)
        sys.exit(1)
    build(sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4])


if __name__ == '__main__':
    main()
