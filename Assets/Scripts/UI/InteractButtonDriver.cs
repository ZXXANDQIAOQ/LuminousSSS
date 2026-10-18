using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// HUD 交互按钮驱动：跟随 <see cref="PlayerInteractor.Current"/> 亮/灰、改文案、转发点击。
    ///
    /// 一个按钮覆盖所有交互（砍树 / 进屋 / 上楼 / 下楼 / 出门）：
    /// 按钮文字直接用目标自己的 Prompt，玩家不用记「这个场景里按钮是干嘛的」——
    /// 站在树前写「砍伐」，站在门口写「进入」。
    ///
    /// 持久点击监听由 GameSceneBuilder 用 UnityEventTools 写入 m_OnClick.m_PersistentCalls，
    /// 运行时 AddListener 不会被序列化，存盘后就没了。
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class InteractButtonDriver : MonoBehaviour
    {
        [Tooltip("按钮上的文字（留空则自动找子物体里名为 Label 的 Text）")]
        public Text label;

        private Button _btn;
        private PlayerInteractor _pi;
        private float _next;
        private string _shown;

        private void Awake()
        {
            _btn = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<Text>();
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.1f;   // 与 PlayerInteractor 的扫描同频，避免各刷各的

            if (_pi == null)
                _pi = FindObjectOfType<PlayerInteractor>();   // 每个场景只有一个玩家，找到即缓存
            if (_pi == null) return;

            bool hasTarget = _pi.Current != null && _pi.Current.CanInteract;
            if (_btn.interactable != hasTarget)
                _btn.interactable = hasTarget;                // disabledColor 让按钮变半透明

            // 只在文案真的变了才赋值：每帧写 Text.text 会持续触发重建（GC + 网格重算）
            string want = hasTarget ? _pi.Current.Prompt : _pi.idlePrompt;
            if (label != null && _shown != want)
            {
                label.text = want;
                _shown = want;
            }
        }

        /// <summary>按钮 onClick 的持久监听入口（UnityEvent 调用，必须 public 无参）。</summary>
        public void OnClickInteract()
        {
            if (_pi == null)
                _pi = FindObjectOfType<PlayerInteractor>();
            _pi?.Interact();
        }
    }
}
