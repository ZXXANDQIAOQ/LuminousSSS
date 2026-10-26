# -*- coding: utf-8 -*-
"""烁（原女巫，004）的行走动画：只位移靴子，**不改一个像素**。

为什么不直接拿 v2_witch_walk.png 当行走帧
    那张是她「举着掸子」的立绘，和 idle 的「抱药瓶」不是一套，交替播放会看到
    手上道具凭空变化。像素重绘腿部也试过 —— 64px 下根本还原不了原画的精度，
    出来是两根方块，反而毁了形象。两条路都不行。

最终方案：位移原像素
    原画两条腿在 x 26..29 之间有 4px 空隙，是分离的，可以各自整体平移。
    抬脚 = 把该腿 y>=90 的靴子像素整体上移 2px、向外撇 1px。
    上半身（帽子 / 头发 / 脸 / 身体 / 手臂 / 药瓶）与裙摆一个像素都不动，
    所以形象 100% 保留，动的只有脚。

6 帧循环
    抬左脚 → 并立 → 抬右脚 → 并立 → 抬左脚 → 并立
    8fps ≈ 0.75 秒一圈，和 2.8 格/秒的移速对得上。

产出（与原 idle 同尺寸 54x101，所以 v2_witch_idle*.png 完全不用改）
    v2_walk_witch_{down,up,left,right}_{01..06}.png
    左右方向互为镜像（整个人朝向翻转），上下共用（原画没有背面帧）。

用法
    python3 Tools/gen_witch_walk.py
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHARS = os.path.join(ROOT, 'Assets/Art/V2/Characters')

IDLE = 'v2_witch_idle.png'

# 靴子区域（实测：y >= 90 是长靴；两腿之间 x 26..29 是 4px 空隙）
BOOT_Y0 = 90
LEFT_BOOT = (14, 26)      # 左腿 x 范围（含端点）
RIGHT_BOOT = (28, 39)     # 右腿 x 范围（含端点）

# 6 帧：(左靴 dx, 左靴 dy, 右靴 dx, 右靴 dy)
FRAMES = [
    (-1, -2, 0, 0),    # 抬左脚：向外撇 1px、离地 2px
    (0, 0, 0, 0),      # 并立
    (0, 0, 1, -2),     # 抬右脚
    (0, 0, 0, 0),      # 并立
    (-1, -2, 0, 0),    # 再抬左脚
    (0, 0, 0, 0),      # 并立
]


def load_idle():
    """读原始 idle：若曾被旧脚本改成别的尺寸，优先读 *_src 备份。"""
    bak = os.path.join(CHARS, IDLE.replace('.png', '_src.png'))
    path = bak if os.path.exists(bak) else os.path.join(CHARS, IDLE)
    return Image.open(path).convert('RGBA')


def make_frame(base, ldx, ldy, rdx, rdy):
    im = base.copy()
    w, h = im.size

    # 先抠出两只靴子（先右后左，避免清空时互相吃掉）
    rbox = (RIGHT_BOOT[0], BOOT_Y0, RIGHT_BOOT[1] + 1, h)
    lbox = (LEFT_BOOT[0], BOOT_Y0, LEFT_BOOT[1] + 1, h)
    rb = im.crop(rbox)
    lb = im.crop(lbox)
    im.paste((0, 0, 0, 0), rbox)
    im.paste((0, 0, 0, 0), lbox)

    # 按各自偏移贴回；上移后底部自然留空 = 脚离地
    im.paste(lb, (lbox[0] + ldx, lbox[1] + ldy), lb)
    im.paste(rb, (rbox[0] + rdx, rbox[1] + rdy), rb)
    return im


def build():
    base = load_idle()
    frames = [make_frame(base, *f) for f in FRAMES]

    sets = {
        'down': frames,
        'up': frames,
        # 左右互为镜像：不是"左右脚反了"，而是整个人朝向翻转
        'left': [f.transpose(Image.FLIP_LEFT_RIGHT) for f in frames],
        'right': frames,
    }

    for d, fr in sets.items():
        for i, im in enumerate(fr, 1):
            im.save(os.path.join(CHARS, 'v2_walk_witch_%s_%02d.png' % (d, i)), optimize=True)

    print('已生成 24 帧（%dx%d）：只位移靴子像素，上半身与原画完全一致'
          % (base.size[0], base.size[1]))


if __name__ == '__main__':
    build()
