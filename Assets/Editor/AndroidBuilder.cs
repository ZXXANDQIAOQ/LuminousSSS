using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 安卓调试包一键构建（菜单：Tools/LuminousSSS/出安卓调试包）。
    ///
    /// 为什么必须走编辑器菜单而不是命令行：
    ///   本机团结引擎的 -batchmode 会卡在 "Application.AssetDatabase Initial Refresh Start"，
    ///   GUI 加 -quit 又会在导入完成前退出（受限环境下许可客户端网络校验过不去），
    ///   所以构建只能由编辑器内的菜单项发起。
    ///
    /// 为什么要把这些设置写死在这里（而不是交给 Build Settings 面板）：
    ///   1) 目标机（M511CD / Android 17）只有 arm64-v8a，旧包只打了 armeabi-v7a → 根本装不上；
    ///   2) Android 上 64 位必须 IL2CPP（Mono 只有 32 位），而工程默认是 Mono；
    ///   3) 真机排错需要 Development Build + Script Debugging，才能用
    ///      `adb logcat -s Unity` 看到 Debug.Log 和异常堆栈。
    ///
    /// 构建过程会阻塞编辑器 10~30 分钟（IL2CPP 首次编译），结果写到 .workbuddy/android_build.log。
    /// </summary>
    public static class AndroidBuilder
    {
        const string OutDir = "OutPut";
        const string OutName = "LuminousSSS_arm64_dev.apk";
        const string BundleId = "com.zxxandqiaoq.luminoussss";
        const string LogPath = ".workbuddy/android_build.log";

        [MenuItem("Tools/LuminousSSS/出安卓调试包（ARM64 + IL2CPP）", false, 41)]
        public static void BuildDebugArm64()
        {
            var sb = new StringBuilder();
            void Log(string s)
            {
                sb.AppendLine(s);
                Debug.Log("[BUILD] " + s);
            }

            try
            {
                Directory.CreateDirectory(OutDir);
                Directory.CreateDirectory(".workbuddy");

                // ---------- 1. 打包设置 ----------
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

                // 横屏锁定（工程设置里已经锁了，这里再保一道，避免面板被改回去 —— HUD 按横屏布局）
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

                EditorUserBuildSettings.buildAppBundle = false;   // 出 APK，不是 AAB

                AssetDatabase.SaveAssets();

                Log($"目标架构={PlayerSettings.Android.targetArchitectures} "
                    + $"后端={PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)} "
                    + $"包名={PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");

                // ---------- 2. 场景列表（取 Build Settings 里勾选的） ----------
                var scenes = Array.FindAll(EditorBuildSettings.scenes, s => s.enabled);
                var paths = new string[scenes.Length];
                for (int i = 0; i < scenes.Length; i++) paths[i] = scenes[i].path;
                Log("场景: " + string.Join(", ", paths));

                if (paths.Length == 0)
                    throw new Exception("Build Settings 里没有勾选任何场景，构建取消");

                // ---------- 3. 构建 ----------
                string outPath = Path.Combine(OutDir, OutName);
                if (File.Exists(outPath)) File.Delete(outPath);

                var opts = new BuildPlayerOptions
                {
                    scenes = paths,
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                              | BuildOptions.AllowDebugging
                              | BuildOptions.DetailedBuildReport,
                };

                Log("开始构建 → " + Path.GetFullPath(outPath));
                File.WriteAllText(LogPath, string.Join(Environment.NewLine,
                    new[] { "[BUILD] 开始 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }) + Environment.NewLine);

                var report = BuildPipeline.BuildPlayer(opts);
                var summary = report.summary;

                Log($"结果={summary.result} 耗时={summary.totalTime} "
                    + $"大小={summary.totalSize / 1024f / 1024f:F1}MB 错误={summary.totalErrors} 警告={summary.totalWarnings}");

                if (summary.result != BuildResult.Succeeded)
                {
                    foreach (var step in report.steps)
                        foreach (var msg in step.messages)
                            if (msg.type == LogType.Error || msg.type == LogType.Exception)
                                Log($"  [{step.name}] {msg.content}");
                    Log("✗ 构建失败");
                }
                else
                {
                    Log("✓ 构建成功: " + Path.GetFullPath(outPath));
                }
            }
            catch (Exception e)
            {
                Log("✗ 异常: " + e);
            }
            finally
            {
                File.AppendAllText(LogPath, sb.ToString());
            }
        }
    }
}
