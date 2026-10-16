# LuminousSSS 落地清单（本轮新增素材）

> 目标引擎：**团结引擎 2022.3（UGUI）**｜落盘位置：`Assets/Art/V2/`
> 配套：`LuminousSSS-UI规格.md`（尺寸与参数）、`LuminousSSS-图标家族表.md`（图标规范）
> 引擎版本差异提示：本文菜单路径基于团结引擎 2022.3，其他版本若路径不同请按你当前版本核验。

---

## 0. 结论先行

1. **50 张素材全部为「整张 + 九宫格」两类**，没有需要手工二次切分的图 —— 不必进 Sprite Editor 逐张切。
2. **8 张需要写 `Sprite Border`**（`switch_on/off` 固定尺寸，不做九宫格），已在 §3 给出四值与可直接粘贴的 C# 补表代码。
3. **图集分 3 包**（UI / 道具 / 特效），UI 包必须与场景包分开 —— 界面常驻显存，跟瓦片混包会一起被换出。

---

## 1. 切图规格

| 类型 | 素材 | 处理 |
|---|---|---|
| **九宫格（Sliced）** | `panel_inventory` `tooltip_panel` `dialog_bubble` `currency_plate` `slot_empty` `slot_selected` `stamina_bg` `stamina_fill` | 设 `Sprite Border` 四值 → `Image.Type = Sliced`。**不要为每种宽度画一张图** |
| **整张（Simple）** | `heart_full/half/empty` `switch_on/off` `bubble_tail` `portrait_frame` `load_01..06` `transition`、全部 `v2_item_*`、全部 `v2_fx_*` | `Image.Type = Simple` |
| **序列帧（Simple + 逐帧轮播）** | `load_01..06`（8fps）、`fx_spark_01..04`、`fx_dust_01..03`、`fx_leaf_01..04`、`fx_splash_01..03` | 序列帧按**等分画布**绘制，帧间几何对齐（`fx_*` 画布分别为 48/32/32/48，同类帧画布一致） |
| **平铺（Tiled）** | `transition` | `Image.Type = Tiled` 或直接铺满全屏 |

### 1.1 九宫格边界算法（本轮统一规则）

边界值 = **圆角半径 + 木框/描边厚度**，再留 2–4px 余量。计算式：

```
border = max(圆角半径, 木框厚度 + 2, 铆钉最远距离) + 余量(2~4)
```

举例 `panel_inventory`：圆角 14、木框 `1..12`、铆钉中心 `5` 半径 3 → 最远 14 → 取 **20**（余量 6，拉伸更稳）。

---

## 2. 图集分组规划

装箱余量系数 1.3，尺寸向上取 2 的幂，边长上限按 2048（多引擎同发取最小值）：

| 图集名 | 内容 | 数量 | 面积(px) | 建议尺寸 | 状态 |
|---|---|---|---|---|---|
| `atlas_ui` | `Assets/Art/V2/UI/` 中**最大边 ≤512px** 的小件 | 34 | 412,751 | **1024×1024** | ✅ 新增 |
| `atlas_item` | `Assets/Art/V2/Items/` 全部 | 13 | 212,992 | **1024×1024** | ✅ 新增（含现有 15 张归一后） |
| `atlas_fx` | `Assets/Art/V2/FX/` 全部 | 15 | 27,392 | **256×256** | ✅ 新增 |
| `atlas_tiles` | `Assets/Art/V2/Tiles/` | 133 | ~546,000 | 1024×1024 | 建议，现有未打包 |
| `atlas_nature` | `Assets/Art/V2/Nature/` | 13 | 194,659 | 512×512 | 建议 |
| `atlas_building` | `Assets/Art/V2/Buildings/` | 9 | 697,244 | 1024×1024 | 建议 |
| `atlas_portrait` | `Assets/Art/V2/Portraits/` | 3 | 1,521,000+ | 需单独评估 | ⚠️ 立绘 1024 高，放图集会撑爆，**建议保持散图** |

**关键取舍**：

