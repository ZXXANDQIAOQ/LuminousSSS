using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 钓鱼点的**表现层**：浮标、抛竿抛物线、落点水花、涟漪、水下鱼影、假咬钩、
    /// 咬钩抖动、鱼惊走。全部是运行时程序化生成的图，不依赖任何美术素材。
    ///
    /// 为什么从 <see cref="FishingSpot"/> 里拆出来：
    ///   状态机 + 调度已经够长了，再加「浮标怎么飞、水花几粒、鱼影怎么游」会变成 800 行巨石；
    ///   而且这些表现彼此独立、可以单独调，放在一起改一处动全身。
    ///   拆开之后 FishingSpot 只在正确的时间点喊一声「抛！」「咬钩了」「鱼跑了」，
    ///   不碰任何 Transform / SpriteRenderer。
    ///
    /// 排序层级（池塘本体 PlaceProp 用的是 -95，地面 -100，物件 ≥0）：
    ///   鱼影 -60 → 涟漪 -50 → 水珠 -45 → 浮标 0
    ///   也就是「水下的东西压在水面下、水面上的一切按原来的层级走」。
    ///
    /// 时间基准一律 <see cref="Time.deltaTime"/>（不是 unscaled）：
    ///   这是**世界**里的动画，玩家打开背包/商店把 timeScale 冻住时，水面也该跟着停；
    ///   用 unscaled 会让水花在冻结的画面里继续动，看起来像穿帮。
    ///   （UI 面板里的动画才用 unscaled —— 那边的世界本来就是停的。）
    /// </summary>
    [DisallowMultipleComponent]
    public class FishingBobberFx : MonoBehaviour
    {
        /// <summary>浮标贴图的像素密度：6x10 的图按 16 PPU 生成，1 世界单位 = 16 像素。</summary>
        public const float PixelsPerUnit = 16f;

        [Header("抛竿")]
        [Tooltip("抛物线最高点相对直线的抬升（世界单位）。1.1 在 2 格距离上像一条舒服的弧")]
        public float castArcHeight = 1.1f;

        [Tooltip("浮标飞出去时的起始缩放（1 = 不变）。略小于 1 会有一点「甩出去」的感觉")]
        public float castStartScale = 0.85f;

        [Header("落点水花")]
        [Tooltip("水珠数量")]
        public int splashDroplets = 7;

        [Tooltip("水珠初速（世界单位/秒）")]
        public float dropletSpeed = 2.4f;

        [Tooltip("水珠重力（越大落得越快，水花越矮）")]
        public float dropletGravity = 9f;

        [Tooltip("水珠存活时长（秒）")]
        public float dropletSeconds = 0.42f;

        [Header("涟漪")]
        [Tooltip("一圈涟漪的存在时长（秒）")]
        public float rippleSeconds = 0.55f;

        [Tooltip("涟漪起始/结束缩放（相对 1 世界单位）")]
        public float rippleStartScale = 0.15f;
        public float rippleEndScale = 1.2f;

        [Tooltip("涟漪颜色（带 alpha）")]
        public Color rippleColor = new Color(0.82f, 0.93f, 1f, 0.8f);

        [Header("水下鱼影")]
        [Tooltip("鱼影透明度。0.45 在池塘的青绿色水面上隐约可见，不抢浮标")]
        [Range(0f, 1f)]
        public float shadowAlpha = 0.45f;

        [Tooltip("鱼影颜色")]
        public Color shadowColor = new Color(0.05f, 0.12f, 0.16f, 1f);

        [Tooltip("鱼影横向游动距离（世界单位）。预警只有 0.35 秒，距离别超过 1.5 —— 否则鱼影像鱼雷")]
        public float shadowSwimDistance = 1.2f;

        [Tooltip("鱼影尺寸（世界单位宽）")]
        public float shadowSize = 0.75f;

        [Header("假咬钩")]
        [Tooltip("预摇幅度（像素）。故意做大一点：这是玩家唯一能看出「这是假动作」的线索")]
        public float fakePreShakePixels = 1.6f;

        [Tooltip("预摇频率（Hz）")]
        public float fakePreShakeHz = 11f;

        [Header("咬钩 / 惊走")]
        [Tooltip("咬钩时浮标上下急跳的幅度（世界单位）")]
        public float biteShakeAmp = 0.12f;

        [Tooltip("咬钩急跳频率（Hz）。26 在 60fps 下每秒约 1.5 个来回，眼睛抓得住")]
        public float biteShakeHz = 26f;

        [Tooltip("鱼惊走时涟漪的扩散缩放")]
        public float escapeRippleScale = 1.8f;

        [Header("排序层级")]
        public int bobberOrder = 0;
        public int rippleOrder = -50;
        public int shadowOrder = -60;
        public int dropletOrder = -45;

        // ------------------------------------------------------------ 运行时

        Transform _anchor;
        Vector2 _offset;
        Transform _bobber;
        SpriteRenderer _bobberSr;
        Vector3 _bobberBaseScale = Vector3.one;

        Coroutine _castCo, _fakeCo, _shakeCo;
        bool _shaking;

        // ---- 小粒子池（涟漪 / 水珠 / 鱼影共用一套推进逻辑）
        enum PKind { Ring, Droplet, Shadow }

        class P
        {
            public Transform tr;
            public SpriteRenderer sr;
            public PKind kind;
            public bool active;
            public float age, life;
            public Vector3 from, to;
            public Vector2 vel;
            public float gravity, s0, s1;
            public Color c0, c1;
            public float wiggle;
        }

        readonly List<P> _pool = new List<P>();
        int _poolCursor;

        // ================================================================
        // 对外接口（FishingSpot 调这些）
        // ================================================================

        /// <summary>
        /// 绑定宿主与浮标偏移。由 FishingSpot 在 Awake 里调用 ——
        /// 偏移是场景装配时写进 Inspector 的，浮标位置必须晚于它才能算。
        /// </summary>
        public void SetAnchor(Transform anchor, Vector2 offset)
        {
            _anchor = anchor;
            _offset = offset;
        }

        /// <summary>浮标该落的位置（宿主位置 + 偏移）。</summary>
        public Vector3 HomePos
        {
            get
            {
                var t = _anchor != null ? _anchor : transform;
                return t.position + (Vector3)_offset;
            }
        }

        /// <summary>
        /// 抛竿：浮标从 <paramref name="fromWorld"/>（玩家手边）沿抛物线飞到落点，
        /// 到点溅水花 + 播入水音效。时长由 FishingSpot 的 castSeconds 决定，两边必须一致。
        /// </summary>
        public void PlayCast(Vector3 fromWorld, float seconds)
        {
            EnsureBobber();
            ResetAll();

            _bobber.gameObject.SetActive(true);
            _bobber.position = fromWorld;
            _bobber.localScale = _bobberBaseScale * Mathf.Max(0.01f, castStartScale);

            _castCo = StartCoroutine(CastRoutine(fromWorld, HomePos, Mathf.Max(0.05f, seconds)));
        }

        /// <summary>
        /// 真咬钩前的预警：1~2 圈涟漪 + 一条鱼影从侧面游到浮标底下。
        /// 这是玩家区分「真咬钩」和「假咬钩」的主要视觉线索（假咬钩只有抖，没有影）。
        /// </summary>
        /// <param name="seconds">预警时长，与 FishingSpot 的 biteWarningSeconds 一致</param>
        /// <param name="rippleCount">涟漪圈数（1~2）</param>
        public void PlayBiteWarning(float seconds, int rippleCount)
        {
            var home = HomePos;

            int n = Mathf.Clamp(rippleCount, 1, 3);
            for (int i = 0; i < n; i++)
            {
                // 错开一点起跳时间：两圈同时出现会像一坨，错开才像水波一圈圈荡开
                SpawnRing(home, rippleSeconds * (1f + i * 0.35f),
                          rippleStartScale, rippleEndScale * (1f + i * 0.4f), i * 0.14f);
            }

            SpawnShadow(home, seconds);
        }

        /// <summary>
        /// 假咬钩：先 0.35s 预摇（抖但不沉），再下沉 2~3px 后回位。
        ///
        /// 预摇为什么必须有：等待期玩家可以点，但只有「下沉」那一下点才会惊跑鱼。
        /// 没有预摇的话，玩家看到浮标动已经来不及了 —— 预摇是给眼睛的 0.35 秒预警，
        /// 让「这动作是假的」成立，惩罚才不算冤枉人。
        /// </summary>
        public void PlayFakeBite(float preShakeSeconds, float dipSeconds, float dipPixels)
        {
            EnsureBobber();
            if (_shakeCo != null) { StopCoroutine(_shakeCo); _shakeCo = null; _shaking = false; }
            if (_fakeCo != null) StopCoroutine(_fakeCo);

            _bobber.gameObject.SetActive(true);
            _fakeCo = StartCoroutine(FakeBiteRoutine(preShakeSeconds, dipSeconds, dipPixels));
        }

        /// <summary>咬钩窗口内的浮标急跳。开 / 关都要能安全重复调用。</summary>
        public void SetBiteShake(bool on)
        {
            if (on)
            {
                if (_shaking) return;
                EnsureBobber();
                _bobber.gameObject.SetActive(true);
                _shaking = true;
                _shakeCo = StartCoroutine(ShakeRoutine());
            }
            else
            {
                _shaking = false;
                if (_shakeCo != null) { StopCoroutine(_shakeCo); _shakeCo = null; }
                if (_bobber != null) _bobber.position = HomePos;
            }
        }

        /// <summary>鱼惊走：浮标被拽走一下、两圈大涟漪、鱼影逃开，然后全部收起。</summary>
        public void PlayEscape()
        {
            var home = HomePos;
            SpawnRing(home, rippleSeconds * 1.2f, rippleStartScale, escapeRippleScale, 0f);
            SpawnRing(home, rippleSeconds * 1.6f, rippleStartScale, escapeRippleScale * 1.3f, 0.12f);
            SpawnShadowFlee(home);

            StopBiteShakeQuietly();
            if (_fakeCo != null) { StopCoroutine(_fakeCo); _fakeCo = null; }
            if (_castCo != null) { StopCoroutine(_castCo); _castCo = null; }
            if (_bobber != null) _bobber.gameObject.SetActive(false);
        }

        /// <summary>收竿 / 回到空闲：停一切动画、收起浮标、清掉残留粒子。</summary>
        public void ResetAll()
        {
            if (_castCo != null) { StopCoroutine(_castCo); _castCo = null; }
            if (_fakeCo != null) { StopCoroutine(_fakeCo); _fakeCo = null; }
            StopBiteShakeQuietly();

            if (_bobber != null)
            {
                _bobber.position = HomePos;
                _bobber.localScale = _bobberBaseScale;
                _bobber.gameObject.SetActive(false);
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                if (!p.active) continue;
                p.active = false;
                p.sr.enabled = false;
            }
        }

        void StopBiteShakeQuietly()
        {
            _shaking = false;
            if (_shakeCo != null) { StopCoroutine(_shakeCo); _shakeCo = null; }
        }

        // ================================================================
        // 浮标动画
        // ================================================================

        IEnumerator CastRoutine(Vector3 from, Vector3 to, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);

                // 水平线性插值 + 竖直抛物线：4k(1-k) 在 k=0.5 取到 1，正是「中点为最高」
                var p = new Vector3(
                    from.x + (to.x - from.x) * k,
                    from.y + (to.y - from.y) * k + castArcHeight * 4f * k * (1f - k),
                    0f);
                _bobber.position = p;
                _bobber.localScale = _bobberBaseScale * Mathf.Lerp(castStartScale, 1f, k);
                yield return null;
            }

            _bobber.position = to;
            _bobber.localScale = _bobberBaseScale;
            _castCo = null;

            Splash(to);
        }

        IEnumerator FakeBiteRoutine(float preShake, float dip, float dipPixels)
        {
            var home = HomePos;
            float px = Mathf.Max(0f, dipPixels) / PixelsPerUnit;   // 像素 → 世界单位
            float preShakeAmp = Mathf.Max(0f, fakePreShakePixels) / PixelsPerUnit;

            // ---- 预摇：只在 X 轴左右轻抖，**不下沉**。
            // 不下沉是刻意的：下沉是「真咬钩」的视觉约定，假动作如果也下沉，
            // 玩家就学不到「下沉 = 别点」这个规则。
            float t = 0f;
            while (t < preShake)
            {
                t += Time.deltaTime;
                float a = Mathf.Sin(t * fakePreShakeHz * Mathf.PI * 2f) * preShakeAmp;
                _bobber.position = home + new Vector3(a, 0f, 0f);
                yield return null;
            }

            // ---- 下沉 2~3px 再回位。用 sin 的半周期：下去再上来都是平滑的，不会「啪」一下
            t = 0f;
            while (t < dip)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dip);
                _bobber.position = home + new Vector3(0f, -px * Mathf.Sin(k * Mathf.PI), 0f);
                yield return null;
            }

            _bobber.position = home;
            _fakeCo = null;
        }

        IEnumerator ShakeRoutine()
        {
            var home = HomePos;
            float t = 0f;
            while (_shaking)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(t * biteShakeHz * Mathf.PI * 2f) * biteShakeAmp;
                if (_bobber != null) _bobber.position = home + new Vector3(0f, k, 0f);
                yield return null;
            }
        }

        // ================================================================
        // 粒子（涟漪 / 水珠 / 鱼影）
        // ================================================================

        void Splash(Vector3 at)
        {
            // 一圈快速扩散的涟漪：落水那一瞬间水面被顶开
            SpawnRing(at, rippleSeconds * 0.8f, rippleStartScale, rippleEndScale * 0.9f, 0f);

            // 几粒像素水珠往斜上方抛散，落回水面
            int n = Mathf.Clamp(splashDroplets, 0, 24);
            for (int i = 0; i < n; i++)
            {
                float ang = Random.Range(35f, 145f) * Mathf.Deg2Rad;   // 只往上半个圆周
                float spd = dropletSpeed * Random.Range(0.55f, 1.15f);
                var vel = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd);
                SpawnDroplet(at, vel);
            }

            AudioManager.PlaySplash();
        }

        void SpawnRing(Vector3 at, float life, float s0, float s1, float delay)
        {
            var p = Take(PKind.Ring);
            if (p == null) return;

            p.life = Mathf.Max(0.05f, life);
            p.age = -Mathf.Max(0f, delay);       // 负 age 就是「延迟出场」
            p.from = at;
            p.s0 = s0;
            p.s1 = s1;
            p.c0 = rippleColor;
            p.c1 = new Color(rippleColor.r, rippleColor.g, rippleColor.b, 0f);

            p.tr.position = at;
            p.tr.localScale = Vector3.one * s0;
            p.sr.sprite = RippleSprite;
            p.sr.sortingOrder = rippleOrder;
            p.sr.color = p.c0;
            p.sr.enabled = false;                // 延迟期间先不显形
        }

        void SpawnDroplet(Vector3 at, Vector2 vel)
        {
            var p = Take(PKind.Droplet);
            if (p == null) return;

            p.life = Mathf.Max(0.05f, dropletSeconds * Random.Range(0.75f, 1.25f));
            p.age = 0f;
            p.vel = vel;
            p.gravity = dropletGravity;
            p.s0 = p.s1 = Random.Range(0.04f, 0.075f);
            p.c0 = new Color(0.80f, 0.93f, 1f, 0.95f);
            p.c1 = new Color(0.80f, 0.93f, 1f, 0f);

            p.tr.position = at;
            p.tr.localScale = Vector3.one * p.s0;
            p.sr.sprite = DotSprite;
            p.sr.sortingOrder = dropletOrder;
            p.sr.color = p.c0;
        }

        /// <summary>鱼影从一侧游到浮标正下方（水面以下、浮标以下）。</summary>
        void SpawnShadow(Vector3 target, float seconds)
        {
            var p = Take(PKind.Shadow);
            if (p == null) return;

            float dir = Random.value < 0.5f ? -1f : 1f;      // 随机从左或右游来
            p.life = Mathf.Max(0.15f, seconds);
            p.age = 0f;
            p.from = target + new Vector3(-dir * shadowSwimDistance, 0f, 0f);
            p.to = target;
            p.s0 = shadowSize;
            p.s1 = shadowSize;
            p.wiggle = 0.06f;
            p.c0 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowAlpha);
            p.c1 = p.c0;

            p.tr.position = p.from;
            p.tr.localScale = Vector3.one * shadowSize;
            p.sr.sprite = ShadowSprite;
            p.sr.sortingOrder = shadowOrder;
            p.sr.color = p.c0;
            p.sr.flipX = dir > 0f;
        }

        /// <summary>鱼惊走：鱼影从浮标下方往外窜出去并淡出。</summary>
        void SpawnShadowFlee(Vector3 from)
        {
            var p = Take(PKind.Shadow);
            if (p == null) return;

            float dir = Random.value < 0.5f ? -1f : 1f;
            p.life = 0.5f;
            p.age = 0f;
            p.from = from;
            p.to = from + new Vector3(dir * shadowSwimDistance * 1.2f, -0.15f, 0f);
            p.s0 = shadowSize;
            p.s1 = shadowSize * 0.7f;
            p.wiggle = 0.04f;
            p.c0 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowAlpha);
            p.c1 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);

            p.tr.position = from;
            p.tr.localScale = Vector3.one * shadowSize;
            p.sr.sprite = ShadowSprite;
            p.sr.sortingOrder = shadowOrder;
            p.sr.color = p.c0;
            p.sr.flipX = dir > 0f;
        }

        /// <summary>从池里取一个粒子（不够就建）。返回 null = 池已满并且没有空闲。</summary>
        P Take(PKind kind)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                int idx = (_poolCursor + i) % _pool.Count;
                if (_pool[idx].active) continue;
                _poolCursor = (idx + 1) % _pool.Count;
                var free = _pool[idx];
                free.active = true;
                free.kind = kind;
                free.sr.enabled = true;
                free.tr.gameObject.SetActive(true);
                return free;
            }

            if (_pool.Count >= 40) return null;    // 上限：极端情况下也绝不无限建对象

            var go = new GameObject("fx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            var p = new P { tr = go.transform, sr = sr, active = true, kind = kind };
            _pool.Add(p);
            _poolCursor = (_poolCursor + 1) % _pool.Count;
            return p;
        }

        void Update()
        {
            // 世界时间：与 FishingSpot 的 WaitForSeconds 同基准，面板暂停时一起停
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                if (!p.active) continue;

                p.age += dt;

                // 延迟出场的涟漪：age 还是负的先不显示
                if (p.age < 0f)
                {
                    if (p.sr.enabled) p.sr.enabled = false;
                    continue;
                }
                if (!p.sr.enabled) p.sr.enabled = true;

                float k = p.life > 0f ? Mathf.Clamp01(p.age / p.life) : 1f;

                switch (p.kind)
                {
                    case PKind.Ring:
                        p.tr.localScale = Vector3.one * Mathf.Lerp(p.s0, p.s1, k);
                        p.sr.color = Color.Lerp(p.c0, p.c1, k);
                        break;

                    case PKind.Droplet:
                        p.vel.y -= p.gravity * dt;
                        p.tr.position += (Vector3)(p.vel * dt);
                        p.sr.color = Color.Lerp(p.c0, p.c1, k);
                        break;

                    case PKind.Shadow:
                        {
                            // 游动 + 一点点上下摆动：直线平移看起来像贴纸在滑
                            var pos = new Vector3(
                                p.from.x + (p.to.x - p.from.x) * k,
                                p.from.y + (p.to.y - p.from.y) * k
                                    + Mathf.Sin(k * Mathf.PI * 3f) * p.wiggle,
                                0f);
                            p.tr.position = pos;
                            p.tr.localScale = Vector3.one * Mathf.Lerp(p.s0, p.s1, k);
                            // 头两成时间淡入，最后一成淡出，避免突然出现/消失
                            float a = k < 0.2f ? k / 0.2f : (k > 0.9f ? (1f - k) / 0.1f : 1f);
                            var c = Color.Lerp(p.c0, p.c1, k);
                            p.sr.color = new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(a));
                            break;
                        }
                }

                if (k >= 1f)
                {
                    p.active = false;
                    p.sr.enabled = false;
                    p.tr.gameObject.SetActive(false);
                }
            }
        }

        // ================================================================
        // 程序化贴图
        // ================================================================

        void EnsureBobber()
        {
            if (_bobber != null) return;

            var go = new GameObject("Bobber", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            _bobber = go.transform;
            _bobber.position = HomePos;

            _bobberSr = go.GetComponent<SpriteRenderer>();
            _bobberSr.sprite = BobberSprite;
            _bobberSr.sortingOrder = bobberOrder;   // 与水面上方的装饰同层
            _bobberBaseScale = Vector3.one;         // 不读 localScale：这里就是新建对象的默认值
        }

        static Sprite _bobberSprite, _rippleSprite, _shadowSprite, _dotSprite;

        /// <summary>
        /// 6x10 的红白浮标（上红下白的小椭圆）。
        /// 不引素材文件：浮标只在运行时出现，为 60 像素专门放一张 png 不值当。
        /// </summary>
        static Sprite BobberSprite
        {
            get
            {
                if (_bobberSprite != null) return _bobberSprite;

                const int w = 6, h = 10;
                var tex = NewTexture(w, h, "fx_bobber");
                var white = new Color32(246, 241, 226, 255);
                var red = new Color32(198, 58, 48, 255);
                var clear = new Color32(0, 0, 0, 0);
                var px = new Color32[w * h];

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                        float ny = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                        bool inside = nx * nx + ny * ny <= 1f;
                        px[y * w + x] = inside ? (ny > 0.12f ? red : white) : clear;
                    }

                tex.SetPixels32(px);
                tex.Apply();
                _bobberSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                return _bobberSprite;
            }
        }

        /// <summary>24x24 的空心环：涟漪。硬边两级 alpha，配 Point 过滤不糊。</summary>
        static Sprite RippleSprite
        {
            get
            {
                if (_rippleSprite != null) return _rippleSprite;

                const int s = 24;
                var tex = NewTexture(s, s, "fx_ripple");
                var px = new Color32[s * s];

                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float nx = (x + 0.5f) / s * 2f - 1f;
                        float ny = (y + 0.5f) / s * 2f - 1f;
                        float d = Mathf.Sqrt(nx * nx + ny * ny);

                        // 环主体 0.72~0.95，外圈再补一圈很淡的（水波的余韵）
                        byte a = 0;
                        if (d > 0.86f && d < 0.97f) a = 255;
                        else if (d > 0.72f && d <= 0.86f) a = 150;

                        px[y * s + x] = new Color32(255, 255, 255, a);
                    }

                tex.SetPixels32(px);
                tex.Apply();
                _rippleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _rippleSprite;
            }
        }

        /// <summary>20x10 的鱼影：椭圆身体 + 三角尾。只做剪影，细节看不见也没意义。</summary>
        static Sprite ShadowSprite
        {
            get
            {
                if (_shadowSprite != null) return _shadowSprite;

                const int w = 20, h = 10;
                var tex = NewTexture(w, h, "fx_fish_shadow");
                var px = new Color32[w * h];
                var solid = new Color32(255, 255, 255, 255);
                var clear = new Color32(0, 0, 0, 0);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (x + 0.5f) / w;
                        float ny = (y + 0.5f) / h;

                        float bx = (nx - 0.60f) / 0.34f;
                        float by = (ny - 0.50f) / 0.44f;
                        bool body = bx * bx + by * by <= 1f;

                        // 尾巴：靠左侧、上下张开的三角
                        bool tail = nx < 0.34f && Mathf.Abs(ny - 0.5f) < (0.34f - nx) * 1.15f;

                        // 背鳍：上缘一个小三角，剪影更像鱼
                        bool fin = nx > 0.52f && nx < 0.74f && ny > 0.72f && ny < 0.72f + (0.74f - nx) * 0.9f;

                        px[y * w + x] = (body || tail || fin) ? solid : clear;
                    }

                tex.SetPixels32(px);
                tex.Apply();
                // PPU = 24：图宽 20px / 24 ≈ 0.83 世界单位，乘 shadowSize 就是最终大小
                _shadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24f);
                return _shadowSprite;
            }
        }

        /// <summary>4x4 纯白方块：水珠。拉伸成小方块就是像素水点。</summary>
        static Sprite DotSprite
        {
            get
            {
                if (_dotSprite != null) return _dotSprite;

                const int s = 4;
                var tex = NewTexture(s, s, "fx_dot");
                var px = new Color32[s * s];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                _dotSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                return _dotSprite;
            }
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
