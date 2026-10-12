# Luminous · 微光 —— 团结引擎（Tuanjie）Android 工程

用**团结引擎 2022.3.62t16（1.14.0）**重做的 Android 版，和 `android/`（WebView 壳）那套是两条独立路线，
玩法表现一致：登录界面 → 荒原小屋主场景，角色自由移动。

## 怎么出 APK

**双击 `Tools\build-apk.cmd`** 就行。它会依次做两件事：

1. `LuminousSetup.SetupAll` —— 配置纹理/字体导入、播放器设置，并生成两个场景
2. `LuminousBuild.BuildFromCommandLine` —— 打 IL2CPP / ARMv7+ARM64 的 APK

产物：`Build\Android\LuminousSSS-tuanjie.apk`，装到手机：

```bash
D:\App\Android\Sdk\platform-tools\adb.exe install -r "Build\Android\LuminousSSS-tuanjie.apk"
```

**或者在编辑器里点菜单**（推荐第一次这么干，能看到全流程）：

打开 Hub → 打开本工程 → 菜单 `Luminous → 一键初始化工程` → 再点 `Luminous → 打包 Android APK`。

## ⚠️ 本机自动化环境的已知限制（重要）

在这台机器的**自动化/沙箱环境**里，编辑器命令行跑不通：

- `-batchmode` → 卡在 `Application.AssetDatabase Initial Refresh Start` 不再前进
- GUI 模式 + `-quit` → 在资源导入完成前就自己退出了
- GUI 模式不加 `-quit` → 进程活着，但同样停在资源导入那一步

许可日志里有 `LicensingClient has failed validation` / `Access token is unavailable`
—— 基本可以确定是**受限环境拦住许可客户端的网络校验**，不是工程问题。
用桌面双击 / Hub 正常打开工程时不受影响（2026-10-03 同一台机器上用同样方式成功打出过 28.4MB 的 APK）。

所以：**出包这一步请在正常桌面环境下跑**（双击 `build-apk.cmd` 或点编辑器菜单）。

## 工程结构

```
Assets/
  Scripts/                 运行时
    GameConfig.cs          常量与图集索引（对应网页版 CONFIG / TILES）
    SpriteFactory.cs       运行时切图：从 tilemap 图集切片、渐变、椭圆阴影
    MapBuilder.cs          荒地地图生成（与网页版同规则同随机种子）+ 整张地面烘成一张贴图
    PlayerController.cs    移动 / AABB 碰撞 / 行走动画 / 朝向翻转 / 影子
    CameraFollow.cs        跟随 + 边界钳制 + 像素对齐
    InputRouter.cs         键盘 + 左半屏虚拟摇杆
    AudioManager.cs        BGM 跨场景常驻、静音
    LoginSceneBuilder.cs   登录界面（星空、标题、昵称输入、进入按钮）
    WastelandSceneBuilder.cs 主场景装配（地图/玩家/相机/HUD/摇杆）
    AndroidFullscreen.cs   沉浸全屏 + 横屏
    UiFactory.cs           运行时建 uGUI 的小工具
    StarTwinkle.cs         星星闪烁
  Editor/
    LuminousSetup.cs       导入设置 + 播放器设置 + 生成场景（菜单 / 命令行）
    LuminousBuild.cs       打 APK（菜单 / 命令行，含 Mono 快速版）
  Resources/
    Art/  Audio/  Fonts/   图集、sprites、两首 BGM、两个 TTF
Tools/
  build-apk.cmd            一键出包（GBK + CRLF）
  CompileCheck/            脱离编辑器做编译检查（dotnet + Roslyn）
```

场景是**运行时构建**的：场景里只有一个 `SceneBuilder` 对象挂着 Builder 脚本，
地图、玩家、HUD、摇杆都在 `Awake` 里用代码搭出来 —— 好处是改动全在代码里，不用手摆 UI。

## 打包参数

| 项 | 值 |
| --- | --- |
| 包名 | `com.zxxandqiaoqiao.luminoussss` |
| 应用名 | Luminous 微光 |
| 脚本后端 / 架构 | IL2CPP / ARMv7 + ARM64 |
| 方向 | 横屏锁定 |
| minSdk / targetSdk | 22 / 自动（跟编辑器最高） |
| 联网权限 | 关（单机） |
| 签名 | Unity 自动生成的 debug keystore（装机试玩够用，上架要换） |

## 编译检查（不用开编辑器）

```bash
cd Tools/CompileCheck
# 需先设好隔离 dotnet 环境变量，见仓库根 README
dotnet build -c Release
```

对着引擎程序集编一遍 `Assets/Scripts/**` + `Assets/Editor/**`。
2026-10-12 实跑：**0 错误 0 警告**（它当场抓出过 `Sprite.filterMode` 不存在、
`NamedBuildTarget` 缺 `using UnityEditor.Build` 两个真错误）。

> 注意：编译检查绿 ≠ 能打包，它绕过资源导入流程。最终必须真的跑一次 `build-apk.cmd` 才算数。

## 素材

与网页版同一套：Kenney Tiny Town（CC0）、两首 OpenGameArt CC0 曲子、
Press Start 2P（OFL）、Noto Sans SC（OFL，用于中文，源文件由 woff2 转 TTF）。
