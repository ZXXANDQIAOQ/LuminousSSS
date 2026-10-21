using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 运行时 UI 需要的那几张底图（面板 / 按钮 / 格子 / 金币盘 / 金币图标）。
    ///
    /// 为什么单独做个资产而不是 Resources.Load 直接读贴图：
    ///   素材放在 Assets/Art/V2/UI 下（受 PixelArtImporter 的 PPU 规则管理），
    ///   而 Resources.Load 只能读 Assets/Resources 里的东西。把美术复制一份到
    ///   Resources 会让同一张图有两个 guid、两份导入设置，改图时必然漏一处。
    ///   这里只存「引用」，图仍然只有一份。
    ///
    /// 由 GameSceneBuilder.EnsureItemDatabase() 生成并填引用；重跑菜单会重建。
    /// </summary>
    public class UiTheme : ScriptableObject
    {
        public const string ResourcePath = "UiTheme";

        [Tooltip("主面板底图（九宫格拉伸）")]
        public Sprite panel;

        [Tooltip("木纹按钮底图")]
        public Sprite button;

        [Tooltip("物品行底板")]
        public Sprite slot;

        [Tooltip("金币数量盘")]
        public Sprite goldPlate;

        [Tooltip("金币图标")]
        public Sprite coinIcon;

        static UiTheme _cached;

        public static UiTheme Instance
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<UiTheme>(ResourcePath);
                return _cached;
            }
        }
    }
}