- **UI 包与场景包分离**：UI 常驻，场景包按场景加载/卸载。混包会导致切场景时 UI 一起被卸载。
- **特效包小（256×256）**：粒子/序列帧高频播放，小包命中率高。
- **立绘不进图集**：3 张 1024 高的图会让图集超 2048，且立绘只在登录界面用一次，散图更合适。
- **UI 大图不进图集**（⚠️ 这条是本轮实测修正的）：`Assets/Art/V2/UI/` 里混着 4 张超大图 ——
  `v2_ui_login_bg` 1920×1080、`v2_ui_dialog_panel_tall` 1411×1411、`v2_ui_dialog_panel` 1411×372、
  `v2_ui_title_bar` 1402×68（已弃用）。**只要把「整个 UI 目录」当一包，图集就会被迫涨到 4096×4096**，
  超过 2048 上限。正确做法是按**最大边 >512px** 把它们剔出去单独散图，其余 34 张小件打一包（1024×1024，实测面积 412,751px）。

---

## 3. 团结引擎配置

### 3.1 贴图导入设置

| 项 | 值 | 位置 |
|---|---|---|
| Texture Type | Sprite (2D and UI) | 贴图导入设置 |
| Sprite Mode | Single | 同上 |
| Pixels Per Unit | **64** | 同上 |
| Filter Mode | **Point (no filter)** | 同上 |
| Compression | **None** | 同上 |
| Generate Mip Maps | 关 | 同上 |
| Alpha Is Transparency | 开 | 同上 |
| Sprite Border | 见 §3.2 | 同上 |
| Mesh Type | Full Rect | 同上（九宫格必须 Full Rect） |

### 3.2 `SpriteBorders` 补表（`Assets/Editor/PixelArtImporter.cs`）

现有表里只有 4 条，本轮需补 8 条（`switch` 不做九宫格）。**注意字段顺序是 左 / 下 / 右 / 上**：

```csharp
// 键 = 文件名（不含扩展名）。Vector4 顺序：(left, bottom, right, top)
private static readonly Dictionary<string, Vector4> SpriteBorders =
    new Dictionary<string, Vector4>
{
    // ---- 已有 ----
    { "v2_ui_button_wood",      new Vector4(12, 12, 12, 12) },
    { "v2_ui_button_selected",  new Vector4(14, 14, 14, 14) },
    { "v2_ui_dialog_panel",     new Vector4(26, 26, 26, 26) },
    { "v2_ui_dialog_panel_tall",new Vector4(26, 26, 26, 26) },

    // ---- 本轮新增（左=右=左/右边界，下=上=……，本批四值全对称）----
    { "v2_ui_panel_inventory",  new Vector4(20, 20, 20, 20) },
    { "v2_ui_tooltip_panel",    new Vector4(18, 18, 18, 18) },
    { "v2_ui_dialog_bubble",    new Vector4(28, 28, 28, 28) },
    { "v2_ui_currency_plate",   new Vector4(12, 12, 12, 12) },
    { "v2_ui_slot_empty",       new Vector4(16, 16, 16, 16) },
    { "v2_ui_slot_selected",    new Vector4(16, 16, 16, 16) },
    { "v2_ui_stamina_bg",       new Vector4( 6,  6,  6,  6) },
    { "v2_ui_stamina_fill",     new Vector4( 6,  6,  6,  6) },
};
```

> 本批四值**全对称**，正好绕开「Unity 是左下右上、其余三家是左上右下」的顺序陷阱。
> 规格文档里我始终写四个**具名值**（左/上/右/下），由你按引擎排。

### 3.3 PPU 补偿（漏了边框会粗 1.5625 倍）

素材 PPU = 64，Canvas `referencePixelsPerUnit` = 100：

```csharp
var img = GetComponent<Image>();
img.type = Image.Type.Sliced;
img.pixelsPerUnitMultiplier = 100f / 64f;   // = 1.5625
```

或者把 Canvas 的 `referencePixelsPerUnit` 直接改成 64，二选一，**不要同时做**（会重复补偿）。

### 3.4 Canvas 设置（沿用已确认值）

