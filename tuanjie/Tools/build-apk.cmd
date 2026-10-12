@echo off
setlocal

rem ============================================================
rem  LuminousSSS —— 团结引擎一键出 Android APK
rem
rem  用法：双击本文件（或命令行 build-apk.cmd --quiet）
rem  产物：<工程>\Build\Android\LuminousSSS-tuanjie.apk
rem
rem  过程中会短暂弹出团结引擎编辑器窗口（正常，跑完自动退出）
rem
rem  为什么不用 -batchmode？
rem    本机编辑器在 -batchmode 下会卡在
rem    "Application.AssetDatabase Initial Refresh Start" 不再前进，
rem    加 -quit 又会在资源导入完成前就把编辑器关掉。
rem    所以：GUI 模式 + -executeMethod，并且**不带 -quit**，
rem    由 C# 方法自己调 EditorApplication.Exit 把退出码交回来。
rem ============================================================

set "PROJ=%~dp0.."
for %%I in ("%PROJ%") do set "PROJ=%%~fI"

if not defined TUANJIE_EDITOR set "TUANJIE_EDITOR=E:\App\GameWorker\Editor\2022.3.62t16\Editor\Tuanjie.exe"

if not exist "%TUANJIE_EDITOR%" (
  echo [错误] 找不到团结引擎编辑器：
  echo        %TUANJIE_EDITOR%
  echo        请设环境变量 TUANJIE_EDITOR 指向 Tuanjie.exe
  if /i not "%~1"=="--quiet" pause
  exit /b 1
)

set "LOGDIR=%PROJ%\Build"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"

rem 代理变量会让 Gradle/MSBuild 报错，先清掉
set "HTTP_PROXY="
set "HTTPS_PROXY="
set "http_proxy="
set "https_proxy="

rem 上一次异常退出可能留下锁文件挡住启动
del /q "%PROJ%\Library\ArtifactDB-lock" 2>nul
del /q "%PROJ%\Library\SourceAssetDB-lock" 2>nul

echo ============================================================
echo  [1/2] 初始化工程（导入设置 + 生成场景）
echo ============================================================
"%TUANJIE_EDITOR%" -projectPath "%PROJ%" ^
  -executeMethod LuminousSetup.SetupAllAndExit ^
  -nosplash -logFile "%LOGDIR%\setup.log"
echo 退出码：%ERRORLEVEL%

echo.
echo ============================================================
echo  [2/2] 打包 Android APK（IL2CPP / ARMv7+ARM64）
echo ============================================================
"%TUANJIE_EDITOR%" -projectPath "%PROJ%" ^
  -executeMethod LuminousBuild.BuildFromCommandLine ^
  -nosplash -logFile "%LOGDIR%\android-build.log"
set "RC=%ERRORLEVEL%"
echo 退出码：%RC%

set "APK=%PROJ%\Build\Android\LuminousSSS-tuanjie.apk"
echo.
if exist "%APK%" (
  echo [成功] APK 已产出：
  echo        %APK%
  for %%F in ("%APK%") do echo        大小：%%~zF 字节
  echo.
  echo 装到手机（开 USB 调试后）：
  echo        adb install -r "%APK%"
) else (
  echo [失败] 没有产出 APK。日志：
  echo        %LOGDIR%\setup.log
  echo        %LOGDIR%\android-build.log
  echo        提示：也可以在编辑器里点菜单 Luminous ^> 打包 Android APK
)

if /i not "%~1"=="--quiet" pause
endlocal
