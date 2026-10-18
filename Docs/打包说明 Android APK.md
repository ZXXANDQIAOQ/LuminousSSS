# 打包说明 — Android APK

> 适用工程：LuminousSSS（团结引擎 2D 农场）
> 引擎：团结引擎 **2022.3.62t16**（内部版本 1.10.4）
> 本机引擎路径：`E:\App\GameWorker\Editor\2022.3.62t16\Editor\Tuanjie.exe`
> 本文最后核对：2026-10-18，对应工程提交 `8ca41c0`（v0.13）

---

## 0.5 一键出包（推荐，2026-10-18 起）

菜单：`Tools → LuminousSSS → 出安卓调试包（ARM64 + IL2CPP）`

由 `Assets/Editor/AndroidBuilder.cs` 提供，**自动完成第 2 节的全部设置**，不用再手点面板：

| 自动设置 | 值 | 为什么 |
|---|---|---|
| Target Architectures | 只 `ARM64` | M511CD 这类 Android 15+ 设备只认 64 位；旧包只打 `armeabi-v7a`，装都装不上 |
| Scripting Backend | `IL2CPP` | Android 上 64 位的前提（Mono 只有 32 位） |
| Package Name | `com.zxxandqiaoq.luminoussss` | 不用默认 `com.DefaultCompany.*` |
| Orientation | `Landscape Left` | HUD 按横屏布局 |
| Build 选项 | `Development` + `Script Debugging` | 真机 `adb logcat -s Unity` 才能看到 Debug.Log 与异常堆栈 |

- 产物：`OutPut/LuminousSSS_arm64_dev.apk`（`OutPut/` 已被 `.gitignore` 排除）
- 构建摘要（含耗时、体积、失败时的逐条报错）：`.workbuddy/android_build.log`
- 构建期间编辑器会**阻塞 10~30 分钟**（IL2CPP 首次编译），属正常。
- WorkBuddy 可通过桌面 MCP 直接点这个菜单并轮询上面的日志，不需要人手点。

---

## 0. 为什么必须手动点

本机的团结引擎**命令行模式不可用**：

| 方式 | 现象 |
|---|---|
| `-batchmode` | 卡在 `Application.AssetDatabase Initial Refresh Start` 不动 |
| GUI + `-quit` | 在资源导入完成前就退出 |
| GUI 不加 `-quit` | 同样停在资源导入阶段，CPU 几乎不动 |

日志里伴随 `LicensingClient has failed validation` / `Access token is unavailable` —— 受限环境里许可客户端的网络校验过不去。
**结论：出包必须由人在桌面环境里点菜单，脚本化这条路在本机走不通。**

---

## 1. 环境检查（一次性）

| 项 | 本机现状 | 说明 |
|---|---|---|
| AndroidPlayer 模块 | ✅ 已装 | `…/Editor/Data/PlaybackEngines/AndroidPlayer/` |
| 自带 SDK platforms | ✅ `android-13`(API33) / `android-34` / `android-35` | |
| 自带 build-tools | ✅ `34.0.0` | 与 API 34 配套 |
| 自带 NDK | ✅ | IL2CPP 必需 |
| 自带 OpenJDK | ✅ 17 | |
| Gradle | ✅ 8.0.2 | |
| 授权 | ✅ `C:\ProgramData\Tuanjie\Tuanjie_lic.ulf` | 不是 `ProgramData\Unity` |

**不装 Android Studio、不额外配 SDK/NDK** —— 直接勾 `Preferences → External Tools` 里的
「使用随编辑器安装的（Use embedded）」即可。

---

## 2. 编译前必须改的设置（一次性，之后不用再动）

`File → Build Settings…` → 左下角 `Player Settings` → `Player` → `Other Settings`：

### 2.1 Target Architectures —— 必须加 ARM64 ⚠️

| 项 | 当前值 | 改成 |
|---|---|---|
| `Target Architectures` | **只勾了 `ARMv7`** | **勾上 `ARM64`**（ARMv7 可留可去） |

原因：
- Google Play 自 2019 年起要求 64 位；Android 14+ 已有设备**不再支持 32 位应用**，只出 ARMv7 的包在新手机上装不上；
- `ARM64` 只在 `Scripting Backend = IL2CPP` 下可用 —— Android 默认就是 IL2CPP ✅，不用改。

> 如果你的机器上 `ARM64` 处于灰选状态，说明 `Scripting Backend` 被切到了 Mono，先把它切回 `IL2CPP`。

### 2.2 Package Name —— 建议改

| 项 | 当前值 | 建议 |
|---|---|---|
| `Package Name` | 空（会回落成 `com.DefaultCompany.LuminousSSS`） | `com.zxxandqiaoq.luminoussss` |

**不改也能打包能安装**，只是包名不正式、且和别人的默认包名容易撞。改一次永久生效。

### 2.3 保持不动的项

| 项 | 值 | 说明 |
|---|---|---|
| `Minimum API Level` | Android 5.1 (API 22) | 保持 |
| `Target API Level` | **Auto** | ⚠️ **不要手选 API 35** —— 自带 build-tools 只有 34.0.0，选 35 可能报 compileSdk 不匹配。若 Auto 报错，就手选 `Android 14 (API 34)` |
| `Scripting Backend` | IL2CPP | 保持（ARM64 的前提） |
| `Orientation` | Landscape Left | 工程已锁 |
| `Keystore` | 空 | 用引擎内置 debug keystore 签名，可正常侧载安装 |

---

## 3. 每次出包的步骤

### 第 1 步 · 打开工程

双击 `E:\App\GameWorker\Editor\2022.3.62t16\Editor\Tuanjie.exe`，选本工程目录。
首次打开（或刚拉过代码）会导入新增素材，**耐心等到 Console 不再滚动**。