| 项 | 值 |
|---|---|
| Render Mode | Screen Space - Overlay |
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1920 × 1080 |
| Screen Match Mode | Match Width Or Height |
| **Match** | **1.0（按高度）** |
| Canvas `referencePixelsPerUnit` | 100（配合 §3.3 补偿）或 64（不需补偿） |

### 3.5 排序层级（沿用设计文档）

| 内容 | Order in Layer |
|---|---|
| 草地 / 地面 | 0 |
| 树 / 石 / 自然元素 | 1 |
| 建筑 | 3 |
| 玩家 | 5 |
| **HUD（本轮新增）** | 独立 Canvas，`Sort Order = 10` |
| **面板 / 对话（本轮新增）** | 同上 Canvas 内靠 `Hierarchy` 顺序，或 `Sort Order = 20` |
| **转场 / 加载（本轮新增）** | `Sort Order = 30`，全屏拦截 |

> 建议：**HUD / 面板 / 转场各用一个独立 Canvas**。UGUI 里同一 Canvas 内任一元素变化会重建整张
> Mesh；体力条每帧变长度，如果和背包面板共 Canvas，会造成整张界面重建。

---

## 4. 多分辨率适配

设计分辨率 1920×1080，策略 **fitHeight（Match = 1.0）**：

| 屏幕比例 | 有效逻辑区 | 富余 / 裁切 | 风险 |
|---|---|---|---|
| 16:9（1920×1080） | 1920×1080 | 宽 +0 高 +0 | 基准比例，无富余 |
| 19.5:9（2340×1080） | 2340×1080 | 宽 +420 高 +0 | 安全：左右各多 210px |
| 20:9（2400×1080） | 2400×1080 | 宽 +480 高 +0 | 安全：左右各多 240px |

**结论：三档均无裁切。** 但要注意：

| 风险点 | 处理 |
|---|---|
| HUD 元素贴左右边缘 | 20:9 下左右多出 240px，贴边元素会被推到很外面 → **用对齐锚点绑安全区**，不要用固定坐标 |
| 背包面板居中 | 安全，居中锚点即可 |
| 血条 / 体力条横向拉伸 | 用九宫格 Sliced，**不要用 Simple + 缩放**（会拉变形） |
| 极窄屏（4:3） | 逻辑区会收窄到 1440×1080，左右各少 240px → 面板宽度不要超过 1400 |
| 超宽屏 | 转场遮罩要铺满 → 用 Tiled 或尺寸跟随 Canvas |

**多语言余量**：按钮与面板文字按最长语种留 **30%–50%**。中文「开始游戏」4 字 ↔ 德文
`Spiel starten` 13 字符，宽度差近 2 倍。面板宽度按最长语种定，不要按中文卡满。

---

## 5. 命名清单（本轮 50 张）

```
Assets/Art/V2/UI/     (22)
  v2_ui_heart_full.png          v2_ui_heart_half.png          v2_ui_heart_empty.png
  v2_ui_stamina_bg.png          v2_ui_stamina_fill.png
  v2_ui_slot_empty.png          v2_ui_slot_selected.png
  v2_ui_panel_inventory.png     v2_ui_tooltip_panel.png
  v2_ui_dialog_bubble.png       v2_ui_bubble_tail.png         v2_ui_portrait_frame.png
  v2_ui_currency_plate.png
  v2_ui_switch_on.png           v2_ui_switch_off.png
  v2_ui_transition.png
  v2_ui_load_01.png ~ v2_ui_load_06.png

Assets/Art/V2/Items/  (13)
  v2_item_seed_carrot.png       v2_item_seed_tomato.png       v2_item_seed_radish.png
  v2_item_stone_pile.png        v2_item_plank.png
  v2_item_gold_ore.png          v2_item_coal.png              v2_item_gem.png
  v2_item_mushroom.png          v2_item_berry.png             v2_item_honey.png
  v2_item_fish_blue.png         v2_item_fish_gold.png

Assets/Art/V2/FX/     (15)   ← 新目录
  v2_fx_dust_01.png ~ 03.png
  v2_fx_spark_01.png ~ 04.png
  v2_fx_leaf_01.png ~ 04.png
  v2_fx_splash_01.png ~ 03.png
  v2_fx_select_cursor.png
```

