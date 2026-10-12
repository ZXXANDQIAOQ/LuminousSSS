# Luminous · 微光

轻量化横屏 2D 像素小游戏（经营 + 小游戏战斗，开发中）。

当前版本 **v0.1**：登录界面 + 主场景「荒原 · 初始之地」（荒地 + 小屋 + 角色自由移动）。

## 运行

### 网页版

纯静态页面，无构建步骤。任意静态服务器指向本目录即可：

```bash
python -m http.server 8080
# 打开 http://127.0.0.1:8080
```

> 注意：需要通过 http(s) 访问，直接双击 index.html（file://）会导致图片/音频加载失败。

### Android 版

游戏本体同一套代码，`android/` 是 WebView 壳 + 离线打包脚本（不需要 Gradle / 联网）：

```bash
python android/build_apk.py     # → dist/LuminousSSS-v0.1.apk
D:\App\Android\Sdk\platform-tools\adb.exe install -r dist/LuminousSSS-v0.1.apk
```

横屏全屏、刘海屏铺满、不息屏、支持虚拟摇杆；包名 `com.zxxandqiaoqiao.luminoussss`，
minSdk 21 / targetSdk 34。细节见 [android/README.md](android/README.md)。

## 操作

| 平台 | 操作 |
| ---- | ---- |
| PC   | WASD / 方向键移动 |
| 手机 | 横屏，左半屏按住拖动出虚拟摇杆 |

## 目录结构

```
index.html          入口 + 登录界面（DOM 层）
src/game.js         游戏主逻辑（地图 / 玩家 / 相机 / 输入 / 渲染）
assets/
  sprites/          主角 sprite（hero_0~4，来自自有角色图切分）
  tiles/            Kenney Tiny Town 图集（CC0）
  audio/            BGM（OpenGameArt，CC0）
  fonts/            Press Start 2P 像素字体（OFL）
android/            Android WebView 壳 + 离线打包脚本（build_apk.py）
tools/smoke-test.js 无头浏览器冒烟测试（playwright-core + 系统 Edge）
dist/               打包产物（APK）
```

## 自测

```bash
# 1. 安装依赖（只需 playwright-core，会调用系统 Edge，不下载浏览器）
npm install playwright-core
# 2. 起本地服务
python -m http.server 8765
# 3. 跑冒烟测试：登录 → 进入游戏 → 移动 → 碰撞，输出截图到 %TEMP%/luminous_test
node tools/smoke-test.js
```

## 素材授权

| 素材 | 来源 | 授权 |
| ---- | ---- | ---- |
| 场景图集 | Kenney Tiny Town | CC0 |
| 登录 BGM | OpenGameArt「8-Bit Curious Theme」(emanresU) | CC0 |
| 场景 BGM | OpenGameArt「Keep your dream alive!」(congusbongus) | CC0 |
| 标题字体 | Press Start 2P (Google Fonts) | OFL |
| 主角图 | 自有素材 | — |

## 路线图

- [x] 登录界面
- [x] 主场景：荒地 + 小屋 + 自由移动
- [x] Android 版本（WebView 壳 + 离线打包脚本）
- [ ] 经营玩法（待规划）
- [ ] 小游戏战斗（待规划）
- [ ] 多主角支持（主角 sprite 已预留 hero_0~4 多姿势）
