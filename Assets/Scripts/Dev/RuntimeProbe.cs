#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 无界面环境下跑运行时验证的入口（v0.16）。
    ///
    /// 为什么放在 Assets/Scripts 而不是 Assets/Editor：
    ///   Editor 目录下的 MonoBehaviour **不能被 AddComponent**
    ///   （引擎报 "it is an editor script. To attach a script it needs to be outside the 'Editor' folder"）。
    ///   整个文件用 #if UNITY_EDITOR 包住，打包时会被剔除，不会进 APK。
    ///
    /// 本机 GUI 编辑器起不来（进程会在 [LAYOUT] 阶段被回收），
    /// 所以「面板开不开、格子几个、播种扣的是不是选中的种子」这类验证只能走这条路。
    ///
    /// 用法（**不加 -quit**，否则进程会在进 Play 前退出）：
    ///   Tuanjie.exe -batchmode -nographics -projectPath &lt;项目&gt; \
    ///     -executeMethod LuminousSSS.RuntimeProbe.RunBackpackProbe \
    ///     -logFile Logs/probe.log
    /// </summary>
    public static class RuntimeProbe
    {
        public static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string FlagPath => Path.Combine(ProjectRoot, "Logs/probe_backpack.flag");
        public static string ReportPath => Path.Combine(ProjectRoot, "Logs/probe_backpack.txt");

        /// <summary>batchmode 入口：切场景 → 写 flag → 进 Play。</summary>
        internal static void RunBackpackProbe()
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Logs"));

            // 必须在进 Play 之前把场景切好：Play 从当前编辑场景启动
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/FarmScene.scene");

            File.WriteAllText(FlagPath, "run");
            Debug.Log("[Probe] 进入 Play 模式，开始背包系统验证");
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
