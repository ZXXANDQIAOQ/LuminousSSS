"""从 v2_ui_dialog_panel 合成纵向面板素材。

原素材 1411x372（3.79:1），直接拉高到 960 会把羊皮纸纹理纵向撕成竖条。
做法：上/下边框原样保留，中段用原素材中心区做「去渐变 + 噪点回铺」重建 ——
纹理在、周期性横带没了。

运行：python Tools/gen_tall_panel.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(ROOT, 'Assets/Art/V2/UI')
SRC = os.path.join(UI, 'v2_ui_dialog_panel.png')
DST = os.path.join(UI, 'v2_ui_dialog_panel_tall.png')

OUT_H = 1411       # 输出高度：与素材等宽的近方形面板（配合 820x840 的显示尺寸）
CAP = 186          # 上下各保留多少原像素（含圆角 + 铆钉）
MID = (100, 272)   # 中段取样区（避开上下的铆钉）

src = Image.open(SRC).convert('RGBA')
a = np.array(src).astype(np.float64)
w, h = src.size                       # 1411 x 372

top = a[0:CAP, :]
bot = a[h - CAP:h, :]

seg = a[MID[0]:MID[1], :]

# 中段拆成「逐列均值 + 残差噪点」两部分重建：
# 直接镜像平铺会把原素材垂直方向的明暗渐变复制成多条可见横带，
# 这里把渐变剥掉只留单色底，再把随机噪点铺回去。
col_avg = seg.mean(axis=0, keepdims=True)
residual = seg - col_avg

# 直接平铺 residual 会让固定行数一循环的图案透成横带，先把行序打乱再铺，
# 保留「同行内左右相关」的纸张颗粒特征，去掉周期性。
rng = np.random.default_rng(20261015)
residual = residual[rng.permutation(residual.shape[0])]

need = OUT_H - CAP * 2
mid = np.tile(col_avg, (need, 1, 1))
reps = need // residual.shape[0] + 1
mid += np.tile(residual, (reps, 1, 1))[:need] * 0.75

out = np.clip(np.vstack([top, mid, bot]), 0, 255).astype(np.uint8)
Image.fromarray(out).save(DST)
print('生成', os.path.relpath(DST, ROOT), f'{w}x{out.shape[0]}')
