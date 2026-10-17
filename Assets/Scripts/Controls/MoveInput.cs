using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 移动输入的汇聚点。键盘 / 手柄与屏幕虚拟摇杆在这里合成一路，
    /// PlayerController 只读这一个入口，不关心输入来自哪种设备。
    ///
    /// 虚拟摇杆那一路由 <see cref="VirtualJoystick"/> 写入（松开或禁用时写回 zero），
    /// 所以没有触摸设备、或场景里没放摇杆时，这里恒为 zero，
    /// 行为与「改之前只有键盘」完全一致。
    /// </summary>
    public static class MoveInput
    {
        /// <summary>
        /// 虚拟摇杆当前值。x/y ∈ [-1,1]，已扣死区并做过程度整形；
        /// 手指离开摇杆时为 zero。不要直接赋值，交给 VirtualJoystick 写。
        /// </summary>
        public static Vector2 VirtualAxis { get; set; }

        /// <summary>键盘 / 手柄轴（旧 Input Manager 的 Horizontal / Vertical，取值 -1 / 0 / 1）。</summary>
        public static Vector2 KeyboardAxis => new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        /// <summary>
        /// 取本帧的移动输入：键盘与摇杆**取幅度大的那一路**。
        ///
        /// 不做相加 —— 相加会让「斜向顶到底」的模长到 1.41，归一化后单轴分量从 1 掉到 0.71，
        /// 于是斜着走反而比直着走慢，手感不一致。取大值则两者都规规矩矩是单位向量。
        /// </summary>
        public static Vector2 Read()
        {
            var kb = KeyboardAxis;
            var vj = VirtualAxis;
            return kb.sqrMagnitude >= vj.sqrMagnitude ? kb : vj;
        }

        /// <summary>清空虚拟轴。场景切换 / 摇杆被禁用时调用，避免残留值把角色带进下一个场景。</summary>
        public static void Clear() => VirtualAxis = Vector2.zero;
    }
}
