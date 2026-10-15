using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 场景内玩家移动。WASD / 方向键，正俯视小人。
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
            _input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (_input.sqrMagnitude > 0.01f)
            {
                _input = _input.normalized;
                // 记录主轴朝向：水平优先（与旧版 flipX 规则一致），纯垂直时记上/下
                _facing = Mathf.Abs(_input.x) >= Mathf.Abs(_input.y)
                    ? new Vector2(Mathf.Sign(_input.x), 0f)
                    : new Vector2(0f, Mathf.Sign(_input.y));
            }
        }

        private void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _input * (moveSpeed * Time.fixedDeltaTime));
        }
    }
}
