using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// HUD 左上角的金币显示：金币图标 + 数字。
    ///
    /// 订阅 <see cref="Inventory.Changed"/> 而不是每帧读 —— 金币只在买卖/收获时变，
    /// 每帧给 Text 赋字符串会持续触发文本网格重建（手机上是很实在的开销）。
    /// </summary>
    public class CoinHud : MonoBehaviour
    {
        public Text label;

        private void Start()
        {
            Inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            // 场景切换时 HUD 会被销毁；不注销就会在静态事件里留下死引用
            Inventory.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (label != null) label.text = Inventory.Gold.ToString();
        }
    }
}
