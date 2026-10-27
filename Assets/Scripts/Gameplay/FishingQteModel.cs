using System;

namespace LuminousSSS
{
    /// <summary>一次上鱼 QTE 的难度输入：稀有度 + 本次重量在区间内的位置。</summary>
    public struct FishingQteInput
    {
        /// <summary>鱼的稀有度（1~5 星），由 ItemDef.rarity 决定。</summary>
        public int rarity;

        /// <summary>本次重量在 [weightMin, weightMax] 里的位置（0 = 下限，1 = 上限）。</summary>
        public float weightRatio01;
    }

    /// <summary>
    /// QTE 的全部可调参数。
    ///
    /// 为什么用结构体而不是让模型自己读 <see cref="FishingMinigame"/> 的 [SerializeField]：
    ///   模型要保持「零 Unity 依赖」，这样它可以在编辑器外被单独编译、跑模拟做数值验证；
    ///   面板每帧把参数塞进来，调参仍然在 Inspector 里做。
    /// </summary>
    public struct FishingQteTuning
    {
        /// <summary>鱼的最低移动速度（竖条/秒，1 = 整条高度）。</summary>
        public float fishSpeedMin;

        /// <summary>鱼在满难度时的移动速度。</summary>
        public float fishSpeedMax;

        /// <summary>最低难度下鱼平均每秒变向多少次。</summary>
        public float turnRateMin;

        /// <summary>满难度下鱼平均每秒变向多少次（「爱突然变向」的直接来源）。</summary>
        public float turnRateMax;

        /// <summary>★1 时捕捞框的半高（归一化）。</summary>
        public float boxHalfBase;

        /// <summary>每多一颗星，捕捞框半高缩小多少。</summary>
        public float boxHalfPerStar;

        /// <summary>捕捞框半高的下限（再难也不能缩成一条线，手机点不准）。</summary>
        public float boxHalfMin;

        /// <summary>按住时捕捞框的向上加速度（竖条/秒²）。</summary>
        public float liftAccel;

        /// <summary>松开时向下的加速度。取小于 liftAccel 会变成「上升快、下落慢」，手感更宽容。</summary>
        public float gravityAccel;

        /// <summary>捕捞框速度上限（上下共用），避免点一下就冲到顶。</summary>
        public float maxBoxSpeed;

        /// <summary>开局进度（0~1）。不给 0 是为了「一开局漏一拍就立刻失败」这种挫败感。</summary>
        public float startProgress01;

        /// <summary>重叠时进度每秒涨多少。</summary>
        public float gainPerSecond;

        /// <summary>不重叠时进度每秒掉多少。</summary>
        public float lossPerSecond;

        /// <summary>限时（秒）。到点视为鱼跑了 —— 也是「卡在中间谁也赢不了」的兜底。</summary>
        public float timeLimitSeconds;

        /// <summary>难度里稀有度的权重（0~1），剩下的权重给重量。1 = 只看稀有度。</summary>
        public float rarityWeight;
    }

    /// <summary>QTE 的结局。</summary>
    public enum FishingQtePhase
    {
        /// <summary>进行中。</summary>
        Running,
        /// <summary>进度满，上鱼成功。</summary>
        Caught,
        /// <summary>进度清零，鱼跑了。</summary>
        Escaped,
        /// <summary>限时到，鱼跑了。</summary>
        TimedOut
    }

    /// <summary>
    /// 上鱼 QTE 的数值内核（纯 C#，不引用 UnityEngine）。
    ///
    /// 为什么单独拆出来、而不是写在面板里：
    ///   1) 「难度曲线」和「框追鱼」的物理是最需要反复调的两块，只有纯 float 运算、
    ///      不碰 Time/Random，才能在编辑器外单独编译跑模拟（见回报里的数值验证）；
    ///   2) 面板 <see cref="FishingMinigame"/> 只管画和吃输入，不做积分；
    ///   3) dt 与时序种子都由调用方喂进来 —— 面板被销毁/换场景时不会留下半场状态。
    ///
    /// 坐标系：竖条内部统一 0..1 归一化（0 = 底、1 = 顶），与 UI 像素解耦，
    /// 这样改竖条高度不会影响手感。
    ///
    /// 判定：鱼自身半高 <see cref="FishHalf01"/> + 捕捞框半高 <see cref="BoxHalf01"/>，
    /// 两者中心距离不超过这个和就算「重叠」，进度上涨。
    /// </summary>
    public class FishingQteModel
    {
        /// <summary>鱼图标在竖条里占的半高（归一化）。固定值：图标大小是表现问题，不该影响难度。</summary>
        public const float FishHalf01 = 0.045f;

