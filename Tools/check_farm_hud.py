# -*- coding: utf-8 -*-
"""校验生成的 FarmScene 是否真的带上了移动端输入（Canvas + EventSystem + 虚拟摇杆）。

为什么需要这个脚本：
    「手机端不能移动」的根因是农场场景没有 Canvas / EventSystem，摇杆无处安放。
    这类**结构缺失**在编辑器里点 Play 之前完全看不出来（PC 上有键盘照常能走），
    所以做一道离线门禁：只读场景 YAML，不开编辑器也能判定修没修好。

用法：
    python Tools/check_farm_hud.py
    python Tools/check_farm_hud.py --verify-ui   # 顺带校验摇杆素材

退出码：0 = 全部通过；1 = 有失败项。
前提：场景已用 Tools/LuminousSSS/一键生成（资源 + 场景）重新生成过。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENE = os.path.join(ROOT, 'Assets/Scenes/FarmScene.scene')
LOGIN_SCENE = os.path.join(ROOT, 'Assets/Scenes/LoginScene.scene')
UI_DIR = os.path.join(ROOT, 'Assets/Art/V2/UI')

DOC_RE = re.compile(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n', re.M)
NAME_RE = re.compile(r'^  m_Name: (.+)$', re.M)
GUID_RE = re.compile(r'guid: ([0-9a-f]{32})')

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
        return None, None
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


def script_guids_of(docs, gos, go_name):
    """某个 GameObject 上挂的所有 MonoBehaviour 的脚本 guid。"""
    out = set()
    for fid in gos.get(go_name, []):
        cls, body = docs.get(fid, (None, ''))
        if cls != 114:                    # 114 = MonoBehaviour
            continue
        m = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]{32})', body)
        if m:
            out.add(m.group(1))
    return out


def comp_with(docs, gos, go_name, needle):
    """返回该 GameObject 上第一个正文含 needle 的组件 (fileID, 正文)。"""
    for fid in gos.get(go_name, []):
        cls, body = docs.get(fid, (None, ''))
        if needle in body:
            return fid, body
    return None, None


def main():
    if not os.path.exists(SCENE):
        print(f'[FAIL] 找不到 {os.path.relpath(SCENE, ROOT)}')
        return 1

    docs, gos, go_count = parse_scene(SCENE)
    login_docs, login_gos, _ = parse_scene(LOGIN_SCENE)

    print(f'场景 {os.path.relpath(SCENE, ROOT)}：{go_count} 个 GameObject'
          f'（{len(gos)} 个不重名）\n')

    # ---- 0. 素材 / 脚本 guid 是否齐备 --------------------------------------
    base_guid = guid_of('Assets/Art/V2/UI/v2_ui_joystick_base.png')
    knob_guid = guid_of('Assets/Art/V2/UI/v2_ui_joystick_knob.png')
    joy_guid = guid_of('Assets/Scripts/Controls/VirtualJoystick.cs')
    plc_guid = guid_of('Assets/Scripts/Gameplay/PlayerController.cs')
    if not all((base_guid, knob_guid, joy_guid, plc_guid)):
        miss = [n for n, g in (('摇杆底圈', base_guid), ('摇杆头', knob_guid),
                               ('VirtualJoystick.cs', joy_guid),
                               ('PlayerController.cs', plc_guid)) if not g]
        print(f'[FAIL] 缺 guid（{ "、".join(miss) }）—— 请先在编辑器里打开一次工程，'
              f'让引擎导入新素材与新脚本生成 .meta，再跑一键生成。')
        return 1

    # ---- 1. 结构：五个必需节点 --------------------------------------------
    for name in ('Canvas', 'EventSystem', 'Joystick', 'Base', 'Knob'):
        check(f'存在 GameObject「{name}」', name in gos)

    if 'Joystick' not in gos:
        print('\n场景里没有 Joystick —— 说明场景还是旧的，请重跑 Tools/LuminousSSS/一键生成（资源 + 场景）。')
        return 1

    # ---- 2. Canvas 组件构成与登录场景一致（那套是已上机验证过的）----------
    if login_gos and 'Canvas' in login_gos:
        want = script_guids_of(login_docs, login_gos, 'Canvas')
        got = script_guids_of(docs, gos, 'Canvas')
        missing = want - got
        check('农场 Canvas 的脚本组件与登录场景一致', not missing,
              f'缺 {len(missing)} 个' if missing else f'{len(want)} 个组件')

    # ---- 3. CanvasScaler：1920x1080 / 按高度适配 --------------------------
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

    # ---- 4. Joystick：脚本 + 全透明热区 + 左下角锚定 ----------------------
    check('Joystick 挂了 VirtualJoystick', joy_guid in script_guids_of(docs, gos, 'Joystick'))

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

    # ---- 5. VirtualJoystick 的参数与 knob 引用 ----------------------------
    joy_body = None
    for fid in gos.get('Joystick', []):
        cls, body = docs.get(fid, (None, ''))
        if cls == 114 and f'guid: {joy_guid}' in body:
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

    # ---- 6. 底圈 / 摇杆头：素材引用 + 不参与拾取 --------------------------
    for name, guid in (('Base', base_guid), ('Knob', knob_guid)):
        body = ''.join(docs.get(f, (None, ''))[1] for f in gos.get(name, []))
        check(f'{name} 引用了摇杆素材', guid in body)
        check(f'{name} raycastTarget = 0（不抢触摸）',
              bool(re.search(r'm_RaycastTarget: 0', body)),
              '摇杆视觉层不能挡住热区')

    # ---- 7. 玩家：PlayerController 在位 -----------------------------------
    check('Player 挂了 PlayerController', plc_guid in script_guids_of(docs, gos, 'Player'))

    # ---- 汇总 --------------------------------------------------------------
    bad = [r for r in RESULTS if not r[1]]
    print(f'\n共 {len(RESULTS)} 项，失败 {len(bad)} 项')
    if bad:
        print('结论：FAIL —— ' + '；'.join(r[0] for r in bad))
        return 1
    print('结论：PASS —— 农场场景已具备手机端移动输入（Canvas + EventSystem + 左下角虚拟摇杆）')
    return 0


def rt_of(docs, comps):
    """在组件列表里找 classId=224（RectTransform）的 fileID。"""
    for fid in comps:
        if docs.get(fid, (None, ''))[0] == 224:
            return fid
    return None


if __name__ == '__main__':
    sys.exit(main())
