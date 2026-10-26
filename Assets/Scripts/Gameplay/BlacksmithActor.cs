using System;
using System.Collections;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 铁匠 NPC：常驻在铁匠铺旁，玩家来打铁时走到面前、按锤击节奏抡锤。
    ///
    /// 为什么不用 Animator：
    ///   1) 挥锤需要「在第 N 帧精确回调命中」（火花 + 音效 + 结算必须和锤子落地同帧），
    ///      Animator 只能靠 Animation Event，而剪辑是编辑器里生成的，事件要额外写资产；
    ///   2) 锤子是独立 sprite，按帧旋转 —— 这套逻辑用代码写比状态机清楚；
    ///   3) 面板打开时 Time.timeScale = 0，这里全程用 unscaledDeltaTime，
    ///      演出不会被冻结（玩家锁住是靠 PlayerController 的输入锁，不是靠时间）。
    ///
    /// 帧序列约定（Tools/gen_cast.py 生成）：
    ///   idle 2 帧（呼吸）/ walk 四方向各 6 帧 / strike 6 帧（站姿→抬臂→过顶→下砸→回弹→收势）
    /// </summary>
    public class BlacksmithActor : MonoBehaviour
    {
        public enum Facing { Down, Up, Left, Right }

        [Header("素材")]
        public Sprite[] idleFrames;
        public Sprite[] walkDown;
        public Sprite[] walkUp;
        public Sprite[] walkLeft;
        public Sprite[] walkRight;
        public Sprite[] strikeFrames;
        public Sprite hammerSprite;

        [Header("节点")]
        [Tooltip("身体渲染器（Visual 子物体）")]
        public SpriteRenderer body;
        [Tooltip("锤子渲染器（手的子节点，pivot 在柄尾）")]
        public SpriteRenderer hammer;

        [Header("参数")]
        [Tooltip("走向玩家的速度（格/秒）")]
        public float walkSpeed = 3.4f;

        [Tooltip("行走帧率")]
        public float walkFps = 12f;

        [Tooltip("站到玩家面前后，与玩家保持的水平距离（格，正值=玩家右侧）")]
        public float standOffset = 1.75f;

        private Vector3 _home;
        private Facing _homeFacing = Facing.Down;
        private Facing _facing = Facing.Down;

        private readonly Sprite[] _singleFrame = new Sprite[1];
        private Sprite[] _frames;
        private int _frame;
        private float _frameTimer;
        private float _frameFps = 8f;

        private Coroutine _act;
        private bool _busy;

        /// <summary>正在演出（走路或挥锤）中，此时不接受新的交互。</summary>
        public bool Busy => _busy;

        /// <summary>锤头在世界坐标里的位置 —— 火花从这儿迸出来，而不是从身体中心。</summary>
        public Vector3 HammerTip
        {
            get
            {
                if (hammer == null) return transform.position + Vector3.up * 0.8f;
                // 锤子 sprite 32x44、pivot 在柄尾；锤头中心距 pivot 约 30px（PPU 64）
                return hammer.transform.position + hammer.transform.up * 0.47f;
            }
        }

        private void Awake()
        {
            _home = transform.position;
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            // 帧数组是生成场景时（编辑器里）赋的，Awake 时还没写进来 —— 呼吸帧留到 Start
        }

        private void Start()
        {
            PlayIdle();
        }

        // ------------------------------------------------------------ 对外接口

        /// <summary>
        /// 走到玩家面前并面向他，就位后回调（此时才该弹面板）。
        /// 玩家在哪一侧不重要：铁匠会绕到玩家的左侧站定，正好落在锻造面板旁边的空位上。
        /// </summary>
        public void Serve(Transform player, Action onReady)
        {
            if (_busy || player == null) { onReady?.Invoke(); return; }
            _act = StartCoroutine(ServeRoutine(player, onReady));
        }

        /// <summary>回铺子待机（关闭面板后调用）。</summary>
        public void GoHome(Action onDone = null)
        {
            // 允许打断挥锤：玩家关面板时铁匠可能正抡到一半，不能让他杵在那儿不动
            if (_act != null) StopCoroutine(_act);
            _busy = false;
            _act = StartCoroutine(GoHomeRoutine(onDone));
        }

        /// <summary>
        /// 抡一锤。第 4 帧（下砸）触发 <paramref name="onImpact"/>，
        /// 调用方在这一刻结算数值 / 播音效 / 放火花，手感才是"锤子落地"而不是"按钮点击"。
        /// </summary>
        public void PlayStrike(Action onImpact, Action onDone = null)
        {
            if (_busy) { onImpact?.Invoke(); onDone?.Invoke(); return; }
            _act = StartCoroutine(StrikeRoutine(onImpact, onDone));
        }

        /// <summary>打断当前演出（场景切换 / 面板被强关时兜底），回到待机。</summary>
        public void Cancel()
        {
            if (_act != null) StopCoroutine(_act);
            _act = null;
            _busy = false;
            SnapHome();
        }

        // ------------------------------------------------------------ 协程

        private IEnumerator ServeRoutine(Transform player, Action onReady)
        {
            _busy = true;

            // 站到玩家右侧：锻造面板靠左，村子里的铁匠铺又在地图最右，
            // 右半边才是"面板之外"的可见区（实测相机 bounds 会把玩家顶到屏幕 67% 处）
            Vector3 target = player.position + new Vector3(standOffset, 0.05f, 0f);
            yield return MoveTo(target);

            FaceTowards(player.position);
            PlayIdle();

            // 一个小"站定"的动作：抬一下手再放下，避免走到位立刻弹窗显得生硬
            yield return WaitUnscaled(0.12f);

            _busy = false;
            _act = null;
            onReady?.Invoke();
        }

        private IEnumerator GoHomeRoutine(Action onDone)
        {
            _busy = true;
            yield return MoveTo(_home);
            SetFacing(_homeFacing);
            PlayIdle();
            _busy = false;
            _act = null;
            onDone?.Invoke();
        }

        private IEnumerator StrikeRoutine(Action onImpact, Action onDone)
        {
            _busy = true;

            // 锤子的「手位 + 角度」逐帧表，与 Tools/gen_cast.py 的 PH 表一一对应。
            // 索引 = strike 帧号；手位是相对身体 sprite 中心的本地坐标（PPU 64）。
            var hand = new[]
            {
                new Vector2(-0.25f,  -0.5625f),   // 0 站姿：手垂在身侧
                new Vector2(-0.3125f,-0.21875f),  // 1 抬臂
                new Vector2(-0.34375f, 0.1875f),  // 2 过顶
                new Vector2(-0.03125f,-0.5f),     // 3 下砸
                new Vector2(-0.0625f, -0.40625f), // 4 回弹
                new Vector2(-0.1875f, -0.53125f), // 5 收势
            };
            var angle = new[] { 0f, -38f, -105f, 118f, 92f, 18f };
            // 每帧时长（秒）。抬-蓄慢、砸下快、回弹稍慢 —— 这是"重量感"的全部来源
            var hold = new[] { 0.06f, 0.10f, 0.10f, 0.055f, 0.11f, 0.09f };

            if (strikeFrames != null && strikeFrames.Length >= 6)
            {
                for (int i = 0; i < strikeFrames.Length; i++)
                {
                    int f = Mathf.Min(i, 5);
                    ShowSingle(strikeFrames[i]);
                    SetHammer(hand[f], angle[f]);
                    yield return WaitUnscaled(hold[f]);

                    if (i == 2)     // 过顶蓄力：一声"呼"的换气感（用余烬代替）
                        ForgeFx.Embers(HammerTip, 3, new Color(1f, 0.75f, 0.4f, 0.8f));

                    if (i == 3)     // 下砸命中
                    {
                        onImpact?.Invoke();
                        yield return null;
                    }
                }
            }
            else
            {
                // 素材缺失时的兜底：至少保证回调按时发生，不影响玩法
                yield return WaitUnscaled(0.18f);
                onImpact?.Invoke();
                yield return WaitUnscaled(0.16f);
            }

            // 收势后把锤子摆回身侧
            if (strikeFrames != null && strikeFrames.Length > 0)
                ShowSingle(strikeFrames[0]);
            SetHammer(hand[0], angle[0]);

            _busy = false;
            _act = null;
            onDone?.Invoke();
        }

        // ------------------------------------------------------------ 移动

        private IEnumerator MoveTo(Vector3 target)
        {
            const float arrive = 0.04f;
            float guard = 0f;

            while ((transform.position - target).sqrMagnitude > arrive * arrive)
            {
                // 用真实时间：面板打开（timeScale=0）时也要能走回铺子
                float dt = Time.unscaledDeltaTime;
                Vector3 delta = target - transform.position;
                if (delta.sqrMagnitude < 1e-6f) break;

                Vector3 step = delta.normalized * (walkSpeed * dt);
                if (step.magnitude > delta.magnitude) step = delta;
                transform.position += step;

                SetFacing(DirOf(step));
                PlayWalk(Time.unscaledTime);

                guard += dt;
                if (guard > 6f) break;      // 兜底：卡住就不走了，别让玩家被锁死
                yield return null;
            }

            transform.position = target;
        }

        private static Facing DirOf(Vector3 d)
        {
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
                return d.x >= 0f ? Facing.Right : Facing.Left;
            return d.y >= 0f ? Facing.Up : Facing.Down;
        }

        private void FaceTowards(Vector3 worldPoint)
        {
            SetFacing(DirOf(worldPoint - transform.position));
        }

        private void SetFacing(Facing f)
        {
            _facing = f;
        }

        // ------------------------------------------------------------ 帧播放

        private void PlayWalk(float now)
        {
            var frames = FramesOf(_facing);
            if (frames == null || frames.Length == 0) return;
            _frames = frames;
            _frameFps = walkFps;
            // 按"总时间轴"取帧：起步/停步不会总是从第 0 帧开始，脚步更自然
            _frame = Mathf.FloorToInt(now * walkFps) % frames.Length;
            ApplyFrame();          // 走路时 Update 不推进（_busy=true），必须在这里刷帧
        }

        private void PlayIdle()
        {
            var frames = FramesOf(_facing);
            if (frames == null || frames.Length == 0) return;

            // 正面有专门的呼吸两帧；其余方向用行走帧里的中立姿势
            if (_facing == Facing.Down && idleFrames != null && idleFrames.Length > 0)
            {
                _frames = idleFrames;
                _frameFps = 2f;
            }
            else
            {
                _singleFrame[0] = frames[0];
                _frames = _singleFrame;
                _frameFps = 1f;
            }
            _frame = 0;
            _frameTimer = 0f;
            ApplyFrame();
        }

        private Sprite[] FramesOf(Facing f)
        {
            switch (f)
            {
                case Facing.Up: return walkUp;
                case Facing.Left: return walkLeft;
                case Facing.Right: return walkRight;
                default: return walkDown;
            }
        }

        /// <summary>显示单张帧（挥锤序列用：每帧由协程控制，不参与自动播放）。</summary>
        private void ShowSingle(Sprite s)
        {
            if (body == null || s == null) return;
            _frames = null;          // 关掉自动推进
            body.sprite = s;
        }

        private void ApplyFrame()
        {
            if (body == null || _frames == null || _frames.Length == 0) return;
            body.sprite = _frames[Mathf.Clamp(_frame, 0, _frames.Length - 1)];
        }

        private void LateUpdate()
        {
            // YSort 只改 body 的 sortingOrder，锤子要永远压在身体上方一层
            if (hammer != null && body != null) hammer.sortingOrder = body.sortingOrder + 1;
        }

        private void Update()
        {
            // 演出中（走路由 MoveTo 按时间轴取帧、挥锤由协程逐帧推）不在这里推进
            if (_busy) return;
            if (_frames == null || _frames.Length <= 1) return;

            _frameTimer += Time.unscaledDeltaTime;
            float step = 1f / Mathf.Max(0.01f, _frameFps);
            if (_frameTimer >= step)
            {
                _frameTimer -= step;
                _frame = (_frame + 1) % _frames.Length;
                ApplyFrame();
            }
        }

        private void SetHammer(Vector2 handLocal, float angle)
        {
            if (hammer == null) return;
            var t = hammer.transform;
            t.localPosition = new Vector3(handLocal.x, handLocal.y, -0.02f);
            t.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ------------------------------------------------------------ 杂项

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>回原位（不播走路，仅用于被打断/场景初始化）。</summary>
        private void SnapHome()
        {
            transform.position = _home;
            SetFacing(_homeFacing);
            PlayIdle();
            if (hammer != null)
            {
                hammer.transform.localPosition = new Vector3(-0.25f, -0.5625f, -0.02f);
                hammer.transform.localRotation = Quaternion.identity;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(HammerTip, 0.12f);
        }
#endif
    }
}