        /// <summary>单步最大 dt。切后台 / 首帧卡顿时别让积分跳一大截。</summary>
        const float MaxStep = 0.05f;

        /// <summary>
        /// 变向间隔的上下夹取（秒）。太小会让鱼抖成噪点，太大就变成一条直线来回。
        ///
        /// 下限特意抬到 0.25 而不是 0.1：指数分布会大量吐出极短间隔，
        /// 鱼就会在同一个点上原地高频抖 —— 玩家既看不清也追不上，
        /// 而且（数值验证时发现的）它会「霸占」某一段让挂机也能赢。
        /// </summary>
        const float MinTurnInterval = 0.25f;
        const float MaxTurnInterval = 3.0f;

        /// <summary>离上下边界这么近就不再随机变向，只许往中间游（防「贴边磨蹭」）。</summary>
        const float EdgeAvoidZone = 0.07f;

        FishingQteTuning _t;
        Random _rng;

        float _fishDir;
        float _fishSpeed01;
        float _turnTimer;
        float _boxVel;

        /// <summary>鱼的竖直位置（0..1）。</summary>
        public float Fish01 { get; private set; }

        /// <summary>捕捞框中心位置（0..1）。</summary>
        public float BoxCenter01 { get; private set; }

        /// <summary>捕捞框半高（归一化）。稀有度越高越小。</summary>
        public float BoxHalf01 { get; private set; }

        /// <summary>进度（0..1）。满 = 上鱼。</summary>
        public float Progress01 { get; private set; }

        /// <summary>剩余时间（秒）。</summary>
        public float TimeLeft { get; private set; }

        /// <summary>当前结局。</summary>
        public FishingQtePhase Phase { get; private set; }

        /// <summary>归一化后的总难度（0..1），面板可以拿它做色温/提示。</summary>
        public float Difficulty01 { get; private set; }

        /// <summary>当前是否重叠（表现层用来闪一下「咬住了」）。</summary>
        public bool Overlapping =>
            Math.Abs(Fish01 - BoxCenter01) <= BoxHalf01 + FishHalf01;

        /// <summary>
        /// 开一杆。<paramref name="seed"/> 由面板用 UnityEngine.Random 取，这里只负责可复现的随机。
        /// </summary>
        public void Reset(FishingQteInput input, FishingQteTuning tuning, int seed)
        {
            _t = tuning;
            _rng = new Random(seed);

            // ---- 难度合成：稀有度为主、重量为辅。
            // 为什么重量只占小头：重量是**同种鱼内部**的浮动（一条河鲈也可能 0.98kg），
            // 若给大权重，钓到同一条鱼的手感会忽难忽易，玩家归因不到「这鱼稀有」上。
            float rarityT = Clamp01((input.rarity - 1) / 4f);
            float weightT = Clamp01(input.weightRatio01);
            float w = Clamp01(_t.rarityWeight);
            Difficulty01 = Clamp01(rarityT * w + weightT * (1f - w));

            _fishSpeed01 = Lerp(_t.fishSpeedMin, _t.fishSpeedMax, Difficulty01);

            // 捕捞框只跟稀有度走，不看重量：框是「这只鱼给多大容错」，
            // 同种鱼每次都变框高会让玩家以为操作失灵。
            BoxHalf01 = Math.Max(_t.boxHalfMin,
                                 _t.boxHalfBase - _t.boxHalfPerStar * Math.Max(0, input.rarity - 1));

            Fish01 = 0.5f;
            BoxCenter01 = 0.5f;          // 开局对齐：第一秒就能吃到进度，不会一上手就亏
            _boxVel = 0f;
            _fishDir = _rng.Next(2) == 0 ? 1f : -1f;
            _turnTimer = SampleTurnInterval();
            Progress01 = Clamp01(_t.startProgress01);
            TimeLeft = Math.Max(1f, _t.timeLimitSeconds);
            Phase = FishingQtePhase.Running;
        }

