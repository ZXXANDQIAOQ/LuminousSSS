using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// HUD「砍伐」按钮驱动：根据 <see cref="PlayerInteractor.CurrentTarget"/> 亮 / 灰，
    /// 并把点击转成砍击。没有可砍目标时按钮不可点（Button 的 disabledColor 变半透明）——
    /// 玩家不用记住「现在能不能砍」，看一眼按钮就知道。
    ///
    /// 持久点击监听由 GameSceneBuilder 用 UnityEventTools.RegisterPersistentListener 写入，
    /// 场景保存后 onClick 依然有效（运行时 AddListener 不会被序列化）。
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class ChopButtonDriver : MonoBehaviour
    {
        private Button _btn;
        private PlayerInteractor _pi;
        private float _next;

        private void Awake()
        {
            _btn = GetComponent<Button>();
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.1f;   // 与 PlayerInteractor 的扫描同频，避免各刷各的

            if (_pi == null)
                _pi = FindObjectOfType<PlayerInteractor>();   // 农场场景只有一个玩家，找到即缓存
            if (_pi == null) return;

            bool hasTarget = _pi.CurrentTarget != null;
            if (_btn.interactable != hasTarget)
                _btn.interactable = hasTarget;
        }

        /// <summary>按钮 onClick 的持久监听入口（UnityEvent 调用，必须 public 无参）。</summary>
        public void OnClickChop()
        {
            if (_pi == null)
                _pi = FindObjectOfType<PlayerInteractor>();
            _pi?.Interact();
        }
    }
}
