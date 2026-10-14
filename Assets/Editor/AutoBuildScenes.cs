using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 打开工程时自动检查游戏场景是否已生成；没生成就自动跑一键生成，
    /// 让「打开工程 → 点播放」就能看到登录界面，不用记得点菜单。
    /// LoginScene 已存在时什么都不做（保护手动改动，绝不覆盖）。
    /// </summary>
    [InitializeOnLoad]
    public static class AutoBuildScenes
    {
        const string LoginScenePath = "Assets/Scenes/LoginScene.scene";
        const string GrassTilePath = "Assets/Art/V2/Tiles/fill_grass_a.png";

        static bool s_tried;

        static AutoBuildScenes()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (s_tried) return;
            if (File.Exists(LoginScenePath)) { s_tried = true; return; }

            // 等编译 / 资源导入都结束（fill 瓦片已能作为 Texture 加载）再动手，
            // 否则首次打开工程时 AssetDatabase 还没就绪会生成出空场景。
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTilePath) == null)
            {
                EditorApplication.delayCall += Run; // 这次没就绪，下次空闲再试
                return;
            }

            s_tried = true;
            Debug.Log("[LuminousSSS] 检测到游戏场景还没生成，自动执行一键生成（登录界面 + 农场场景）……");
            GameSceneBuilder.OneClick();
        }
    }
}
