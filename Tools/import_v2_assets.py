"""
LuminousSSS — v0.2 美术素材导入脚本

把豆包云盘下载的 14 张素材（黑底合集图 / 白底立绘 / 场景参考图）
处理成可直接被 Unity 使用的 PNG：

  1. 黑底合集图 -> 抠成透明 PNG（原图存档）
  2. 按网格 / 投影切分成独立元素 -> Assets/Art/V2/<分类>/
  3. 白底立绘 -> 抠成透明 PNG
  4. 场景参考图 -> Docs/Refs/v2/

依赖：Pillow、numpy
用法（在工程根目录）：
  python Tools/import_v2_assets.py --src <素材目录> [--preview <预览输出目录>]
"""
import argparse
import os
import json

import numpy as np
from PIL import Image

BLACK_THRESH = 30          # max(R,G,B) <= 该值视为纯黑背景
ALPHA_KEEP = 255

# ---------------------------------------------------------------------------
# 输出尺寸归一化
#   源素材的瓦片是 181x181。统一缩放到 64 px/tile，
#   1080p 下 1 世界单位 ≈ 54 px（相机 size=10 -> 20 单位高），正好接近 1:1，
#   既不浪费显存又保持清晰。对应 Unity 导入 PPU = 64。
# ---------------------------------------------------------------------------
SRC_TILE_PX = 181.0                # 源素材一格瓦片的像素宽
WORLD_SCALE = 64.0 / SRC_TILE_PX   # 世界素材（瓦片/自然/建筑/道具/行走）缩放比
UI_SCALE = 0.5                     # UI 素材：屏幕空间用，独立缩放
SHEET_SCALE = 1024.0 / 2896.0      # 合集原图存档：缩到 1024 宽
REF_SCALE = 0.5                    # 场景参考图：缩到一半
PORTRAIT_MAX_H = 1024              # 立绘最大高度


# --------------------------------------------------------------------------
# 基础工具
# --------------------------------------------------------------------------
def load_rgb(path):
    return Image.open(path).convert("RGB")


def black_to_alpha(img, thresh=BLACK_THRESH):
    """把纯黑背景抠成透明。返回 RGBA。"""
    a = np.asarray(img).astype(np.int16)
    m = a.max(axis=2)
    alpha = np.where(m > thresh, ALPHA_KEEP, 0).astype(np.uint8)
    rgb = a.astype(np.uint8).copy()
    rgb[alpha == 0] = 0                       # 透明区域清成纯黑，避免边缘发灰
    return Image.fromarray(np.dstack([rgb, alpha]), "RGBA")


def white_to_alpha(img, thresh=235):
    """把纯白背景抠成透明（立绘用）。"""
    a = np.asarray(img).astype(np.int16)
    m = a.min(axis=2)
    alpha = np.where(m < thresh, ALPHA_KEEP, 0).astype(np.uint8)
    rgb = a.astype(np.uint8).copy()
    rgb[alpha == 0] = 0
    return Image.fromarray(np.dstack([rgb, alpha]), "RGBA")


def trim(rgba, pad=0):
    """按 alpha 裁掉四周透明边。"""
    alpha = np.asarray(rgba)[:, :, 3]
    ys, xs = np.where(alpha > 0)
    if len(xs) == 0:
        return rgba
    x0, x1 = xs.min(), xs.max() + 1
    y0, y1 = ys.min(), ys.max() + 1
    if pad:
        x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
        x1 = min(rgba.width, x1 + pad); y1 = min(rgba.height, y1 + pad)
    return rgba.crop((int(x0), int(y0), int(x1), int(y1)))


def find_segments(profile, gap=6, thresh=0):
    """在一维投影上找前景段，间距 <= gap 的合并。"""
    on = profile > thresh
    segs, start = [], None
    for i, v in enumerate(on):
        if v and start is None:
            start = i
        elif not v and start is not None:
            segs.append([start, i - 1]); start = None
    if start is not None:
        segs.append([start, len(on) - 1])
    merged = []
    for s in segs:
        if merged and s[0] - merged[-1][1] <= gap:
            merged[-1][1] = s[1]
        else:
            merged.append(s)
    return [(a, b) for a, b in merged]