        /// <summary>
        /// 推进一帧。<paramref name="holding"/> = 玩家此刻是否按住（上升）。
        /// 已经分出胜负之后再调是空操作 —— 面板的淡出期还会继续更新 UI，
        /// 不能让淡出期里的输入再改变结果。
        /// </summary>
        public void Step(float dt, bool holding)
        {
            if (Phase != FishingQtePhase.Running) return;
            if (dt <= 0f) return;
            if (dt > MaxStep) dt = MaxStep;

            StepFish(dt);
            StepBox(dt, holding);

            // ---- 进度：看的是「上面两步算完之后」的重叠，顺序不能颠倒
            bool overlap = Overlapping;
            float delta = overlap ? _t.gainPerSecond : -_t.lossPerSecond;
            Progress01 = Clamp01(Progress01 + delta * dt);

            TimeLeft -= dt;

            // ---- 结算优先级：进度满 > 进度空 > 超时。
            // 同一帧既满又空不可能（一次只加一项），但满与超时可能同帧，
            // 优先判赢 —— 玩家在最后一瞬间吃满进度不该被判负。
            if (Progress01 >= 1f) Phase = FishingQtePhase.Caught;
            else if (Progress01 <= 0f) Phase = FishingQtePhase.Escaped;
            else if (TimeLeft <= 0f) Phase = FishingQtePhase.TimedOut;
        }

        // ------------------------------------------------------------ 内部

        void StepFish(float dt)
        {
            float half = FishHalf01;

            // 贴边时强制往中间游。只有「撞到边界再掉头」是不够的：
            // 鱼正好在边界上随机变向，就会在同一角上磨很多秒（表现上像卡住，
            // 数值上让「不操作」也能把进度吃满）。
            if (Fish01 <= half + EdgeAvoidZone) _fishDir = 1f;
            else if (Fish01 >= 1f - half - EdgeAvoidZone) _fishDir = -1f;
            else
            {
                // 随机变向：间隔按指数分布取（rate = 每秒次数），比「固定间隔摇一次」
                // 更难被玩家背出节奏，也更像鱼。
                _turnTimer -= dt;
                if (_turnTimer <= 0f)
                {
                    _fishDir = -_fishDir;
                    _turnTimer = SampleTurnInterval();
                }
            }

            float next = Fish01 + _fishDir * _fishSpeed01 * dt;

            // 撞到上下边界：贴边后掉头。夹住而不是直接掉头，
            // 是为了让「鱼贴着边界走」这件事看起来仍然是在游，而不是瞬移回中间。
            if (next > 1f - half) { next = 1f - half; _fishDir = -1f; }
            else if (next < half) { next = half; _fishDir = 1f; }

            Fish01 = next;
        }

        void StepBox(float dt, bool holding)
        {
            float accel = holding ? _t.liftAccel : -_t.gravityAccel;
            _boxVel += accel * dt;

            float cap = Math.Max(0.01f, _t.maxBoxSpeed);
            if (_boxVel > cap) _boxVel = cap;
            else if (_boxVel < -cap) _boxVel = -cap;

            float next = BoxCenter01 + _boxVel * dt;

            // 贴边即停：夹住位置并清速度，否则框会在边界上「弹」一下，判定飘
            if (next > 1f - BoxHalf01) { next = 1f - BoxHalf01; _boxVel = 0f; }
            else if (next < BoxHalf01) { next = BoxHalf01; _boxVel = 0f; }

            BoxCenter01 = next;
        }

        /// <summary>按「每秒 turnRate 次」的泊松过程取一个变向间隔。</summary>
        float SampleTurnInterval()
        {
            float rate = Lerp(_t.turnRateMin, _t.turnRateMax, Difficulty01);
            if (rate <= 0.0001f) return MaxTurnInterval;

            // u 取 (0,1)：1-u 落进 (0,1]，Log 不会炸
            double u = _rng.NextDouble();
            float interval = (float)(-Math.Log(1.0 - u * 0.999) / rate);
            if (interval < MinTurnInterval) return MinTurnInterval;
            if (interval > MaxTurnInterval) return MaxTurnInterval;
            return interval;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
