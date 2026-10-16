# 对照 LuminousSSS 讲 Unity 开发（小白实践版）

> 适用对象：Unity 零基础、想边动手边学的开发者。
> 全部例子都取自你自己的工程 [ZXXANDQIAOQ/LuminousSSS](https://github.com/ZXXANDQIAOQ/LuminousSSS)，讲解时请把工程用编辑器打开，对照着看。
> 你的编辑器：团结引擎 2022.3.62f1c1（工程里记录的是团结引擎 1.10.4 / 2022.3.62t16，版本正好对得上，可以直接打开）。

---

## 一、你现在手里是什么

一句话：**一个用团结引擎（Unity 中国版）做的 2D 俯视角像素风农场游戏雏形**，参考《星露谷物语 / 牧场物语》，目前做完了「登录选人界面」+「可走动的农场场景」。

用 Unity 的话描述它的组成：

| 你的东西 | Unity 里的名字 | 在哪 |
|---|---|---|
| 两个「界面/关卡」 | 场景 Scene：`LoginScene`（选人）、`FarmScene`（农场） | `Assets/Scenes/` |
| 会动的玩家、木屋、树 | 游戏对象 GameObject + 各种组件 Component | 场景里 |
| 12 个 `.cs` 文件 | 脚本（本质是挂在 GameObject 上的自定义组件） | `Assets/Scripts/`、`Assets/Editor/` |
| 128 张瓦片、人物、UI 图 | 资源 Asset（贴图、音频、动画、Tile 资产） | `Assets/Art/`、`Assets/Audio/`、`Assets/Animations/` |
| 一键生成场景的工具 | 编辑器扩展 Editor Script | `Assets/Editor/GameSceneBuilder.cs` |
| 「音乐/音效开关、选了谁」 | 本地存档 PlayerPrefs | 代码里 |

**当前能玩到的效果**：打开工程 → 自动生成两个场景 → 点播放 → 看到选人界面（4 个角色、◀▶ 切换、女巫有待机动画）→ 开始游戏 → 进入农场 → WASD/方向键控制小人走动、相机跟随、树和石头有碰撞、能绕到树后面（被树挡住）。

---

## 二、先建心智模型：Unity 的 5 个核心概念

小白最容易卡住的不是语法，是「Unity 式的思维方式」。用你项目里的真实例子讲：

### 1. GameObject + Component：世界万物都是「空壳 + 插件」

Unity 里没有「类」，只有**挂在场景里的对象**。一个 GameObject 本身什么都不做，它的行为全部来自挂上去的组件。

看你项目里玩家是怎么拼出来的（`Assets/Editor/GameSceneBuilder.cs` 第 474-475 行）：

```csharp
var player = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                            typeof(PlayerController), typeof(PlayerAppearance), typeof(YSort));
```

翻译成人话：

| 组件 | 作用 | 来自 |
|---|---|---|
| `Rigidbody2D` | 物理身体，让物理系统推着它动 | 引擎内置 |
| `CircleCollider2D` | 圆形碰撞体（半径 0.32，挡树用） | 引擎内置 |
| `PlayerController` | **你自己写的移动逻辑** | `Assets/Scripts/Gameplay/PlayerController.cs` |
| `PlayerAppearance` | **你自己写的换皮肤逻辑** | `Assets/Scripts/Gameplay/PlayerAppearance.cs` |
| `YSort` | **你自己写的遮挡排序逻辑** | `Assets/Scripts/Gameplay/YSort.cs` |

**记住这句话：在 Unity 里写代码，90% 的情况下你写的不是「程序」，而是「一种新组件」。** 你写的每个类都必须继承 `MonoBehaviour`，才能挂到 GameObject 上。

### 2. Inspector 面板：代码的「可视化调参窗口」

你代码里写的 `public` 字段，会出现在编辑器右侧的 Inspector 面板里，可以直接拖拽赋值。这是 Unity 最重要的设计：**策划/美术改数值不用碰代码**。

看你项目里的写法（`PlayerController.cs` 第 12-19 行）：

```csharp
[Header("移动")]
public float moveSpeed = 5f;

[Header("朝向（水平翻转 + 动画方向）")]
public SpriteRenderer bodyRenderer;
```

`[Header("移动")]` 是给 Inspector 加一个分组标题；`public float moveSpeed` 让你能在编辑器里直接拖滑块改移动速度——这就是「边实践边学」最好的入口：**改一个数字，立刻能看到效果**。

### 3. 脚本生命周期：Unity 会在特定时刻自动调用你的方法

你不需要写 `main()`，Unity 在运行时按固定顺序调用脚本里的这些方法。你的项目把这几个全用上了：

```
场景加载
  │
  ├─ Awake()          「我醒了」——最早执行，适合拿组件引用、初始化单例
  │                  例：PlayerController.Awake() 拿 Rigidbody2D，关重力（第 36-45 行）
  │
  ├─ Start()          「第一次帧更新前」——适合读存档、绑定按钮
  │                  例：CharacterSelector.Start() 绑定 ◀▶ 按钮、读上次选的角色（第 38-59 行）
  │
  ├─ 每帧循环 ────────────────────────────────────────┐
  │   Update()        「每帧调用」——处理输入、计时      │  例：PlayerController.Update() 读键盘（第 47-69 行）
  │   FixedUpdate()   「固定 0.02 秒一次」——物理相关    │  例：PlayerController.FixedUpdate() 推 Rigidbody2D（第 71-74 行）
  │   LateUpdate()    「每帧最后调用」——相机、排序      │  例：CameraFollow.LateUpdate() 跟随玩家；YSort.LateUpdate() 排序
  │   ───────────────────────────────────────────────┘
  │
  └─ 场景卸载 / 对象销毁
      OnDestroy()
```

**小白第一个必懂的问题：为什么移动要写在 FixedUpdate 里？**
因为物理引擎按固定步长跑，`Update` 每帧间隔时间不稳定（掉帧时变长）。位置移动用 `MovePosition` 属于物理操作，放 `FixedUpdate` 才能保证不同电脑上速度一致。你的 `PlayerController` 就是这么分的：`Update` 只负责读输入和翻转，`FixedUpdate` 负责真正移动。

### 4. 场景（Scene）与场景切换

场景 = 一「关」或一「界面」。游戏在不同场景之间跳转靠：

```csharp
// CharacterSelector.cs 第 101 行
if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
```

点「开始游戏」→ 把角色 id 写进 `GameData.SelectedCharacterId`（内部是 PlayerPrefs）→ `LoadScene("FarmScene")` 加载农场。**注意：切场景时旧场景里所有对象全部销毁**——这就是为什么你的 BGM 没断，见下面第 5 点。

场景名常量集中在 `GameData`（`Assets/Scripts/Core/GameData.cs` 第 8-9 行），别在代码里散落硬编码字符串。

### 5. 跨场景存活：单例 + DontDestroyOnLoad

你的 `AudioManager`（`Assets/Scripts/Core/AudioManager.cs`）展示了 Unity 最常用的模式之一：

```csharp
// 第 29-37 行
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);   // 新场景里又出现一个 AudioManager？销毁新的，保住旧的
        return;
    }
    Instance = this;
    DontDestroyOnLoad(gameObject);   // 切场景时不销毁我
}
```

这就是**单例模式**：全局唯一、谁都能通过 `AudioManager.Instance` 访问、切场景不消失。你项目里登录场景和农场场景各放了一个 AudioManager（生成器在两个场景都调了 `EnsureAudioManager()`），靠这段代码去重，BGM 才能从头播到尾。

配套的 `GameData.SelectedCharacterId` 是**静态属性 + PlayerPrefs**：切场景后新场景的 `PlayerAppearance.Start()` 靠它知道「玩家刚才选了女巫」。

---

## 三、工程目录：每个文件夹是干什么的

```
LuminousSSS/
├── Assets/               ← 你的全部工作区（图片/脚本/场景都放这里）
│   ├── Art/              ← 像素素材（旧版 32px/格 + V2/ 新版 64px/格）
│   ├── Audio/            ← 3 个 wav（bgm_main / sfx_click / sfx_confirm）
│   ├── Animations/       ← 女巫的 idle/walk 动画剪辑 + AnimatorController
│   ├── Scenes/           ← SampleScene.scene（工程自带）等
│   ├── Scripts/
│   │   ├── Core/         ← GameData（常量+存档）、AudioManager（音频单例）
│   │   ├── Gameplay/     ← PlayerController、PlayerAppearance、YSort
│   │   └── UI/           ← CharacterSelector、CharacterPreview、SettingsPanel、UIFontFixer
│   ├── Editor/           ← 只在编辑器里跑的工具代码（见第六节）
│   └── Settings/         ← URP 渲染管线资产
├── Packages/             ← manifest.json：声明工程依赖哪些官方/第三方包
├── ProjectSettings/      ← 工程设置（分辨率、输入轴、Player 设置、引擎版本）
└── Docs/                 ← 设计文档与参考图
```

小白必知的 3 条铁律：

1. **所有资源必须放 `Assets/` 里**，放外面 Unity 看不见。
2. **`Assets/` 里每个文件/文件夹都有一个对应的 `.meta` 文件**（比如 `PlayerController.cs.meta`）。它记录这个文件的 GUID（全局唯一身份证）。场景、预制体靠 GUID 互相引用。**删了 `.meta`，引用就全断**，所以你的工程把 `.meta` 都提交进了 Git（v0.4.1 提交专门「补齐引擎导入生成的 .meta」）。
3. **`Library/`、`Temp/`、`Logs/` 不要提交 Git**（你的 `.gitignore` 已排除）。Library 是本地缓存，几 GB 大，删了重新打开工程会自动生成。

---

## 四、跟着主线走一遍：从「点播放」到「小人会走」

这是最值得反复读的一节。把 12 个脚本按执行顺序串起来，你就懂了 Unity 程序是怎么「活」的。

### 第 0 步：打开工程时发生了什么（你项目的特色设计）

你的工程有个贴心设计：**打开工程自动生成场景，不用手动点菜单**（`Assets/Editor/AutoBuildScenes.cs`）：

```csharp
[InitializeOnLoad]                  // 编辑器加载时就跑（不用点任何按钮）
static AutoBuildScenes()
{
    EditorApplication.delayCall += Run;   // 等编辑器空闲时执行
}

static void Run()
{
    if (File.Exists("Assets/Scenes/LoginScene.scene")) return;  // 已生成过就跳过
    ...
    GameSceneBuilder.OneClick();   // 一键：重导入素材 + 生成两个场景
}
```

它做的事情在 `GameSceneBuilder.cs` 里：创建 Tile 资产、修女巫的动画控制器、用代码拼出登录界面 UI、铺农场 Tilemap、摆树/石头/木屋、建玩家、存场景。**等于用程序代替了手动拖场景**——这是独立开发者很值得学的技巧：场景可复现、改素材重跑一次就好、Git 里 diff 也更干净。

### 第 1 步：点播放，登录界面活了

```
UIFontFixer.Awake()            ← [DefaultExecutionOrder(-100)] 保证它第一个跑
    └─ 给所有 Text 赋值动态中文字体（LegacyRuntime.ttf 不含中文字形，不处理会显示方块）

CharacterSelector.Start()      ← 绑定 ◀ ▶ 开始 设置 按钮；读 PlayerPrefs 恢复上次选的角色
    └─ Refresh() 更新立绘和名字
```

小白知识点：**UI 在 Unity 里也是 3D 场景里的对象**。`Canvas` 是画布，`Image`/`Text`/`Button` 都是挂在 Canvas 下的组件。你项目里整个登录界面是代码生成的：`BuildLoginScene()`（`GameSceneBuilder.cs` 第 206 行起）创建 Canvas、按钮、文字，再 `canvasGO.AddComponent<CharacterSelector>()` 把逻辑脚本挂上去，最后用 `selector.startButton = startBtn.GetComponent<Button>()` 把「UI 引用」接好。

**这是 Unity UI 的标准套路：UI 表现（Image/Text）和 UI 逻辑（你的脚本）分离，脚本通过 public 字段拿引用。** 你在 Inspector 里能看到这些字段被填得满满的。

### 第 2 步：按 ◀ ▶ 切换角色

```
Next() → AudioManager.Click()（全局音效单例）→ Move(1) → Refresh()
Refresh() → preview.SetFrames(frames) → CharacterPreview 按 fps 轮播 idle 帧
```

注意 `Move` 里的取模循环（第 67 行）：

```csharp
_index = (_index + delta + characters.Length) % characters.Length;
```

按到最后一个再按下一个 → 回到第一个。这是小白第一课：**Unity 不帮你防数组越界，自己写边界逻辑**。

### 第 3 步：按「开始游戏」，跨场景传递数据

```csharp
// CharacterSelector.cs 第 94-102 行
public void StartGame()
{
    var c = Current;
    AudioManager.Confirm();
    GameData.SelectedCharacterId = c.id;      // 写 PlayerPrefs 并 Save()
    SceneManager.LoadScene(nextSceneName);    // 加载 FarmScene
}
```

**PlayerPrefs = Unity 送的本地小存档**（键值对，存在本机）。适合存设置、选人结果这种小数据。注意两点：它是同步写的、会 `Save()` 落盘；但它不是数据库，别往里塞大结构。

### 第 4 步：农场场景里，角色「活」了

新场景加载后，`PlayerAppearance.Start()` 先根据 PlayerPrefs 里的 id 换皮肤（第 23-47 行）：选女巫就挂上 AnimatorController 并启用动画；选静态角色就 `animator.enabled = false`（防止动画把静态图刷成别人的帧——这种细节就是读代码才能学到的实战经验）。

然后每帧：

```
Update()      Input.GetAxisRaw("Horizontal"/"Vertical") 读键盘 → 归一化 → 记朝向 → flipX 翻转 → 通知 Animator
FixedUpdate() _rb.MovePosition(_rb.position + _input * (moveSpeed * Time.fixedDeltaTime))  →  物理引擎真正移动
LateUpdate()  CameraFollow 相机 SmoothDamp 跟随；YSort 按 y 算 sortingOrder 决定谁盖谁
```

小白知识点串烧：

- **输入**：`Input.GetAxisRaw` 读的是 ProjectSettings 里配置的「Horizontal/Vertical 轴」（默认 WASD + 方向键，已带平滑/死区处理）。项目没用新 Input System，用的经典 Input Manager，对小白更友好。
- **`normalized`**：斜着走（W+D）速度是 √2 倍，归一化后各方向速度一致。
- **`flipX`**：2D 小人不用做左走动画，水平翻转 Sprite 就是最省事的「假」转向。
- **`Time.fixedDeltaTime`**：固定步长下每步的时间，乘上速度才是每步位移。
- **`sortingOrder`**：2D 里没有「前后」，只有绘制顺序，数字越大越晚画（盖在上面）。你的 YSort 每帧按 y 重算：靠下的对象 sortingOrder 大，所以玩家可以绕到树后面被树冠挡住——这就是俯视角 2D 的「假 3D」。
- **`SmoothDamp`**：相机平滑跟随的标准写法，比直接 `Lerp` 可控。

---

## 五、逐个概念对照速查表

| Unity 概念 | 你项目里的实例 | 看哪里 |
|---|---|---|
| 组件化 | Player = Rigidbody2D + Collider2D + 3 个自制脚本 | `GameSceneBuilder.cs:474` |
| 生命周期 | Awake/Start/Update/FixedUpdate/LateUpdate 各司其职 | `PlayerController.cs` 全篇 |
| public 字段 = Inspector 调参口 | `moveSpeed`、`multiplier`、`smoothTime` | 各脚本 `[Header]` 段 |
| 物理 2D | Rigidbody2D 关重力、冻结旋转、`MovePosition` | `PlayerController.cs:39-42,73` |
| 碰撞 | 树/石头/木屋底部 BoxCollider2D + 四围墙 | `GameSceneBuilder.cs:530-557` |
| Tilemap | 草地/土地/土路/石墙用 Tile 平铺 | `GameSceneBuilder.cs:378-416` |
| ScriptableObject | Tile 资产（`v2_grass_a.asset` 等） | `GameSceneBuilder.cs:97-111` |
| UI（UGUI） | Canvas + Image/Text/Button + EventSystem | `GameSceneBuilder.cs:206-302` |
| 事件驱动 UI | `button.onClick.AddListener(Next)` | `CharacterSelector.cs:46-49` |
| 场景管理 | `SceneManager.LoadScene`、Build Settings 注册场景 | `CharacterSelector.cs:101`、`GameSceneBuilder.cs:599` |
| 本地持久化 | PlayerPrefs 存角色 id、音乐/音效开关 | `GameData.cs:12-24`、`AudioManager.cs:39-40` |
| 音频 | AudioSource + AudioClip，BGM 循环、PlayOneShot 音效 | `AudioManager.cs` 全篇 |
| 动画 | Animator + AnimatorController，Float/Bool 参数驱动 Idle↔Walk 过渡 | `GameSceneBuilder.cs:117-189`、`PlayerController.cs:64-68` |
| 单例 | AudioManager.Instance + DontDestroyOnLoad | `AudioManager.cs:29-37` |
| 命名空间 | 全部代码在 `namespace LuminousSSS` 下 | 各文件 |
| 像素导入规范 | AssetPostprocessor 自动设 Point 过滤、无压缩、PPU | `PixelArtImporter.cs` 全篇 |
| 编辑器自动化 | MenuItem 菜单、InitializeOnLoad 自动跑 | `GameSceneBuilder.cs:41-65`、`AutoBuildScenes.cs` |

---

## 六、团结引擎特别篇（和国际版 Unity 的差异）

你的工程用的是**团结引擎（Tuanjie）1.10.4**，对应 Unity 2022.3.62t16。它和国际版 Unity 的关系：同源代码、加了中国本土化能力（小游戏/鸿蒙/真机调试等），包名以 `cn.tuanjie.*` 开头。你项目里能观察到的差异：

1. **场景文件扩展名是 `.scene`**（`Assets/Scenes/SampleScene.scene`）。这是团结引擎原生场景格式，工程内自洽，Git 正常提交没问题，别手动改后缀。
2. **`Packages/manifest.json` 里有团结专属包**：`cn.tuanjie.codely.bridge`、`cn.unity.uos.launcher`、以及指向本地 `.codely-cli/extensions/TJGenerators` 的 `file:` 依赖。其中 `cn.tuanjie.ai.generators` 指向仓库内 `Assets/TJGenerators/` 目录。如果换机器 clone 后这个本地路径不存在，Unity 会报包解析错误——看到报错先检查这里。
3. **`-batchmode` 命令行构建会在资源导入阶段卡住**（`GameSceneBuilder.cs` 第 15-16 行注释明确记了这个坑），所以工程把「生成场景」设计成必须在编辑器里点菜单/自动跑，而不是命令行。
4. 版本对应关系：你本机 `2022.3.62f1c1` 能打开记录为 `2022.3.62t16 / Tuanjie 1.10.4` 的工程（小版本号一致即可，不需要严格相同）。若打开后提示升级/降级，以编辑器实际提示为准。

---

## 七、边实践边学路线图（4 周，每周只改一个小东西）

原则：**每周都让游戏发生一个你能看见的变化**，改完运行验证，再读代码。每周都从「改数值」→「改逻辑」→「加系统」递进。

### 第 1 周：跑起来 + 学会「改参数看效果」

目标：把工程跑起来，建立「数字 → 效果」的直觉。

1. 打开工程，等场景自动生成完，点播放，走一遍流程。
2. 选中 Player，在 Inspector 里把 `PlayerController.moveSpeed` 从 5 改成 10，感受加速。
3. 把 `YSort.multiplier` 从 10 改成 100，看树遮挡关系是否变怪（理解排序公式）。
4. 把 `CameraFollow.smoothTime` 从 0.18 改成 0.001，感受「硬跟随」和「平滑跟随」的区别。
5. 试着改 `GameSceneBuilder.cs` 里的 `MapW/MapH`（第 30-31 行）从 40×24 改成 60×36，删掉 `Assets/Scenes/LoginScene.scene` 和 `FarmScene.scene`，重开工程触发自动重建，看地图变大。

**这周学会的**：Inspector 调参、Play 模式实验、场景重建流程。

### 第 2 周：读代码 + 改逻辑

目标：能定位「这个行为是哪行代码决定的」，并安全地改它。

1. 精读 `PlayerController.cs`（76 行，全项目最适合入门的文件）。回答自己：斜着走为什么不变快？`flipX` 在哪一行？
2. 小练习 A：加一个「跑步」——按住 Shift 时速度 ×2。提示：在 `Update` 里读 `Input.GetKey(KeyCode.LeftShift)`，存一个 `bool running`，`FixedUpdate` 里乘系数。
3. 小练习 B：在登录界面加一个输入框，让玩家输名字，农场里显示。提示：UI 用 `TMP_InputField` 或 `InputField`（工程已装 TextMeshPro），名字存 `GameData` 新键。
4. 学会看 Console 面板的 `Debug.Log` 输出——你项目里到处都有 `Debug.Log("[LuminousSSS] ...")`，这是 Unity 的 print，调试第一手段。

**这周学会的**：输入处理、条件逻辑、Debug.Log 调试、GameData 加新键。

### 第 3 周：加第一个交互系统

目标：做出「按 E 砍树/捡东西」，理解碰撞与触发。

1. 学习 `OnTriggerEnter2D` / `OnCollisionEnter2D` 的区别（你的项目目前只有碰撞，没有触发）。
2. 小练习：走到树旁按 E，树消失并弹一行 Debug.Log。思路：给树加 Tag（比如 `Tree`），玩家身上挂一个脚本检测 `Input.GetKeyDown(KeyCode.E)` + 用 `Physics2D.OverlapCircle` 找面前的树，`Destroy` 它。
3. 顺便学会 Unity 的 **Prefab（预制体）**：目前场景全靠代码生成，下一步应该把树做成预制体，代码 `Instantiate` 批量摆——这是从「能跑」到「工程化」的关键一跃。

**这周学会的**：Tag/Layer、射线或重叠检测、Destroy、Prefab。

### 第 4 周：加一个数据驱动的小系统

目标：学会 ScriptableObject + 简单的游戏数据表，从此数值不硬编码。

1. 小练习：把 4 个角色的定义（id/名字/立绘/世界图）从 `GameSceneBuilder` 里搬到一个 `CharacterDatabase : ScriptableObject` 资产，选中角色时读它。体会「数据在 .asset 文件里，代码读数据」。
2. 有余力就加一个最简「体力系统」：跑步掉体力，显示在 UI 上。用 `PlayerPrefs` 存不下（每帧变），用内存变量即可。

**这周学会的**：ScriptableObject、数据与逻辑分离、UI 绑定数值。

### 之后（按你项目的设计文档 roadmap）

设计文档 `Docs/游戏设计文档 v0.1` 里列了后续方向：四方向行走动画、砍树/挖矿/采集、建造系统、木屋室内场景、底部出口连小镇。**每开一个新系统前，先在设计文档里写清楚再做**——你这个项目的文档习惯（每个版本提交信息都写清楚改了什么）本身就是好实践，保持住。

---

## 八、小白常见坑速查（你的项目已经替你踩过一遍）

| 现象 | 原因 | 处理 |
|---|---|---|
| 中文显示方块 | 内置字体不含中文字形 | 运行时赋动态字体，见 `UIFontFixer.cs` |
| 像素图发虚/边缘糊 | 贴图没设 Point 过滤、开了压缩/ mipmap | 见 `PixelArtImporter.cs` 的导入设置 |
| Tilemap 平铺有接缝 | 素材自带描边直接拼会有缝 | 用镜像四拼生成的无缝填充块（`Tools/gen_fill_tiles.py`） |
| 两套素材格子大小不一致 | PPU（每单位像素数）不统一 | Art/ 用 32、Art/V2/ 用 64，见 `PixelArtImporter.ResolvePpu` |
| 切场景后 BGM 断了 | 对象被场景卸载销毁 | `DontDestroyOnLoad` + 单例去重，见 `AudioManager.Awake` |
| 女巫不播动画 | AnimatorController 是空壳/没参数/没状态机 | 见 `EnsureWitchController()` 用编辑器 API 原地修复 |
| 打开工程场景是空的 | 场景未生成 | 等 `AutoBuildScenes` 自动跑，或手动点 `Tools/LuminousSSS/一键生成` |
| 包解析报错 | manifest 里的本地 file: 依赖路径不存在 | 检查 `.codely-cli/extensions/TJGenerators` 是否存在 |
| 提交工程后别人打开丢引用 | 漏提交 `.meta` | 所有 `.meta` 必须进 Git |
| Play 模式里改了场景，停止后没了 | Play 模式的改动只是临时的 | 想保留就在 Play 模式外改，或退出 Play 后 Apply |

---

## 九、给你的三条总建议

1. **读代码的顺序**：`YSort.cs`（26 行，最短）→ `PlayerController.cs`（76 行，核心玩法）→ `CharacterSelector.cs`（UI 逻辑）→ `AudioManager.cs`（单例模式）→ `GameSceneBuilder.cs`（612 行，编辑器自动化，压轴）。按这个顺序，一周就能通读完全部。
2. **每改一行，先猜效果，再运行验证**。猜错就是你知识点缺漏的地方，比从头读教程效率高得多。
3. **保持你这个项目已经养成的两个好习惯**：版本提交信息写清改了什么；每个阶段有设计文档。独立开发者最大的敌人不是不会写代码，是三个月后看不懂自己三个月前写了什么。

---

*本文档基于 LuminousSSS main 分支（v0.4.1，2026-10-14）实际代码撰写，行号对应该版本。引擎相关不确定项以团结引擎官方文档当前版本为准。*
