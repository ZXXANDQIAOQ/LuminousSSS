# Luminous · 微光 —— Android 版

网页游戏本体不变，这一层是 **WebView 壳 + 离线打包脚本**：
页面在 `android/assets/www/`（构建时自动从仓库根目录同步），
逻辑全在 HTML/JS 里，壳只负责横屏全屏、常亮、允许音频播放。

## 构建环境（本机已就绪）

| 组件 | 路径 | 说明 |
| ---- | ---- | ---- |
| Android SDK | `D:\App\Android\Sdk` | build-tools 34.0.0 + platforms/android-34 |
| OpenJDK | `D:\App\Android\jdk11` | 从 `E:\App\_installers\jdk11.0.14.1-1.zip` 解出 |

> 不需要 Gradle、不需要联网：`aapt2 → javac → d8 → zipalign → apksigner` 全部用 SDK 自带工具。
> 路径不同时用环境变量覆盖：`ANDROID_SDK`、`JAVA_HOME`。

## 打包

```bash
python android/build_apk.py           # 正式签名包 → dist/LuminousSSS-v0.1.apk
python android/build_apk.py --debug   # 调试密钥签名（便于反复安装）
```

流程：同步网页资源 → aapt2 编译/链接资源 → javac → d8 生成 dex → 塞进 APK → zipalign → apksigner 签名并校验。

首次构建会自动生成签名密钥 `android/keystore/luminous.jks`
（口令见 `build_apk.py` 里的 `KEY_PASS`）。**这个文件别删、别提交**——删了以后新包装不上旧包，
要用同一个 key 才能覆盖升级。

## 安装到手机

```bash
D:\App\Android\Sdk\platform-tools\adb.exe install -r dist/LuminousSSS-v0.1.apk
```

或者直接把 APK 传到手机上点安装（需允许「安装未知来源应用」）。

## 应用信息

- 包名 `com.zxxandqiaoqiao.luminoussss`，版本 0.1（versionCode 1）
- minSdk 21（Android 5.0+），targetSdk 34
- 横屏（sensorLandscape）、全屏沉浸、刘海屏铺满、游戏过程不息屏
- 支持触摸虚拟摇杆（左半屏）+ 外接键盘方向键

## 改了网页游戏后要做什么

```bash
python android/build_apk.py   # 资源同步 + 重新打包一条龙
```
