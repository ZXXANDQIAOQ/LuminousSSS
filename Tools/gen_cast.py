# -*- coding: utf-8 -*-
"""角色素材生成器（v0.20）：铁匠 NPC + 三个新可玩角色。

为什么要有这个脚本
------------------
项目里所有会走路的角色都是「程序化像素 + 共用骨架」：同一套腿/臂/头几何 +
同一张步态表，只换配色与装饰层。这样 24 帧之间的特征不会漂移（历史上栽过
「up 方向凭空多一顶草帽」），也保证四个角色站在一起是同一套美术语言。

约定（与 GameSceneBuilder.EnsureCharacterAnimations 对齐）
    · 画布 64x112，脚底基线 y=109，主体水平居中 x=32
    · 硬边：alpha 只有 0 / 255
    · 行走帧命名 v2_walk_{key}_{dir}_{01..06}.png（dir = down/up/left/right）
    · 待机帧命名 v2_{key}_idle.png / v2_{key}_idle_bob.png
    · 铁匠挥锤帧 v2_npc_smith_strike_0{1..6}.png

产出角色
    knight     银骑士     板甲 + 红披风 + 圆盔羽饰
    hunter     森之猎手   绿兜帽斗篷 + 皮甲 + 背后箭袋
    alchemist  炼金术士   白袍 + 青绿滚边 + 额上护目镜 + 药瓶
    smith      铁匠（NPC）深棕皮围裙 + 灰衬衫 + 铁锤

用法
    python3 Tools/gen_cast.py                 # 全部生成
    python3 Tools/gen_cast.py knight          # 只生成某个角色
    python3 Tools/gen_cast.py --preview       # 额外拼一张预览大图到 out/
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from luminous_pixel import new_canvas, paint_mask, round_rect_mask, dilate  # noqa: E402

W, H = 64, 112
BASE = 109          # 脚底基线
CX = 32             # 主体水平中心

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       'Assets/Art/V2/Characters')

# 六帧步态：(左腿dx, 左腿抬起, 右腿dx, 右腿抬起, 身体起伏, 左臂dy, 右臂dy)
# 与 gen_farmer_pilot.py 完全一致 —— 农夫那 24 帧已经在游戏里验证过手感。
WALK = [
    (0, 0, 0, 0, 0, 0, 0),
    (0, 2, 0, 0, -1, 0, 1),
    (-3, 0, 3, 0, 0, 1, -1),
    (0, 0, 0, 0, 0, 0, 0),
    (0, 0, 0, 2, -1, 1, 0),
    (3, 0, -3, 0, 0, -1, 1),
]


# ---------------------------------------------------------------- 几何工具

def ell(cx, cy, rx, ry):
    yy, xx = np.mgrid[0:H, 0:W]
    return ((xx - cx) / float(max(0.5, rx))) ** 2 + ((yy - cy) / float(max(0.5, ry))) ** 2 <= 1.0


def rr(x0, y0, x1, y1, r):
    return round_rect_mask(W, H, x0, y0, x1, y1, r)


def poly(pts):
    """多边形填充（尖帽、披风下摆这类硬直边）。

    用 PIL 的 polygon 而不是自己算半平面交集：点序一旦写反，半平面法会把
    "内部"填成"外部"，帽子/披风会整块消失（第一版女巫的尖顶就是这么没的）。
    """
    layer = Image.new('L', (W, H), 0)
    ImageDraw.Draw(layer).polygon([(float(x), float(y)) for x, y in pts], fill=255)
    return np.array(layer) > 0


def _c(rgb, a=255):
    return (rgb[0], rgb[1], rgb[2], a)


# ---------------------------------------------------------------- 角色定义
# 每个角色只声明「颜色 + 装饰开关」，几何全部走共用骨架。
# 配色原则：主色饱和度拉高、明度分层（fill / hi / lo），保证 64px 下也能分辨。

STYLES = {
    'witch': dict(
        name='女巫',
        c=dict(
            outline=(0x1A, 0x10, 0x20),
            outfit=(0x2E, 0x36, 0x76), outfit_hi=(0x48, 0x52, 0xA2), outfit_lo=(0x18, 0x1C, 0x48),
            accent=(0x6A, 0x3A, 0xA0), accent_hi=(0x92, 0x5E, 0xCC), accent_lo=(0x40, 0x1C, 0x6A),
            pants=(0x28, 0x1C, 0x3C), pants_hi=(0x40, 0x30, 0x5C), pants_lo=(0x14, 0x0C, 0x20),
            boot=(0x28, 0x1A, 0x2A), boot_hi=(0x42, 0x2C, 0x42), boot_lo=(0x12, 0x08, 0x12),
            skin=(0xF6, 0xC0, 0x96), skin_hi=(0xFC, 0xDE, 0xC0), skin_lo=(0xCE, 0x94, 0x6A),
            hair=(0x6A, 0x3A, 0x8E), hair_hi=(0x90, 0x5C, 0xB8),
            metal=(0xF2, 0xCE, 0x58),
        ),
        hat='wizard', hair='long', cape=False, prop='potion', beard=False,
    ),
    'knight': dict(
        name='银骑士',
        c=dict(
            outline=(0x1C, 0x12, 0x20),
            outfit=(0xB9, 0xC6, 0xD8), outfit_hi=(0xEA, 0xF3, 0xFB), outfit_lo=(0x74, 0x84, 0x9A),
            accent=(0xB8, 0x2A, 0x34), accent_hi=(0xE0, 0x4E, 0x50), accent_lo=(0x74, 0x14, 0x20),
            pants=(0x6A, 0x76, 0x8A), pants_hi=(0x8C, 0x9A, 0xAE), pants_lo=(0x44, 0x4E, 0x60),
            boot=(0x3E, 0x3A, 0x40), boot_hi=(0x60, 0x5A, 0x62), boot_lo=(0x22, 0x1E, 0x24),
            skin=(0xF2, 0xAE, 0x78), skin_hi=(0xFA, 0xCE, 0xA2), skin_lo=(0xC8, 0x82, 0x50),
            hair=(0x4A, 0x32, 0x24), hair_hi=(0x6E, 0x4C, 0x34),
            metal=(0xD8, 0xE2, 0xEE),
        ),
        hat='helmet', hair='short', cape=False, prop='sheath', beard=False,
    ),
    'hunter': dict(
        name='森之猎手',
        c=dict(
            outline=(0x14, 0x1E, 0x12),
            outfit=(0x3C, 0x7E, 0x46), outfit_hi=(0x5C, 0xA6, 0x58), outfit_lo=(0x1E, 0x50, 0x2A),
            accent=(0x8A, 0x56, 0x28), accent_hi=(0xB0, 0x76, 0x3C), accent_lo=(0x54, 0x30, 0x12),
            pants=(0x5A, 0x46, 0x2E), pants_hi=(0x7C, 0x62, 0x40), pants_lo=(0x36, 0x28, 0x18),
            boot=(0x46, 0x2E, 0x18), boot_hi=(0x68, 0x46, 0x24), boot_lo=(0x26, 0x16, 0x0A),
            skin=(0xEE, 0xA6, 0x72), skin_hi=(0xF8, 0xC6, 0x9A), skin_lo=(0xC2, 0x7A, 0x48),
            hair=(0xD2, 0x8E, 0x3C), hair_hi=(0xF0, 0xB0, 0x54),
            metal=(0xC8, 0xC8, 0xD0),
        ),
        hat='hood', hair='short', cape=True, prop='bow', beard=False,
    ),
    'alchemist': dict(
        name='炼金术士',
        c=dict(
            outline=(0x1A, 0x1E, 0x2A),
            outfit=(0xE6, 0xE2, 0xD4), outfit_hi=(0xFA, 0xF8, 0xF0), outfit_lo=(0xB0, 0xAC, 0x9E),
            accent=(0x2A, 0x8A, 0x8A), accent_hi=(0x46, 0xB2, 0xAC), accent_lo=(0x14, 0x56, 0x58),
            pants=(0x5A, 0x60, 0x70), pants_hi=(0x7C, 0x84, 0x96), pants_lo=(0x36, 0x3C, 0x48),
            boot=(0x3A, 0x2E, 0x2A), boot_hi=(0x58, 0x48, 0x40), boot_lo=(0x1E, 0x16, 0x14),
            skin=(0xF6, 0xC0, 0x96), skin_hi=(0xFC, 0xDC, 0xBC), skin_lo=(0xCE, 0x94, 0x6A),
            hair=(0x6A, 0x46, 0x8E), hair_hi=(0x8C, 0x64, 0xB4),
            metal=(0xD8, 0xC0, 0x58),   # 护目镜铜圈
        ),
        hat='goggles', hair='long', cape=False, prop='potion', beard=False,
    ),
    'smith': dict(
        name='铁匠',
        c=dict(
            outline=(0x1A, 0x10, 0x0A),
            outfit=(0x9E, 0x9A, 0x92), outfit_hi=(0xC4, 0xC0, 0xB6), outfit_lo=(0x6A, 0x66, 0x5E),
            accent=(0x5E, 0x38, 0x1C), accent_hi=(0x86, 0x54, 0x2C), accent_lo=(0x36, 0x1E, 0x0C),
            pants=(0x4A, 0x3A, 0x2A), pants_hi=(0x6C, 0x56, 0x3E), pants_lo=(0x2A, 0x1E, 0x12),
            boot=(0x3A, 0x24, 0x12), boot_hi=(0x5C, 0x3A, 0x1E), boot_lo=(0x1E, 0x10, 0x06),
            skin=(0xE2, 0x96, 0x60), skin_hi=(0xF4, 0xBA, 0x8C), skin_lo=(0xB4, 0x6A, 0x3C),
            hair=(0x3E, 0x2A, 0x1C), hair_hi=(0x5E, 0x42, 0x2C),
            metal=(0xB0, 0xB6, 0xBE),
        ),
        hat='bald_cap', hair='short', cape=False, prop='none', beard=True,
    ),
}


# ---------------------------------------------------------------- 各部件

def leg_masks(dx, lift, side, top, bot):
    cx = CX + side * 7 + dx
    up = rr(cx - 6, top, cx + 5, bot - 10, 3)
    boot = rr(cx - 7, bot - 11 - lift, cx + 6, bot - 1 - lift, 3)
    sole = ell(cx, bot - 2 - lift, 7, 3)
    return up, (boot | sole)


def arm_masks(dy, side, ty0, sleeve_len=9):
    cx = CX + side * 15
    sleeve = rr(cx - 4, ty0 + 1, cx + 3, ty0 + sleeve_len, 2)
    fore = rr(cx - 3, ty0 + sleeve_len, cx + 2, ty0 + 22 + dy, 2)
    hand = ell(cx - 0.5, ty0 + 23 + dy, 4, 3)
    return sleeve, (fore | hand)


def head_masks(direction):
    if direction == 'down':
        hair = rr(14, 12, 49, 56, 12) | ell(32, 21, 18, 11)
        face = rr(19, 26, 45, 56, 9)
        fringe = (ell(32, 26, 18, 10) | rr(14, 12, 49, 31, 10)) & hair
        return hair, face, fringe
    if direction == 'up':
        return rr(14, 12, 49, 56, 12) | ell(32, 21, 18, 11), None, None
    hair = rr(14, 12, 48, 56, 12)
    face = rr(27, 27, 46, 56, 8)
    return hair, face, None


def draw_head(im, st, direction, c):
    """头 + 头发 + 素颜 + 眼睛。帽饰另外叠（draw_hat）。"""
    hair, face, fringe = head_masks(direction)

    # 长发在背后先铺一层（down 时露在肩两侧，side 时垂到背后）
    if st['hair'] == 'long':
        if direction in ('down', 'up'):
            back = rr(12, 24, 52, 74, 10)
        else:
            back = rr(9, 24, 38, 74, 10)
        paint_mask(im, back, _c(c['hair_hi'] if direction == 'up' else c['hair']))

    paint_mask(im, hair, _c(c['hair']))
    paint_mask(im, hair & ell(24, 20, 14, 10), _c(c['hair_hi']))

    if face is not None:
        paint_mask(im, face, _c(c['skin']))
        paint_mask(im, face & ell(26, 34, 10, 14), _c(c['skin_hi']))
        paint_mask(im, face & ell(42, 52, 8, 8), _c(c['skin_lo']))

    if fringe is not None:
        paint_mask(im, fringe, _c(c['hair']))
        paint_mask(im, fringe & ell(24, 20, 12, 8), _c(c['hair_hi']))

    # 眼睛：正面两只，侧面一只（朝向侧靠右）
    if direction == 'down':
        for ex in (-8, 8):
            paint_mask(im, ell(CX + ex, 40, 4, 5), _c((0xFF, 0xFF, 0xFF)))
            paint_mask(im, ell(CX + ex, 41, 3, 3.5), _c((0x2A, 0x4A, 0x8E)))
            paint_mask(im, ell(CX + ex, 41, 1.5, 2), _c((0x12, 0x18, 0x2C)))
            paint_mask(im, ell(CX + ex - 1, 39, 1, 1), _c((0xFF, 0xFF, 0xFF)))
    elif direction in ('left', 'right'):
        ex = 38
        paint_mask(im, ell(ex, 40, 3, 4), _c((0xFF, 0xFF, 0xFF)))
        paint_mask(im, ell(ex, 41, 2.5, 3), _c((0x2A, 0x4A, 0x8E)))
        paint_mask(im, ell(ex, 41, 1.2, 1.6), _c((0x12, 0x18, 0x2C)))

    # 胡子（铁匠）：下巴一圈深色，正面/侧面才有
    if st.get('beard') and direction in ('down', 'left', 'right'):
        bm = (rr(21, 47, 43, 58, 8) if direction == 'down' else rr(29, 47, 46, 58, 7)) & face
        paint_mask(im, bm, _c(c['hair']))


def draw_hat(im, st, direction, c):
    kind = st['hat']
    if kind == 'helmet':
        # 圆盔 + 护鼻 + 顶部红缨
        dome = rr(13, 7, 50, 33, 13) | ell(32, 17, 18, 12)
        paint_mask(im, dome, _c(c['outfit']))
        paint_mask(im, dome & ell(26, 14, 14, 9), _c(c['outfit_hi']))
        paint_mask(im, dome & ell(42, 32, 14, 10), _c(c['outfit_lo']))
        # 帽檐
        brim = rr(11, 31, 52, 38, 4)
        paint_mask(im, brim, _c(c['outfit_lo']))
        if direction in ('down', 'left', 'right'):
            nose = rr(30, 31, 35, 45, 2)
            paint_mask(im, nose, _c(c['outfit_hi']))
        # 红缨
        plume = ell(32, 7, 7, 10) | rr(29, 3, 35, 11, 2)
        paint_mask(im, plume, _c(c['accent']))
        paint_mask(im, plume & ell(30, 7, 4, 5), _c(c['accent_hi']))
    elif kind == 'hood':
        hood = (rr(11, 8, 52, 60, 16) | ell(32, 20, 21, 15))
        # 兜帽内圈挖空，露出脸
        if direction == 'down':
            hood = hood & ~rr(19, 26, 46, 58, 9)
        elif direction in ('left', 'right'):
            hood = hood & ~rr(28, 28, 47, 58, 8)
        paint_mask(im, hood, _c(c['outfit_lo']))
        paint_mask(im, hood & ell(26, 16, 16, 8), _c(c['outfit']))
        # 尖角拖在脑后
        tip = poly([(44, 26), (58, 44), (46, 46)]) if direction != 'left' else poly([(18, 26), (6, 44), (18, 46)])
        paint_mask(im, tip, _c(c['outfit_lo']))
    elif kind == 'goggles':
        # 额上护目镜：两片铜圈 + 深色镜片 + 皮带
        band = rr(12, 22, 51, 28, 3)
        paint_mask(im, band, _c(c['accent_lo']))
        for gx in ((24, 40) if direction == 'down' else (36,)):
            ring = ell(gx, 26, 9, 8)
            paint_mask(im, ring, _c(c['metal']))
            paint_mask(im, ell(gx, 26, 6, 5), _c((0x3E, 0x7A, 0x86)))
            paint_mask(im, ell(gx - 2, 24, 2, 2), _c((0x9E, 0xD8, 0xE0)))
    elif kind == 'wizard':
        # 宽檐 + 尖顶（略右倾）+ 金色帽带：Q 版女巫的标志性剪影
        # 宽檐压得很低（贴住额头），帽锥窄而高 —— 这两条决定"女巫帽"的剪影
        # 帽檐用暗紫、帽体用亮紫：同色系叠在一起时，只靠明度差才分得清"檐/筒"
        brim = ell(32, 31, 25, 5)
        paint_mask(im, brim, _c(c['accent_lo']))
        cone = poly([(24, 31), (40, 31), (37, 5), (32, 5)])
        paint_mask(im, cone, _c(c['accent']))
        paint_mask(im, cone & ell(29, 21, 5, 12), _c(c['accent_hi']))
        paint_mask(im, cone & ell(37, 22, 5, 11), _c(c['accent_lo']))
        paint_mask(im, rr(32, 3, 37, 7, 2), _c(c['accent_hi']))
        # 帽上的小金星（原版女巫的识别点，比一条金色帽带更像帽子）
        paint_mask(im, ell(34, 4, 3, 3), _c(c['metal']))
        paint_mask(im, ell(27, 26, 2, 2), _c(c['metal']))
        paint_mask(im, ell(38, 18, 1.5, 1.5), _c(c['metal']))
    elif kind == 'bald_cap':
        # 铁匠：旧皮帽（后压）+ 额前汗巾
        cap = rr(12, 11, 51, 30, 11)
        paint_mask(im, cap, _c(c['accent']))
        paint_mask(im, cap & ell(26, 16, 16, 7), _c(c['accent_hi']))
        paint_mask(im, cap & ell(42, 27, 16, 7), _c(c['accent_lo']))
        band = rr(12, 27, 51, 33, 3)
        paint_mask(im, band, _c(c['accent_lo']))
        if direction in ('down', 'left', 'right'):
            knot = ell(14, 30, 4, 4)
            paint_mask(im, knot, _c(c['accent_hi']))


def draw_cape(im, st, direction, c):
    if not st.get('cape'):
        return
    if direction == 'up':
        cape = poly([(16, 46), (48, 46), (56, 104), (8, 104)])
    elif direction == 'down':
        cape = poly([(14, 48), (50, 48), (58, 100), (6, 100)])
    else:
        # 侧面：披风只做「背侧半披」，不向身后拖长 ——
        # 拖尾会让整幅 bbox 偏左 4~5px，角色在场景里看起来站位偏。
        cape = poly([(14, 48), (42, 48), (36, 100), (10, 100)])
    paint_mask(im, cape, _c(c['outfit_lo']))
    paint_mask(im, cape & ell(22, 80, 18, 20), _c(c['outfit']))


def draw_prop_back(im, st, direction, c):
    """拿在身后的道具（弓/箭袋），必须在身体之前画。"""
    if st['prop'] != 'bow':
        return
    if direction == 'up':
        # 背后箭袋 + 三支箭，背影一眼看出是猎手
        quiver = rr(38, 44, 50, 92, 4)
        paint_mask(im, quiver, _c(c['accent']))
        paint_mask(im, rr(38, 44, 41, 92, 3), _c(c['accent_hi']))
        for i, ax in enumerate((39, 43, 47)):
            paint_mask(im, rr(ax - 1, 26 + i, ax + 1, 46, 0), _c((0xC8, 0xA8, 0x78)))
            paint_mask(im, rr(ax - 2, 22 + i, ax + 2, 28 + i, 1), _c(c['accent_lo']))
    elif direction in ('left', 'right'):
        limb = rr(18, 34, 22, 96, 2)
        paint_mask(im, limb, _c(c['accent']))
        paint_mask(im, rr(19, 36, 20, 94, 0), _c((0xE8, 0xE0, 0xC8)))


def draw_prop_front(im, st, direction, c, arm_dy=0):
    """身前的道具：锤子 / 药瓶 / 剑。"""
    prop = st['prop']
    if prop == 'hammer':
        # 锤柄斜插在右手（画面左），锤头朝上
        handle = rr(7, 60, 13, 96, 2)
        paint_mask(im, handle, _c(c['accent']))
        paint_mask(im, rr(7, 60, 9, 96, 2), _c(c['accent_hi']))
        head = rr(0, 47, 20, 63, 4)
        paint_mask(im, head, _c(c['metal']))
        paint_mask(im, rr(0, 47, 20, 52, 3), _c((0xE4, 0xE8, 0xEE)))
        paint_mask(im, rr(0, 59, 20, 63, 3), _c((0x7C, 0x82, 0x8C)))
    elif prop == 'potion':
        body = ell(46, 74, 7, 9)
        paint_mask(im, body, _c((0x6E, 0xD8, 0xC0)))
        paint_mask(im, body & ell(44, 70, 4, 5), _c((0xA8, 0xF0, 0xDE)))
        neck = rr(43, 62, 49, 68, 1)
        paint_mask(im, neck, _c((0xE8, 0xE6, 0xD8)))
        cork = rr(42, 58, 50, 63, 2)
        paint_mask(im, cork, _c(c['accent']))
    elif prop == 'sheath':
        # 背后斜挂的剑鞘：只露鞘口和柄，不抢脸部视线
        if direction == 'up':
            sc = rr(38, 30, 46, 88, 3)
            paint_mask(im, sc, _c(c['accent_lo']))
            paint_mask(im, rr(39, 30, 41, 88, 1), _c(c['accent']))
            grip = rr(38, 20, 46, 30, 2)
            paint_mask(im, grip, _c(c['accent']))
            pommel = ell(42, 19, 4, 3)
            paint_mask(im, pommel, _c(c['metal']))
        elif direction in ('left', 'right'):
            sc = rr(44, 40, 50, 92, 3)
            paint_mask(im, sc, _c(c['accent_lo']))
    elif prop == 'sword' and direction != 'up':
        blade = rr(2, 40, 8, 92, 2)
        paint_mask(im, blade, _c(c['metal']))
        paint_mask(im, rr(2, 40, 4, 92, 1), _c((0xF4, 0xF8, 0xFF)))
        guard = rr(0, 36, 12, 41, 2)
        paint_mask(im, guard, _c(c['accent']))
        grip = rr(3, 28, 7, 36, 1)
        paint_mask(im, grip, _c(c['accent_lo']))


# ---------------------------------------------------------------- 整帧绘制

def draw_character(key, direction, fi=0, idle_bob=False):
    st = STYLES[key]
    c = st['c']
    lx, llift, rx, rlift, bob, ldy, rdy = WALK[fi]
    if idle_bob:
        bob -= 1                     # 呼吸：整体下沉 1px（视觉上更"吸一口气"）

    im = new_canvas(W, H)
    ty0, ty1 = 58 + bob, 86 + bob

    draw_cape(im, st, direction, c)
    draw_prop_back(im, st, direction, c)

    # ---- 腿 + 靴
    lu, lb = leg_masks(lx, llift, -1, 83 + bob, BASE + 1)
    ru, rb = leg_masks(rx, rlift, +1, 83 + bob, BASE + 1)
    for m, k in ((lu, 'pants'), (lb, 'boot'), (ru, 'pants'), (rb, 'boot')):
        paint_mask(im, m, _c(c[k]))
    paint_mask(im, lu & ell(CX - 7 + lx, 88 + bob, 5, 6), _c(c['pants_hi']))
    paint_mask(im, ru & ell(CX + 7 + rx, 88 + bob, 5, 6), _c(c['pants_hi']))
    paint_mask(im, lb & ell(CX - 6 + lx, BASE - 12 - llift, 5, 4), _c(c['boot_hi']))
    paint_mask(im, rb & ell(CX + 6 + rx, BASE - 12 - rlift, 5, 4), _c(c['boot_hi']))

    # ---- 躯干
    body = rr(17, ty0, 46, ty1, 4) if direction in ('down', 'up') else rr(19, ty0, 45, ty1, 4)
    paint_mask(im, body, _c(c['outfit']))
    paint_mask(im, body & ell(26, ty0 + 6, 22, 22), _c(c['outfit_hi']))
    paint_mask(im, body & ell(40, ty1 - 4, 20, 18), _c(c['outfit_lo']))

    # 角色专属的躯干装饰
    if key == 'smith':
        apron = rr(20, ty0 + 8, 43, ty1 + 3, 3)          # 皮围裙
        paint_mask(im, apron, _c(c['accent']))
        paint_mask(im, apron & ell(28, ty0 + 16, 16, 18), _c(c['accent_hi']))
        paint_mask(im, rr(19, ty0 - 1, 24, ty0 + 14, 1), _c(c['accent']))   # 背带
        paint_mask(im, rr(39, ty0 - 1, 44, ty0 + 14, 1), _c(c['accent']))
    elif key == 'knight':
        plate = rr(20, ty0 + 2, 43, ty1 - 1, 3)          # 胸甲
        paint_mask(im, plate, _c(c['outfit_hi']))
        paint_mask(im, plate & ell(40, ty1 - 4, 16, 14), _c(c['outfit_lo']))
        belt = rr(18, ty1 - 4, 45, ty1 + 2, 2)
        paint_mask(im, belt, _c(c['accent']))
        paint_mask(im, ell(32, ty1 - 1, 4, 3), _c(c['metal']))
    elif key == 'hunter':
        strap = rr(20, ty0, 27, ty1 + 2, 2)              # 斜挎皮带
        paint_mask(im, strap, _c(c['accent']))
        belt = rr(18, ty1 - 4, 45, ty1 + 2, 2)
        paint_mask(im, belt, _c(c['accent_lo']))
        paint_mask(im, ell(34, ty1 - 1, 4, 3), _c(c['metal']))
    elif key == 'alchemist':
        trim = rr(20, ty0 + 2, 24, ty1 + 2, 2) | rr(39, ty0 + 2, 43, ty1 + 2, 2)
        paint_mask(im, trim, _c(c['accent']))            # 长袍前襟滚边
        belt = rr(18, ty1 - 3, 45, ty1 + 2, 2)
        paint_mask(im, belt, _c(c['accent_lo']))
        paint_mask(im, ell(32, ty1 - 1, 4, 3), _c(c['metal']))

    # ---- 手臂
    ls, lf = arm_masks(ldy, -1, ty0)
    rs, rf = arm_masks(rdy, +1, ty0)
    if direction in ('down', 'up'):
        for m, k in ((ls, 'outfit'), (lf, 'skin'), (rs, 'outfit'), (rf, 'skin')):
            paint_mask(im, m, _c(c[k]))
    else:
        paint_mask(im, lf, _c(c['skin']))
        paint_mask(im, ls, _c(c['outfit']))
        paint_mask(im, lf & ell(CX - 13, ty0 + 20, 4, 6), _c(c['skin_hi']))

    if key == 'knight':
        for side in (-1, 1):
            sx = CX + side * 17
            pauldron = ell(sx, ty0 + 4, 8, 6)
            paint_mask(im, pauldron, _c(c['outfit_hi']))
            paint_mask(im, pauldron & ell(sx, ty0 + 7, 8, 4), _c(c['outfit_lo']))

    # 护手 / 手套（骑士有铁手套，铁匠有皮护腕）
    if key == 'knight' and direction in ('down', 'up'):
        paint_mask(im, ell(CX - 15.5, ty0 + 23 + ldy, 5, 4), _c(c['metal']))
        paint_mask(im, ell(CX + 15.5, ty0 + 23 + rdy, 5, 4), _c(c['metal']))
    if key == 'smith':
        paint_mask(im, rr(13, ty0 + 18 + ldy, 20, ty0 + 24 + ldy, 2), _c(c['accent']))

    # ---- 头 + 帽
    draw_head(im, st, direction, c)
    draw_hat(im, st, direction, c)

    # ---- 身前道具
    if direction != 'up':
        draw_prop_front(im, st, direction, c, rdy)

    return outline(im, c)


def outline(im, c):
    """整幅 2px 外描边，统一所有素材的边缘厚度。"""
    solid = np.array(im)[:, :, 3] > 0
    out = im.copy()
    paint_mask(out, dilate(solid, 2) & ~solid, _c(c['outline']))
    return align_baseline(out)


def align_baseline(im):
    """把整幅图上/下平移到「最底非透明像素 = BASE」。

    外描边是向外扩 2px 的，直接画的话脚底会落在 BASE+2，
    播放行走时角色会比静态物件高 2px（踩空感）。这里统一收口。
    """
    a = np.array(im)[:, :, 3]
    ys = np.where(a.any(axis=1))[0]
    if len(ys) == 0:
        return im
    dy = BASE - int(ys.max())
    if dy == 0:
        return im
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    out.paste(im, (0, dy))
    return out


# ---------------------------------------------------------------- 铁匠挥锤

def draw_smith_strike(phase):
    """铁匠挥锤的身体姿态（6 帧，面向下方，玩家在铁匠下方）。

    锤子不画在身体帧里 —— 它是一张独立 sprite，运行时挂在手上按角度旋转。
    原因：64px 宽的画布里，锤子举过头顶 / 砸到身前必然越界，硬塞进帧里
    只能缩小动作幅度，看起来像"抖一下"而不是"抡一锤"。

    相位：0 站姿 / 1 抬臂 / 2 过顶蓄力 / 3 下砸命中 / 4 回弹 / 5 收势
    每相位给的是「持锤手的世界像素坐标 + 身体前倾 + 弓步」。
    """
    st = STYLES['smith']
    c = st['c']
    im = new_canvas(W, H)

    # (手x, 手y, 前倾, 弓步)
    PH = [
        (16, 92, 0, 0),    # 站姿：手垂在身侧
        (12, 70, -1, 0),   # 抬臂：往外侧抬，不挡脸
        (10, 44, -2, 0),   # 过顶：手举到头顶左外侧
        (30, 88, 3, 5),    # 下砸：手落到身前下方，同时弓步
        (28, 82, 2, 3),    # 回弹
        (20, 90, 1, 1),    # 收势
    ]
    hx, hy, lean, step = PH[phase]
    bob = lean // 3
    ty0, ty1 = 58 + bob, 86 + bob
    dx = lean * 2

    # ---- 腿：命中帧弓步（左脚前跨）
    lu, lb = leg_masks(-step, 0, -1, 83 + bob, BASE + 1)
    ru, rb = leg_masks(step // 3, 0, +1, 83 + bob, BASE + 1)
    for m, k in ((lu, 'pants'), (lb, 'boot'), (ru, 'pants'), (rb, 'boot')):
        paint_mask(im, m, _c(c[k]))
    paint_mask(im, lu & ell(CX - 7 - step, 88 + bob, 5, 6), _c(c['pants_hi']))
    paint_mask(im, ru & ell(CX + 7, 88 + bob, 5, 6), _c(c['pants_hi']))
    paint_mask(im, lb & ell(CX - 6 - step, BASE - 12, 5, 4), _c(c['boot_hi']))
    paint_mask(im, rb & ell(CX + 6, BASE - 12, 5, 4), _c(c['boot_hi']))

    # ---- 躯干（前倾 = 整体 x 偏移）
    body = rr(17 + dx, ty0, 46 + dx, ty1, 4)
    paint_mask(im, body, _c(c['outfit']))
    paint_mask(im, body & ell(26 + dx, ty0 + 6, 22, 22), _c(c['outfit_hi']))
    paint_mask(im, body & ell(40 + dx, ty1 - 4, 20, 18), _c(c['outfit_lo']))
    apron = rr(20 + dx, ty0 + 8, 43 + dx, ty1 + 3, 3)
    paint_mask(im, apron, _c(c['accent']))
    paint_mask(im, apron & ell(28 + dx, ty0 + 16, 16, 18), _c(c['accent_hi']))

    # ---- 非持锤手（画面右）
    ls, lf = arm_masks(0, +1, ty0, 9)
    paint_mask(im, ls, _c(c['outfit']))
    paint_mask(im, lf, _c(c['skin']))

    # ---- 头（先画头，持锤臂后画：抬手过顶时手在脸前，不能被人头盖掉）
    draw_head(im, st, 'down', c)
    draw_hat(im, st, 'down', c)

    # ---- 持锤臂：肩 → 手，前段是短袖、后段是皮肤，中间偏一点色才有"胳膊"感
    shx, shy = CX - 11 + dx, ty0 + 5
    n = int(max(abs(hx - shx), abs(hy - shy))) + 1
    for i in range(n + 1):
        t = i / float(max(1, n))
        ax = int(round(shx + (hx - shx) * t))
        ay = int(round(shy + (hy - shy) * t))
        col_ = c['outfit'] if t < 0.35 else c['skin']
        paint_mask(im, ell(ax, ay, 3.5, 3.5), _c(col_))
    # 皮护腕 + 手
    paint_mask(im, ell(hx, hy, 4.5, 4.5), _c(c['accent']))
    paint_mask(im, ell(hx, hy, 3.5, 3.5), _c(c['skin']))
    return outline(im, c)


def build_hammer_sprite():
    """铁匠锤（独立 sprite）：32x44，柄尾在底边中点，导入时 pivot 设 (0.5, 0.04)。

    横置锤头 + 中间铁箍 + 木柄，是这个像素比例下最容易读成"锤子"的形状。
    """
    hw, hh = 32, 44
    im = Image.new('RGBA', (hw, hh), (0, 0, 0, 0))
    c = STYLES['smith']['c']
    yy, xx = np.mgrid[0:hh, 0:hw]

    def paint(m, col):
        paint_mask(im, m, _c(col))

    # 木柄：贯穿锤头，下端到手
    handle = (xx >= 14) & (xx <= 18) & (yy >= 8) & (yy <= 42)
    paint(handle, c['accent'])
    paint((xx >= 14) & (xx <= 15) & (yy >= 8) & (yy <= 42), c['accent_hi'])
    paint((xx >= 18) & (xx <= 18) & (yy >= 8) & (yy <= 42), c['accent_lo'])

    # 锤头：横置长方体，上亮下暗
    head = (xx >= 1) & (xx <= 30) & (yy >= 4) & (yy <= 20)
    paint(head, c['metal'])
    paint(head & (yy <= 7), (0xEC, 0xF0, 0xF6))
    paint(head & (yy >= 17), (0x76, 0x7C, 0x86))
    paint(head & ((xx <= 3) | (xx >= 28)), (0x9A, 0xA2, 0xAC))
    # 铁箍（把锤头夹在柄上）
    paint((xx >= 13) & (xx <= 19) & (yy >= 3) & (yy <= 21), c['accent_lo'])
    paint((xx >= 14) & (xx <= 18) & (yy >= 3) & (yy <= 21), c['accent'])

    solid = np.array(im)[:, :, 3] > 0
    out = im.copy()
    paint_mask(out, dilate(solid, 1) & ~solid, _c(c['outline']))
    save(out, 'v2_npc_smith_hammer.png')


# ---------------------------------------------------------------- 输出

def save(im, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    im.save(os.path.join(OUT_DIR, name), optimize=True)


def build_playable(key):
    for d in ('down', 'up', 'left', 'right'):
        # right 一律由 left 镜像得到：两边若各自画，兜帽/道具会朝反方向，
        # 出现"往右走却朝左看"的错帧。
        src = 'left' if d == 'right' else d
        frames = [draw_character(key, src, i) for i in range(6)]
        if d == 'right':
            frames = [f.transpose(Image.FLIP_LEFT_RIGHT) for f in frames]
        for i, im in enumerate(frames, 1):
            save(im, 'v2_walk_%s_%s_%02d.png' % (key, d, i))
    save(draw_character(key, 'down', 0), 'v2_%s_idle.png' % key)
    save(draw_character(key, 'down', 0, idle_bob=True), 'v2_%s_idle_bob.png' % key)


def build_smith():
    # 行走：四方向（铁匠要能走到玩家面前）
    for d in ('down', 'up', 'left', 'right'):
        src = 'left' if d == 'right' else d
        frames = [draw_character('smith', src, i) for i in range(6)]
        if d == 'right':
            frames = [f.transpose(Image.FLIP_LEFT_RIGHT) for f in frames]
        for i, im in enumerate(frames, 1):
            save(im, 'v2_npc_smith_walk_%s_%02d.png' % (d, i))
    save(draw_character('smith', 'down', 0), 'v2_npc_smith_idle_01.png')
    save(draw_character('smith', 'down', 0, idle_bob=True), 'v2_npc_smith_idle_02.png')
    for p in range(6):
        save(draw_smith_strike(p), 'v2_npc_smith_strike_%02d.png' % (p + 1))
    build_hammer_sprite()


def preview(keys, path):
    """拼一张 6 帧 x 4 方向的预览图，方便一次看清所有帧。"""
    cols = 6
    rows = len(keys) * 4
    sheet = Image.new('RGBA', (cols * W + 8, rows * H + 8), (40, 40, 48, 255))
    r = 0
    for k in keys:
        for d in ('down', 'up', 'left', 'right'):
            for i in range(6):
                im = draw_character(k, d, i)
                if d == 'right':
                    im = im.transpose(Image.FLIP_LEFT_RIGHT)
                sheet.paste(im, (4 + i * W, 4 + r * H), im)
            r += 1
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if args:
        for k in args:
            build_smith() if k == 'smith' else build_playable(k)
    else:
        for k in ('witch', 'knight', 'hunter', 'alchemist'):
            build_playable(k)
        build_smith()
        print('已生成：knight / hunter / alchemist / smith -> ' + OUT_DIR)
    if '--preview' in sys.argv:
        preview(['knight', 'hunter', 'alchemist', 'smith'], 'out/cast_preview.png')
        print('预览：out/cast_preview.png')
