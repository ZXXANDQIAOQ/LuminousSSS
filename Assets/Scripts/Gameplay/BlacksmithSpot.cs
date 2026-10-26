using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 铁匠铺交互点（挂在 VillageScene 的铁匠铺建筑上）。
    ///
    /// v0.20 起「打铁」不再是一按就弹面板：
    ///   1) 玩家按 E → 锁住玩家输入；
    ///   2) 铁匠从铺子旁走到铁砧后面站定、面向铁砧（世界内的"人"先出场）；
    ///   3) 就位后才弹出锻造面板（面板靠右，左边留出舞台）；
    ///   4) 每次锤击由铁匠真的抡一锤，锤头落在铁砧上，命中帧才结算数值 + 火花 + 音效；
    ///   5) 关面板 → 铁匠走回铺子，解锁玩家。
    /// </summary>
    public class BlacksmithSpot : MonoBehaviour, IInteractable
    {
        [Tooltip("铁匠名称（显示在面板标题里）")]
        public string smithName = "铁匠老王";

        [Tooltip("问候语（显示在面板顶部）")]
        [TextArea(2, 3)]
        public string greeting = "来打把剑？铁矿和煤炭备好，剩下的看运气。";

        [Tooltip("场景里的铁匠 NPC。留空时运行时自动找场景里第一个 BlacksmithActor")]
        public BlacksmithActor actor;

        public string Prompt => "打铁";

        public bool CanInteract =>
            !ForgePanel.IsOpen && !ShopPanel.IsOpen && !BackpackPanel.IsOpen
            && (Actor == null || !Actor.Busy);

        /// <summary>取铁匠（未配置时按场景兜底查找，避免生成器漏配就完全不能打铁）。</summary>
        public BlacksmithActor Actor
        {
            get
            {
                if (actor == null) actor = FindObjectOfType<BlacksmithActor>();
                return actor;
            }
        }

        public void Interact(PlayerInteractor who)
        {
            var player = who != null ? who.GetComponent<PlayerController>() : null;
            var smith = Actor;

            // 没有铁匠 NPC 时退回旧行为：直接开面板，保证功能不断
            if (smith == null)
            {
                ForgePanel.Open(this);
                return;
            }

            if (player != null) player.InputLocked = true;

            // 铁匠走向铁砧（不是走向玩家）：锤子要砸在砧面上，火花才有落点
            smith.Serve(() => ForgePanel.Open(this));
        }

        /// <summary>面板关闭时由 ForgePanel 调用：解锁玩家、铁匠回家。</summary>
        public void Release()
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null) player.InputLocked = false;

            var smith = Actor;
            if (smith != null) smith.GoHome();
        }
    }
}
