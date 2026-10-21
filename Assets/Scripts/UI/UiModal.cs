using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 模态面板的「时间冻结」唯一出口。
    ///
    /// 为什么需要它：商店和背包都会把 Time.timeScale 设成 0，但它们各自都写了
    /// 「我没开就恢复成 1」的兜底 —— 两个面板同时在场景里时，后者的 Update 会把
    /// 前者刚冻结的时间又放开（玩家在背包里数格子时摇杆能带着小人跑）。
    ///
    /// 所以恢复与否只能由「所有模态都关了吗」决定，不能各写各的。
    /// <see cref="Apply"/> 是幂等的：重复调用不会算错，不像计数式 Push/Pop 那样
    /// 一旦某次 Open 被重复触发就永久失衡。
    /// </summary>
    public static class UiModal
    {
        /// <summary>当前是否有任何模态面板在显示。</summary>
        public static bool AnyOpen => ShopPanel.IsOpen || BackpackPanel.IsOpen;

        /// <summary>按当前模态状态设置 timeScale。打开/关闭面板、以及每帧兜底都调它。</summary>
        public static void Apply()
        {
            float want = AnyOpen ? 0f : 1f;
            // 值没变就不写：避免每帧无意义地碰全局状态
            if (!Mathf.Approximately(Time.timeScale, want)) Time.timeScale = want;
        }
    }
}
