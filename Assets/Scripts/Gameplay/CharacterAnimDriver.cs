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
        [Tooltip("小跳幅度，世界单位。V2 素材 64 PPU，0.12 约等于 8 像素")]
        public float bobHeight = 0.12f;

        [Tooltip("小跳频率，每秒步数，与行走速度观感匹配")]
        public float bobFrequency = 6f;

        [Tooltip("停下时回位速度")]
        public float bobReturnSpeed = 10f;

        [Tooltip("走路时的水平微摆幅度（身体左右轻晃），0 关闭")]
        public float swayAmount = 0.03f;

        [Header("飘浮角色（烁）：没有行走帧，改用悬浮表现")]
        [Tooltip("悬浮起伏幅度（格）。比走路小跳更柔、更慢")]
        public float floatyBobHeight = 0.09f;

        [Tooltip("悬浮频率（Hz）。站着也在飘，所以取很低的值")]
        public float floatyBobFrequency = 1.6f;

        [Tooltip("飘浮时的水平摆动幅度")]
        public float floatySway = 0.09f;

        [Tooltip("飘浮角色走路时的额外起伏倍率（让「移动」比「站着」明显一点）")]
        public float floatyMoveBoost = 1.8f;

        // AnimatorController 里的参数名（与 GameSceneBuilder.EnsureWitchController 一致）
        private const string SpeedParam = "Speed";
        private const string MoveXParam = "MoveX";
        private const string MoveYParam = "MoveY";

        private PlayerController _pc;
        private SpriteRenderer _sr;
        private Animator _anim;
        private Transform _bobTarget;
        private float _bobBaseY;
        private float _bobBaseX;
        private float _bobPhase;
        private float _bobCurrent;
        private bool _floaty;

        private void Awake()
        {
            _pc = GetComponent<PlayerController>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _anim = GetComponentInChildren<Animator>();
            _bobTarget = _sr != null ? _sr.transform : transform;
            RefreshBase();
        }

        /// <summary>
        /// 重取「静止基准位置」。PlayerAppearance 换角色时会按新 sprite 的高度重设
        /// Visual.localPosition（不同角色素材高度不同，脚底必须贴地），
        /// 不重取的话小跳/悬浮会把角色又拽回旧高度。
        /// </summary>
        public void RefreshBase()
        {
            if (_bobTarget == null) return;
            _bobBaseY = _bobTarget.localPosition.y;
            _bobBaseX = _bobTarget.localPosition.x;
            _bobPhase = 0f;
            _bobCurrent = 0f;
        }

        /// <summary>切换「飘浮」表现。烁这种没有行走帧的角色用它代替走路动画。</summary>
        public void SetFloaty(bool on)
        {
            _floaty = on;
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
            else if (_floaty)
            {
                if (_sr != null) _sr.flipX = facing.x < -0.01f;
                UpdateFloaty(moving);
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
            // 水平微摆：与 bob 同相位，幅度更小，制造「踏步换重心」的感觉
            pos.x = _bobBaseX + (moving ? Mathf.Sin(_bobPhase) * swayAmount : 0f);
            _bobTarget.localPosition = pos;
        }

        /// <summary>
        /// 飘浮：正弦上下 + 左右轻摆，**站着也在动**（sin 而不是 |sin|），
        /// 与走路小跳的「落地节奏」区分开 —— 这是烁的移动语言。
        /// </summary>
        private void UpdateFloaty(bool moving)
        {
            if (_bobTarget == null) return;

            _bobPhase += Time.deltaTime * floatyBobFrequency * (moving ? 2.2f : 1f);
            float amp = floatyBobHeight * (moving ? floatyMoveBoost : 1f);
            _bobCurrent = Mathf.Sin(_bobPhase) * amp;

            var pos = _bobTarget.localPosition;
            pos.y = _bobBaseY + _bobCurrent;
            pos.x = _bobBaseX + Mathf.Sin(_bobPhase * 0.5f) * floatySway * (moving ? 1f : 0.55f);
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
            pos.x = _bobBaseX;
            _bobTarget.localPosition = pos;
        }
    }
}
