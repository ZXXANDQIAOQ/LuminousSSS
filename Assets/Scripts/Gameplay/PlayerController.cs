using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 场景内玩家移动。输入来自 <see cref="MoveInput"/>：键盘 WASD / 方向键，
    /// 以及手机端的屏幕虚拟摇杆（<see cref="VirtualJoystick"/>），正俯视小人。
    /// 只负责「输入 → 移动 → 记录朝向」；朝向的视觉表现（Animator 参数 /
    /// flipX / 走路小跳）全部交给 CharacterAnimDriver，两边通过公开属性通信。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        public float moveSpeed = 5f;

        private Rigidbody2D _rb;
        private Vector2 _input;
        private Vector2 _facing = Vector2.down;   // 出生时面向下方（设计文档 4.4）

        /// <summary>当前朝向：主轴单位向量（上/下/左/右之一，水平优先）。供动画组件读取。</summary>
        public Vector2 Facing => _facing;

        /// <summary>是否正在移动（有输入且已归一化）。</summary>
        public bool IsMoving => _input.sqrMagnitude > 0.01f;

        /// <summary>
        /// 直接设定朝向（传送落地时用：进屋后面朝里、下楼后面朝楼梯外）。
        /// 只写朝向，不碰输入 —— 下一帧有输入时照常被覆盖。
        /// </summary>
        public void SetFacing(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            _facing = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)
                ? new Vector2(Mathf.Sign(dir.x), 0f)
                : new Vector2(0f, Mathf.Sign(dir.y));
        }

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Update()
        {
            // 键盘 / 手柄 / 屏幕摇杆的统一入口；摇杆那一路没有设备时恒为 zero
            Vector2 input = MoveInput.Read();

            // 定速行走：只要推过输入源自己的死区就按满速走。
            // 阈值取 0.0001（幅度 0.01）而不是原来的 0.01（幅度 0.1）——
            // 对键盘两者完全等价（轴值只有 -1/0/1），但对摇杆这种连续输入，
            // 旧阈值会把「刚过死区」的一段变成 0~10% 速度的爬行区，手感发黏。
            // 摇杆的死区在 VirtualJoystick 里（deadZone = 0.18），到这一步非零即有效。
            //
            // 不做「摇杆推多远走多快」的变速：行走动画是定帧的，
            // 位移变速会让脚步和地面打滑对不上，看起来像滑步。
            if (input.sqrMagnitude > 0.0001f)
            {
                _input = input.normalized;

                // 记录主轴朝向：水平优先（与旧版 flipX 规则一致），纯垂直时记上/下
                _facing = Mathf.Abs(_input.x) >= Mathf.Abs(_input.y)
                    ? new Vector2(Mathf.Sign(_input.x), 0f)
                    : new Vector2(0f, Mathf.Sign(_input.y));
            }
            else
            {
                // 旧代码在阈值以下不写 _input，键盘下恰好是 zero 所以看不出问题；
                // 摇杆是连续量，必须显式清零，否则会留一个极小速度让角色缓缓爬。
                _input = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _input * (moveSpeed * Time.fixedDeltaTime));
        }
    }
}
