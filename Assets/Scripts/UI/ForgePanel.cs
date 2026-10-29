using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 铁匠铺锻造界面。
    ///
    /// 运行时用代码铺 UI（与 ShopPanel / BackpackPanel 同套路），内容由
    /// <see cref="ForgeConfig"/> 与 <see cref="RelicDatabase"/> 决定。
    ///
    /// 布局：
    ///   ┌─────────────────────────────────────┐
    ///   │ [剑价值]        850  (+20↑)          │
    ///   │ [破碎概率]      12%                  │
    ///   │ ─────────────────────────────────── │
    ///   │ [本锤概率]  破坏5 损伤15 维持30       │
    ///   │            成功40 大成功10            │
    ///   │ ─────────────────────────────────── │
    ///   │ [入神充能]  ████████░░  8/10         │
    ///   │ ─────────────────────────────────── │
    ///   │   [入炉]  [锤击]  [收剑]              │
    ///   │   [材料] 铁矿×1 + 煤炭×1              │
    ///   │ ─────────────────────────────────── │
    ///   │ [遗物兑换]  龙鳞护符(500)  ...        │
    ///   └─────────────────────────────────────┘
    /// </summary>
    public class ForgePanel : MonoBehaviour
    {
        public static bool IsOpen => _inst != null && _inst._root != null && _inst._root.gameObject.activeSelf;

        const int SortingOrder = 200;
        const float PanelW = 1040f;
        const float PanelLeftInset = 40f;    // 面板离屏幕左边缘
        const float CameraShift = -2f;       // 打开面板时相机左移，把玩家/铁匠让到右半屏
        const float PanelH = 860f;
        const float UiPpu = UiFactory.UiPpu;

        static ForgePanel _inst;
        static BlacksmithSpot _currentSpot;

        GameObject _root;
        ForgeSession _session;

        // UI 引用
        Text _valueText;
        Text _breakText;
        Text _probText;
        Text _frenzyText;
        Image _frenzyBar;
        Text _statusText;
        Text _materialText;
        Button _startBtn;
        Button _strikeBtn;
        Button _collectBtn;
        RectTransform _relicContent;

        // ---- 打铁舞台（v0.20：像素精灵可视化）----
        Image _furnaceImg;
        Image _flameImg;
        Image _anvilImg;
        Image _swordImg;
        Image _hammerImg;
        Coroutine _hammerCo, _flameCo;

        // ------------------------------------------------------------ 对外入口

        public static void Open(BlacksmithSpot spot)
        {
            if (ShopPanel.IsOpen) ShopPanel.Close();
            if (BackpackPanel.IsOpen) BackpackPanel.Close();

            ForgeFxLoader.EnsureLoaded();   // v0.20：自动加载像素精灵 + 补挂 AudioManager clip

            _currentSpot = spot;
            if (_inst == null)
            {
                var go = new GameObject("[ForgePanel]");
                Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<ForgePanel>();
                _inst.Build();
            }

            _inst._root.SetActive(true);
            _inst.BindStage(spot);
            _inst.ResetSession();
            UiModal.Apply();
        }

        public static void Close()
        {
            if (_inst == null || _inst._root == null) return;
            _inst._root.SetActive(false);
            _inst.ClearStage();
            UiModal.Apply();
        }

        // ================================================================
        // 打铁舞台（v0.20）：像素精灵场景
        // 左侧熔炉（火焰循环）+ 中央铁砧（剑坯随加热变色）+ 上方锤子（三帧挥击）
        // ================================================================

        const float StageScale = 2.5f;   // 像素放大倍数（保持硬边）

        void BuildStage(RectTransform panelPt)
        {
            // 舞台底板（暗暖色，像铁匠铺墙角）
            var stageGo = new GameObject("Stage", typeof(RectTransform), typeof(Image));
            stageGo.transform.SetParent(panelPt, false);
            var stageRt = stageGo.GetComponent<RectTransform>();
            UiFactory.Place(stageRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(900f, 220f), new Vector2(0f, -90f));
            var stageImg = stageGo.GetComponent<Image>();
            stageImg.color = new Color(0.18f, 0.12f, 0.08f, 0.9f);
            stageImg.raycastTarget = false;

            // 地面线
            var ground = new GameObject("Ground", typeof(RectTransform), typeof(Image));
            ground.transform.SetParent(stageRt, false);
            var gRt = ground.GetComponent<RectTransform>();
            UiFactory.Place(gRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(860f, 4f), new Vector2(0f, 20f));
            ground.GetComponent<Image>().color = new Color(0.3f, 0.22f, 0.15f, 1f);

            // ---- 熔炉（左侧）----
            _furnaceImg = MakeStageSprite(stageRt, "Furnace",
                ForgeFxLoader.Furnace,
                new Vector2(80 * 2f, 60 * 2f),          // 160x120
                new Vector2(-320f, 30f));                // 舞台内偏左

            // 火焰（叠在炉口位置）
            _flameImg = MakeStageSprite(stageRt, "Flame",
                ForgeFxLoader.FlameFrames != null && ForgeFxLoader.FlameFrames.Length > 0
                    ? ForgeFxLoader.FlameFrames[0] : null,
                new Vector2(20 * 2f, 28 * 2f),          // 40x56
                new Vector2(-320f, -10f));               // 炉口位置

            // ---- 铁砧（中央）----
            _anvilImg = MakeStageSprite(stageRt, "Anvil",
                ForgeFxLoader.Anvil,
                new Vector2(64 * StageScale, 48 * StageScale),   // 160x120
                new Vector2(60f, 10f));

            // ---- 剑坯（放在砧面上）----
            _swordImg = MakeStageSprite(stageRt, "Sword",
                ForgeFxLoader.SwordCold,
                new Vector2(56 * StageScale, 12 * StageScale),   // 140x30
                new Vector2(60f, 55f));                            // 砧面高度

            // ---- 锤子（铁砧上方，默认举高姿态）----
            _hammerImg = MakeStageSprite(stageRt, "Hammer",
                ForgeFxLoader.HammerFrames != null && ForgeFxLoader.HammerFrames.Length > 0
                    ? ForgeFxLoader.HammerFrames[0] : null,
                new Vector2(32 * StageScale, 32 * StageScale),   // 80x80
                new Vector2(120f, 130f));                          // 砧面上方

            // 启动火焰循环
            if (ForgeFxLoader.FlameFrames != null && ForgeFxLoader.FlameFrames.Length > 0)
                _flameCo = StartCoroutine(FlameLoopRoutine());
        }

        Image MakeStageSprite(RectTransform parent, string name, Sprite sprite, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            UiFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, pos);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.pixelsPerUnit = 1f;
            return img;
        }

        IEnumerator FlameLoopRoutine()
        {
            int i = 0;
            while (true)
            {
                if (_flameImg != null && ForgeFxLoader.FlameFrames != null
                    && i < ForgeFxLoader.FlameFrames.Length)
                {
                    _flameImg.sprite = ForgeFxLoader.FlameFrames[i];
                }
                i = (i + 1) % 4;
                yield return new WaitForSeconds(0.15f);
            }
        }

        /// <summary>根据当前锤击次数切换剑坯加热颜色。</summary>
        void UpdateSwordHeatVisual(int hitCount, bool broken)
        {
            if (_swordImg == null) return;
            if (broken)
            {
                _swordImg.sprite = ForgeFxLoader.SwordBroken;
                return;
            }
            if (hitCount <= 0) _swordImg.sprite = ForgeFxLoader.SwordCold;
            else if (hitCount <= 3) _swordImg.sprite = ForgeFxLoader.SwordHot1;
            else if (hitCount <= 6) _swordImg.sprite = ForgeFxLoader.SwordHot2;
            else _swordImg.sprite = ForgeFxLoader.SwordHot3;
        }

        /// <summary>播放一次锤子挥击动画（举高→下挥→命中→回位）。</summary>
        void PlayHammerAnim()
        {
            if (_hammerImg == null || ForgeFxLoader.HammerFrames == null
                || ForgeFxLoader.HammerFrames.Length < 3) return;
            if (_hammerCo != null) StopCoroutine(_hammerCo);
            _hammerCo = StartCoroutine(HammerAnimRoutine());
        }

        IEnumerator HammerAnimRoutine()
        {
            // f1: 举高
            _hammerImg.sprite = ForgeFxLoader.HammerFrames[0];
            yield return new WaitForSecondsRealtime(0.05f);
            // f2: 下挥
            _hammerImg.sprite = ForgeFxLoader.HammerFrames[1];
            yield return new WaitForSecondsRealtime(0.05f);
            // f3: 命中（压下）
            _hammerImg.sprite = ForgeFxLoader.HammerFrames[2];
            // 命中瞬间在砧面位置爆火花（用像素精灵替换原来的方块）
            PixelSparks(new Color(1f, 0.85f, 0.4f), 8);
            yield return new WaitForSecondsRealtime(0.08f);
            // 回位
            _hammerImg.sprite = ForgeFxLoader.HammerFrames[0];
        }

        /// <summary>像素星火花（替换原 UGUI 方块粒子）。</summary>
        void PixelSparks(Color color, int count = 8)
        {
            if (ForgeFxLoader.Spark == null || _root == null) { Sparks(color, count); return; }
            var canvasRt = _root.transform.parent;
            // 火花出生点：铁砧中央偏上（舞台在面板内偏移约 (60,55) + 舞台锚点）
            Vector2 origin = _panelBasePos + new Vector2(60f, 55f);
            for (int i = 0; i < count; i++)
            {
                var spark = new GameObject("PxSpark", typeof(RectTransform), typeof(Image));
                spark.transform.SetParent(canvasRt, false);
                var img = spark.GetComponent<Image>();
                img.sprite = ForgeFxLoader.Spark;
                img.color = color;
                img.raycastTarget = false;
                var rt = spark.GetComponent<RectTransform>();
                float s = Random.Range(8f, 16f);
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = origin + new Vector2(Random.Range(-20f, 20f), Random.Range(0f, 20f));
                Vector2 vel = new Vector2(Random.Range(-180f, 180f), Random.Range(80f, 260f));
                StartCoroutine(PixelSparkRoutine(rt, img, vel));
            }
        }

        IEnumerator PixelSparkRoutine(RectTransform rt, Image img, Vector2 vel)
        {
            float t = 0f, dur = Random.Range(0.3f, 0.55f);
            Color c = img.color;
            Vector2 pos = rt.anchoredPosition;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                img.color = new Color(c.r, c.g, c.b, c.a * (1f - t / dur));
                vel += Vector2.down * 600f * Time.unscaledDeltaTime;
                pos += vel * Time.unscaledDeltaTime;
                rt.anchoredPosition = pos;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // ------------------------------------------------------------ 构建

        void Build()
        {
            var canvas = UiFactory.CreateCanvas(transform, "Canvas", SortingOrder);

            // 全屏遮罩
            UiFactory.MakeDim(canvas.transform, 0.55f);

            // 主面板
            var panelImg = UiFactory.MakePanel(canvas.transform, "Panel",
                UiTheme.Instance != null ? UiTheme.Instance.panel : null,
                new Vector2(PanelW, PanelH));

            // v0.20：面板靠左，右半边屏幕留给铁匠演出。
            // 锻造的打击感全在世界里（铁匠抡锤 / 火花 / 震屏），面板居中的话什么都看不见。
            // 靠左而不是靠右：村子里的铁匠铺在地图最右（x≈26.5/34），相机 bounds 会把
            // 玩家顶到屏幕 67% 处 —— 面板放右边必然把人盖住。
            var panelRt2 = panelImg.GetComponent<RectTransform>();
            panelRt2.anchorMin = panelRt2.anchorMax = new Vector2(0f, 0.5f);
            panelRt2.pivot = new Vector2(0f, 0.5f);
            panelRt2.anchoredPosition = new Vector2(PanelLeftInset, 0f);

            // _root 必须绑整个 Canvas（含 Dim 遮罩），不能只绑 Panel ——
            // 否则 Close() 只隐藏面板，遮罩还留在屏幕上吃掉所有点击，表现为"卡死"。
            // ShopPanel / BackpackPanel 都是这么绑的（v0.19 修）。
            _root = canvas.gameObject;

            var pt = panelImg.transform;

            // ---- 标题栏
            var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            titleBar.transform.SetParent(pt, false);
            var tbRt = titleBar.GetComponent<RectTransform>();
            UiFactory.Place(tbRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(PanelW - 40f, 80f), new Vector2(0f, -10f));
            var tbImg = titleBar.GetComponent<Image>();
            tbImg.sprite = UiTheme.Instance != null ? UiTheme.Instance.titleBar : null;
            tbImg.type = Image.Type.Sliced;
            tbImg.pixelsPerUnitMultiplier = UiPpu;

            UiFactory.MakeText(titleBar.transform, "铁匠铺", 42, TextAnchor.MiddleCenter,
                new Color32(255, 243, 208, 255));

            // 关闭按钮
            var closeBtn = UiFactory.MakeButton(titleBar.transform, "×", new Vector2(60f, 60f),
                () => Close(), false);
            var closeRt = closeBtn.GetComponent<RectTransform>();
            UiFactory.Place(closeRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(60f, 60f), new Vector2(-10f, 0f));

            // ---- 打铁舞台（v0.20：像素精灵可视化）----
            BuildStage(pt);

            // ---- 价值区
            float y = -320f;
            _valueText = UiFactory.MakeText(pt, "剑价值：100", 38, TextAnchor.MiddleLeft,
                new Color32(255, 220, 120, 255));
            UiFactory.Place(_valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(400f, 50f), new Vector2(60f, y));

            _breakText = UiFactory.MakeText(pt, "破碎概率：5%", 32, TextAnchor.MiddleLeft,
                new Color32(255, 120, 100, 255));
            UiFactory.Place(_breakText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(300f, 50f), new Vector2(-60f, y));

            y -= 60f;

            // ---- 概率表
            _probText = UiFactory.MakeText(pt, "本锤概率：破坏5% 损伤15% 维持30% 成功40% 大成功10%", 26,
                TextAnchor.MiddleLeft, new Color32(200, 200, 200, 255));
            UiFactory.Place(_probText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(PanelW - 120f, 40f), new Vector2(60f, y));

            y -= 50f;

            // ---- 入神进度条
            _frenzyText = UiFactory.MakeText(pt, "入神充能：0/10", 28, TextAnchor.MiddleLeft,
                new Color32(180, 220, 255, 255));
            UiFactory.Place(_frenzyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(300f, 40f), new Vector2(60f, y));

            var barBg = new GameObject("FrenzyBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(pt, false);
            var barBgRt = barBg.GetComponent<RectTransform>();
            UiFactory.Place(barBgRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(400f, 28f), new Vector2(380f, y + 6f));
            var barBgImg = barBg.GetComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            var barFill = new GameObject("FrenzyBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            _frenzyBar = barFill.GetComponent<Image>();
            _frenzyBar.color = new Color32(100, 180, 255, 255);
            UiFactory.Stretch(barFill.GetComponent<RectTransform>(), 2f);

            y -= 60f;

            // ---- 状态提示
            _statusText = UiFactory.MakeText(pt, "点「入炉」开始锻造", 30, TextAnchor.MiddleCenter,
                new Color32(255, 255, 200, 255));
            UiFactory.Place(_statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(PanelW - 120f, 40f), new Vector2(0f, y));

            y -= 55f;

            // ---- 材料
            _materialText = UiFactory.MakeText(pt, "材料：铁矿×1 + 煤炭×1", 26, TextAnchor.MiddleLeft,
                new Color32(180, 180, 180, 255));
            UiFactory.Place(_materialText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(500f, 35f), new Vector2(60f, y));

            y -= 70f;

            // ---- 操作按钮
            float btnW = 220f, btnH = 80f, btnGap = 40f;
            float btnStartX = -(btnW * 1.5f + btnGap);
            float btnY = y;

            _startBtn = UiFactory.MakeButton(pt, "入炉", new Vector2(btnW, btnH),
                () => OnStart(), false);
            UiFactory.Place(_startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(btnW, btnH), new Vector2(btnStartX, btnY));

            _strikeBtn = UiFactory.MakeButton(pt, "锤击", new Vector2(btnW, btnH),
                () => OnStrike(), false);
            UiFactory.Place(_strikeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(btnW, btnH), new Vector2(0f, btnY));

            _collectBtn = UiFactory.MakeButton(pt, "收剑", new Vector2(btnW, btnH),
                () => OnCollect(), false);
            UiFactory.Place(_collectBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(btnW, btnH), new Vector2(-btnStartX, btnY));

            y -= 110f;

            // ---- 遗物兑换区
            var relicLabel = UiFactory.MakeText(pt, "遗物兑换（用高价值剑换）", 28, TextAnchor.MiddleLeft,
                new Color32(200, 180, 255, 255));
            UiFactory.Place(relicLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(500f, 35f), new Vector2(60f, y));

            y -= 45f;

            // 滚动列表
            var scrollGO = new GameObject("RelicScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGO.transform.SetParent(pt, false);
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            UiFactory.Place(scrollRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(PanelW - 80f, 160f), new Vector2(0f, y));
            var scrollImg = scrollGO.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.25f);

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            _relicContent = contentGO.GetComponent<RectTransform>();
            _relicContent.anchorMin = new Vector2(0f, 1f);
            _relicContent.anchorMax = new Vector2(1f, 1f);
            _relicContent.pivot = new Vector2(0.5f, 1f);
            _relicContent.sizeDelta = Vector2.zero;

            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var fitter = contentGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.content = _relicContent;
            scroll.horizontal = false;
            scroll.vertical = true;

            // ---- 视觉特效（v0.19）：内嵌实现
            InitFx();
        }

        // ------------------------------------------------------------ 舞台（v0.20）

        BlacksmithActor _actor;
        bool _striking;

        /// <summary>面板打开时把「谁在打铁」接进来：相机让位、拿到铁匠用于演出。</summary>
        void BindStage(BlacksmithSpot spot)
        {
            _actor = spot != null ? spot.Actor : null;
            _striking = false;

            var cam = FindObjectOfType<CameraFollow>();
            if (cam != null) cam.SetUiOffset(new Vector2(CameraShift, 0f));
        }

        /// <summary>面板关闭：相机复位、解锁玩家、铁匠走回铺子。</summary>
        void ClearStage()
        {
            var cam = FindObjectOfType<CameraFollow>();
            if (cam != null) cam.SetUiOffset(Vector2.zero);

            if (_currentSpot != null) _currentSpot.Release();
            _currentSpot = null;
            _actor = null;
            _striking = false;
        }

        /// <summary>当前有没有铁匠在场（没有就退回纯 UI 表现，保证老场景也能玩）。</summary>
        bool HasActor => _actor != null;

        static CameraFollow FindCamera()
        {
            return FindObjectOfType<CameraFollow>();
        }

        // ------------------------------------------------------------ 状态机驱动

        void ResetSession()
        {
            _session = new ForgeSession();
            _session.OnStrike += OnStrikeResult;
            _session.OnBroken += () =>
            {
                _statusText.text = "剑碎了！价值归零...";
                UpdateSwordHeatVisual(0, broken: true);   // v0.20：剑变断剑
                StrikeFx(FxKind.Break, 16);
                AudioManager.Break();
                Refresh();
            };
            _session.OnDone += v =>
            {
                _statusText.text = $"收剑成功！价值 {v}";
                PunchValue(true); StrikeFx(FxKind.Success, 8);
                AudioManager.Success();
                Refresh();
            };
            _session.OnFrenzy += msg =>
            {
                _statusText.text = msg;
                Flash(new Color(0.35f, 0.75f, 1f, 0.55f)); SetFrenzyPulse(true);
                AudioManager.Frenzy();
            };

            _statusText.text = "点「入炉」开始锻造";
            SetFrenzyPulse(false);
            Refresh();
            RefreshRelics();   // v0.18 修：此前从未调用，遗物列表一直是空的
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void OnStart()
        {
            // 检查材料
            var cfg = ForgeConfig.Instance;
            if (cfg != null)
            {
                if (!string.IsNullOrEmpty(cfg.oreItemId) && Inventory.Count(cfg.oreItemId) < cfg.oreCost)
                {
                    Toast.Show($"铁矿不足，需要 {cfg.oreCost} 个");
                    return;
                }
                if (!string.IsNullOrEmpty(cfg.coalItemId) && Inventory.Count(cfg.coalItemId) < cfg.coalCost)
                {
                    Toast.Show($"煤炭不足，需要 {cfg.coalCost} 个");
                    return;
                }

                // 扣材料
                if (!string.IsNullOrEmpty(cfg.oreItemId)) Inventory.TryConsume(cfg.oreItemId, cfg.oreCost);
                if (!string.IsNullOrEmpty(cfg.coalItemId)) Inventory.TryConsume(cfg.coalItemId, cfg.coalCost);
            }

            _session.StartForge();
            _session.FinishHeating();   // 简化：跳过加热等待，直接可锤
            _statusText.text = "锤击开始！见好就收还是再敲一锤？";
            UpdateSwordHeatVisual(0, broken: false);   // v0.20：新剑冷灰
            AudioManager.Heat();   // v0.19：入炉火焰音效

            // v0.20：铁匠真的敲一下点炉 —— 火星四溅 + 余烬上飘
            if (HasActor && !_actor.Busy)
            {
                _actor.PlayStrike(() =>
                {
                    Vector3 p = _actor.HammerTip;
                    ForgeFx.SparkBurst(p, 12, new Color(1f, 0.72f, 0.32f), 1.15f);
                    ForgeFx.Impact(p, new Color(1f, 0.55f, 0.18f), 0.8f);
                    ForgeFx.Embers(p, 10, new Color(1f, 0.68f, 0.28f, 0.9f));
                    var cam = FindCamera();
                    if (cam != null) cam.Shake(0.06f, 0.16f);
                });
            }
            Refresh();
        }

        void OnStrike()
        {
            if (_session.Current != ForgeSession.State.Ready) return;
            if (_striking) return;                       // 上一锤还没落地，别叠
            if (HasActor && _actor.Busy) return;         // 铁匠在点火/收势中

            if (!HasActor)
            {
                _session.Strike();                       // 没有铁匠：保持旧行为
                Refresh();
                return;
            }

            // 关键：先播挥锤，**命中帧**才结算。数值变化、火花、音效全部对齐锤子落地。
            _striking = true;
            SetActionButtons(false);
            _actor.PlayStrike(
                onImpact: () =>
                {
                    _session.Strike();                   // 触发 OnStrikeResult（特效 + 音效 + 文案）
                    Refresh();
                },
                onDone: () =>
                {
                    _striking = false;
                    Refresh();
                });
        }

        void SetActionButtons(bool on)
        {
            if (_strikeBtn != null) _strikeBtn.interactable = on;
            if (_collectBtn != null) _collectBtn.interactable = on;
        }

        void OnCollect()
        {
            if (_session.Current != ForgeSession.State.Ready) return;
            float value = _session.Collect();
            if (value <= 0) return;

            // 剑入背包（用一个特殊物品 id）
            int added = Inventory.Add("forged_sword", 1);
            if (added > 0)
            {
                Toast.Show($"收剑！价值 {value}，剑已入背包");
            }
            else
            {
                // 背包满：直接换金币。注意金币是 Inventory.Gold（独立数值），
                // 不是背包物品 —— 之前写 Add("gold_coin") 会塞进一个物品表里
                // 不存在的幽灵物品，钱一分没拿到（v0.18 修）。
                int gold = Mathf.RoundToInt(value * 0.6f);
                Inventory.AddGold(gold);
                Toast.Show($"背包已满，剑已折算 {gold} 金币");
            }

            // 双生印：概率复制
            if (_session.RollDuplicate())
            {
                Inventory.Add("forged_sword", 1);
                Toast.Show("双生印触发！额外复制一把剑");
            }

            Refresh();
        }

        void OnStrikeResult(ForgeResult result, float newValue)
        {
            string msg = result switch
            {
                ForgeResult.Break => "碎了！",
                ForgeResult.Damage => $"损伤！价值 → {newValue}",
                ForgeResult.Keep => $"维持，价值 {newValue}",
                ForgeResult.Success => $"成功！价值 → {newValue}",
                ForgeResult.Critical => $"大成功！价值 → {newValue}",
                _ => ""
            };
            _statusText.text = msg;

            // v0.20：锤子挥击动画 + 剑随锤次加热变色
            PlayHammerAnim();
            UpdateSwordHeatVisual(_session.HitCount, broken: false);

            // v0.19：按锤击结果播放特效 + 音效（ForgeTax 风格反馈）
            // 按锤击结果播放特效 + 音效
            {
                switch (result)
                {
                    case ForgeResult.Damage:
                        StrikeFx(FxKind.Damage);
                        PunchValue(false);
                        AudioManager.Damage();
                        break;
                    case ForgeResult.Keep:
                        Shake(8f);
                        Sparks(new Color(1f, 0.85f, 0.5f), 6, 180f);
                        AudioManager.Hammer();
                        break;
                    case ForgeResult.Success:
                        StrikeFx(FxKind.Success);
                        PunchValue(true);
                        AudioManager.Success();
                        break;
                    case ForgeResult.Critical:
                        StrikeFx(FxKind.Critical, 16);
                        PunchValue(true);
                        AudioManager.Critical();
                        break;
                    // Break 由 OnBroken 处理
                }
            }
            Refresh();
        }

        // ------------------------------------------------------------ 刷新

        void Refresh()
        {
            if (_session == null) return;

            _valueText.text = $"剑价值：{_session.Value}";
            _breakText.text = $"破碎概率：{_session.BreakChance * 100f:F1}%";

            var cfg = ForgeConfig.Instance;
            if (cfg != null)
            {
                cfg.GetProbabilities(out float bp, out float dp, out float kp, out float sp, out float cp);
                _probText.text = $"本锤概率：破坏{bp * 100:F0}% 损伤{dp * 100:F0}% 维持{kp * 100:F0}% 成功{sp * 100:F0}% 大成功{cp * 100:F0}%";

                int need = cfg.frenzyHits;
                int cur = _session.FrenzyProgress;
                _frenzyText.text = $"入神充能：{cur}/{need}";
                _frenzyBar.fillAmount = _session.FrenzyProgress01;

                string oreName = string.IsNullOrEmpty(cfg.oreItemId) ? "" : ItemDatabase.NameOf(cfg.oreItemId);
                string coalName = string.IsNullOrEmpty(cfg.coalItemId) ? "" : ItemDatabase.NameOf(cfg.coalItemId);
                _materialText.text = $"材料：{oreName}×{cfg.oreCost} + {coalName}×{cfg.coalCost}";
            }

            bool isReady = _session.Current == ForgeSession.State.Ready;
            bool isIdle = _session.Current == ForgeSession.State.Idle;
            bool canAct = isReady && !_striking;   // 锤子还在空中时按钮保持灰，防止连点
            _startBtn.interactable = isIdle;
            _strikeBtn.interactable = canAct;
            _collectBtn.interactable = canAct;
        }

        // ------------------------------------------------------------ 遗物列表

        void RefreshRelics()
        {
            // 清空旧列表
            for (int i = _relicContent.childCount - 1; i >= 0; i--)
                Destroy(_relicContent.GetChild(i).gameObject);

            foreach (var relic in RelicDatabase.All())
            {
                var row = new GameObject("Relic_" + relic.id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                row.transform.SetParent(_relicContent, false);
                var le = row.GetComponent<LayoutElement>();
                le.preferredHeight = 60f;

                var rowImg = row.GetComponent<Image>();
                rowImg.color = new Color(0.2f, 0.18f, 0.15f, 0.6f);

                var nameText = UiFactory.MakeText(row.transform, relic.displayName, 26,
                    TextAnchor.MiddleLeft, new Color32(255, 240, 200, 255));
                UiFactory.Place(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(250f, 50f), new Vector2(15f, 0f));

                var descText = UiFactory.MakeText(row.transform, relic.description, 20,
                    TextAnchor.MiddleLeft, new Color32(180, 180, 180, 255));
                UiFactory.Place(descText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(450f, 50f), new Vector2(270f, 0f));

                int cost = relic.requiredSwordValue;
                var btn = UiFactory.MakeButton(row.transform, $"兑换({cost})", new Vector2(140f, 48f),
                    () => OnExchangeRelic(relic), false);
                UiFactory.Place(btn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f), new Vector2(140f, 48f), new Vector2(-15f, 0f));
            }
        }

        void OnExchangeRelic(RelicDef relic)
        {
            if (_session == null || _session.Value < relic.requiredSwordValue)
            {
                Toast.Show($"剑价值不够，需要 ≥{relic.requiredSwordValue}");
                return;
            }

            // 消耗剑换遗物
            _session.Collect();
            _session.ActivateRelic(relic.id);
            Toast.Show($"获得遗物：{relic.displayName}");
            _statusText.text = $"已获得遗物：{relic.displayName}";
            Refresh();
        }

        // ================================================================
        // 视觉特效（ForgeTax 风格）v0.19
        // 全部内嵌为 ForgePanel 私有成员，不另建 .cs（绕开 Unity 资产导入问题）。
        // 用 UGUI（RectTransform + Image + Coroutine），不依赖 ParticleSystem。
        // ================================================================

        Image _flashImg;             // 全屏闪烁层
        RectTransform _panelRt;      // 面板根（震动目标）
        Vector2 _panelBasePos;
        Coroutine _shakeCo, _flashCo, _punchCo, _frenzyCo;

        /// <summary>Build() 末尾调用，创建闪烁层并缓存震动目标。</summary>
        void InitFx()
        {
            _panelRt = _root.GetComponent<RectTransform>();
            _panelBasePos = _panelRt.anchoredPosition;

            // 全屏闪烁层：放在 Canvas 上、Panel 之前
            var canvasRt = _root.transform.parent;
            var flashGO = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGO.transform.SetParent(canvasRt, false);
            flashGO.transform.SetSiblingIndex(0);
            _flashImg = flashGO.GetComponent<Image>();
            _flashImg.raycastTarget = false;
            _flashImg.color = Color.clear;
            var rt = flashGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ---- 屏幕震动 ----
        void Shake(float strength = 10f)
        {
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeRoutine(strength));
        }
        IEnumerator ShakeRoutine(float strength)
        {
            float elapsed = 0f, dur = 0.28f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = 1f - elapsed / dur;
                float offset = strength * decay;
                _panelRt.anchoredPosition = _panelBasePos + new Vector2(
                    Random.Range(-offset, offset), Random.Range(-offset, offset));
                yield return null;
            }
            _panelRt.anchoredPosition = _panelBasePos;
        }

        // ---- 色彩闪烁 ----
        void Flash(Color c)
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashRoutine(c));
        }
        IEnumerator FlashRoutine(Color c)
        {
            float t = 0f, fadeIn = 0.05f, hold = 0.12f, fadeOut = 0.25f;
            while (t < fadeIn) { t += Time.unscaledDeltaTime; _flashImg.color = new Color(c.r, c.g, c.b, c.a * (t / fadeIn)); yield return null; }
            yield return new WaitForSecondsRealtime(hold);
            t = 0f;
            while (t < fadeOut) { t += Time.unscaledDeltaTime; _flashImg.color = new Color(c.r, c.g, c.b, c.a * (1f - t / fadeOut)); yield return null; }
            _flashImg.color = Color.clear;
        }

        // ---- 火花粒子 ----
        void Sparks(Color color, int count = 10, float speed = 220f)
        {
            var canvasRt = _root.transform.parent;
            if (canvasRt == null) return;
            for (int i = 0; i < count; i++)
            {
                var spark = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                spark.transform.SetParent(canvasRt, false);
                var img = spark.GetComponent<Image>();
                img.color = color; img.raycastTarget = false;
                var rt = spark.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(Random.Range(4f, 10f), Random.Range(4f, 10f));
                rt.anchoredPosition = _panelBasePos + new Vector2(Random.Range(-40f, 40f), Random.Range(-20f, 20f));
                StartCoroutine(SparkRoutine(rt, img, Random.insideUnitCircle.normalized * speed * Random.Range(0.5f, 1.3f)));
            }
        }
        IEnumerator SparkRoutine(RectTransform rt, Image img, Vector2 vel)
        {
            float t = 0f, dur = Random.Range(0.25f, 0.5f);
            Color c = img.color; Vector2 pos = rt.anchoredPosition;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                img.color = new Color(c.r, c.g, c.b, c.a * (1f - t / dur));
                vel += Vector2.down * 500f * Time.unscaledDeltaTime;
                pos += vel * Time.unscaledDeltaTime;
                rt.anchoredPosition = pos;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // ---- 数值弹跳 ----
        void PunchValue(bool up)
        {
            if (_valueText == null) return;
            if (_punchCo != null) StopCoroutine(_punchCo);
            _punchCo = StartCoroutine(PunchRoutine(up));
        }
        IEnumerator PunchRoutine(bool up)
        {
            float t = 0f, dur = 0.30f, peak = up ? 1.35f : 0.75f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float scale = Mathf.Lerp(1f, peak, Mathf.Sin((t / dur) * Mathf.PI));
                _valueText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            _valueText.transform.localScale = Vector3.one;
        }

        // ---- 入神脉冲 ----
        void SetFrenzyPulse(bool active)
        {
            if (active) { if (_frenzyCo == null) _frenzyCo = StartCoroutine(FrenzyPulseRoutine()); }
            else { if (_frenzyCo != null) { StopCoroutine(_frenzyCo); _frenzyCo = null; } if (_frenzyBar != null) _frenzyBar.transform.localScale = Vector3.one; }
        }
        IEnumerator FrenzyPulseRoutine()
        {
            while (true)
            {
                float p = Mathf.PingPong(Time.unscaledTime * 2.5f, 1f);
                if (_frenzyBar != null) _frenzyBar.transform.localScale = new Vector3(1f, 1f + p * 0.12f, 1f);
                yield return null;
            }
        }

        // ---- 组合反馈
        // v0.20：有铁匠时，全部反馈发生在世界里（锤头位置 + 相机震屏）；
        // 没有铁匠（老场景 / 素材缺失）才退回 UGUI 的震动 + 闪屏 + 方块火花。
        enum FxKind { Damage, Break, Success, Critical, Frenzy }

        void StrikeFx(FxKind kind, int sparkCount = 10)
        {
            Color flash = kind switch
            {
                FxKind.Damage   => new Color(0.95f, 0.42f, 0.18f, 1f),
                FxKind.Break    => new Color(1f, 0.28f, 0.18f, 1f),
                FxKind.Success  => new Color(1f, 0.86f, 0.32f, 1f),
                FxKind.Critical => new Color(0.86f, 0.56f, 1f, 1f),
                FxKind.Frenzy   => new Color(0.42f, 0.82f, 1f, 1f),
                _ => Color.white
            };
            Color spark = kind switch
            {
                FxKind.Damage   => new Color(1f, 0.58f, 0.26f),
                FxKind.Break    => new Color(1f, 0.42f, 0.2f),
                FxKind.Success  => new Color(1f, 0.9f, 0.4f),
                FxKind.Critical => new Color(0.9f, 0.6f, 1f),
                FxKind.Frenzy   => new Color(0.5f, 0.88f, 1f),
                _ => Color.white
            };

            if (HasActor)
            {
                Vector3 p = _actor.HammerTip;
                float radius = kind == FxKind.Break ? 1.7f : (kind == FxKind.Critical ? 1.35f : 0.9f);
                float amp = kind == FxKind.Break ? 0.16f : (kind == FxKind.Critical ? 0.12f : 0.07f);

                ForgeFx.Impact(p, flash, radius);
                ForgeFx.SparkBurst(p, sparkCount, spark, kind == FxKind.Break ? 1.6f : 1f);

                if (kind == FxKind.Critical || kind == FxKind.Frenzy)
                    ForgeFx.Pillar(p, flash, 2.6f);
                if (kind == FxKind.Break)
                    ForgeFx.Smoke(p, 8, new Color(0.42f, 0.38f, 0.36f, 0.6f));

                var cam = FindCamera();
                if (cam != null) cam.Shake(amp, kind == FxKind.Break ? 0.34f : 0.2f);
                return;
            }

            // ---- 兜底：纯 UGUI 表现（无铁匠 NpC 的场景）
            Shake(kind == FxKind.Break ? 16f : 10f);
            Flash(new Color(flash.r, flash.g, flash.b, 0.55f));
            Sparks(spark, sparkCount);
        }
    }
}
