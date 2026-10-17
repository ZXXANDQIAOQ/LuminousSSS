using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 打开工程时自动检查游戏场景是否已生成；没生成就自动补建，
    /// 让「打开工程 → 点播放」就能看到登录界面，不用记得点菜单。
    ///
    /// v0.12：改为分别检查两个场景、缺哪个补哪个 —— 老逻辑是「LoginScene 在就全跳过」，
    /// 农场场景更新后已存在的旧 FarmScene 不会重建，玩家进游戏看到的还是老布局。
    /// 补建农场走 GameSceneBuilder.BuildFarmSceneOnly()，绝不覆盖已存在的 LoginScene
    /// （保护手动改动）。想要最新农场布局也可以随时手动点
    /// Tools/LuminousSSS/2. 生成游戏场景（会重建两个场景）。
    /// </summary>
    [InitializeOnLoad]
    public static class AutoBuildScenes
    {
        const string LoginScenePath = "Assets/Scenes/LoginScene.scene";
        const string FarmScenePath = "Assets/Scenes/FarmScene.scene";
        const string GrassTilePath = "Assets/Art/V2/Tiles/fill_grass_a.png";

        static bool s_tried;

        static AutoBuildScenes()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (s_tried) return;

            bool needLogin = !File.Exists(LoginScenePath);
            bool needFarm = !File.Exists(FarmScenePath);
            if (!needLogin && !needFarm) { s_tried = true; return; }

            // 等编译 / 资源导入都结束（fill 瓦片已能作为 Texture 加载）再动手，
            // 否则首次打开工程时 AssetDatabase 还没就绪会生成出空场景。
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTilePath) == null)
            {
                EditorApplication.delayCall += Run; // 这次没就绪，下次空闲再试
                return;
            }

            s_tried = true;

            if (needLogin && needFarm)
            {
                // 两个都没有：一键全建（首启路径，与旧行为一致）
                Debug.Log("[LuminousSSS] 检测到两个游戏场景都没生成，自动执行一键生成（登录界面 + 农场场景）……");
                GameSceneBuilder.OneClick();
                return;
            }

            // 缺哪个补哪个，绝不重建已存在的场景
            if (needFarm)
            {
                Debug.Log("[LuminousSSS] 检测到农场场景缺失，自动补建 FarmScene（登录场景未动）……");
                GameSceneBuilder.BuildFarmSceneOnly();
            }
            else
            {
                // 走 BuildLoginSceneOnly，不能用 OneClick —— 后者是 BuildAll，
                // 会把已存在的 FarmScene 一起重建掉。
                Debug.Log("[LuminousSSS] 检测到登录场景缺失，自动补建 LoginScene（农场场景未动）……");
                GameSceneBuilder.BuildLoginSceneOnly();
            }
        }
    }
}
