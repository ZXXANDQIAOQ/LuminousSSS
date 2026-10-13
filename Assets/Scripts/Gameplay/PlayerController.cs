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

        [Header("朝向（用于后续动画，本期仅翻转）")]
        public SpriteRenderer bodyRenderer;

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
        }

        private void Update()
        {
            _input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (_input.sqrMagnitude > 0.01f)
            {
                _input = _input.normalized;
                if (Mathf.Abs(_input.x) > 0.01f)
                {
                    _lastMoveDir = new Vector2(Mathf.Sign(_input.x), 0f);
                    if (bodyRenderer != null) bodyRenderer.flipX = _input.x < 0f;
                }
            }
        }

        private void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _input * (moveSpeed * Time.fixedDeltaTime));
        }
    }
}