def alpha_profile(rgba):
    alpha = np.asarray(rgba)[:, :, 3]
    return alpha.sum(axis=0), alpha.sum(axis=1)     # 列投影, 行投影


def split_by_projection(rgba, name_prefix, gap_x=6, gap_y=6,
                        min_w=8, min_h=8, min_area=200, log=None):
    """先用行投影切行，再对每行用列投影切列，返回 [(name, image), ...]"""
    out = []
    _, rowp = alpha_profile(rgba)
    rows = find_segments(rowp, gap=gap_y)
    for ri, (y0, y1) in enumerate(rows):
        band = rgba.crop((0, y0, rgba.width, y1 + 1))
        colp, _ = alpha_profile(band)
        cols = find_segments(colp, gap=gap_x)
        for ci, (x0, x1) in enumerate(cols):
            cell = band.crop((x0, 0, x1 + 1, band.height))
            cell = trim(cell)
            if cell.width < min_w or cell.height < min_h:
                continue
            if cell.width * cell.height < min_area:
                continue
            out.append((f"{name_prefix}_r{ri+1}c{ci+1}", cell))
    if log is not None:
        log.append(f"{name_prefix}: {len(rows)} 行, 共切出 {len(out)} 个元素 "
                   f"(每行列数 {[len(find_segments(alpha_profile(rgba.crop((0,y0,rgba.width,y1+1)))[0], gap=gap_x)) for y0,y1 in rows]})")
    return out


