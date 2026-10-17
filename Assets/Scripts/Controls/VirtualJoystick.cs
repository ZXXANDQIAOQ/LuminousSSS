using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 屏幕虚拟摇杆：按住 / 拖动 → 把方向写进 <see cref="MoveInput.VirtualAxis"/>。
    ///
    /// 职责边界：只做「指针事件 → 方向向量」，不碰玩家、不碰相机、不碰动画。
    /// 以后要换成 D-Pad 或外接手柄，只要同样写 MoveInput，PlayerController 一行都不用改。
    ///
    /// 挂载方式（由 GameSceneBuilder.BuildFarmHud 生成）：
    ///   Joystick        触摸热区，[全透明 Image(raycastTarget=true)] + 本组件
    ///   ├─ Base        底圈，raycastTarget = false
    ///   └─ Knob        摇杆头，raycastTarget = false，拖动时改 anchoredPosition
    ///
    /// 为什么热区要比底圈大：手指落在底圈外一点点也算「在推摇杆」，
    /// 否则玩家得瞄着圆环按，手机上很难受。热区是隐形的大方块，视觉上不可见。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("视觉")]
        [Tooltip("摇杆头。留空则只处理输入、不移动任何东西。")]
        public RectTransform knob;

        [Header("手感")]
        [Tooltip("底圈半径（UI 像素）。摇杆头的可用行程 = radius - knobRadius。")]
        public float radius = 144f;

        [Tooltip("摇杆头半径（UI 像素）。")]
        public float knobRadius = 64f;

        [Range(0f, 0.9f)]
        [Tooltip("死区：推动幅度低于该比例时视为不动，避免手指微抖就走起来。")]
        public float deadZone = 0.18f;

        [Header("平台")]
        [Tooltip("勾选后：非触屏设备（PC / 编辑器）自动隐藏，只保留键盘。" +
                 "默认不勾，方便在编辑器里直接用鼠标拖动验证。")]
        public bool hideOnNonTouchDevice;

        private RectTransform _area;
        private Vector2 _value;
        private int? _pointerId;   // 只跟第一根按下的手指，第二根手指不抢

        /// <summary>当前输出（-1..1）。未按住时为 zero。</summary>
        public Vector2 Value => _value;

        private void Awake()
        {
            _area = (RectTransform)transform;

            if (hideOnNonTouchDevice && !Application.isMobilePlatform && !Input.touchSupported)
            {
                gameObject.SetActive(false);
                return;
            }
            ResetKnob();
        }

        private void OnDisable()
        {
            // 禁用 / 切场景时必须松开：否则残留的 VirtualAxis 会让角色一直自己走
            Release();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId.HasValue && _pointerId.Value != e.pointerId) return;
            _pointerId = e.pointerId;
            Track(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_pointerId.HasValue || _pointerId.Value != e.pointerId) return;
            Track(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_pointerId.HasValue || _pointerId.Value != e.pointerId) return;
            _pointerId = null;
            Release();
        }

        /// <summary>把一次指针位置换算成方向向量，并同步摇杆头位置。</summary>
        private void Track(PointerEventData e)
        {
            // 热区轴心在 (0.5,0.5)，所以 local 就是「相对底圈圆心的偏移」
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _area, e.position, e.pressEventCamera, out var local))
                return;

            float travel = Mathf.Max(1f, radius - knobRadius);
            float dist = local.magnitude;
            Vector2 dir = dist > 0.001f ? local / dist : Vector2.zero;

            // 死区后线性重映射：跨过死区时从 0 平滑起速，不会「啪」一下跳到满速
            float mag = dist / travel;
            float shaped = mag <= deadZone ? 0f : Mathf.Clamp01((mag - deadZone) / (1f - deadZone));

            _value = dir * shaped;
            MoveInput.VirtualAxis = _value;

            if (knob != null)
                knob.anchoredPosition = dir * Mathf.Min(dist, travel);
        }

        private void Release()
        {
            _pointerId = null;
            _value = Vector2.zero;
            MoveInput.VirtualAxis = Vector2.zero;
            ResetKnob();
        }

        private void ResetKnob()
        {
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }
    }
}
