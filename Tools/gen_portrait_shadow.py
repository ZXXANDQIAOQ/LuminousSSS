"""程序化生成立绘脚下的椭圆投影（柔边，RGBA）。

让角色「站」在面板上，而不是飘在纸上。
运行：python Tools/gen_portrait_shadow.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets/Art/V2/UI/v2_ui_portrait_shadow.png')

W, H = 280, 72
yy, xx = np.mgrid[0:H, 0:W]
cx, cy = W / 2.0, H / 2.0
d = np.sqrt(((xx - cx) / (W / 2.0)) ** 2 + ((yy - cy) / (H / 2.0)) ** 2)

alpha = np.clip(1 - d, 0, 1) ** 1.8 * 160          # 中心浓、边缘渐隐

img = np.zeros((H, W, 4), np.uint8)
img[..., 0], img[..., 1], img[..., 2] = 56, 30, 8
img[..., 3] = alpha.astype(np.uint8)
Image.fromarray(img, 'RGBA').save(OUT)
print('生成', os.path.relpath(OUT, ROOT), f'{W}x{H}')
