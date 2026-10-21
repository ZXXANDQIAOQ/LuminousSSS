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
        const string House1FPath = "Assets/Scenes/House1FScene.scene";
        const string House2FPath = "Assets/Scenes/House2FScene.scene";
        const string VillageScenePath = "Assets/Scenes/VillageScene.scene";
        const string ItemDbPath = "Assets/Resources/ItemDB.asset";
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
            bool needHouse = !File.Exists(House1FPath) || !File.Exists(House2FPath);
            bool needVillage = !File.Exists(VillageScenePath);
            bool needDb = !File.Exists(ItemDbPath);       // v0.15：商店 / 播种 / 钓鱼都靠这张表
            if (!needLogin && !needFarm && !needHouse && !needVillage && !needDb) { s_tried = true; return; }

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
                // 两个主场景都没有：一键全建（首启路径，含室内与集市 —— OneClick 走 BuildAll）
                Debug.Log("[LuminousSSS] 检测到主场景都没生成，自动执行一键生成（登录 + 农场 + 小洋楼 + 集市）……");
                GameSceneBuilder.OneClick();
                return;
            }

            // 物品表缺失（老工程升级上来时会碰到）：只补表，不动任何场景
            if (needDb)
            {
                Debug.Log("[LuminousSSS] 检测到物品表缺失，补建 Assets/Resources/ItemDB.asset……");
                GameSceneBuilder.EnsureItemDatabase();
            }

            // 缺哪个补哪个，绝不重建已存在的场景
            if (needFarm)
            {
                Debug.Log("[LuminousSSS] 检测到农场场景缺失，自动补建 FarmScene（其他场景未动）……");
                GameSceneBuilder.BuildFarmSceneOnly();
            }

            if (needLogin)
            {
                Debug.Log("[LuminousSSS] 检测到登录场景缺失，自动补建 LoginScene（其他场景未动）……");
                GameSceneBuilder.BuildLoginSceneOnly();
            }

            if (needHouse)
            {
                Debug.Log("[LuminousSSS] 检测到小洋楼场景缺失，自动补建 House1FScene / House2FScene……");
                GameSceneBuilder.BuildHouseScenesOnly();
            }

            if (needVillage)
            {
                Debug.Log("[LuminousSSS] 检测到集市场景缺失，自动补建 VillageScene（其他场景未动）……");
                GameSceneBuilder.BuildVillageSceneOnly();
            }
        }
    }
}
