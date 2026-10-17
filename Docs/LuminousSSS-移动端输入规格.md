# LuminousSSS — 移动端移动输入规格（v0.11）

> 起因：安卓包在手机上**完全无法移动**。本文记录根因、修复方案、参数表与可复跑的验证方式。
> 相关文件：`Assets/Scripts/Controls/{MoveInput,VirtualJoystick}.cs`、
> `Assets/Scripts/Gameplay/PlayerController.cs`、`Assets/Editor/GameSceneBuilder.cs`、
> `Tools/gen_ui_joystick.py`、`Tools/check_farm_hud.py`。

---

## 0. 结论

| | |
|---|---|
| **根因** | `FarmScene` 从建立起**就没有 Canvas、也没有 EventSystem**，而移动只读 `Input.GetAxisRaw("Horizontal"/"Vertical")` —— 键盘轴。手机既没键盘也没 EventSystem，等于零输入。 |
| **修复** | ① 农场场景新增 HUD Canvas + EventSystem + 左下角虚拟摇杆；② 输入走 `MoveInput` 汇聚层，键盘与摇杆二选一取幅度大的那路。 |
| **兼容性** | 键盘 / 手柄手感与改之前**逐位一致**；PC 与编辑器照旧。 |
| **验证** | 编译 0 错 0 警；`Tools/check_farm_hud.py` 离线校验场景结构（不用开编辑器）。 |

---

## 1. 问题定位

| 检查项 | 结果 |
|---|---|
| `PlayerController.Update()` | 只读 `Input.GetAxisRaw("Horizontal"/"Vertical")`，没有任何触摸分支 |
| 全工程 `Input.` 调用点 | 仅 `PlayerController.cs` 两行 |
| 全工程 `Touch` / `joystick` 相关代码 | **零** |
| `BuildFarmScene()` 是否建 Canvas | **否**（只有相机 / Tilemap / 物件 / 边界 / 玩家） |
| `FarmScene.scene` 实际节点 | `Main Camera、Grid、Ground、Props、Bounds、Player、Visual、AudioManager` + 25 个布景 —— **无 Canvas / EventSystem** |
| 摇杆素材 | `Assets/Art/V2/UI/` 下**无任何摇杆 / 方向键素材** |

登录场景（`LoginScene`）是**有** Canvas + EventSystem 的，所以手机上的登录、选角、设置按钮一直正常
—— 这也解释了为什么「能进游戏，但进去就动不了」。

---

## 2. 输入架构

```
       键盘 / 手柄                         屏幕虚拟摇杆
  Input.GetAxisRaw(...)              VirtualJoystick（指针事件）
            │                                    │
            └──────────► MoveInput ◄─────────────┘
                    （静态汇聚层）
                            │
                    MoveInput.Read()
                            │
                    PlayerController
                    （移动 / 记录朝向）
                            │
                   Rigidbody2D  +  CharacterAnimDriver
```

**为什么加一层 `MoveInput`**：`PlayerController` 不需要知道输入来自什么设备。
以后换 D-Pad、接外接手柄、加「点击地面寻路」，只要写同一个入口，移动逻辑一行都不用改。

**为什么是「取幅度大的一路」而不是相加**：
相加会让斜向顶到底的模长到 1.414，归一化后单轴分量从 1 掉到 0.71 —— 斜着走反而比直着走慢。
取大值则键盘与摇杆都稳定输出单位向量，手感一致。

**为什么 `VirtualJoystick.OnDisable` 必须清零**：切场景 / 摇杆被禁用时如果不清，
残留的 `VirtualAxis` 会让角色一进新场景就自己往某个方向走。这是触屏项目最常见的「鬼走路」来源。

---

## 3. 摇杆参数表

Canvas 规范与登录界面完全一致：`ScreenSpaceOverlay` + `1920×1080` + `matchWidthOrHeight = 1.0`（**按高度适配**）。
因为按高度适配，摇杆相对屏高的比例在 16:9 / 19.5:9 / 21:9 / 4:3 下**恒定**，且锚定左下角 `(0,0)`，永远不会跑位。

| 项 | 值（UI 像素） | 说明 |
|---|---|---|
| 锚点 | `min = max = (0,0)` | 左下角，各宽高比都贴角 |
| 距屏幕边距 | 40 | 左 / 下各留 40 |
| 触摸热区边长 | 480 | 隐形全透明 `Image`，比底圈大一圈，手指不用瞄准圆环 |
| 底圈显示尺寸 | 288 | 与素材 1:1，半径 144 |
| 摇杆头显示尺寸 | 128 | 与素材 1:1，半径 64 |
| 可用行程 | **80** | `radius - knobRadius = 144 - 64` |
| 死区 | 0.18 | 推动幅度 < 18% 视为不动，防手指微抖就走起来 |
| 输出范围 | `[-1, 1]` | 过死区后线性重映射，跨过死区从 0 平滑起速 |

**体量核对**（以 6.1" 19.5:9 手机、屏高约 14.6 cm 计，1 UI 像素 = 14.6/1080 cm）：

| | UI 像素 | 实物 | 判断 |
|---|---|---|---|
| 底圈直径 | 288 | ≈ 3.9 cm | ✅ 与主流手游摇杆相当（星露谷 / Minecraft PE 约 3~4 cm） |
| 摇杆头直径 | 128 | ≈ 1.7 cm | ✅ 大于拇指指腹 |
| 触摸热区边长 | 480 | ≈ 6.5 cm | ✅ 远大于 `UI规格 §2.4` 要求的 88×88 触屏下限 |