### 第 2 步 · 一键生成资源 + 场景

菜单：`Tools → LuminousSSS → 一键生成（资源 + 场景）`

这一步做四件事：重导像素素材（PPU / Point / 九宫格）→ 生成 Tile 资产 → 生成角色动画剪辑与控制器 → 重建两个场景。

**预期 Console 输出**（v0.10 起）：

```
[LuminousSSS] 已按像素规范重新导入 N 张贴图…
[LuminousSSS] 角色动画：1 个角色已生成剪辑，3 个因缺帧跳过（走 bob 兜底）。
[LuminousSSS] 角色控制器：1/4 个已就绪，清理过期 1 个（缺帧的角色走 flipX + bob 兜底）。
[LuminousSSS] 农夫 动画控制器已就绪（Idle + 四方向行走）…
[LuminousSSS] 场景生成完成…
```

> 目前只有**农夫**的 24 帧行走素材已交付，所以是 `1/4`。雨女/冒险者/女巫走 `flipX + bob` 兜底，属**正常**。

**⚠️ 正常但容易误会的一点**：这一步会**删除** `Assets/Animations/WitchPlayerController.controller`
（女巫缺帧，旧控制器引用的是过期剪辑）。跑完 `git status` 会显示该文件 `deleted` —— 这是 v0.10 的预期清理行为，不是出错。

### 第 3 步 · 切到 Android 平台

`File → Build Settings…` → 选中 `Android` → 点 `Switch Platform`。

首次切换会**全量重新导入资源**，按机器性能可能十几分钟。切完确认 `Android` 行右侧显示 ✓。

### 第 4 步 · 确认场景列表

`Build Settings` 的 `Scenes In Build` 里应有三条且**全部勾选**，顺序如下（第 0 条是首场景）：

```
0  Assets/Scenes/LoginScene.scene   ✓
1  Assets/Scenes/FarmScene.scene    ✓
2  Assets/Scenes/SampleScene.scene  ✓
```

顺序不对就拖拽调整。多条或少条说明第 2 步没跑成功。

### 第 5 步 · 出包

`Build Settings` → `Build`（不是 `Build And Run`）→ 输出路径填：

```
<工程根>\Builds\LuminousSSS-v0.10.apk
```

`Builds/` 已被 `.gitignore` 排除，APK 不会污染仓库。

IL2CPP 首次编译较慢（**10~30 分钟**属正常），之后增量会快很多。

### 第 6 步 · 交付

把 APK 路径发给 WorkBuddy，附上一个 GitHub Personal Access Token，
由 WorkBuddy 创建 Release 并把 APK 作为资产传上去。

---

## 4. 常见报错与对策

| 报错关键字 | 原因 | 对策 |
|---|---|---|
| `Compilation failed` / `Bee` 卡住 | 脚本有编译错误 | 看 Console 第一条红色报错；本机可先用 `Tools/CompileCheck` 离线查（见下） |
| `compileSdk 35 requires AGP 8.x` | Target API 选了 35，自带 AGP 不支持 | Target API Level 改选 `Android 14 (API 34)` |
| `Gradle build failed` + 网络超时 | 工程声明了自定义 Gradle 模板要联网拉依赖 | 确认 `Player Settings → Publishing Settings` 里四个 `useCustom*` 全为 **不勾选**（当前工程就是不勾） |
| `NDK not found` / `il2cpp` 报错 | External Tools 里 NDK 指向了外部路径 | `Preferences → External Tools` 改回「使用随编辑器安装的」 |
| `LicensingClient has failed validation` | 授权校验网络不通 | 一般只影响命令行；GUI 里能正常出包就不用管。若 GUI 也失败，重新激活授权 |
| 手机上装不上/提示「不支持」 | 只出了 ARMv7 的包 | 回到 §2.1，勾上 ARM64 重新出包 |

### 离线编译自检（不开编辑器，随时可用）

```bash
cd Tools/CompileCheck
rm -rf bin obj
env -u HTTP_PROXY -u HTTPS_PROXY -u http_proxy -u https_proxy \
  DOTNET_ROOT="C:/Users/25484/.workbuddy/binaries/dotnet/sdk8" \
  DOTNET_CLI_HOME="C:/Users/25484/.workbuddy/binaries/dotnet/clihome" \
  NUGET_PACKAGES="C:/Users/25484/.workbuddy/binaries/dotnet/packages" \
  "C:/Users/25484/.workbuddy/binaries/dotnet/sdk8/dotnet.exe" build -c Release -v minimal
```

期望输出：`已成功生成。 0 个警告 0 个错误`。
（必须清掉 `HTTP_PROXY` 等代理变量，否则 MSBuild 报 `MSB6001`。）

---

## 5. 上传 GitHub Release

仓库 `ZXXANDQIAOQ/LuminousSSS` 目前**无 Release、无 tag**，本次为第一个。

**Token 要求**：fine-grained PAT，`Repository access` 只选 `LuminousSSS`，
权限 `Contents: Read and write`（创建 Release 与上传资产需要）。用完后建议在 GitHub 上撤销。

建议的 Release 元信息：

| 字段 | 值 |
|---|---|
| Tag | `v0.10` |
| Title | `LuminousSSS v0.10 — 角色动画按角色拆分 + 2D 硬边化收尾` |
| 资产 | `LuminousSSS-v0.10.apk` |

> 本机没有 `gh` CLI、没有可用的 GitHub token，GitHub 连接器也只提供**只读**的 release 接口，
> 所以「创建 Release + 上传 APK」这一步由 WorkBuddy 用 REST API（`curl`）完成，需要你提供上述 token。
