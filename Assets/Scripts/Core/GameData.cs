using UnityEngine;

namespace LuminousSSS
{
    /// <summary>全局常量与存档键。场景名与设计文档一一对应。</summary>
    public static class GameData
    {
        public const string SceneLogin = "LoginScene";
        public const string SceneFarm = "FarmScene";
        public const string SceneHouse1F = "House1FScene";   // 小洋楼一层（客厅 / 厨房）
        public const string SceneHouse2F = "House2FScene";   // 小洋楼二层（卧室 / 书房）
        public const string SceneVillage = "VillageScene";   // 村口集市（沿农场土路往下走）

        // ---- 出生点 id（PlayerSpawnPoint.id）
        /// <summary>默认落地：农场门口 / 一楼大门口。</summary>
        public const string SpawnDefault = "Default";

        /// <summary>楼梯口：从一楼上去落在二楼楼梯口，从二楼下来落在一楼楼梯口。</summary>
        public const string SpawnStairs = "Stairs";

        /// <summary>农场木屋门口：从屋里出来落在这里。</summary>
        public const string SpawnDoor = "Door";

        /// <summary>农场土路底部：从集市回农场时落在这里（面朝上，正好往家走）。</summary>
        public const string SpawnVillage = "Village";

        /// <summary>集市入村路口：从农场过来时落在集市的这条路上（面朝上，往村里走）。</summary>
        public const string SpawnRoad = "Road";

        /// <summary>已选角色编号（004 女巫 / 005 银骑士 / 006 森之猎手 / 007 炼金术士）在 PlayerPrefs 里的键。</summary>
        public const string PrefCharacterId = "LuminousSSS.CharacterId";

        /// <summary>首次进入的默认角色：女巫（唯一有专属 idle 呼吸帧的角色）</summary>
        public const string DefaultCharacterId = "004";

        public static string SelectedCharacterId
        {
            get => PlayerPrefs.GetString(PrefCharacterId, DefaultCharacterId);
            set
            {
                PlayerPrefs.SetString(PrefCharacterId, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>返回一个能正常显示中文的动态字体（跨平台挑第一个可用的）。</summary>
        public static Font GetUIFont()
        {
            string[] candidates =
            {
                "Microsoft YaHei", "微软雅黑", "SimHei", "黑体",
                "Noto Sans CJK SC", "Source Han Sans SC",
                "PingFang SC", "Droid Sans Fallback", "Arial Unicode MS"
            };
            var f = Font.CreateDynamicFontFromOSFont(candidates, 32);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }
    }

    /// <summary>一个可选角色的完整信息（立绘 + 场景内小人 + 待机动画帧）。</summary>
    [System.Serializable]
    public class CharacterInfo
    {
        public string id = "000";
        public string displayName = "无名";
        public Sprite portrait;      // 登录界面立绘
        public Sprite worldSprite;   // 场景内的小像素人
        public Sprite[] idleFrames;  // 待机动画帧（多帧则轮播；为空则立绘 + 浮动）

        /// <summary>
        /// 立绘显示缩放，只影响登录界面的展示大小，不改变素材本身。
        /// 人形立绘（高 1024）填 1；Q 版小像素素材（如女巫 54×101）远小于人形立绘，
        /// 放进同一个 260×512 容器会被 preserveAspect 放大到几乎和人形立绘一样大，
        /// 需要用 &lt;1 的值压回来，让两种素材在版式里体量协调。
        /// </summary>
        public float displayScale = 1f;
    }
}
