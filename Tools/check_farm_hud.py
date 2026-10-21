# -*- coding: utf-8 -*-
"""离线校验生成的游戏场景是否「结构完整」（不开编辑器，只读场景 YAML）。

覆盖 4 个玩法场景：FarmScene / House1FScene / House2FScene / VillageScene。

为什么需要这个脚本：
    「手机端不能移动」的根因是农场场景没有 Canvas / EventSystem，摇杆无处安放；
    「进屋后一片空白」的根因是场景没生成或传送点漏了。这类**结构缺失**在编辑器里
    点 Play 之前完全看不出来（PC 上有键盘照常能走），所以做一道离线门禁。

三类判据：
    1. 通用 HUD：Canvas + EventSystem + 摇杆三件套 + CanvasScaler 参数 + 玩家控制器；
    2. 传送链路（v0.14）：每个门 / 楼梯都有 ScenePortal，且 targetScene / spawnPointId 正确，
       每个场景都有能接住它的 PlayerSpawnPoint；
    3. 室内专属：地板 Tilemap、家具数量、相机 bounds 与房间尺寸一致；
    4. v0.15 集市与池塘：农场的池塘水面 / 碰撞 / 钓鱼点、土路尽头的集市传送口，
       集市里的两个交易入口（ShopKeeper）与回农场的路、金币 HUD。

用法：
    python Tools/check_farm_hud.py
    python Tools/check_farm_hud.py --farm-only    # 只查农场（旧行为）

退出码：0 = 全部通过；1 = 有失败项。
前提：场景已用 Tools/LuminousSSS/2. 生成游戏场景 重新生成过。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES_DIR = os.path.join(ROOT, 'Assets/Scenes')
FARM = os.path.join(SCENES_DIR, 'FarmScene.scene')
HOUSE1F = os.path.join(SCENES_DIR, 'House1FScene.scene')
HOUSE2F = os.path.join(SCENES_DIR, 'House2FScene.scene')
VILLAGE = os.path.join(SCENES_DIR, 'VillageScene.scene')
LOGIN_SCENE = os.path.join(SCENES_DIR, 'LoginScene.scene')

DOC_RE = re.compile(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n', re.M)
NAME_RE = re.compile(r'^  m_Name: (.+)$', re.M)
# 团结引擎的 meta guid 是 41 字节随机数的 base64（56 字符，末尾 '='），
# 而场景 YAML 里引用的是 32 位 hex —— 两套写法**不相等**，不能直接拿 meta guid 去场景里搜。
# 所以下面的判据一律走「结构 + 字段特征 + 跨场景一致性」，不再比对 guid 字符串本身。
GUID_RE = re.compile(r'guid: ([0-9a-f]{32}|[A-Za-z0-9+/]{55}=)')
SCRIPT_GUID_RE = re.compile(r'm_Script: \{fileID: \d+, guid: ([A-Za-z0-9+/=]+)')
SPRITE_RE = re.compile(r'm_Sprite: \{fileID: -?\d+, guid: ([A-Za-z0-9+/=]+)')

RESULTS = []


def check(label, ok, detail=''):
    RESULTS.append((label, ok, detail))
    print(f'[{"OK " if ok else "FAIL"}] {label}' + (f'  —— {detail}' if detail else ''))


def parse_scene(path):
    """把场景切成 {fileID: (classId, text)}，并返回 GameObject 名 -> 组件 fileID 列表。

    注意：场景里同名 GameObject 很常见（草丛、石头、花都是按素材名命名的，
    会重复十几次），所以同名必须**合并**组件列表，不能后写覆盖前写。
    """
    if not os.path.exists(path):
        return None, None, 0
    text = open(path, encoding='utf-8', errors='ignore').read()
    docs = {}
    count = 0
    for m in DOC_RE.finditer(text):
        cls, fid = int(m.group(1)), int(m.group(2))
        end = text.find('\n--- ', m.end())
        docs[fid] = (cls, text[m.end():end if end != -1 else len(text)])
        if cls == 1:
            count += 1
    gos = {}
    for fid, (cls, body) in docs.items():
        if cls != 1:                      # 1 = GameObject
            continue
        nm = NAME_RE.search(body)
        if not nm:
            continue
        comps = [int(c) for c in re.findall(r'component: \{fileID: (\d+)\}', body)]
        gos.setdefault(nm.group(1).strip(), []).extend(comps)
    return docs, gos, count


def guid_of(asset_rel):
    """读某个资源的 .meta 拿 guid（没有就返回 None）。"""
    p = os.path.join(ROOT, asset_rel + '.meta')
    if not os.path.exists(p):
        return None
    m = GUID_RE.search(open(p, encoding='utf-8', errors='ignore').read())
    return m.group(1) if m else None


def comp_with(docs, gos, go_name, needle):
    """返回该 GameObject 上第一个正文含 needle 的组件 (fileID, 正文)。"""
    for fid in gos.get(go_name, []):
        cls, body = docs.get(fid, (None, ''))
        if needle in body:
            return fid, body
    return None, None


def rt_of(docs, comps):
    """在组件列表里找 classId=224（RectTransform）的 fileID。"""
    for fid in comps:
        if docs.get(fid, (None, ''))[0] == 224:
            return fid
    return None


# ---------------------------------------------------------------- 判据集合

def check_common(docs, gos):
    """三个玩法场景共用的 HUD / 玩家判据。"""
    for name in ('Canvas', 'EventSystem', 'Joystick', 'Base', 'Knob'):
        check(f'存在 GameObject「{name}」', name in gos)

    canvas_comps = [docs.get(f, (None, '')) for f in gos.get('Canvas', [])]
    check('Canvas 有 GraphicRaycaster（否则点不到按钮）',
          any(c == 114 and 'm_BlockingMask' in b for c, b in canvas_comps),
          '看 m_BlockingMask 字段')
    n_mb = len([1 for c, b in canvas_comps if c == 114])
    check('Canvas 挂了 3 个通用组件（Scaler / Raycaster / UIFontFixer）', n_mb >= 3,
          f'实测 {n_mb} 个 MonoBehaviour')

    _, scaler = comp_with(docs, gos, 'Canvas', 'm_MatchWidthOrHeight')
    if scaler:
        res = re.search(r'm_ReferenceResolution: \{x: ([\d.]+), y: ([\d.]+)\}', scaler)
        mw = re.search(r'm_MatchWidthOrHeight: ([\d.]+)', scaler)
        ok = bool(res and res.group(1) == '1920' and res.group(2) == '1080'
                  and mw and float(mw.group(1)) == 1.0)
        check('CanvasScaler 1920x1080 且按高度适配', ok,
              f'{res.group(1)}x{res.group(2)}, match={mw.group(1)}' if res and mw else '读不到参数')
    else:
        check('CanvasScaler 存在', False, 'Canvas 上没找到 CanvasScaler')

    joy_comps = [docs.get(f, (None, '')) for f in gos.get('Joystick', [])]
    check('Joystick 挂了 VirtualJoystick',
          any(c == 114 and 'knobRadius' in b for c, b in joy_comps),
          '看 knobRadius 字段')

    _, img = comp_with(docs, gos, 'Joystick', 'm_RaycastTarget')
    check('Joystick 热区 raycastTarget = 1（否则收不到触摸）',
          bool(img and re.search(r'm_RaycastTarget: 1', img)),
          '透明 Image 必须显式打开射线拾取')

    _, rt = comp_with(docs, gos, 'Joystick', 'm_AnchorMin')
    if rt:
        amin = re.search(r'm_AnchorMin: \{x: ([\d.-]+), y: ([\d.-]+)\}', rt)
        amax = re.search(r'm_AnchorMax: \{x: ([\d.-]+), y: ([\d.-]+)\}', rt)
        ok = bool(amin and amax and amin.group(1) == '0' and amin.group(2) == '0'
                  and amax.group(1) == '0' and amax.group(2) == '0')
        check('Joystick 锚定左下角 (0,0)（各宽高比都贴角）', ok,
              f'min=({amin.group(1)},{amin.group(2)}) max=({amax.group(1)},{amax.group(2)})'
              if amin and amax else '读不到锚点')
    else:
        check('Joystick 的 RectTransform 存在', False)

    joy_body = None
    for fid in gos.get('Joystick', []):
        cls, body = docs.get(fid, (None, ''))
        if cls == 114 and 'knobRadius' in body:
            joy_body = body
            break
    if joy_body:
        km = re.search(r'knob: \{fileID: (\d+)\}', joy_body)
        r = re.search(r'radius: ([\d.]+)', joy_body)
        kr = re.search(r'knobRadius: ([\d.]+)', joy_body)
        knob_rt = rt_of(docs, gos.get('Knob', []))
        check('knob 引用指向 Knob 的 RectTransform',
              bool(km and knob_rt and int(km.group(1)) == knob_rt),
              f'knob.fileID={km.group(1) if km else "无"}，Knob 的 RectTransform={knob_rt}')
        check('radius=144 / knobRadius=64（行程 80 UI 像素）',
              bool(r and kr and float(r.group(1)) == 144.0 and float(kr.group(1)) == 64.0),
              f'radius={r.group(1) if r else "?"}, knobRadius={kr.group(1) if kr else "?"}')
    else:
        check('VirtualJoystick 组件正文可读', False)

    for name in ('Base', 'Knob'):
        body = ''.join(docs.get(f, (None, ''))[1] for f in gos.get(name, []))
        m = SPRITE_RE.search(body)
        check(f'{name} 的 Image 引用了摇杆素材', bool(m),
              f'sprite guid={m.group(1)[:12]}…' if m else 'm_Sprite 为空')
        check(f'{name} raycastTarget = 0（不抢触摸）',
              bool(re.search(r'm_RaycastTarget: 0', body)),
              '摇杆视觉层不能挡住热区')

    plc = [docs.get(f, (None, '')) for f in gos.get('Player', [])]
    check('Player 挂了 PlayerController',
          any(c == 114 and 'moveSpeed' in b for c, b in plc), '看 moveSpeed 字段')
    check('Player 挂了 PlayerInteractor（v0.12 起统一交互）',
          any(c == 114 and 'interactRange' in b for c, b in plc), '看 interactRange 字段')

    # HUD 交互按钮（v0.14 由 ChopButton 改名）——驱动上必带 label 引用（Text）
    act = [docs.get(f, (None, '')) for f in gos.get('InteractButton', [])]
    check('存在 HUD「InteractButton」且挂了 InteractButtonDriver',
          any(c == 114 and re.search(r'^  label: \{fileID: \d+', b, re.M) for c, b in act),
          '看 label 引用')

    # 金币 HUD（v0.15）：集市买卖要能随时看到余额
    coin = [docs.get(f, (None, '')) for f in gos.get('CoinHud', [])]
    check('存在 HUD「CoinHud」且绑定了数字 Text',
          any(c == 114 and re.search(r'^  label: \{fileID: \d+', b, re.M) for c, b in coin),
          '看 label 引用')

    # Missing Script 检查（类名与文件名不一致时会踩）
    orphan = []
    for fid, (cls, body) in docs.items():
        if cls != 114 or SCRIPT_GUID_RE.search(body):
            continue
        go = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
        onm = '?'
        if go and int(go.group(1)) in docs:
            nm = NAME_RE.search(docs[int(go.group(1))][1])
            if nm:
                onm = nm.group(1).strip()
        orphan.append(f'{onm}(fileID {fid})')
    check('场景里没有 Missing Script', not orphan,
          '、'.join(orphan) if orphan else '每个 MonoBehaviour 都带有效 guid')


def check_portal(docs, gos, go_name, want_scene, want_spawn):
    fid, body = comp_with(docs, gos, go_name, 'targetScene')
    if not body:
        check(f'传送点「{go_name}」存在且挂了 ScenePortal', False, '没找到目标物体或缺少 targetScene 字段')
        return
    t = re.search(r'targetScene: (\S*)', body)
    s = re.search(r'spawnPointId: (\S*)', body)
    ok = bool(t and s and t.group(1) == want_scene and s.group(1) == want_spawn)
    check(f'传送点「{go_name}」→ {want_scene}/{want_spawn}', ok,
          f'实测 {t.group(1) if t else "?"}/{s.group(1) if s else "?"}')

    # 触发区必须是 isTrigger，否则会挡路
    go_comps = gos.get(go_name, [])
    box = [docs.get(f, (None, ''))[1] for f in go_comps if docs.get(f, (None, ''))[0] == 61]
    check(f'「{go_name}」的 BoxCollider2D 是 isTrigger（不挡路）',
          bool(box and re.search(r'm_IsTrigger: 1', box[0])),
          f'找到 {len(box)} 个 BoxCollider2D')


def check_spawn(docs, gos, go_name, want_id):
    for fid in gos.get(go_name, []):
        cls, body = docs.get(fid, (None, ''))
        if cls == 114 and 'facing' in body:
            # 注意：不能用 \bid: 匹配 —— 「guid: xxx」里也含 "id: "，
            # 会把 guid 误当成出生点 id。这里锚定行首两空格缩进。
            m = re.search(r'^  id: (\S+)$', body, re.M)
            check(f'出生点「{go_name}」id = {want_id}', bool(m and m.group(1) == want_id),
                  f'实测 {m.group(1) if m else "?"}')
            return
    check(f'出生点「{go_name}」存在且挂了 PlayerSpawnPoint', False, '没找到目标物体')


def check_camera_bounds(docs, gos, want_max):
    _, body = comp_with(docs, gos, 'Main Camera', 'smoothTime')
    if not body:
        check('相机挂了 CameraFollow', False)
        return
    check('相机挂了 CameraFollow', True)
    ub = re.search(r'useBounds: (\d)', body)
    mx = re.search(r'max: \{x: ([\d.-]+), y: ([\d.-]+)\}', body)
    ok = bool(ub and ub.group(1) == '1' and mx
              and abs(float(mx.group(1)) - want_max[0]) < 0.01
              and abs(float(mx.group(2)) - want_max[1]) < 0.01)
    check(f'相机 bounds 锁到房间尺寸 {want_max}', ok,
          f'useBounds={ub.group(1) if ub else "?"}, max=({mx.group(1)},{mx.group(2)})' if mx else '读不到 max')


def check_house_extra(docs, gos, floor):
    # 团结引擎把 Tilemap / Grid 这类组件写成了「字段哈希」classId
    # （Tilemap = 1839735485、Grid = 156049354），不是老 Unity 的 183 —— 两种都认。
    tiles = [1 for cls, _ in docs.values() if cls in (183, 1839735485)]
    check(f'{floor} 有地板 Tilemap', len(tiles) >= 1, f'找到 {len(tiles)} 个 Tilemap')

    # 家具 = 带 SpriteRenderer 的物件（地毯/家具/墙饰）
    sprites = sum(1 for cls, body in docs.values() if cls == 212)   # 212 = SpriteRenderer
    check(f'{floor} 摆了家具（SpriteRenderer ≥ 10）', sprites >= 10, f'实测 {sprites} 个 SpriteRenderer')

    check_camera_bounds(docs, gos, (26.0, 14.0))


def check_farmland(docs, gos):
    """砍树 → 原地变耕地：耕地层在不在、每棵树有没有配齐耕地引用与格子坐标。

    判据来自 TreeChop 的序列化字段：farmland（Tilemap 引用）、tilledTile（Tile 引用）、
    cellX/cellY（种植格坐标）。缺 farmland/tilledTile 时 TreeChop 会静默跳过，
    表现就是「树砍掉了、地面还是原来的土」——正是这次要避免的效果。
    """
    # 1) 耕地层：名为 Farmland 的对象必须挂 Tilemap（团结引擎 classId 1839735485 / 原生 183）
    has_layer = any(docs.get(fid, (None, ''))[0] in (183, 1839735485)
                    for fid in gos.get('Farmland', []))
    check('存在耕地层「Farmland」（Tilemap）', has_layer,
          '砍树后铺耕地用；缺了树会直接消失而地面不变')

    # 2) 每棵可砍树都要配齐：farmland / tilledTile / cellX / cellY
    trees = [n for n in gos if n.startswith('Tree_')]
    bad = []
    for name in trees:
        fid, body = comp_with(docs, gos, name, 'treeId:')
        if fid is None:
            bad.append(name + ' 没挂 TreeChop')
            continue
        if not re.search(r'farmland: \{fileID: [1-9]\d*', body):
            bad.append(name + ' 的 farmland 为空')
        elif not re.search(r'tilledTile: \{fileID: [1-9]\d*', body):
            bad.append(name + ' 的 tilledTile 为空')
        elif not re.search(r'^\s+cellX: [1-9]\d*$', body, re.M) or \
                not re.search(r'^\s+cellY: [1-9]\d*$', body, re.M):
            bad.append(name + ' 的 cellX/cellY 没写入')
    check(f'可砍树 {len(trees)} 棵都配了耕地层 + 耕地瓦片 + 格子坐标',
          len(trees) > 0 and not bad,
          ('缺项：' + '；'.join(bad[:4])) if bad else f'实测 {len(trees)} 棵')


def check_pond(docs, gos):
    """农场池塘（v0.15）：水面图、挡水的碰撞盒、岸边钓鱼点。

    三样缺一不可：
      水面图缺失 → 那里只有草地，玩家找不到池塘；
      碰撞盒缺失 → 玩家能走进水里（视觉穿帮，而且能走到池塘中央）；
      FishingSpot 缺失 → 走得到水边但按不出「钓鱼」。
    """
    check('农场有池塘水面「v2_nature_pond」', 'v2_nature_pond' in gos)

    # 水面碰撞：PondWater 上必须有 BoxCollider2D 且**不是** trigger（要真挡人）
    box = [docs.get(f, (None, ''))[1] for f in gos.get('PondWater', [])
           if docs.get(f, (None, ''))[0] == 61]
    solid = bool(box and re.search(r'm_IsTrigger: 0', box[0]))
    check('池塘水面有实心碰撞（走不进水里）', solid,
          f'找到 {len(box)} 个 BoxCollider2D' if box else '没有 PondWater 的碰撞盒')

    # 钓鱼点：BoxCollider2D 必须是 trigger（只检测不挡路）+ FishingSpot 字段齐全
    fbox = [docs.get(f, (None, ''))[1] for f in gos.get('FishingSpot', [])
            if docs.get(f, (None, ''))[0] == 61]
    check('钓鱼点的碰撞盒是 isTrigger（不挡路）',
          bool(fbox and re.search(r'm_IsTrigger: 1', fbox[0])),
          f'找到 {len(fbox)} 个 BoxCollider2D')

    _, body = comp_with(docs, gos, 'FishingSpot', 'biteWindow')
    if body:
        has_fish = 'fish_blue' in body
        win = re.search(r'biteWindow: ([\d.]+)', body)
        check('FishingSpot 配了鱼种与咬钩窗口', has_fish and bool(win),
              f'fishIds 含 fish_blue={has_fish}, biteWindow={win.group(1) if win else "?"}')
    else:
        check('钓鱼点挂了 FishingSpot', False, '没找到 biteWindow 字段')


def check_village_extra(docs, gos):
    """集市专属：两个交易入口、摊位/水井/路灯、相机 bounds。"""
    check_camera_bounds(docs, gos, (34.0, 20.0))

    for name in ('ShopKeeper_Shop', 'ShopKeeper_Stall'):
        _, body = comp_with(docs, gos, name, 'shopTitle')
        if not body:
            check(f'交易入口「{name}」存在且挂了 ShopKeeper', False, '没找到目标物体')
            continue
        title = re.search(r'shopTitle: (.*)', body)
        trig = [docs.get(f, (None, ''))[1] for f in gos.get(name, [])
                if docs.get(f, (None, ''))[0] == 61]
        check(f'交易入口「{name}」配了店名且是 isTrigger',
              bool(title and title.group(1).strip() and trig and re.search(r'm_IsTrigger: 1', trig[0])),
              f'shopTitle={title.group(1).strip() if title else "?"}')

    for name in ('v2_build_stall', 'v2_build_well', 'v2_build_lamp', 'v2_build_fence'):
        check(f'集市摆了「{name}」', name in gos)

    # 广场地面：至少要有 Tilemap（草地 + 沙地两张，或至少一张）
    tiles = [1 for cls, _ in docs.values() if cls in (183, 1839735485)]
    check('集市有地面 Tilemap', len(tiles) >= 1, f'找到 {len(tiles)} 个 Tilemap')

    sprites = sum(1 for cls, _ in docs.values() if cls == 212)
    check('集市摆了建筑与道具（SpriteRenderer ≥ 20）', sprites >= 20, f'实测 {sprites} 个 SpriteRenderer')


def main():
    farm_only = '--farm-only' in sys.argv

    need_meta = ('Assets/Art/V2/UI/v2_ui_joystick_base.png',
                 'Assets/Art/V2/UI/v2_ui_joystick_knob.png',
                 'Assets/Art/V2/Interior/v2_in_floor_wood_a.png',
                 'Assets/Art/V2/Tiles/fill_tilled_a.png',
                 'Assets/Scripts/Controls/VirtualJoystick.cs',
                 'Assets/Scripts/Gameplay/PlayerController.cs',
                 'Assets/Scripts/Gameplay/CameraFollow.cs',
                 'Assets/Scripts/Gameplay/ScenePortal.cs',
                 'Assets/Scripts/Core/SceneFlow.cs',
                 'Assets/Art/V2/Nature/v2_nature_pond.png',
                 'Assets/Art/V2/Village/v2_build_stall.png',
                 'Assets/Art/V2/Village/v2_build_well.png',
                 'Assets/Art/V2/Village/v2_build_lamp.png',
                 'Assets/Resources/ItemDB.asset',
                 'Assets/Resources/UiTheme.asset',
                 'Assets/Scripts/Core/Inventory.cs',
                 'Assets/Scripts/Data/ItemDatabase.cs',
                 'Assets/Scripts/Gameplay/ShopKeeper.cs',
                 'Assets/Scripts/Gameplay/FishingSpot.cs',
                 'Assets/Scripts/Gameplay/PlantSpot.cs',
                 'Assets/Scripts/UI/ShopPanel.cs',
                 'Assets/Scripts/UI/CoinHud.cs')
    miss = [p for p in need_meta if not guid_of(p)]
    if miss:
        print('[FAIL] 缺 .meta：' + '、'.join(os.path.basename(p) for p in miss)
              + ' —— 请先在编辑器里打开一次工程，让引擎导入新素材与新脚本，再跑生成菜单。')
        return 1

    targets = [(FARM, '农场')]
    if not farm_only:
        targets += [(HOUSE1F, '小洋楼 1F'), (HOUSE2F, '小洋楼 2F'), (VILLAGE, '村口集市')]

    for path, label in targets:
        if not os.path.exists(path):
            print(f'[FAIL] 找不到 {os.path.relpath(path, ROOT)}（{label}）')
            return 1

        RESULTS.clear()
        docs, gos, go_count = parse_scene(path)
        print(f'\n===== {label}：{os.path.relpath(path, ROOT)}'
              f'（{go_count} 个 GameObject / {len(gos)} 个不重名）=====')
        check_common(docs, gos)

        if path == FARM:
            check_portal(docs, gos, 'DoorToHouse', 'House1FScene', 'Default')
            check_spawn(docs, gos, 'Spawn_Door', 'Door')
            check_camera_bounds(docs, gos, (40.0, 24.0))
            check_farmland(docs, gos)
            check_pond(docs, gos)
            # v0.15：土路尽头的集市传送口 + 回农场的落点
            check_portal(docs, gos, 'RoadToVillage', 'VillageScene', 'Road')
            check_spawn(docs, gos, 'Spawn_Village', 'Village')
        elif path == VILLAGE:
            check_portal(docs, gos, 'RoadToFarm', 'FarmScene', 'Village')
            check_spawn(docs, gos, 'Spawn_Road', 'Road')
            check_village_extra(docs, gos)
        elif path == HOUSE1F:
            check_portal(docs, gos, 'DoorToFarm', 'FarmScene', 'Door')
            check_portal(docs, gos, 'StairsTo2F', 'House2FScene', 'Default')
            check_spawn(docs, gos, 'Spawn_Default', 'Default')
            check_spawn(docs, gos, 'Spawn_Stairs', 'Stairs')
            check_house_extra(docs, gos, '1F')
        else:
            check_portal(docs, gos, 'StairsTo1F', 'House1FScene', 'Stairs')
            check_spawn(docs, gos, 'Spawn_Default', 'Default')
            check_house_extra(docs, gos, '2F')

        bad = [r for r in RESULTS if not r[1]]
        print(f'—— {label}：{len(RESULTS)} 项，失败 {len(bad)} 项')
        if bad:
            print(f'结论：FAIL —— ' + '；'.join(r[0] for r in bad))
            return 1

    print('\n结论：PASS —— 农场 / 两层小洋楼 / 集市的 HUD、传送链路、池塘与交易入口全部就位')
    return 0


if __name__ == '__main__':
    sys.exit(main())