def split_blobs(rgba, prefix, min_area=400, min_w=8, min_h=8,
                merge_dx=6, merge_dy=6, filter_flat=None, log=None):
    """
    连通域切分：找出所有互不相连的前景块，按 行→列 排序输出。

    merge_dx/dy : 两个块的间隙小于该值时按重叠方向合并（处理被切成两半的元素）
    filter_flat : (max_ratio, max_h) 宽扁小元素（如 sprite sheet 里的箭头）过滤掉
    """
    from scipy import ndimage
    alpha = np.asarray(rgba)[:, :, 3] > 0
    lab, n = ndimage.label(alpha, structure=np.ones((3, 3), dtype=int))
    objs = ndimage.find_objects(lab)

    boxes = []
    for i, sl in enumerate(objs):
        if sl is None:
            continue
        h = sl[0].stop - sl[0].start
        w = sl[1].stop - sl[1].start
        area = int((lab[sl] == i + 1).sum())
        if w < min_w or h < min_h or area < min_area:
            continue
        if filter_flat is not None:
            ratio, max_h = filter_flat
            if h <= max_h and w / max(1, h) >= ratio:
                continue
        boxes.append([sl[1].start, sl[0].start, sl[1].stop, sl[0].stop])

    # 合并：x 投影重叠且 y 间隙小 / y 投影重叠且 x 间隙小
    changed = True
    while changed:
        changed = False
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                a, b = boxes[i], boxes[j]
                ox = min(a[2], b[2]) - max(a[0], b[0])
                oy = min(a[3], b[3]) - max(a[1], b[1])
                gap_x = max(a[0], b[0]) - min(a[2], b[2])
                gap_y = max(a[1], b[1]) - min(a[3], b[3])
                if (ox > 0 and -merge_dy <= gap_y <= merge_dy) or \
                   (oy > 0 and -merge_dx <= gap_x <= merge_dx):
                    boxes[i] = [min(a[0], b[0]), min(a[1], b[1]),
                                max(a[2], b[2]), max(a[3], b[3])]
                    boxes.pop(j)
                    changed = True
                    break
            if changed:
                break

    boxes.sort(key=lambda b: (b[1] // 200, b[0]))     # 先按粗行，再按列
    # 精修行分组
    rows = []
    for b in sorted(boxes, key=lambda b: b[1]):
        if rows and b[1] - rows[-1][-1][1] < 150:
            rows[-1].append(b)
        else:
            rows.append([b])

    out = []
    for ri, row in enumerate(rows):
        row.sort(key=lambda b: b[0])
        for ci, b in enumerate(row):
            cell = trim(rgba.crop((b[0], b[1], b[2], b[3])))
            if cell.width < min_w or cell.height < min_h:
                continue
            out.append((f"{prefix}_r{ri+1}c{ci+1}", cell))
    if log is not None:
        log.append(f"{prefix}: 连通域 {n} 个 -> 有效块 {len(boxes)} 个 -> 输出 {len(out)} 个，"
                   f"每行 {[len(r) for r in rows]}")
    return out


def split_grid(rgba, cols, rows, name_fmt, do_trim=False):
    """
    按等分网格切分。
    do_trim=False（瓦片默认）：保持格子原始尺寸 —— 瓦片必须等大且边缘完整才能无缝平铺。
    """
    out = []
    cw = rgba.width / cols
    ch = rgba.height / rows
    for r in range(rows):
        for c in range(cols):
            box = (int(round(c * cw)), int(round(r * ch)),
                   int(round((c + 1) * cw)), int(round((r + 1) * ch)))
            cell = rgba.crop(box)
            if do_trim:
                cell = trim(cell)
            if cell.width < 4 or cell.height < 4:
                continue
            out.append((name_fmt.format(r=r + 1, c=c + 1), cell))
    return out


def dedup(items, tol=0):
    """按像素内容去重，返回 (唯一列表, 重复映射)。"""
    seen = {}
    uniq = []
    dup = {}
    for name, im in items:
        key = (im.width, im.height, im.tobytes().__hash__())
        if key in seen:
            dup[name] = seen[key]
        else:
            seen[key] = name
            uniq.append((name, im))
    return uniq, dup


def save(items, folder):
    os.makedirs(folder, exist_ok=True)
    names = []
    for name, im in items:
        p = os.path.join(folder, name + ".png")
        im.save(p, optimize=True)
        names.append(name)
    return names


def rescale(items, s):
    """整体按比例缩放。"""
    out = []
    for nm, im in items:
        w = max(1, int(round(im.width * s)))
        h = max(1, int(round(im.height * s)))
        out.append((nm, im.resize((w, h), Image.LANCZOS)))
    return out


def fit_height(im, max_h):
    if im.height <= max_h:
        return im
    s = max_h / im.height
    return im.resize((max(1, int(round(im.width * s))), max_h), Image.LANCZOS)


def save_sheet(items, path, cols=None, bg=(28, 28, 32, 255), cell=160, label=True):
    """把切分结果拼成一张总览图，便于肉眼核对。"""
    n = len(items)
    if cols is None:
        cols = int(np.ceil(np.sqrt(n)))
    rows = int(np.ceil(n / cols))
    W, H = cols * cell, rows * (cell + 22)
    canvas = Image.new("RGBA", (W, H), bg)
    from PIL import ImageDraw
    d = ImageDraw.Draw(canvas)
    for i, (name, im) in enumerate(items):
        r, c = divmod(i, cols)
        scale = min(cell / im.width, cell / im.height, 4.0)
        w, h = max(1, int(im.width * scale)), max(1, int(im.height * scale))
        thumb = im.resize((w, h), Image.NEAREST)
        x = c * cell + (cell - w) // 2
        y = r * (cell + 22) + (cell - h) // 2
        canvas.alpha_composite(thumb, (x, y))
        if label:
            d.text((c * cell + 4, r * (cell + 22) + cell + 4), name, fill=(230, 230, 230, 255))
    canvas.convert("RGB").save(path)
    return path


# --------------------------------------------------------------------------
# 元素重命名表（依据预览图人工核对）
# --------------------------------------------------------------------------
NATURE_NAMES = {
    "r1c1": "tree_broadleaf", "r1c2": "tree_pine", "r2c1": "bush", "r2c2": "rock",
    "r2c3": "stump", "r3c1": "log", "r3c2": "grass_tuft", "r4c1": "sign",
    "r4c2": "flower_yellow", "r4c3": "flower_pink", "r4c4": "flower_white",
    "r4c5": "barrel", "r4c6": "crate",
}
BUILDING_NAMES = {
    "r1c1": "barn_red", "r1c2": "coop_blue", "r1c3": "workshop", "r1c4": "house_blue",
    "r2c1": "house_green", "r2c2": "house_red_a", "r2c3": "house_red_b", "r2c4": "shop_striped",
}
ITEM_NAMES = {
    "r1c1": "axe", "r1c2": "pickaxe", "r1c3": "watering_can", "r1c4": "wood_pile",
    "r2c1": "copper_ore", "r3c1": "iron_ore", "r3c2": "carrot", "r3c3": "crop_growth_1",
    "r3c4": "crop_growth_2", "r3c5": "crop_growth_3", "r3c6": "crop_growth_4",
    "r3c7": "tomato", "r3c8": "radish", "r3c9": "coin_gold", "r3c10": "coin_silver",
}
UI_NAMES = {
    "r1c2": "button_wood", "r1c3": "button_selected", "r1c4": "heart",
    "r1c6": "heart", "r1c7": "heart", "r1c8": "heart", "r1c9": "heart",
    "r1c5": "stamina_bar",
    "r2c1": "slot_empty", "r2c2": "slot_item", "r2c3": "icon_heart",
    "r2c4": "icon_bolt", "r2c5": "icon_gear", "r2c6": "icon_help",
}
DIR_NAMES = {1: "down", 2: "up", 3: "left", 4: "right"}


def rename(items, table, prefix):
    out = []
    for nm, im in items:
        key = nm.rsplit("_r", 1)[-1]
        key = "r" + key
        # 兼容 "r1c2" / "r1c2" 形式
        k = key if key in table else None
        if k is None:
            short = "r" + nm.split("_r")[-1]
            k = short if short in table else None
        label = table.get(k, nm)
        out.append((f"{prefix}_{label}", im))
    return out


# --------------------------------------------------------------------------
# 主流程
# --------------------------------------------------------------------------
def run(src, root, preview_dir=None):
    V2 = os.path.join(root, "Assets/Art/V2")
    REFS = os.path.join(root, "Docs/Refs/v2")
    if preview_dir:
        os.makedirs(preview_dir, exist_ok=True)
    log, manifest = [], {}

    def load(n):
        return load_rgb(os.path.join(src, n + ".jpg"))

    # ---------- 1. 黑底合集：抠透明 -> 原尺寸存档 ----------
    sheets = {
        "sheet_tiles": "tile001_basetiles",
        "sheet_nature": "nat001_nature",
        "sheet_buildings": "bld002_buildings",
        "sheet_walk": "chr004_walk",
        "sheet_ui": "ui001_ui",
        "sheet_items": "item001_items",
        "sheet_house_single": "bld001_house",
    }
    sheet_dir = os.path.join(V2, "Sheets")
    os.makedirs(sheet_dir, exist_ok=True)
    sheet_rgba = {}
    for out_name, src_name in sheets.items():
        rgba = black_to_alpha(load(src_name))
        sheet_rgba[src_name] = rgba
        small = rgba.resize((int(round(rgba.width * SHEET_SCALE)),
                             int(round(rgba.height * SHEET_SCALE))), Image.LANCZOS)
        small.save(os.path.join(sheet_dir, out_name + ".png"), optimize=True)
    log.append(f"Sheets: {len(sheets)} 张合集已抠透明并缩到 1024 宽存档 -> Assets/Art/V2/Sheets/")

    # ---------- 2. 瓦片集：16x8 等分 ----------
    tiles_all = split_grid(sheet_rgba["tile001_basetiles"], 16, 8, "tile_r{r:02d}c{c:02d}")
    uniq, dup = dedup(tiles_all)
    tile_dir = os.path.join(V2, "Tiles")
    save(rescale(uniq, WORLD_SCALE), tile_dir)
    manifest["tiles"] = {"total_cells": len(tiles_all), "unique": len(uniq), "duplicates": len(dup)}
    log.append(f"Tiles: 16x8={len(tiles_all)} 格 -> 去重后 {len(uniq)} 个唯一瓦片")
    if preview_dir:
        save_sheet(tiles_all, os.path.join(preview_dir, "prev_tiles_all.png"), cols=16, cell=110)

    # ---------- 3. 自然元素 ----------
    nat = split_blobs(sheet_rgba["nat001_nature"], "nat001_nature", min_area=400)
    nat = rename(nat, NATURE_NAMES, "v2_nature")
    nat, dup_n = dedup(nat)
    save(rescale(nat, WORLD_SCALE), os.path.join(V2, "Nature"))
    manifest["nature"] = len(nat)
    log.append(f"Nature: {len(nat)} 个元素 -> Assets/Art/V2/Nature/")
    if preview_dir:
        save_sheet(nat, os.path.join(preview_dir, "prev_nature.png"), cell=200)

    # ---------- 4. 建筑 ----------
    bld = split_blobs(sheet_rgba["bld002_buildings"], "bld002_buildings", min_area=2000)
    bld = rename(bld, BUILDING_NAMES, "v2_building")
    house = trim(sheet_rgba["bld001_house"])
    bld.append(("v2_building_house_main", house))
    save(rescale(bld, WORLD_SCALE), os.path.join(V2, "Buildings"))
    manifest["buildings"] = len(bld)
    log.append(f"Buildings: {len(bld)} 栋（含单独木屋）-> Assets/Art/V2/Buildings/")
    if preview_dir:
        save_sheet(bld, os.path.join(preview_dir, "prev_buildings.png"), cell=200)

    # ---------- 5. 道具 ----------
    it = split_blobs(sheet_rgba["item001_items"], "item001_items", min_area=800)
    it = rename(it, ITEM_NAMES, "v2_item")
    save(rescale(it, WORLD_SCALE), os.path.join(V2, "Items"))
    manifest["items"] = len(it)
    log.append(f"Items: {len(it)} 个道具 -> Assets/Art/V2/Items/")
    if preview_dir:
        save_sheet(it, os.path.join(preview_dir, "prev_items.png"), cell=200)

    # ---------- 6. UI ----------
    ui = split_blobs(sheet_rgba["ui001_ui"], "ui001_ui", min_area=400)
    big = [im for nm, im in ui if nm.endswith("r1c1")]
    ui = [(nm, im) for nm, im in ui if not nm.endswith("r1c1")]
    extra = []
    if big:
        b = big[0]
        arr = np.asarray(b)[:, :, 3]
        ys = np.where(arr.sum(axis=1) > 0)[0]
        # 标题栏在顶部约 135px 内，对话框占其余
        cut = 135
        top = b.crop((0, 0, b.width, cut))
        top = trim(top)
        panel = b.crop((0, cut, b.width, b.height))
        panel = trim(panel)
        extra = [("v2_ui_title_bar", top), ("v2_ui_dialog_panel", panel)]
    ui = rename(ui, UI_NAMES, "v2_ui")
    ui, _ = dedup(ui)
    ui = ui + extra
    save(rescale(ui, UI_SCALE), os.path.join(V2, "UI"))
    manifest["ui"] = len(ui)
    log.append(f"UI: {len(ui)} 个元素（含拆出的标题栏/对话框）-> Assets/Art/V2/UI/")
    if preview_dir:
        save_sheet(ui, os.path.join(preview_dir, "prev_ui.png"), cell=200)

    # ---------- 7. 行走帧 ----------
    walk = split_blobs(sheet_rgba["chr004_walk"], "chr004_walk",
                       min_area=400, filter_flat=(2.5, 60))
    frames = []
    for nm, im in walk:
        r = int(nm.split("_r")[1].split("c")[0])
        c = int(nm.split("c")[-1])
        frames.append((f"v2_walk_{DIR_NAMES.get(r,'r%d'%r)}_{c:02d}", im))
    save(rescale(frames, WORLD_SCALE), os.path.join(V2, "Characters"))
    manifest["walk_frames"] = len(frames)
    log.append(f"Walk: {len(frames)} 帧（4 方向 x 6 帧，箭头已过滤）-> Assets/Art/V2/Characters/")
    if preview_dir:
        save_sheet(frames, os.path.join(preview_dir, "prev_walk.png"), cols=6, cell=170)

    # ---------- 8. 白底立绘 ----------
    port_dir = os.path.join(V2, "Portraits")
    os.makedirs(port_dir, exist_ok=True)
    for i, (name, f) in enumerate([("farmer", "chr001_farmer"),
                                   ("rain_maiden", "chr002_rain_maiden"),
                                   ("adventurer", "chr003_adventurer")], 1):
        rgba = white_to_alpha(load(f))
        rgba = trim(rgba, pad=4)
        rgba = fit_height(rgba, PORTRAIT_MAX_H)
        rgba.save(os.path.join(port_dir, f"v2_portrait_{i:03d}_{name}.png"), optimize=True)
    log.append("Portraits: 3 张白底立绘已抠透明 -> Assets/Art/V2/Portraits/")

    # ---------- 9. 场景参考图 ----------
    os.makedirs(REFS, exist_ok=True)
    for out_name, src_name in [("scene_01_farm", "scn001_farm_scene"),
                               ("scene_02_town", "scn002_town"),
                               ("scene_03_forest", "scn003_forest"),
                               ("scene_04_river", "scn004_river")]:
        im = load(src_name)
        im = im.resize((int(round(im.width * REF_SCALE)),
                        int(round(im.height * REF_SCALE))), Image.LANCZOS)
        im.save(os.path.join(REFS, out_name + ".jpg"), quality=88, optimize=True)
    log.append("Refs: 4 张场景参考图（缩到一半）-> Docs/Refs/v2/")

    with open(os.path.join(V2, "manifest.json"), "w", encoding="utf-8") as fp:
        json.dump(manifest, fp, ensure_ascii=False, indent=2)
    return log, manifest


def retile_opaque(sheet_jpg, root):
    """
    瓦片专用重切：瓦片是「地面」，本身就该全不透明。
    首版流程对瓦片也做了黑底抠图，暗色像素（如泥土边缘）被误判成背景，
    导致部分瓦片边缘半透明、平铺时露缝。这里直接从原始 jpg 重切，
    不做抠图（alpha=255），等比缩放到 64x64 后覆盖 Tiles/。
    """
    im = load_rgb(sheet_jpg)
    cols, rows = 16, 8
    cw, ch = im.width / cols, im.height / rows
    tile_dir = os.path.join(root, "Assets/Art/V2", "Tiles")
    os.makedirs(tile_dir, exist_ok=True)
    n = 0
    for r in range(rows):
        for c in range(cols):
            box = (int(round(c * cw)), int(round(r * ch)),
                   int(round((c + 1) * cw)), int(round((r + 1) * ch)))
            cell = im.crop(box).convert("RGBA")          # 不透明
            cell = cell.resize((64, 64), Image.LANCZOS)
            cell.save(os.path.join(tile_dir, f"tile_r{r+1:02d}c{c+1:02d}.png"))
            n += 1
    return f"Retile: {n} 张瓦片已按全不透明重切 -> {tile_dir}"


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description="LuminousSSS v0.2 素材导入")
    ap.add_argument("--src",
                    help="下载的素材目录（14 个 jpg，文件名见 Docs/素材导入说明 v0.2.md）")
    ap.add_argument("--retile", default=None, metavar="SHEET_JPG",
                    help="只重切瓦片：给定 tile001 原图 jpg，全不透明覆盖 Tiles/（不跑完整流程）")
    ap.add_argument("--root", default=None, help="工程根目录（默认取脚本上级）")
    ap.add_argument("--preview", default=None, help="预览图输出目录")
    a = ap.parse_args()
    _root = a.root or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if a.retile:
        print(retile_opaque(a.retile, _root))
    elif a.src:
        _log, _man = run(a.src, _root, a.preview)
        print("\n".join(_log))
        print("\n" + json.dumps(_man, ensure_ascii=False, indent=2))
    else:
        ap.error("需要 --src（完整导入）或 --retile（只重切瓦片）")
