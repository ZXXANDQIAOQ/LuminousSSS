"""程序化生成登录界面背景：像素风暗色渐变 + 中心辉光 + 块状噪点。

纯原创、可复现，不含任何第三方素材。
运行：python Tools/gen_login_bg.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets/Art/V2/UI/v2_ui_login_bg.png')

W, H = 1920, 1080
BLOCK = 4                      # 像素块尺寸：噪声按 4px 量化，贴合像素风

y = np.linspace(0, 1, H)[:, None]
# 垂直渐变：顶部暖棕 -> 底部近黑
top = np.array([42, 30, 20], float)
bottom = np.array([20, 14, 9], float)
img = top[None, None, :] * (1 - y[..., None]) + bottom[None, None, :] * y[..., None]
img = np.repeat(img, W, axis=1)

# 中心偏上辉光（呼应项目名 Luminous）
cx, cy, radius = W * 0.5, H * 0.42, H * 0.62
xs = np.arange(W)[None, :]
ys = np.arange(H)[:, None]
d = np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2) / radius
glow = np.clip(1 - d, 0, 1) ** 2 * 26
img += glow[..., None]

# 块状噪点：先在小网格上取随机值再放大，得到像素块
rng = np.random.default_rng(20261015)
gw, gh = W // BLOCK + 1, H // BLOCK + 1
noise = rng.integers(-4, 5, size=(gh, gw)).astype(float)
noise = np.repeat(np.repeat(noise, BLOCK, 0), BLOCK, 1)[:H, :W]
img += noise[..., None]

# 四角压暗，把视线收拢到中央
vig = np.clip(1 - (((xs - cx) / (W * 0.62)) ** 2 + ((ys - cy) / (H * 0.68)) ** 2), 0.55, 1)
img *= vig[..., None]

Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB').save(OUT)
print('生成', os.path.relpath(OUT, ROOT), f'{W}x{H}')
