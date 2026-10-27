using System.Collections;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 钓鱼点：放在池塘岸边，玩家走近按交互键（或点 HUD 按钮）开始钓鱼。
    ///
    /// 本类只负责**状态机 + 调度**，一点表现都不做：
    ///   抛竿动画 / 水花 / 涟漪 / 鱼影 / 假咬钩 / 咬钩抖动 / 鱼惊走 → <see cref="FishingBobberFx"/>
    ///   上鱼 QTE 面板                                          → <see cref="FishingMinigame"/>
    ///   结算卡片                                              → <see cref="FishingCatchCard"/>
    ///
    /// 节奏（v0.21）：
    ///   抛竿 0.55s → 等待 1.5~3.8s（期间 0~2 次假咬钩）→ 真咬钩预警 0.35s
    ///   → 咬钩窗口 3.0s 内再交互一次 → 拉条 QTE → 结算
    ///
    /// 为什么咬钩要「再点一次」而不是自动上鱼：
    ///   自动上鱼等于挂机刷钱，种子钱就没意义了；一次点击的成本刚好是「留意水面」的代价，
    ///   和星露谷的手感一致（那边是看浮标下沉按一下）。
    ///
    /// 等待期可交互，但**只有假咬钩的下沉窗口内点击才有反应**（= 提前提竿，鱼惊走）。
    /// 其余时间点击完全无反应，也不弹任何提示 —— 见 <see cref="Interact"/> 里的说明。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FishingBobberFx))]
    public class FishingSpot : MonoBehaviour, IInteractable
    {
        [Header("节奏")]
        [Tooltip("抛竿动画时长（秒）。浮标的飞行时长与它严格一致")]
        public float castSeconds = 0.55f;

        [Tooltip("咬钩前的最短等待（秒）")]
        public float minWait = 1.5f;

        [Tooltip("咬钩前的最长等待（秒）")]
        public float maxWait = 3.8f;

        [Tooltip("咬钩窗口（秒）。这段时间内再交互一次才能上鱼，超时鱼跑。手机上要点按钮，给足反应时间")]
        public float biteWindow = 3.0f;

        [Header("假咬钩（v0.21）")]
        [Tooltip("一次等待里最多出现几次假咬钩。0 = 关掉这个机制")]
        public int fakeBiteMaxCount = 2;

        [Tooltip("假咬钩的预摇时长（秒）。这段时间浮标只左右轻抖、不下沉 —— 玩家据此判断「这是假的，别点」")]
        public float fakePreShakeSeconds = 0.35f;

        [Tooltip("假咬钩的下沉时长（秒）。只有这段窗口内点击才会惊跑鱼")]
        public float fakeDipSeconds = 0.25f;

        [Tooltip("假咬钩下沉的深度（像素，按浮标 16 PPU 换算）。2~3px 在浮标上看得出来，又不至于像真咬钩")]
        public float fakeDipPixels = 2.5f;

        [Tooltip("抛竿后多久才允许出第一次假咬钩（秒）。太靠前玩家还没看清浮标就被考了一次")]
        public float fakeFirstDelay = 0.45f;

        [Tooltip("两次假咬钩之间至少隔多久（秒）")]
        public float fakeMinGap = 0.5f;

        [Tooltip("假咬钩与真咬钩预警之间要留的余量（秒）。两者挨在一起玩家会分不清哪个是真信号")]
        public float fakeTailGuard = 0.3f;

        [Header("真咬钩预警（v0.21）")]
        [Tooltip("真咬钩前的预警时长（秒）：涟漪扩散 + 鱼影游过来。和假咬钩的预摇一样长，节奏才统一")]
        public float biteWarningSeconds = 0.35f;

        [Tooltip("预警时扩散的涟漪圈数（1~2）")]
        public int biteWarningRipples = 2;

        [Header("失败冷却")]
        [Tooltip("提前提竿惊跑鱼之后的冷却（秒）。这段时间 CanInteract = false，玩家得等一下才能再抛")]
        public float scareCooldownSeconds = 1.5f;

        [Header("产出")]
        [Tooltip("可能钓到的鱼 id，重复写就是提高权重。河鲈×3 : 锦鲤×2 : 鳜鱼×2 : 湖主×1")]
        public string[] fishIds =
        {
            "fish_blue", "fish_blue", "fish_blue",
            "fish_gold", "fish_gold",
            "fish_mandarin", "fish_mandarin",
            "fish_legend",
        };

        [Tooltip("浮标相对本物体的偏移（指向水面中心）")]
        public Vector2 bobberOffset = new Vector2(0f, 1.4f);

        enum State { Idle, Casting, Waiting, Biting, Playing, Cooldown }

        State _state = State.Idle;
        Coroutine _routine;
        FishingBobberFx _fx;

        /// <summary>本杆的抛竿起点（玩家所在位置）。只在 Idle → Casting 时写一次。</summary>
        Vector3 _castFrom;

        /// <summary>假咬钩的下沉窗口是否打开。**只有它**决定等待期的点击会不会惊跑鱼。</summary>
        bool _fakeWindowOpen;

        /// <summary>上鱼 QTE 期间锁定下来的鱼与重量（QTE 结果回来时结算用）。</summary>
        string _hookedFishId;
        float _hookedWeight;

        // ------------------------------------------------------------ IInteractable

        public string Prompt
        {
            get
            {
                switch (_state)
                {
                    case State.Casting: return "抛竿中";
                    case State.Waiting: return "等待咬钩";
                    case State.Biting: return "收竿！";
                    case State.Playing: return "拉杆中";
                    case State.Cooldown: return "鱼惊走了";
                    default: return "钓鱼";
                }
            }
        }

        /// <summary>
        /// 可交互的时刻：空闲 / 等待 / 咬钩。
        ///
        /// 等待期为什么又回到「可交互」（v0.21）：假咬钩机制需要玩家「点了才惊跑」，
        /// CanInteract = false 的话点击根本传不到 Interact。
        ///
        /// 为什么这不会把 v0.18 的 bug 放回来：v0.18 修的是「等待期点了 = 空竿放弃」
        /// （按钮写着「收竿」，手机连点就白抛一杆）。现在等待期点击在绝大多数时刻
        /// **什么都不做**，只有假咬钩下沉的那 0.25 秒才算惊跑，而且那 0.25 秒前面
        /// 还有 0.35 秒预摇给玩家看。文字也从「收竿」改成了「等待咬钩」，
        /// 不再是邀请点击的动词。
        /// </summary>
        public bool CanInteract =>
            _state == State.Idle || _state == State.Waiting || _state == State.Biting;

        public void Interact(PlayerInteractor who)
        {
            switch (_state)
            {
                case State.Idle:
                    // 记下玩家位置：浮标要「从他手里」飞出去，而不是凭空出现在水面上
                    _castFrom = who != null
                        ? who.transform.position + new Vector3(0f, 0.45f, 0f)
                        : transform.position;
                    _routine = StartCoroutine(FishRoutine());
                    break;

                case State.Waiting:
                    // 等待期只做「惊跑判定」，不做收竿：
                    // 窗口外点击 = 无反应（不弹提示、不打断、不惩罚）——
                    // 手机玩家连点是常态，任何有代价的响应都会变成新的误伤。
                    if (_fakeWindowOpen) ScareAway();
                    break;

                case State.Biting:
                    StartMinigame();
                    break;

                // Casting / Playing / Cooldown 不响应（CanInteract 也是 false）
            }
        }

        // ------------------------------------------------------------ 生命周期

        void Awake()
        {
            _fx = GetComponent<FishingBobberFx>();

            // RequireComponent 正常会补上这一层；这里是手写兜底 ——
            // 场景是程序化生成的，万一哪天有人用旧场景跑（组件还没加上），
            // 少了 fx 就是一堆 NullReference，补一行比到处判空便宜。
            if (_fx == null) _fx = gameObject.AddComponent<FishingBobberFx>();

            _fx.SetAnchor(transform, bobberOffset);
        }

        /// <summary>
        /// 换场景 / 对象销毁时收尾。
        ///
        /// 这里必须做两件事，缺一件都会留下「看不见的坑」：
        ///   1) 停掉协程 —— 否则协程会在对象被禁用后继续跑（Unity 只在销毁时停）；
        ///   2) 关掉 QTE —— QTE 面板是 DontDestroyOnLoad 的，它会把 timeScale = 0 和
        ///      输入锁一起带到下一个场景，玩家在新场景里动都动不了。
        /// </summary>
        void OnDisable()
        {
            StopRoutineOnly();
            _fakeWindowOpen = false;
            _state = State.Idle;

            FishingMinigame.AbortOwner(this);
            if (_fx != null) _fx.ResetAll();
        }

        // ------------------------------------------------------------ 主流程

        IEnumerator FishRoutine()
        {
            // ---- 抛竿
            _state = State.Casting;
            _fakeWindowOpen = false;
            _fx.PlayCast(_castFrom, castSeconds);
            yield return new WaitForSeconds(castSeconds);

            // ---- 等待（夹着 0~2 次假咬钩）
            _state = State.Waiting;
            Toast.Show("抛竿了，等浮标动");
            float wait = Random.Range(minWait, maxWait);
            yield return WaitWithFakeBites(wait);

            // ---- 真咬钩预警：涟漪 + 鱼影。比 Toast 更早，让玩家有时间把手指移到按钮上
            _fx.PlayBiteWarning(biteWarningSeconds, biteWarningRipples);
            yield return new WaitForSeconds(biteWarningSeconds);

            // ---- 咬钩窗口
            _state = State.Biting;
            _fx.SetBiteShake(true);
            AudioManager.PlayBite();
            Toast.Show("咬钩了！快收杆");

            float t = 0f;
            while (t < biteWindow)
            {
                t += Time.deltaTime;
                yield return null;
            }

            StopFishing("鱼跑了…");
        }

        /// <summary>
        /// 等待一段时间，期间按排好的时间点插入假咬钩。
        ///
        /// 计时用 Time.time 的绝对差（而不是自己累加 deltaTime）：
        /// 假咬钩本身要跑 0.6 秒，累加式计时在它跑的时候会「停住」，
        /// 后面所有时间点都会整体前移，最后可能和真咬钩预警叠在一起。
        /// </summary>
        IEnumerator WaitWithFakeBites(float wait)
        {
            float t0 = Time.time;
            var plan = PlanFakeBites(wait);

            for (int i = 0; i < plan.Length; i++)
            {
                while (Time.time - t0 < plan[i]) yield return null;
                yield return FakeBiteRoutine();
            }

            while (Time.time - t0 < wait) yield return null;
        }

        /// <summary>
        /// 排出本次等待里的假咬钩时间点（可能一个都没有）。
        /// 只排在「离真咬钩预警还够远」的区间里，塞不下就少放几个 ——
        /// 假动作和真信号挨在一起，玩家就没有可信的判断依据了。
        /// </summary>
        float[] PlanFakeBites(float wait)
        {
            int maxCount = Mathf.Clamp(fakeBiteMaxCount, 0, 4);
            if (maxCount <= 0) return new float[0];

            float fakeLength = Mathf.Max(0f, fakePreShakeSeconds) + Mathf.Max(0f, fakeDipSeconds);
            float lastAllowed = wait - biteWarningSeconds - fakeTailGuard - fakeLength;
            if (lastAllowed <= fakeFirstDelay) return new float[0];

            int want = Random.Range(0, maxCount + 1);
            var list = new System.Collections.Generic.List<float>(want);
            float cursor = fakeFirstDelay;

            for (int i = 0; i < want; i++)
            {
                float span = lastAllowed - cursor;
                if (span <= 0f) break;

                // 在可用区间里随机取一个起点（留出这一整套假动作的长度）
                float t = cursor + Random.Range(0f, span);
                list.Add(t);
                cursor = t + fakeLength + Mathf.Max(0f, fakeMinGap);
            }

            return list.ToArray();
        }

        /// <summary>
        /// 一次假咬钩：先预摇（安全），再下沉（危险窗口）。
        /// 窗口的开合只在这一个地方设置，别处一律读它 —— 这是「连点不会误伤」的唯一依据。
        /// </summary>
        IEnumerator FakeBiteRoutine()
        {
            _fakeWindowOpen = false;
            _fx.PlayFakeBite(fakePreShakeSeconds, fakeDipSeconds, fakeDipPixels);

            // 预摇：玩家看得出「浮标在抖，但没沉」= 假动作，所以这段时间点了不算
            yield return new WaitForSeconds(fakePreShakeSeconds);

            _fakeWindowOpen = true;
            yield return new WaitForSeconds(fakeDipSeconds);
            _fakeWindowOpen = false;
        }

        // ------------------------------------------------------------ 惊跑 / 收竿

        /// <summary>玩家在假咬钩窗口里点了：鱼惊走，进冷却。</summary>
        void ScareAway()
        {
            StopFishing(null);

            Toast.Show("太急了，鱼被惊跑了", 1.5f);
            _fx.PlayEscape();

            _routine = StartCoroutine(CooldownRoutine());
        }

        IEnumerator CooldownRoutine()
        {
            _state = State.Cooldown;
            yield return new WaitForSeconds(Mathf.Max(0f, scareCooldownSeconds));
            _state = State.Idle;
            _routine = null;
        }

        /// <summary>收尾：停协程、回空闲、收起浮标，可选弹一句提示。</summary>
        void StopFishing(string message)
        {
            StopRoutineOnly();
            _state = State.Idle;
            // 窗口标记必须在这里一起清：如果留着 true，下一杆的等待期会整体变成「点了就惊跑」，
            // 那就是把 v0.18 的连点误伤原样放回来了。
            _fakeWindowOpen = false;
            _fx.ResetAll();
            if (!string.IsNullOrEmpty(message)) Toast.Show(message);
        }

        void StopRoutineOnly()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        // ------------------------------------------------------------ 上鱼 QTE

        /// <summary>咬钩窗口里点中了：定鱼、定重量，然后把画面交给 QTE 面板。</summary>
        void StartMinigame()
        {
            string fishId = PickFish();
            if (string.IsNullOrEmpty(fishId))
            {
                StopFishing("鱼跑了…");
                return;
            }

            _hookedFishId = fishId;
            _hookedWeight = RollWeight(fishId);

            // 先把主流程协程停掉（它还在跑咬钩窗口的倒计时），但状态先不切 Playing：
            // 顺序是有讲究的 —— Begin 里会做「建面板 + 上锁」，万一中间抛异常，
            // 状态留在 Biting 的话玩家还能再点一次重来；先写成 Playing 就会原地卡死
            // （CanInteract 永远 false，也没有面板可操作）。
            StopRoutineOnly();
            _fakeWindowOpen = false;
            _fx.SetBiteShake(false);
            _fx.ResetAll();

            if (!FishingMinigame.Begin(fishId, _hookedWeight, this, OnMinigameDone))
            {
                // 已经有一杆在跑（理论上不可能，只有一个池塘）。别把状态卡住
                _state = State.Idle;
                Toast.Show("鱼跑了…");
                _hookedFishId = null;
                return;
            }

            // 从这一刻起，状态机的出口只有 OnMinigameDone（或被 OnDisable 打断）
            _state = State.Playing;
        }

        /// <summary>
        /// QTE 结果回调。<paramref name="success"/> = 上鱼。
        /// 注意：回调到达时 QTE 面板已经把 timeScale 与输入锁还回去了（见 FishingMinigame），
        /// 这里只做玩法结算，不再碰任何全局状态。
        /// </summary>
        void OnMinigameDone(bool success)
        {
            if (_state != State.Playing) return;    // 已经被 OnDisable 收过尾

            _state = State.Idle;

            string fishId = _hookedFishId;
            _hookedFishId = null;
            if (string.IsNullOrEmpty(fishId)) return;

            if (!success)
            {
                // 「鱼跑了」也记账：跑掉次数能反推每种鱼的实际成功率，比只看钓获数有用
                FishingLog.RecordEscaped(fishId);
                Toast.Show("鱼跑了…");
                AudioManager.Click();
                return;
            }

            SettleCatch(fishId, _hookedWeight);
        }

        /// <summary>
        /// 结算：背包 → 图鉴 → 结算卡片。
        ///
        /// 背包满的判定放在**这里**（上鱼成功之后）而不是抛竿时：
        /// 抛竿就拦等于让玩家先白玩一局 QTE 才知道装不下，体验更差；
        /// 而且背包满时鱼不算钓获（不写图鉴、不进背包），和 P3 的需求一致。
        /// </summary>
        void SettleCatch(string fishId, float weight)
        {
            if (!Inventory.CanAdd(fishId, 1))
            {
                Toast.Show("背包满了，这条鱼没能装下 —— 去集市卖掉一些");
                AudioManager.Click();
                return;
            }

            // 先读旧纪录再写新的：Record 里会立刻把最重值刷成本次重量
            float bestBefore = FishingLog.GetMaxWeight(fishId);
            bool isRecord = weight > bestBefore + 0.0005f;   // 第一次钓到也算新纪录

            Inventory.Add(fishId, 1);
            FishingLog.Record(fishId, weight);

            FishingCatchCard.Show(fishId, weight, isRecord);
            AudioManager.Confirm();
        }

        // ------------------------------------------------------------ 选鱼 / 掷重量

        /// <summary>按鱼的重量区间随机一个重量（kg）。</summary>
        static float RollWeight(string fishId)
        {
            var def = ItemDatabase.Find(fishId);
            if (def == null) return 0.5f;
            return Random.Range(def.weightMin, def.weightMax);
        }

        string PickFish()
        {
            if (fishIds == null || fishIds.Length == 0) return null;

            // 从随机位置开始转一圈，挑第一个物品表里真实存在的鱼 ——
            // 防止物品表改过之后钓出「不存在的东西」进背包
            int start = Random.Range(0, fishIds.Length);
            for (int i = 0; i < fishIds.Length; i++)
            {
                string id = fishIds[(start + i) % fishIds.Length];
                if (ItemDatabase.Find(id) != null) return id;
            }
            return null;
        }

        // ------------------------------------------------------------ Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)bobberOffset);
            Gizmos.DrawWireSphere(transform.position + (Vector3)bobberOffset, 0.2f);
        }
    }
}
