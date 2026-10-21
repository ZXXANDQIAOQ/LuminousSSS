"""把背包面板的 UI 版式离线合成成一张 PNG（v0.16）。

为什么要这个：本机 GUI 编辑器起不来（进程卡在 [LAYOUT] 就被回收），
没法进游戏截图；而背包面板是运行时用代码铺的，场景里没有它的样子。
所以这里**按 BackpackPanel.cs 里的同一套坐标和同一批素材**在离屏画一遍 ——
坐标、尺寸、九宫格边框都照抄代码，看的虽然不是真实渲染，但版式和实际一致，
能提前发现「按钮压到格子」「文字放不下」这类问题。

代价：只能画静态状态（示例物品、示例选中项），交互与动画仍然要在游戏里看。

运行：python Tools/preview_backpack_ui.py
产物：Docs/preview_v2/backpack_v016.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(ROOT, 'Assets/Art/V2/UI')
ITEMS = os.path.join(ROOT, 'Assets/Art/V2/Items')
OUT = os.path.join(ROOT, 'Docs/preview_v2/backpack_v016.png')

FONT_PATH = 'C:/Windows/Fonts/msyh.ttc'

# ---- 与 BackpackPanel.cs 完全一致的版式常量
PANEL_W, PANEL_H = 1320, 840
SLOT, GAP = 132, 12
COLS, ROWS = 6, 4
GRID_X, GRID_Y = 56, 206
DETAIL_W = 320

INK_DARK = (74, 42, 12, 255)
INK_MID = (126, 92, 52, 255)
BUTTON_TEXT = (255, 243, 208, 255)

# 九宫格边框（照抄 .meta 的 spriteBorder）
BORDERS = {
    'panel_inventory': 20,
    'tooltip_panel': 18,
    'slot_empty': 16,
    'slot_selected': 16,
    'button_wood': 12,
    'currency_plate': 12,
    'slot_item': 14,
}


def load(name, folder=UI):
    return Image.open(os.path.join(folder, name + '.png')).convert('RGBA')


def nine_slice(src, border, size):
    """按九宫格把 src 拉伸到 size（与 Image.Type.Sliced 的表现一致）。"""
    w, h = size
    sw, sh = src.size
    b = border
    sw_c, sh_c = sw - 2 * b, sh - 2 * b
    dw_c, dh_c = max(1, w - 2 * b), max(1, h - 2 * b)

    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))

    def piece(box, dst_size, dst):
        out.paste(src.crop(box).resize(dst_size, Image.NEAREST), dst, src.crop(box).resize(dst_size, Image.NEAREST))

    # 四角
    corners = [
        ((0, 0, b, b), (b, b), (0, 0)),
        ((sw - b, 0, sw, b), (b, b), (w - b, 0)),
        ((0, sh - b, b, sh), (b, b), (0, h - b)),
        ((sw - b, sh - b, sw, sh), (b, b), (w - b, h - b)),
    ]
    # 四边（拉伸）
    edges = [
        ((b, 0, sw - b, b), (dw_c, b), (b, 0)),
        ((b, sh - b, sw - b, sh), (dw_c, b), (b, h - b)),
        ((0, b, b, sh - b), (b, dh_c), (0, b)),
        ((sw - b, b, sw, sh - b), (b, dh_c), (w - b, b)),
    ]
    center = ((b, b, sw - b, sh - b), (dw_c, dh_c), (b, b))

    for box, dst_size, dst in corners + edges + [center]:
        crop = src.crop(box).resize(dst_size, Image.NEAREST)
        out.paste(crop, dst, crop)
    return out


def paste(base, img, xy):
    base.alpha_composite(img, (int(xy[0]), int(xy[1])))


def font(size):
    return ImageFont.truetype(FONT_PATH, size)


def text(draw, xy, s, size, color, anchor='la'):
    draw.text((int(xy[0]), int(xy[1])), s, font=font(size), fill=color, anchor=anchor)


def button(base, draw, xy, size, label, text_size=32):
    w, h = size
    plate = nine_slice(load('v2_ui_button_wood'), BORDERS['button_wood'], (w, h))
    paste(base, plate, xy)
    text(draw, (xy[0] + w / 2, xy[1] + h / 2), label, text_size, BUTTON_TEXT, anchor='mm')


def outlined_text(draw, xy, s, size, color, outline, anchor='mm', width=2):
    """模拟 UI 上的 Outline 组件：先在四周铺一圈深色再写正文。"""
    for dx in (-width, 0, width):
        for dy in (-width, 0, width):
            if dx or dy:
                draw.text((xy[0] + dx, xy[1] + dy), s, font=font(size), fill=outline, anchor=anchor)
    draw.text(xy, s, font=font(size), fill=color, anchor=anchor)


def main():
    # ---- 演示用数据：4 种物品 + 选中萝卜种子（与探针里的场景一致）
    sample = [
        ('v2_item_seed_carrot', 3, False),
        ('v2_item_seed_radish', 2, True),      # 待播种子（左上角金点）
        ('v2_item_carrot', 4, False),
        ('v2_item_fish_gold', 1, False),
    ]

    base = Image.new('RGBA', (PANEL_W, PANEL_H), (0, 0, 0, 0))
    base.alpha_composite(nine_slice(load('v2_ui_panel_inventory'),
                                    BORDERS['panel_inventory'], (PANEL_W, PANEL_H)))
    draw = ImageDraw.Draw(base)

    # ---- 标题
    text(draw, (56, 32 + 35), '背包', 46, INK_DARK, anchor='lm')

    # ---- 关闭按钮（右上角 76x76）
    button(base, draw, (PANEL_W - 34 - 76, 28), (76, 76), '×')

    # ---- 金币条（关闭按钮左侧 240x70）
    plate = nine_slice(load('v2_ui_currency_plate'), BORDERS['currency_plate'], (240, 70))
    plate_x = PANEL_W - 126 - 240
    paste(base, plate, (plate_x, 31))
    coin = load('v2_item_coin_gold', ITEMS).resize((50, 50), Image.NEAREST)
    paste(base, coin, (plate_x + 14, 31 + 10))
    text(draw, (plate_x + 72, 31 + 35), '180', 34, INK_DARK, anchor='lm')

    # ---- 三个页签（全部 / 种子 / 收获）
    for i, label in enumerate(('全部', '种子', '收获')):
        x = 56 + i * (176 + 12)
        button(base, draw, (x, 118), (176, 66), label)
        if i == 0:      # 「全部」选中 → 底部高亮条
            draw.rectangle([x + 16, 118 + 66 - 16, x + 176 - 16, 118 + 66 - 9],
                           fill=(255, 206, 92, 255))

    # ---- 容量（页签行右侧）
    text(draw, (PANEL_W - 56, 118 + 33), '4 / 24 格', 30, INK_MID, anchor='rm')

    # ---- 格子网格
    slot_empty = load('v2_ui_slot_empty')
    slot_item = load('v2_ui_slot_item')
    slot_selected = load('v2_ui_slot_selected')
    for i in range(COLS * ROWS):
        cx = GRID_X + (i % COLS) * (SLOT + GAP)
        cy = GRID_Y + (i // COLS) * (SLOT + GAP)

        if i < len(sample):
            icon_name, count, picked = sample[i]
            # 选中态换底图而不是叠层（v2_ui_slot_selected 不透明，叠上去会盖住图标）
            slot_base = slot_selected if icon_name == 'v2_item_seed_radish' else slot_item
            paste(base, nine_slice(slot_base, BORDERS['slot_item'], (SLOT, SLOT)), (cx, cy))

            icon = load(icon_name, ITEMS)
            scale = 96 / max(icon.size)
            icon = icon.resize((max(1, int(icon.width * scale)), max(1, int(icon.height * scale))),
                               Image.NEAREST)
            paste(base, icon, (cx + (SLOT - icon.width) // 2, cy + (SLOT - icon.height) // 2))

            outlined_text(draw, (cx + SLOT - 12, cy + SLOT - 12), f'×{count}', 26,
                          (255, 246, 222, 255), (40, 22, 6, 255), anchor='rs', width=2)

            if picked:
                outlined_text(draw, (cx + 12, cy + 12), '●', 26, (232, 168, 42, 255),
                              (60, 30, 3, 255), anchor='la', width=2)
        else:
            paste(base, nine_slice(slot_empty, BORDERS['slot_empty'], (SLOT, SLOT)), (cx, cy))

    # ---- 详情面板
    detail_x = PANEL_W - 56 - DETAIL_W
    paste(base, nine_slice(load('v2_ui_tooltip_panel'), BORDERS['tooltip_panel'],
                           (DETAIL_W, GRID_Y + 0 + ROWS * SLOT + (ROWS - 1) * GAP - GRID_Y)),
          (detail_x, GRID_Y))

    inner_x = detail_x + 18 + 6
    inner_w = DETAIL_W - 36 - 12
    y = GRID_Y + 18 + 6

    def_line = 'data'
    icon = load('v2_item_seed_radish', ITEMS)
    icon_big = icon.resize((148, 148), Image.NEAREST) if max(icon.size) >= 148 \
        else icon.resize((148, int(icon.height * 148 / icon.width)), Image.NEAREST)
    paste(base, icon_big, (inner_x + (inner_w - icon_big.width) / 2, y))
    y += 148 + 10

    text(draw, (inner_x + inner_w / 2, y + 23), '萝卜种子', 36, INK_DARK, anchor='mm')
    y += 46 + 10

    text(draw, (inner_x + inner_w / 2, y + 16), '种子 · 25 金币 / 包', 26, INK_MID, anchor='mm')
    y += 32 + 10

    desc = '种在耕地上，约 2 分钟\n成熟，收获 萝卜。\n集市上 萝卜 卖 38 金币。'
    draw.multiline_text((inner_x, y), desc, font=font(24), fill=INK_MID, spacing=8)
    y += 100 + 10

    button(base, draw, (inner_x + (inner_w - 272) / 2, y), (272, 66), '取消待播')
    y += 66 + 10
    button(base, draw, (inner_x + (inner_w - 272) / 2, y), (272, 66), '丢弃 1 个')

    # ---- 深色背景，方便看透明边缘
    bg = Image.new('RGBA', (PANEL_W + 80, PANEL_H + 80), (34, 30, 24, 255))
    bg.alpha_composite(base, (40, 40))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bg.convert('RGB').save(OUT)
    print('生成', os.path.relpath(OUT, ROOT), bg.size)


if __name__ == '__main__':
    main()
