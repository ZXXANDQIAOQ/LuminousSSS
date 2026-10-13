using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 场景内玩家移动。WASD / 方向键，正俯视小人。
    /// 设计文档 4.4 / 五：本期标注为「可选」，先按最小可用实现。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        public float moveSpeed = 5f;

        [Header("朝向（水平翻转 + 动画方向）")]
        public SpriteRenderer bodyRenderer;

        [Header("动画（可选：留空则只翻转不播动画）")]
        public Animator animator;

        // AnimatorController 里的参数名
        private const string SpeedParam = "Speed";
        private const string FacingUpParam = "FacingUp";

        private Rigidbody2D _rb;
        private Vector2 _input;
        private Vector2 _lastMoveDir = Vector2.down;   // 出生时面向下方（设计文档 4.4）

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
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            _input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (_input.sqrMagnitude > 0.01f)
            {
                _input = _input.normalized;
                // 记录主轴朝向：水平优先（与翻转一致），纯垂直时记上/下供动画区分
                _lastMoveDir = Mathf.Abs(_input.x) >= Mathf.Abs(_input.y)
                    ? new Vector2(Mathf.Sign(_input.x), 0f)
                    : new Vector2(0f, Mathf.Sign(_input.y));
                if (Mathf.Abs(_input.x) > 0.01f && bodyRenderer != null)
                    bodyRenderer.flipX = _input.x < 0f;
            }

            if (animator != null)
            {
                animator.SetFloat(SpeedParam, _input.sqrMagnitude > 0.01f ? 1f : 0f);
                animator.SetBool(FacingUpParam, _lastMoveDir.y > 0f);
            }
        }

        private void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _input * (moveSpeed * Time.fixedDeltaTime));
        }
    }
}
