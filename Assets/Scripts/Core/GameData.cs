using UnityEngine;

namespace LuminousSSS
{
    /// <summary>全局常量与存档键。场景名与设计文档一一对应。</summary>
    public static class GameData
    {
        public const string SceneLogin = "LoginScene";
        public const string SceneFarm = "FarmScene";

        /// <summary>已选角色编号（001 / 002 / 003）在 PlayerPrefs 里的键。</summary>
        public const string PrefCharacterId = "LuminousSSS.CharacterId";

        public const string DefaultCharacterId = "001";

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
