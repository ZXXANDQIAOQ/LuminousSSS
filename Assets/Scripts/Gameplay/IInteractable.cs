using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 「玩家能对这个世界做点什么」的统一契约。
    ///
    /// 之前 PlayerInteractor 只认 <see cref="TreeChop"/>，加门 / 楼梯 / 商店时
    /// 每加一种就得改一次扫描逻辑与 HUD 按钮。抽成接口后：
    ///   - 扫描端（PlayerInteractor）只找 IInteractable；
    ///   - 表现端（InteractButtonDriver）只读 Prompt / CanInteract；
    ///   - 新交互物只实现三个成员即可，不碰 HUD 与输入。
    ///
    /// 约定：挂在带 Collider2D 的物体上（PlayerInteractor 靠 Physics2D.OverlapCircle 找它），
    /// Collider 若是「触发区」请设 isTrigger=true —— 门/楼梯不应该挡住玩家走路。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>HUD 按钮上显示的动词（砍伐 / 进入 / 上楼 / 下楼 / 出门）。</summary>
        string Prompt { get; }

        /// <summary>现在能不能交互。false 时按钮变灰、扫描会直接跳过这个目标。</summary>
        bool CanInteract { get; }

        /// <summary>执行交互。<paramref name="who"/> 是发起者，需要玩家状态时用它。</summary>
        void Interact(PlayerInteractor who);
    }
}
