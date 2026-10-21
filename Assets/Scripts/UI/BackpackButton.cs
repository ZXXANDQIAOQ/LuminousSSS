using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// HUD 右上角的背包按钮（v0.16）。
    ///
    /// 为什么要一个专门的组件而不是直接把 onClick 绑到别处：
    ///   UnityEvent 的持久化监听只能绑到「场景里存在的对象的实例方法」，
    ///   静态方法绑不上。所以要有一个挂在按钮上的脚本当入口。
    ///
    /// 同时兼任键盘快捷键：Tab 是这类游戏「开背包」的通用键，
    /// B 给不习惯 Tab 的人（Tab 在部分浏览器式操作里会被系统截走）。
    /// 快捷键放在这里而不是 BackpackPanel 里，是因为面板第一次打开前还不存在，
    /// 而 HUD 按钮每个场景都有 —— 玩家不必先点一次按钮才能用键盘。
    /// </summary>
    [DisallowMultipleComponent]
    public class BackpackButton : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.B))
                BackpackPanel.Toggle();
        }

        /// <summary>按钮 onClick 的持久监听入口（UnityEvent 调用，必须 public 无参）。</summary>
        public void OnClickOpen()
        {
            AudioManager.Click();
            BackpackPanel.Toggle();
        }
    }
}
