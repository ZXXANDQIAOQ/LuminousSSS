using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 角色动画驱动（挂在玩家根物体上，PlayerAppearance 之后生效）：
    ///
    /// 1) 有 AnimatorController 的角色（女巫）：每帧把 PlayerController 的朝向写进
    ///    Animator 参数（Speed / MoveX / MoveY），由四方向状态机播放对应行走动画，
    ///    停下自动回 Idle。左右有专门帧，不翻转。
    ///
    /// 2) 没有动画素材的角色（农夫/雨女/冒险者等静态立绘）：
    ///    flipX 转向 + 走动时身体上下小跳（bob），停下平滑回位，
    ///    消除「双脚不动纯滑步」的感觉。bob 改的是 Visual 子物体的
    ///    localPosition，不影响根物体上的物理移动。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class CharacterAnimDriver : MonoBehaviour
    {
        [Header("走路小跳（无动画素材角色的占位表现）")]
        [Tooltip("小跳幅度，世界单位。V2 素材 64 PPU，0.06 约等于 4 像素")]
        public float bobHeight = 0.06f;

        [Tooltip("小跳频率，每秒步数，与行走速度观感匹配")]
        public float bobFrequency = 8f;

        [Tooltip("停下时回位速度")]
        public float bobReturnSpeed = 8f;

        // AnimatorController 里的参数名（与 GameSceneBuilder.EnsureWitchController 一致）
        private const string SpeedParam = "Speed";
        private const string MoveXParam = "MoveX";
        private const string MoveYParam = "MoveY";

        private PlayerController _pc;
        private SpriteRenderer _sr;
        private Animator _anim;
        private Transform _bobTarget;
        private float _bobBaseY;
        private float _bobPhase;
        private float _bobCurrent;

        private void Awake()
        {
            _pc = GetComponent<PlayerController>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _anim = GetComponentInChildren<Animator>();
            _bobTarget = _sr != null ? _sr.transform : transform;
            _bobBaseY = _bobTarget.localPosition.y;
        }

        private void Update()
        {
            if (_pc == null) return;
            bool moving = _pc.IsMoving;
            Vector2 facing = _pc.Facing;

            // PlayerAppearance 对静态角色会 Animator.enabled = false，
            // 所以这里同时检查 enabled，决定走哪条路线。
            bool hasAnim = _anim != null && _anim.enabled && _anim.runtimeAnimatorController != null;
            if (hasAnim)
            {
                _anim.SetFloat(SpeedParam, moving ? 1f : 0f);
                _anim.SetFloat(MoveXParam, facing.x);
                _anim.SetFloat(MoveYParam, facing.y);
                ResetBob();
            }
            else
            {
                if (_sr != null) _sr.flipX = facing.x < -0.01f;
                UpdateBob(moving);
            }
        }

        private void UpdateBob(bool moving)
        {
            if (_bobTarget == null) return;
            if (moving)
            {
                _bobPhase += Time.deltaTime * bobFrequency;
                // 用 |sin| 而不是 sin：每一步「落地」一次，节奏更接近走路
                _bobCurrent = Mathf.Abs(Mathf.Sin(_bobPhase)) * bobHeight;
            }
            else
            {
                _bobPhase = 0f;
                _bobCurrent = Mathf.MoveTowards(_bobCurrent, 0f, bobReturnSpeed * Time.deltaTime);
            }
            var pos = _bobTarget.localPosition;
            pos.y = _bobBaseY + _bobCurrent;
            _bobTarget.localPosition = pos;
        }

        /// <summary>切到动画路线时把 bob 位移清掉，避免残留偏移。</summary>
        private void ResetBob()
        {
            if (_bobTarget == null) return;
            _bobPhase = 0f;
            if (Mathf.Approximately(_bobCurrent, 0f)) return;
            _bobCurrent = 0f;
            var pos = _bobTarget.localPosition;
            pos.y = _bobBaseY;
            _bobTarget.localPosition = pos;
        }
    }
}
