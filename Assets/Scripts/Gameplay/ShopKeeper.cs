using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 村民（摊主）：走到他跟前按交互键 / 点 HUD 按钮，打开集市交易面板。
    ///
    /// 这个类只负责"我是谁 + 打开哪个界面"，买和卖的规则都在
    /// <see cref="ShopPanel"/> 里 —— 将来集市里再摆一个只收鱼的渔夫、
    /// 或一个只卖种子的种子商，各自挂一个 ShopKeeper 换个 title 就行。
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopKeeper : MonoBehaviour, IInteractable
    {
        [Tooltip("村民名字，会出现在打招呼的提示里")]
        public string npcName = "杂货铺老板";

        [Tooltip("打开的面板标题")]
        public string shopTitle = "村口集市";

        [Tooltip("见面打招呼的那句话")]
        [TextArea] public string greeting = "有什么要买要卖的？";

        public string Prompt => "交易";

        /// <summary>面板已经打开时不再响应，避免按 E 又触发一次（会重置页签）。</summary>
        public bool CanInteract => !ShopPanel.IsOpen && isActiveAndEnabled;

        public void Interact(PlayerInteractor who)
        {
            if (ShopPanel.IsOpen) return;
            AudioManager.Confirm();
            if (!string.IsNullOrEmpty(greeting)) Toast.Show($"{npcName}：{greeting}");
            ShopPanel.Open(shopTitle);
        }
    }
}
