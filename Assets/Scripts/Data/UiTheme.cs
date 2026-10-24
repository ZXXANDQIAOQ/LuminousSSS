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

        [Tooltip("背包面板底图（比主面板更厚重的一张，九宫格拉伸）")]
        public Sprite panelInventory;

        [Tooltip("空物品格底图（背包空格 / 商店行共用以外的地方）")]
        public Sprite slotEmpty;

        [Tooltip("物品格选中态底图（金色描边）")]
        public Sprite slotSelected;

        [Tooltip("详情窗口底图")]
        public Sprite tooltipPanel;

        [Tooltip("HUD 背包按钮上的布袋图标")]
        public Sprite bagIcon;

        [Tooltip("面板标题栏底图")]
        public Sprite titleBar;

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
