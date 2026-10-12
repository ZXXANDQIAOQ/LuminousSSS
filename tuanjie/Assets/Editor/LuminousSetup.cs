// 一键工程配置：导入设置 → 播放器设置 → 生成两个场景 → 写入打包场景列表
// 注意：本机 Tuanjie.exe -batchmode 会卡在 AssetDatabase 导入，必须用 GUI 模式调用：
//   Tuanjie.exe -projectPath <工程> -executeMethod LuminousSetup.SetupAll -quit -logFile <日志>
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LuminousSetup
{
    const string SceneDir = "Assets/Scenes";
    const string LoginScene = SceneDir + "/Login.scene";
    const string WorldScene = SceneDir + "/Wasteland.scene";

    [MenuItem("Luminous/一键初始化工程（导入设置 + 场景）", false, 10)]
    public static void SetupAll()
    {
        ConfigureTextures();
        ConfigureFonts();
        ConfigurePlayerSettings();
        CreateScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LuminousSetup] 完成：导入设置 + 播放器设置 + 场景生成");
    }

    /// <summary>
    /// 命令行入口。刻意**不带 -quit**：实测带 -quit 时编辑器会在
    /// 资源导入阶段就提前退出，执行方法根本轮不到。
    /// 由本方法自己在结束时把退出码交还给调用方。
    /// </summary>
    public static void SetupAllAndExit()
    {
        int code = 0;
        try { SetupAll(); }
        catch (System.Exception e)
        {
            Debug.LogError("[LuminousSetup] 初始化失败: " + e);
            code = 1;
        }
        EditorApplication.Exit(code);
    }

    static void ConfigureTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.isReadable = true;                 // 运行时需要用像素拼地图
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
            Debug.Log("[LuminousSetup] 纹理: " + path);
        }
    }

    static void ConfigureFonts()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { "Assets/Resources/Fonts" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (imp == null) continue;
            imp.fontTextureCase = FontTextureCase.Dynamic;   // 动态字形：任意字号、任意汉字
            imp.fontSize = 48;
            imp.includeFontData = true;
            imp.SaveAndReimport();
            Debug.Log("[LuminousSetup] 字体: " + path);
        }
    }

    static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "LuminousSSS";
        PlayerSettings.productName = "Luminous 微光";
        PlayerSettings.bundleVersion = "0.1";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.zxxandqiaoqiao.luminoussss");
        PlayerSettings.Android.bundleVersionCode = 1;

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.buildApkPerCpuArchitecture = false;
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_0);

        // 横屏
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        PlayerSettings.Android.renderOutsideSafeArea = true;   // 刘海屏铺满
        PlayerSettings.Android.forceInternetPermission = false;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.runInBackground = true;

        EditorUserBuildSettings.buildAppBundle = false;        // 出 APK 而不是 AAB
        Debug.Log("[LuminousSetup] 播放器设置完成（IL2CPP / ARMv7+ARM64 / 横屏 / 不联网）");
    }

    static void CreateScenes()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        // —— 登录场景 ——
        var login = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera("LoginCamera");
        new GameObject("SceneBuilder", typeof(LoginSceneBuilder));
        bool ok1 = EditorSceneManager.SaveScene(login, LoginScene);
        Debug.Log("[LuminousSetup] 保存登录场景: " + ok1 + " → " + LoginScene);

        // —— 荒原场景 ——
        var world = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera("MainCamera");
        new GameObject("SceneBuilder", typeof(WastelandSceneBuilder));
        bool ok2 = EditorSceneManager.SaveScene(world, WorldScene);
        Debug.Log("[LuminousSetup] 保存荒原场景: " + ok2 + " → " + WorldScene);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(LoginScene, true),
            new EditorBuildSettingsScene(WorldScene, true),
        };
        Debug.Log("[LuminousSetup] 场景已写入 Build Settings");
    }

    static void CreateCamera(string name)
    {
        var camGo = new GameObject(name, typeof(Camera), typeof(AudioListener));
        if (name == "MainCamera") camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = GameConfig.CamOrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.04f, 0.08f);
        camGo.transform.position = new Vector3(32f, 24f, -10f);
    }
}