**位置安全性**：底圈占据 y ∈ [136, 424]（自屏幕底算起）。安卓手势条 / 虚拟导航栏一般占底部 ~50 UI 像素，
底圈整体在其之上，不会被系统手势抢走触摸。

---

## 4. 素材规范

| 文件 | 尺寸 | 形态 | 说明 |
|---|---|---|---|
| `v2_ui_joystick_base.png` | 288×288 | **空心环**（环宽 18） | 中心镂空，尽量少遮农场画面 |
| `v2_ui_joystick_knob.png` | 128×128 | 实心木纹圆盘 | 中心奶油点，左上 45° 高光 |

- 由 `Tools/gen_ui_joystick.py` 程序化生成，配色全部取自 `Tools/luminous_pixel.py` 的木纹族
  （`wood / wood_hi / wood_dark / frame / outline / cream`），与 `v2_ui_button_wood` 同源，天然像同一套 UI。
- 为什么底圈是**空心**而不是半透明：项目硬边规范禁止半透明（`luminous_pixel.py` 顶部第 1 条：
  alpha 只有 0 或 255），而实心圆盘在 1080 高的屏上会遮掉一大块农场 —— 空心是唯一既能看清又不挡视野的做法。
- 放在 `Assets/Art/V2/` 下，由 `PixelArtImporter` 自动按 `PPU = 64` / Point 过滤 / 无压缩导入，无需额外配置。
- 环上四个方向的奶油色刻度是刻意的：像素素材没有渐变，纯木环不容易一眼看出「这是个可推动的方向盘」。

预览：
- `Docs/preview_joystick.png` —— 摇杆三态对照（居中 / 右上极限 / 左下极限）
- `Docs/preview_joystick_layout.png` —— **落位示意**：按 `BuildFarmHud` 的真实参数摆到 1920×1080 画布左下角，
  红色虚线是 480×480 触摸热区，底部蓝色带是安卓手势条 / 导航栏的大致占用区。
  这张图可以直接判断位置与大小是否合适，不用开编辑器。

> ⚠️ `Tools/gen_ui_joystick.py` 顶部的 `AREA / BASE / KNOB / INSET` 与
> `GameSceneBuilder.BuildFarmHud()` 里的同名常量是**一份数据的两个副本**，改一边必须同步改另一边
> （已用 `Tools/check_farm_hud.py` 校验场景侧的实际取值）。

---

## 5. 生成与验证

### 5.1 出素材

```bash
python Tools/gen_ui_joystick.py            # 生成 + 自检
python Tools/gen_ui_joystick.py --verify   # 只校验，不写文件
```

自检项：尺寸、硬边（半透明像素数必须为 0）、几何探针（底圈圆心必须透明、环体中点必须不透明）。

### 5.2 重建场景

编辑器菜单：`Tools → LuminousSSS → 一键生成（资源 + 场景）`

> ⚠️ 场景**必须重建**。`BuildFarmHud()` 只在生成时执行，已存在的旧 FarmScene 不会自动补 Canvas。
> 跑之前先 `File → Save` 或直接让弹窗保存，别点 Cancel —— `BuildAll()` 第一步就是
> `SaveCurrentModifiedScenesIfUserWantsTo()`，点了 Cancel 会**整个中止**（只重导了素材、场景没重建）。

### 5.3 离线校验场景结构（不开编辑器）

```bash
python Tools/check_farm_hud.py
```

退出码 0 = 通过。校验 15 项，覆盖：五个必需节点是否存在、Canvas 组件构成是否与登录场景一致、
CanvasScaler 是否为 1920×1080 / 按高度适配、热区 `raycastTarget` 是否为 1、是否锚定左下角、
`VirtualJoystick` 的 `knob` 引用是否指向 Knob 的 RectTransform、`radius/knobRadius` 是否为 144/64、
底圈与摇杆头是否引用了正确素材且 `raycastTarget = 0`、`Player` 是否挂了 `PlayerController`。

### 5.4 运行期验证

| 环境 | 操作 | 预期 |
|---|---|---|
| 编辑器（不开手机） | 点 Play，**用鼠标在左下角圆环里按下并拖动** | 摇杆头跟着鼠标走，小人朝对应方向移动（`StandaloneInputModule` 对鼠标与触摸一视同仁） |
| 编辑器 | 松开鼠标 | 摇杆头弹回圆心，小人停下 |
| 编辑器 | 按 WASD / 方向键 | 与改之前完全一致 |
| 手机 | 单指按住左下角摇杆拖动 | 小人连续移动，四方向朝向切换正确 |
| 手机 | 第二根手指同时点屏幕别处 | 不影响摇杆（只跟第一根按下的手指） |

---

## 6. 待确认

| 项 | 现状 | 待定 |
|---|---|---|
| 摇杆在 PC 上是否显示 | **显示**（`hideOnNonTouchDevice = false`），便于编辑器里用鼠标验证 | 若要 PC 版纯净，在 Prefab / 场景里把该勾打开即可隐藏，键盘不受影响 |
| 摇杆位置是否固定 | 固定圆心（左下角） | 若想要「手指按哪、圆心就在哪」的动态摇杆，属于手感调优，可后续做 |
| 刘海 / 挖孔屏安全区 | **未处理**（当前靠 `40` 边距 + 底圈位置规避） | 若目标机型有侧边挖孔压住左下角，需要加 `Screen.safeArea` 适配 |
| 是否需要「交互键 / 挥砍键」 | 本期无（农场场景暂无交互动作） | 等播种 / 采集实装时再按 `UI规格 §2.4` 的 88×88 下限补按钮 |
