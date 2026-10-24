using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 铁匠铺交互点（挂在 VillageScene 的铁匠铺建筑上）。
    /// 实现 <see cref="IInteractable"/>，玩家走近后按 E 打开锻造面板。
    /// </summary>
    public class BlacksmithSpot : MonoBehaviour, IInteractable
    {
        [Tooltip("铁匠名称（显示在面板标题里）")]
        public string smithName = "铁匠老王";

        [Tooltip("问候语（显示在面板顶部）")]
        [TextArea(2, 3)]
        public string greeting = "来打把剑？铁矿和煤炭备好，剩下的看运气。";

        public string Prompt => "打铁";

        public bool CanInteract => !ForgePanel.IsOpen && !ShopPanel.IsOpen && !BackpackPanel.IsOpen;

        public void Interact(PlayerInteractor who)
        {
            ForgePanel.Open(this);
        }
    }
}
