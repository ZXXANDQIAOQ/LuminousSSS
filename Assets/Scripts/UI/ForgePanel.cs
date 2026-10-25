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
        const float PanelW = 1100f;
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

        // ------------------------------------------------------------ 对外入口

        public static void Open(BlacksmithSpot spot)
        {
            if (ShopPanel.IsOpen) ShopPanel.Close();
            if (BackpackPanel.IsOpen) BackpackPanel.Close();

            _currentSpot = spot;
            if (_inst == null)
            {
                var go = new GameObject("[ForgePanel]");
                Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<ForgePanel>();
                _inst.Build();
            }

            _inst._root.SetActive(true);
            _inst.ResetSession();
            UiModal.Apply();
        }

        public static void Close()
        {
            if (_inst == null || _inst._root == null) return;
            _inst._root.SetActive(false);
            UiModal.Apply();
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

            // ---- 价值区
            float y = -100f;
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
        }

        // ------------------------------------------------------------ 状态机驱动

        void ResetSession()
        {
            _session = new ForgeSession();
            _session.OnStrike += OnStrikeResult;
            _session.OnBroken += () => { _statusText.text = "剑碎了！价值归零..."; Refresh(); };
            _session.OnDone += v => { _statusText.text = $"收剑成功！价值 {v}"; Refresh(); };
            _session.OnFrenzy += msg => { _statusText.text = msg; };

            _statusText.text = "点「入炉」开始锻造";
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
            Refresh();
        }

        void OnStrike()
        {
            if (_session.Current != ForgeSession.State.Ready) return;
            _session.Strike();
            Refresh();
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
            _startBtn.interactable = isIdle;
            _strikeBtn.interactable = isReady;
            _collectBtn.interactable = isReady;
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
    }
}