---

## 6. 导入步骤

1. 把本包 `Assets/Art/V2/{UI,Items,FX}/` 拷进工程同名目录（**覆盖同名文件前先确认 §7 的替换清单**）。
2. 跑菜单 **`Tools → LuminousSSS → 1. 重新导入像素资源`** —— 让 `PixelArtImporter` 按路径设 PPU（`Assets/Art/V2/` = 64）与过滤方式。
3. 打开 `PixelArtImporter.cs`，把 §3.2 的 8 条补进 `SpriteBorders`，再跑一次该菜单（Border 要重导入才生效）。
4. 在 Hierarchy 建三个 Canvas：`HUDCanvas(10)` / `PanelCanvas(20)` / `OverlayCanvas(30)`。
5. 逐元素设 `Image.Type`：九宫格素材 = Sliced + `pixelsPerUnitMultiplier = 1.5625`；其余 = Simple。
6. `Transition` 设 Tiled 并拉满全屏，`raycastTarget = true`。
7. Console 确认 0 报错。

---

## 7. 替换清单（覆盖已有文件，引用不会断）

| 文件 | 现有问题 | 处理 |
|---|---|---|
| `v2_ui_heart.png` | 柔边红心，非硬边；且需求文档说的「血条」其实是单颗心 | 保留原名可覆盖，但**建议改用 `v2_ui_heart_full/half/empty` 三态**，旧文件标记废弃 |
| `v2_ui_stamina_bar.png` | 40.7% 半透明像素；黄色填充断续、右侧有大块黑残留 | **建议直接弃用**，改用 `v2_ui_stamina_bg` + `v2_ui_stamina_fill` |
| `v2_ui_slot_item.png` | 内置绿色叶片示例图案，与真实道具图标打架 | **建议直接弃用**，改用 `v2_ui_slot_empty` + 引擎内叠道具图 |
| `v2_ui_title_bar.png` | 图内伪英文「STARD SVENT」，版权风险，v0.6 已弃用 | 保持弃用 |

> 直接覆盖会改变已有引用物的外观（比如登录界面里如果引用了 `v2_ui_heart`）。**替换前先搜一遍引用**：
> 在工程里搜 `v2_ui_heart`、`v2_ui_stamina_bar`、`v2_ui_slot_item` 三个 GUID 对应的 `.prefab` / `.scene`。

---

## 8. 进引擎前验收表

| 项 | 判定 | 本包状态 |
|---|---|---|
| 画布尺寸为 2 的整数倍 | 全部 | ✅ 26+13+15 全通过 |
| 硬边（半透明像素 = 0） | 全部 | ✅ 0.0% |
| 命名符合项目规范 | 全部 | ✅ |
| 图内无文字 / 无字母 | 全部 | ✅ 逐张查过 |
| 九宫格边界已写入导入表 | 8 张 | ⚠️ **待你补表**（§3.2） |
| 九宫格可变区 ≥ 1px | 全部 | ✅ 见 UI 规格 §4 |
| 序列帧帧间对齐 | `load` 6 帧 / `fx` 各序列 | ✅ 同画布等分 |
| 图集分组尺寸 ≤ 2048 | 3 包 | ✅ 1024/1024/256 |
| 三档比例无裁切 | fitHeight | ✅ |
| **引擎内实际渲染** | — | ⚠️ **未执行，需你本地确认** |

---

## 9. 复现方式（素材可重跑）

```bash
cd <交付包根目录>
export LUMINOUS_OUT=Assets/Art/V2

python3 Tools/gen_ui.py      # 22 张 UI
python3 Tools/gen_items.py   # 13 张道具图标
python3 Tools/gen_fx.py      # 15 张特效
python3 Tools/verify_hardedge.py   # 复核硬边质量
```

依赖：`Pillow`、`numpy`。修改 `Tools/luminous_pixel.py` 顶部的 `PAL` 调色板即可改整批配色，
删除后重跑即完全复现 —— **这是本项目 `Tools/gen_*.py` 的既有做法，本包沿用**。
