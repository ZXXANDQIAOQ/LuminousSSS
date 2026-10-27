using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 上鱼 QTE（拉条小游戏）面板：咬钩命中后弹出，玩家长按让「捕捞框」追鱼。
    ///
    /// 为什么不再「咬钩点一下直接进背包」：
    ///   一次点击换一条鱼，等于把钓鱼变成 3 秒一次的挂机刷钱；种子钱的定价会瞬间失效。
    ///   QTE 给的是一条**有失败可能**的产出通道，鱼的稀有度/重量直接决定这一杆有多难。
    ///
    /// 交互（横屏手指够得着）：
    ///   竖条放在屏幕右侧（左下是摇杆、右下是交互按钮，都不挡）；
    ///   按住屏幕任意位置 → 捕捞框向上加速，松开 → 向下落。
    ///   PC 上按住空格 / W / ↑ 等价于按住屏幕。
    ///
    /// 时间与输入锁的配对（本类最容易出事故的地方，改动前先看这里）：
    ///   面板打开 = <see cref="AcquireLocks"/>：PlayerController.InputLocked = true，
    ///   且 <see cref="UiModal"/> 把 Time.timeScale 置 0（面板是用 unscaledDeltaTime 跑的）。
    ///   面板关闭 = <see cref="ReleaseLocks"/>：两件事一起还回去。
    ///   所有退出路径（成功 / 失败 / 放弃 / 宿主 FishingSpot 被销毁 / 本组件被禁用）
    ///   都必须经过 <see cref="Shutdown"/>；<see cref="OnDisable"/> 里再兜一次 ——
    ///   任何一条异常路径漏掉恢复，玩家就会被永久锁在原地动不了（比丢一条鱼严重得多）。
    ///
    /// 数值内核在 <see cref="FishingQteModel"/>（纯 C#、不依赖 Unity），本类只负责
    /// 吃输入、画条、判胜负。
    /// </summary>
    public class FishingMinigame : MonoBehaviour
    {
        /// <summary>面板是否打开。<see cref="UiModal"/> 靠它决定要不要冻结时间。</summary>
        public static bool IsOpen => _inst != null && _inst._open;

        const int SortingOrder = 250;   // HUD 100 / 商店与打铁 200 / Toast 300

        static FishingMinigame _inst;

        // ------------------------------------------------------------ 可调参数

        [Header("竖条几何（UI 像素，1920x1080 基准）")]
        [Tooltip("竖条宽度")]
        public float barWidth = 150f;

        [Tooltip("竖条高度。难度与手感全部按归一化算，改这个只影响观感不影响平衡")]
        public float barHeight = 620f;

        [Tooltip("竖条离屏幕右边缘的距离")]
        public float barRightInset = 190f;

        [Tooltip("竖条内容离边框的内缩")]
        public float barInnerInset = 8f;

        [Header("难度 · 鱼")]
        [Tooltip("最低难度时鱼的移动速度（竖条/秒，1 = 整条高度）")]
        public float fishSpeedMin = 0.32f;

        [Tooltip("满难度时鱼的移动速度")]
        public float fishSpeedMax = 0.78f;

        [Tooltip("最低难度下鱼平均每秒变向次数")]
        public float turnRateMin = 0.25f;

        [Tooltip("满难度下鱼平均每秒变向次数（「爱突然变向」的来源）")]
        public float turnRateMax = 0.70f;

        [Range(0f, 1f)]
        [Tooltip("难度里稀有度的权重（1 = 只看稀有度，0 = 只看重量）")]
        public float rarityWeight = 0.65f;

        [Header("难度 · 捕捞框")]
        [Tooltip("★1 时捕捞框的半高（归一化）")]
        public float boxHalfBase = 0.155f;

        [Tooltip("每多一星，捕捞框半高缩小多少（「随 rarity 略微缩小」）")]
        public float boxHalfPerStar = 0.011f;

        [Tooltip("捕捞框半高下限：再难也不能缩成一条线，手机点不准")]
        public float boxHalfMin = 0.115f;

        [Tooltip("按住时向上加速度（竖条/秒²）。越大越跟手，也越容易过冲")]
        public float liftAccel = 5f;

        [Tooltip("松开时向下加速度。略小于 liftAccel = 下落更飘，给玩家一点喘息")]
        public float gravityAccel = 4.2f;

        [Tooltip("捕捞框速度上限")]
        public float maxBoxSpeed = 2f;

        [Header("进度与限时")]
        [Tooltip("开局进度（0~1）。不给 0 是为了避免「一上手漏一拍就立刻失败」")]
        public float startProgress = 0.30f;

        [Tooltip("重叠时进度每秒涨多少")]
        public float gainPerSecond = 0.20f;

        [Tooltip("不重叠时进度每秒掉多少。必须小于 gain 的 1/3 才不会一失误就必输")]
        public float lossPerSecond = 0.16f;

        [Tooltip("限时（秒）。到点算鱼跑了 —— 也是「双方都赢不了」的兜底")]
        public float timeLimitSeconds = 22f;

        [Header("表现")]
        [Tooltip("面板淡入时长（秒，unscaled）")]
        public float fadeInSeconds = 0.18f;

        [Tooltip("面板淡出时长（秒，unscaled）。淡出跑完才恢复 timeScale，避免玩家看到世界瞬间跳一帧")]
        public float fadeOutSeconds = 0.22f;

        // ------------------------------------------------------------ 运行时状态

        GameObject _root;          // Canvas（含遮罩）
        CanvasGroup _group;        // 整体淡入淡出
        RectTransform _barInner;   // 竖条内区（子元素的 0..1 坐标基准）
        RectTransform _boxRt;      // 捕捞框
        Image _boxImg;
        RectTransform _fishRt;     // 鱼标记
        Image _fishImg;
        Text _nameText, _starsText, _progressText, _timeText;
        Image _progressFill;

        FishingQteModel _model;
        string _fishId;
        float _weight;

        bool _open;
        bool _locksHeld;
        bool _finishing;
        bool _pointerHold;         // 手指/鼠标按住
        PlayerController _player;

        // 谁在等结果（FishingSpot 实例）。用它挡住「另一个钓鱼点抢答」
        object _owner;
        Action<bool> _onDone;

        // ================================================================
        // 对外入口
        // ================================================================

        /// <summary>
        /// 开一杆 QTE。返回 false = 已经有一杆在跑（重复调用直接忽略，不叠面板）。
        /// </summary>
        /// <param name="fishId">已确定的鱼（难度与展示都靠它）</param>
        /// <param name="weight">本次重量（kg），决定同种鱼内部的难度插值</param>
        /// <param name="owner">调用方（<see cref="FishingSpot"/>），用于销毁时收尾</param>
        /// <param name="onDone">结果回调：true = 上鱼，false = 鱼跑了。宿主已销毁时不会回调</param>
        public static bool Begin(string fishId, float weight, object owner, Action<bool> onDone)
        {
            if (string.IsNullOrEmpty(fishId)) return false;
            if (IsOpen) return false;

            if (_inst == null)
            {
                var go = new GameObject("[FishingMinigame]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<FishingMinigame>();
                _inst.Build();
            }

            _inst._fishId = fishId;
            _inst._weight = weight;
            _inst._owner = owner;
            _inst._onDone = onDone;
            _inst.StartRound();
            return true;
        }

        /// <summary>
        /// 宿主（FishingSpot）被销毁 / 换场景时收尾：只关属于它的那一杆，且不回调。
        /// 不回调是故意的 —— 场景都换了，再把「上鱼成功 + 结算卡片」弹到新场景里是错的。
        /// </summary>
        public static void AbortOwner(object owner)
        {
            if (_inst == null || !_inst._open) return;
            if (!ReferenceEquals(_inst._owner, owner)) return;

            _inst._onDone = null;
            _inst._owner = null;
            _inst.Shutdown();
        }

        // ================================================================
        // 锁：timeScale 与输入锁必须成对
        // ================================================================

        void AcquireLocks()
        {
            if (_locksHeld) return;
            _locksHeld = true;

            // InputLocked 只挡输入、不碰 Rigidbody，和 timeScale=0 是两道保险：
            // timeScale=0 本身已经冻住物理，但 Update 里的读键/摇杆轮询不会停。
            _player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            if (_player != null) _player.InputLocked = true;

            _open = true;
            UiModal.Apply();      // 顺序要求：先把 _open 置真，Apply 才会冻结时间
        }

        void ReleaseLocks()
        {
            if (!_locksHeld) return;
            _locksHeld = false;

            _open = false;
            if (_player != null) _player.InputLocked = false;
            _player = null;

            UiModal.Apply();      // 所有模态都关了才会把 timeScale 恢复成 1
        }

        /// <summary>
        /// 幂等收尾：关面板 → 还锁 → 清回调。所有退出路径的唯一出口。
        /// </summary>
        void Shutdown(bool stopCoroutines = true)
        {
            _open = false;
            _finishing = false;
            _pointerHold = false;
            _owner = null;

            // 从 FinishRoutine 里调用时不能 StopAllCoroutines：那会把「正在执行的自己」掐掉，
            // 后面的回调就不会跑。由调用方决定要不要停。
            if (stopCoroutines) StopAllCoroutines();

            ReleaseLocks();

            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// 兜底：组件被禁用（销毁父级 / 退出播放 / 换场景把常驻对象一起带走）时，
        /// 如果锁还在手里就必须还回去 —— 否则玩家永远动不了。
        /// 这个面板是 DontDestroyOnLoad 的，正常流程走不到这里，但「正常流程」不值得赌。
        /// </summary>
        void OnDisable()
        {
            if (!_locksHeld) return;
            _onDone = null;
            Shutdown();
        }

        // ================================================================
        // 一杆的开始 / 结束
        // ================================================================

        void StartRound()
        {
            var def = ItemDatabase.Find(_fishId);
            int rarity = def != null ? Mathf.Clamp(def.rarity, 1, 5) : 1;

            float min = def != null ? def.weightMin : 0f;
            float max = def != null ? def.weightMax : 1f;
            float ratio = max > min ? Mathf.Clamp01((_weight - min) / (max - min)) : 0f;

            var input = new FishingQteInput { rarity = rarity, weightRatio01 = ratio };
            int seed = UnityEngine.Random.Range(1, 999999);

            if (_model == null) _model = new FishingQteModel();
            _model.Reset(input, BuildTuning(), seed);

            _finishing = false;
            _pointerHold = false;
            _root.SetActive(true);
            ApplyIdentity(rarity);
            RefreshAll();

            AcquireLocks();

            StopAllCoroutines();
            StartCoroutine(FadeRoutine(0f, 1f, fadeInSeconds));
        }

        /// <summary>把 Inspector 上的参数打包给纯数据内核。</summary>
        FishingQteTuning BuildTuning()
        {
            return new FishingQteTuning
            {
                fishSpeedMin = fishSpeedMin,
                fishSpeedMax = fishSpeedMax,
                turnRateMin = turnRateMin,
                turnRateMax = turnRateMax,
                boxHalfBase = boxHalfBase,
                boxHalfPerStar = boxHalfPerStar,
                boxHalfMin = boxHalfMin,
                liftAccel = liftAccel,
                gravityAccel = gravityAccel,
                maxBoxSpeed = maxBoxSpeed,
                startProgress01 = startProgress,
                gainPerSecond = gainPerSecond,
                lossPerSecond = lossPerSecond,
                timeLimitSeconds = timeLimitSeconds,
                rarityWeight = rarityWeight,
            };
        }

        /// <summary>鱼名 / 星标 / 图标：没钓到过的鱼只给剪影 + 「？？？」，别提前剧透。</summary>
        void ApplyIdentity(int rarity)
        {
            bool known = FishingLog.GetCount(_fishId) > 0;
            _nameText.text = known ? ItemDatabase.NameOf(_fishId) : "？？？";
            _starsText.text = new string('★', rarity);

            var icon = ItemDatabase.IconOf(_fishId);
            if (icon == null) icon = FallbackFishSprite;
            _fishImg.sprite = icon;

            // 未知鱼压成剪影：不泄露颜色/花纹，但玩家能看出「是条鱼、多大一条」
            _fishImg.color = known ? Color.white : new Color(0.06f, 0.07f, 0.10f, 0.95f);
        }

        /// <summary>成功 / 失败都走这里；由协程负责淡出后再还锁，避免世界闪一下。</summary>
        void Finish(bool success)
        {
            if (_finishing) return;
            _finishing = true;
            StartCoroutine(FinishRoutine(success));
        }

        IEnumerator FinishRoutine(bool success)
        {
            yield return FadeRoutine(1f, 0f, fadeOutSeconds);

            // 先摘回调再 Shutdown：Shutdown 会清 _onDone
            var cb = _onDone;
            _onDone = null;
            Shutdown(false);
            cb?.Invoke(success);
        }

        IEnumerator FadeRoutine(float from, float to, float seconds)
        {
            if (_group == null) yield break;
            if (seconds <= 0.0001f) { _group.alpha = to; yield break; }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            _group.alpha = to;
        }

        // ================================================================
        // 每帧：吃输入 → 推进模型 → 画
        // ================================================================

        void Update()
        {
            if (!_open) return;

            if (!_finishing)
            {
                // 空格 / W / ↑ 三个都收：PC 上玩家的手可能停在任意一个
                bool kbd = Input.GetKey(KeyCode.Space)
                           || Input.GetKey(KeyCode.W)
                           || Input.GetKey(KeyCode.UpArrow);
                bool hold = _pointerHold || kbd;

                // 用 unscaledDeltaTime：面板开着时 timeScale = 0
                _model.Step(Time.unscaledDeltaTime, hold);
                RefreshAll(hold);

                switch (_model.Phase)
                {
                    case FishingQtePhase.Caught:
                        Finish(true);
                        break;
                    case FishingQtePhase.Escaped:
                    case FishingQtePhase.TimedOut:
                        Finish(false);
                        break;
                }
            }

            // 放弃：手长按到底也咬不上钩的极端情况，留一条「我认输」的路
            if (Input.GetKeyDown(KeyCode.Escape) && !_finishing) Finish(false);
        }

        /// <summary>指针按下（遮罩层转发）。面板淡出中忽略，避免最后 0.2 秒改结果。</summary>
        internal void OnHoldDown()
        {
            if (!_open || _finishing) return;
            _pointerHold = true;
        }

        internal void OnHoldUp()
        {
            _pointerHold = false;
        }

        // ================================================================
        // 画
        // ================================================================

        void RefreshAll(bool holding = false)
        {
            if (_model == null || _barInner == null) return;

            // 内区高度直接按布局算：UiFactory.Stretch 已经把它定死成 barHeight - 2*inset，
            // 不去读 RectTransform.rect —— 首帧布局还没算时 rect 会是 0，条会闪一下。
            float h = Mathf.Max(1f, barHeight - barInnerInset * 2f);

            // 竖条内区以中心为原点：把 0..1 映射到 -h/2..+h/2
            _fishRt.anchoredPosition = new Vector2(0f, (_model.Fish01 - 0.5f) * h);
            _boxRt.anchoredPosition = new Vector2(0f, (_model.BoxCenter01 - 0.5f) * h);
            _boxRt.sizeDelta = new Vector2(barWidth - barInnerInset * 2f, _model.BoxHalf01 * 2f * h);

            // 按住时把框提亮：手机上手指盖住屏幕时，这是唯一能确认「按住生效了」的反馈
            _boxImg.color = holding
                ? new Color(0.45f, 0.95f, 0.55f, 0.52f)
                : new Color(0.35f, 0.85f, 0.45f, 0.30f);

            // 进度条用锚点宽度（而不是 Image.fillAmount）：像素更硬，也不受 Filled 的 origin 影响
            float p = Mathf.Clamp01(_model.Progress01);
            var fillRt = _progressFill.rectTransform;
            fillRt.anchorMax = new Vector2(p, 1f);
            fillRt.offsetMax = Vector2.zero;
            _progressFill.color = p > 0.66f
                ? new Color32(120, 220, 130, 255)
                : (p > 0.33f ? new Color32(230, 200, 90, 255) : new Color32(225, 110, 90, 255));

            _progressText.text = $"{Mathf.RoundToInt(p * 100f)}%";

            int seconds = Mathf.Max(0, Mathf.CeilToInt(_model.TimeLeft));
            _timeText.text = $"剩余 {seconds}s";
            // 最后 5 秒变红：不给颜色提示的话，玩家会以为「怎么也钓不上来」是 bug
            _timeText.color = seconds <= 5 ? new Color32(255, 120, 100, 255) : new Color32(220, 220, 210, 255);
        }

        // ================================================================
        // 构建（运行时铺 UI，与 ShopPanel / ForgePanel 同套路）
        // ================================================================

        void Build()
        {
            var canvas = UiFactory.CreateCanvas(transform, "Canvas", SortingOrder);
            _root = canvas.gameObject;
            _group = _root.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // 全屏遮罩：既压暗场景，也是「按住」的热区。
            // 遮罩的 sortingOrder(250) 高于 HUD(100)，所以 QTE 期间点不到背包/交互按钮，
            // 不需要额外去禁用 HUD。
            var dim = UiFactory.MakeDim(_root.transform, 0.62f);
            var hold = dim.gameObject.AddComponent<FishingHoldArea>();
            hold.onDown = OnHoldDown;
            hold.onUp = OnHoldUp;

            BuildHeader();
            BuildBar();
            BuildHint();

            _root.SetActive(false);
        }

        void BuildHeader()
        {
            var root = _root.transform;

            // ---- 标题
            var title = UiFactory.MakeText(root, "咬钩了！按住拉杆", 46, TextAnchor.MiddleCenter,
                new Color32(255, 243, 208, 255));
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900f, 60f), new Vector2(0f, -70f));

            // ---- 鱼：图标 + 名字 + 星标（未知鱼只有剪影和 ？？？）
            var icon = UiFactory.MakeIcon(root, null, new Vector2(120f, 120f));
            UiFactory.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(120f, 120f), new Vector2(110f, -150f));
            _fishImg = icon;

            _nameText = UiFactory.MakeText(root, "", 44, TextAnchor.MiddleLeft,
                new Color32(255, 243, 208, 255));
            UiFactory.Place(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(420f, 56f), new Vector2(250f, -150f));

            _starsText = UiFactory.MakeText(root, "", 34, TextAnchor.MiddleLeft,
                new Color32(255, 206, 92, 255));
            UiFactory.Place(_starsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(420f, 44f), new Vector2(250f, -210f));

            // ---- 进度条
            var progLabel = UiFactory.MakeText(root, "上鱼进度", 30, TextAnchor.MiddleLeft,
                new Color32(200, 200, 190, 255));
            UiFactory.Place(progLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(300f, 40f), new Vector2(110f, -290f));

            var bg = new GameObject("ProgressBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root, false);
            var bgRt = bg.GetComponent<RectTransform>();
            UiFactory.Place(bgRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(620f, 46f), new Vector2(110f, -340f));
            bg.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.92f);

            var fill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            _progressFill = fill.GetComponent<Image>();
            _progressFill.color = new Color32(230, 200, 90, 255);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = new Vector2(4f, 4f);
            fillRt.offsetMax = new Vector2(-4f, -4f);

            _progressText = UiFactory.MakeText(root, "30%", 30, TextAnchor.MiddleLeft,
                new Color32(230, 230, 220, 255));
            UiFactory.Place(_progressText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(120f, 40f), new Vector2(750f, -340f));

            // ---- 剩余时间（右上角，远离竖条，不挡手）
            _timeText = UiFactory.MakeText(root, "剩余 22s", 36, TextAnchor.MiddleRight,
                new Color32(220, 220, 210, 255));
            UiFactory.Place(_timeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(400f, 50f), new Vector2(-80f, -70f));
        }

        void BuildBar()
        {
            var root = _root.transform;

            // ---- 竖条外框（右侧竖直居中）
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root, false);
            var barRt = bar.GetComponent<RectTransform>();
            UiFactory.Place(barRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(barWidth, barHeight), new Vector2(-barRightInset, -20f));
            bar.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.90f);

            // ---- 内区：0..1 的坐标基准（子元素都挂在这上面）
            var inner = new GameObject("BarInner", typeof(RectTransform));
            inner.transform.SetParent(bar.transform, false);
            _barInner = inner.GetComponent<RectTransform>();
            UiFactory.Stretch(_barInner, barInnerInset);

            var innerBg = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
            innerBg.transform.SetParent(inner.transform, false);
            UiFactory.Stretch(innerBg.GetComponent<RectTransform>(), 0f);
            innerBg.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            innerBg.GetComponent<Image>().raycastTarget = false;   // 装饰层，别抢「按住」
            innerBg.transform.SetAsFirstSibling();   // 垫在框和鱼下面

            // ---- 捕捞框
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(inner.transform, false);
            _boxRt = box.GetComponent<RectTransform>();
            _boxRt.anchorMin = _boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            _boxRt.pivot = new Vector2(0.5f, 0.5f);
            _boxRt.sizeDelta = new Vector2(barWidth - barInnerInset * 2f, barHeight * 0.3f);
            _boxImg = box.GetComponent<Image>();
            _boxImg.color = new Color(0.35f, 0.85f, 0.45f, 0.30f);
            _boxImg.raycastTarget = false;

            // ---- 鱼标记
            var fish = new GameObject("Fish", typeof(RectTransform), typeof(Image));
            fish.transform.SetParent(inner.transform, false);
            _fishRt = fish.GetComponent<RectTransform>();
            _fishRt.anchorMin = _fishRt.anchorMax = new Vector2(0.5f, 0.5f);
            _fishRt.pivot = new Vector2(0.5f, 0.5f);
            // 标记高度 ≈ 2 * FishHalf01 * barHeight：让「看到的重叠」= 「判定上的重叠」
            float markerH = Mathf.Max(28f, FishingQteModel.FishHalf01 * 2f * Mathf.Max(1f, barHeight));
            _fishRt.sizeDelta = new Vector2(markerH, markerH);
            _fishImg = fish.GetComponent<Image>();
            _fishImg.preserveAspect = true;
            _fishImg.raycastTarget = false;
        }

        void BuildHint()
        {
            var hint = UiFactory.MakeText(_root.transform, "按住屏幕上升 · 松开下落（PC：空格）", 34,
                TextAnchor.MiddleCenter, new Color32(210, 210, 200, 235));
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(1200f, 50f), new Vector2(0f, 90f));
        }

        // ------------------------------------------------------------ 兜底图标

        static Sprite _fallbackFish;

        /// <summary>
        /// 物品表里查不到图标时的兜底：程序化画一条 16x10 的鱼。
        /// 这种情况只会在美术素材没导入时出现，但「白方块」比「粗糙的鱼」更像 bug。
        /// </summary>
        static Sprite FallbackFishSprite
        {
            get
            {
                if (_fallbackFish != null) return _fallbackFish;

                const int w = 16, h = 10;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var body = new Color32(70, 120, 190, 255);
                var clear = new Color32(0, 0, 0, 0);
                var px = new Color32[w * h];

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (x + 0.5f) / w;   // 0..1
                        float ny = (y + 0.5f) / h;

                        // 身体：x 3..13 的椭圆；尾巴：x 0..3 的三角
                        bool inBody = ((nx - 0.58f) / 0.32f) * ((nx - 0.58f) / 0.32f)
                                    + ((ny - 0.5f) / 0.42f) * ((ny - 0.5f) / 0.42f) <= 1f;
                        bool inTail = nx < 0.30f && Mathf.Abs(ny - 0.5f) < (0.30f - nx) * 1.4f;

                        px[y * w + x] = (inBody || inTail) ? body : clear;
                    }

                tex.SetPixels32(px);
                tex.Apply();
                _fallbackFish = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
                return _fallbackFish;
            }
        }
    }

    /// <summary>
    /// 「按住屏幕」的事件接收层（挂在全屏遮罩上）。
    ///
    /// 为什么不做成一根专门的长按按钮：
    ///   手机横屏时双手都在屏幕两侧，让玩家去瞄一个小按钮反而更慢；
    ///   整屏可按住 = 随便哪根手指落下去都算，这是触屏拉条最舒服的做法。
    ///
    /// 单独成类（而不是嵌套在 FishingMinigame 里）：Unity 对嵌套 MonoBehaviour
    /// 的 AddComponent 支持并不可靠，独立类最稳。
    /// </summary>
    public class FishingHoldArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action onDown;
        public Action onUp;

        public void OnPointerDown(PointerEventData e) { onDown?.Invoke(); }

        // UGUI 会把 pointerUp 发回「按下时命中的那个对象」，所以手指滑出屏幕边缘再松
        // 也能收到这一下 —— 不然捕捞框会一直往上顶。
        public void OnPointerUp(PointerEventData e) { onUp?.Invoke(); }

        void OnDisable()
        {
            // 切后台 / 面板关闭时补一次松开：残留的「按住」会让框一直上浮到顶
            onUp?.Invoke();
        }
    }
}
