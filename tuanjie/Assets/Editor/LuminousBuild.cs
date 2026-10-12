// 打 Android APK
// 本机必须用 GUI 模式且**不要加 -quit**（-quit 会在资源导入阶段就把编辑器关掉）：
//   Tuanjie.exe -projectPath <工程> -executeMethod LuminousBuild.BuildFromCommandLine -nosplash -logFile <日志>
// 编辑器菜单也可直接跑：Luminous → 打包 Android APK
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class LuminousBuild
{
    const string OutputDir = "Build/Android";
    const string ApkName = "LuminousSSS-tuanjie.apk";
    const string BundleId = "com.zxxandqiaoqiao.luminoussss";

    /// <summary>菜单入口：打包 IL2CPP 双架构包（打包完不会关编辑器）</summary>
    [MenuItem("Luminous/打包 Android APK（IL2CPP 双架构）", false, 40)]
    public static void BuildAndroid()
    {
        try
        {
            string apk = Build(ScriptingImplementation.IL2CPP);
            EditorUtility.RevealInFinder(apk);
        }
        catch (Exception e)
        {
            Debug.LogError("[LuminousBuild] 打包失败: " + e);
        }
    }

    /// <summary>菜单入口：Mono + ARMv7 快速出包（只覆盖 32 位设备）</summary>
    [MenuItem("Luminous/打包 Android APK（快速版 · Mono/ARMv7）", false, 41)]
    public static void BuildAndroidFast()
    {
        try
        {
            string apk = Build(ScriptingImplementation.Mono2x);
            EditorUtility.RevealInFinder(apk);
        }
        catch (Exception e)
        {
            Debug.LogError("[LuminousBuild] 快速打包失败: " + e);
        }
    }

    /// <summary>命令行入口：IL2CPP 双架构，结束把退出码交还调用方</summary>
    public static void BuildFromCommandLine()
    {
        int code = 0;
        try { Build(ScriptingImplementation.IL2CPP); }
        catch (Exception e)
        {
            Debug.LogError("[LuminousBuild] 打包失败: " + e);
            code = 1;
        }
        EditorApplication.Exit(code);
    }

    /// <summary>命令行入口：Mono/ARMv7 快速版</summary>
    public static void BuildFastFromCommandLine()
    {
        int code = 0;
        try { Build(ScriptingImplementation.Mono2x); }
        catch (Exception e)
        {
            Debug.LogError("[LuminousBuild] 快速打包失败: " + e);
            code = 1;
        }
        EditorApplication.Exit(code);
    }

    public static string Build(ScriptingImplementation backend)
    {
        Debug.Log("[LuminousBuild] ===== 开始打包 =====");
        Debug.Log("[LuminousBuild] 编辑器: " + Application.unityVersion + "，后端: " + backend);

        EnsureAndroidTarget();
        ApplySettings(backend);

        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outDir = Path.Combine(root, OutputDir);
        Directory.CreateDirectory(outDir);
        string apk = Path.Combine(outDir, ApkName);
        if (File.Exists(apk)) File.Delete(apk);   // 免得旧的包冒充新结果

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
            throw new Exception("Build Settings 里没有启用的场景，先跑 LuminousSetup.SetupAll");
        Debug.Log("[LuminousBuild] 场景: " + string.Join(", ", scenes));
        Debug.Log("[LuminousBuild] 包名: " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        Debug.Log("[LuminousBuild] 架构: " + PlayerSettings.Android.targetArchitectures);
        Debug.Log("[LuminousBuild] 输出: " + apk);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        if (s.result != BuildResult.Succeeded)
            throw new Exception("BuildPipeline 返回 " + s.result + "，往上翻日志找第一条 error");

        Debug.Log("[LuminousBuild] ===== 打包成功 =====");
        Debug.Log(string.Format("[LuminousBuild] APK: {0}（{1:F1} MB，{2:F0}s）",
            apk, s.totalSize / 1024.0 / 1024.0, s.totalTime.TotalSeconds));
        return apk;
    }

    static void EnsureAndroidTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return;
        Debug.Log("[LuminousBuild] 切换平台 → Android（当前 " + EditorUserBuildSettings.activeBuildTarget + "）");
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            throw new Exception("切换 Android 平台失败：检查 Android 模块是否装全");
    }

    static void ApplySettings(ScriptingImplementation backend)
    {
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.bundleVersion = "0.1";

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
        if (backend == ScriptingImplementation.IL2CPP)
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
        }
        else
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
        }

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        PlayerSettings.Android.forceInternetPermission = false;
        PlayerSettings.Android.useCustomKeystore = false;   // 走 Unity 自动 debug 签名
        AssetDatabase.SaveAssets();
    }
}
