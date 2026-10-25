using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 村口集市的交易界面（买种子 / 卖收获）。
    ///
    /// 为什么整块 UI 在运行时用代码搭、而不是做成预制体放在场景里：
    ///   1) 商店内容完全由 <see cref="ItemDatabase"/> 决定 —— 加一种作物不该要求
    ///      回编辑器里手动复制一行 UI；代码铺行，加物品只改物品表。
    ///   2) 村庄 / 农场两个场景都要能开同一个商店，预制体得各放一份或做成常驻，
    ///      反而多一层生命周期要维护。
    ///
    /// 打开时冻结 timeScale（走 <see cref="UiModal"/>，因为背包面板也会冻结它）：
    /// 面板是模态的，玩家不该在数钱时被怪物/摇杆带着跑。
    /// Toast 与所有淡入淡出都用 unscaledDeltaTime，所以关闭状态下提示照常显示。
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        /// <summary>面板是否打开。PlayerController / 其他交互据此避免在面板后面乱动。</summary>
        public static bool IsOpen => _inst != null && _inst._root != null && _inst._root.gameObject.activeSelf;

        const int SortingOrder = 200;      // HUD 是 100，Toast 300，换场景黑幕 32000
        const float PanelW = 1180f;
        const float PanelH = 800f;
        const float RowH = 118f;
        const float UiPpu = UiFactory.UiPpu;    // 与 HUD 一致：九宫格边框按素材原始像素 1:1

        static ShopPanel _inst;

        GameObject _root;
        RectTransform _listContent;
        Text _goldText;
        Text _titleText;
        Button _tabBuy;
        Button _tabSell;
        bool _buyTab = true;

        // ------------------------------------------------------------ 对外入口

        /// <summary>打开商店（已打开时只切到购买页，不重复建 UI）。</summary>
        public static void Open(string title = "村口集市")
        {
            if (BackpackPanel.IsOpen) BackpackPanel.Close();   // 两个模态互斥

            if (_inst == null)
            {
                var go = new GameObject("[ShopPanel]");
                Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<ShopPanel>();
                _inst.Build(title);
            }

            _inst._titleText.text = title;
            _inst._root.SetActive(true);
            _inst._buyTab = true;
            _inst.Refresh();
            UiModal.Apply();
        }

        public static void Close()
        {
            if (_inst == null || _inst._root == null) return;
            _inst._root.SetActive(false);
            UiModal.Apply();
        }

        // ------------------------------------------------------------ 构建

        void Build(string title)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // 面板需要自己的 EventSystem 才能点（场景里的 HUD 有，但商店可能在没有 HUD 的场景被打开）
            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasRt = canvasGO.GetComponent<RectTransform>();

            // ---- 半透明遮罩：吃掉面板外的点击，防止误触后面的摇杆
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvasRt, false);
            var dimRt = dim.GetComponent<RectTransform>();
            UiFactory.Stretch(dimRt, 0f);
            var dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            // ---- 主面板
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasRt, false);
            _root = canvasGO;      // 整个 Canvas 一起开关，避免遮罩残留
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PanelW, PanelH);
            panelRt.anchoredPosition = Vector2.zero;

            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = PanelSprite;
            panelImg.type = Image.Type.Sliced;
            panelImg.pixelsPerUnitMultiplier = UiPpu;
            panelImg.color = new Color(1f, 0.98f, 0.94f, 1f);

            // ---- 标题
            _titleText = UiFactory.MakeText(panel.transform, title, 46, TextAnchor.MiddleLeft,
                                  new Color32(74, 42, 12, 255));
            var titleRt = _titleText.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(600f, 70f);
            titleRt.anchoredPosition = new Vector2(56f, -32f);

            // ---- 关闭按钮（右上角）
            var closeBtn = UiFactory.MakeButton(panel.transform, "×", new Vector2(76f, 76f), () => Close(), false);
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-34f, -28f);

            // ---- 金币条（标题下方）
            var coinPlate = new GameObject("Gold", typeof(RectTransform), typeof(Image));
            coinPlate.transform.SetParent(panel.transform, false);
            var plateRt = coinPlate.GetComponent<RectTransform>();
            plateRt.anchorMin = plateRt.anchorMax = new Vector2(0f, 1f);
            plateRt.pivot = new Vector2(0f, 1f);
            plateRt.sizeDelta = new Vector2(260f, 78f);
            plateRt.anchoredPosition = new Vector2(56f, -110f);
            var plateImg = coinPlate.GetComponent<Image>();
            plateImg.sprite = GoldPlateSprite;
            plateImg.type = Image.Type.Sliced;
            plateImg.pixelsPerUnitMultiplier = UiPpu;

            var coinIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            coinIcon.transform.SetParent(coinPlate.transform, false);
            var coinIconRt = coinIcon.GetComponent<RectTransform>();
            coinIconRt.anchorMin = coinIconRt.anchorMax = new Vector2(0f, 0.5f);
            coinIconRt.pivot = new Vector2(0f, 0.5f);
            coinIconRt.sizeDelta = new Vector2(58f, 58f);
            coinIconRt.anchoredPosition = new Vector2(12f, 0f);
            var coinIconImg = coinIcon.GetComponent<Image>();
            coinIconImg.sprite = CoinSprite;
            coinIconImg.preserveAspect = true;
            coinIconImg.raycastTarget = false;

            _goldText = UiFactory.MakeText(coinPlate.transform, "0", 40, TextAnchor.MiddleLeft,
                                 new Color32(74, 42, 12, 255));
            var goldRt = _goldText.rectTransform;
            goldRt.anchorMin = goldRt.anchorMax = new Vector2(0f, 0.5f);
            goldRt.pivot = new Vector2(0f, 0.5f);
            goldRt.sizeDelta = new Vector2(170f, 60f);
            goldRt.anchoredPosition = new Vector2(78f, 0f);

            // ---- 两个页签
            _tabBuy = UiFactory.MakeButton(panel.transform, "买种子", new Vector2(220f, 72f), () => SwitchTab(true), false);
            UiFactory.AddTabUnderline(_tabBuy);
            var buyRt = _tabBuy.GetComponent<RectTransform>();
            buyRt.anchorMin = buyRt.anchorMax = new Vector2(1f, 1f);
            buyRt.pivot = new Vector2(1f, 1f);
            buyRt.anchoredPosition = new Vector2(-330f, -118f);

            _tabSell = UiFactory.MakeButton(panel.transform, "卖东西", new Vector2(220f, 72f), () => SwitchTab(false), false);
            UiFactory.AddTabUnderline(_tabSell);
            var sellRt = _tabSell.GetComponent<RectTransform>();
            sellRt.anchorMin = sellRt.anchorMax = new Vector2(1f, 1f);
            sellRt.pivot = new Vector2(1f, 1f);
            sellRt.anchoredPosition = new Vector2(-96f, -118f);

            // ---- 列表（ScrollRect：物品多了一屏放不下）
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            var viewRt = viewport.GetComponent<RectTransform>();
            viewRt.anchorMin = new Vector2(0f, 0f);
            viewRt.anchorMax = new Vector2(1f, 1f);
            viewRt.offsetMin = new Vector2(56f, 44f);
            viewRt.offsetMax = new Vector2(-56f, -212f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                                         typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _listContent = content.GetComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = _listContent;
            scroll.viewport = viewRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            canvasGO.SetActive(false);   // 建完先关着，等 Open()
        }

        // ------------------------------------------------------------ 内容刷新

        void SwitchTab(bool buy)
        {
            _buyTab = buy;
            Refresh();
        }

        /// <summary>重建列表 + 刷新金币。任何买卖之后都要调。</summary>
        void Refresh()
        {
            if (_listContent == null) return;

            _goldText.text = Inventory.Gold.ToString();
            UiFactory.TintTab(_tabBuy, _buyTab);
            UiFactory.TintTab(_tabSell, !_buyTab);

            // 清空旧行：先摘出层级再销毁，否则 Destroy 延迟到帧末，本帧布局里还留着旧行
            while (_listContent.childCount > 0)
            {
                var child = _listContent.GetChild(0);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }

            if (_buyTab) BuildBuyRows();
            else BuildSellRows();

            // 列表比一屏短时挪回顶部，免得看着像"内容丢了"
            var scroll = _listContent.parent.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>购买页：物品表里所有带 buyPrice 的物品（种子 + 铁矿/煤炭/肥料等材料）。</summary>
        void BuildBuyRows()
        {
            // 用 PurchasableAll 而不是 Purchasable(Seed)：铁矿/煤炭是 Crop 类，
            // 只按 Seed 过滤会把打铁材料从商店里弄丢，打铁玩法直接开不了工（v0.18 修）。
            var goods = ItemDatabase.PurchasableAll();
            if (goods.Count == 0)
            {
                AddEmptyRow("物品表里还没有可购买的物品（检查 ItemDB）");
                return;
            }

            // 种子排前面（主玩法先看到），材料排后面
            goods.Sort((a, b) =>
            {
                int ka = a.kind == ItemKind.Seed ? 0 : 1;
                int kb = b.kind == ItemKind.Seed ? 0 : 1;
                return ka != kb ? ka.CompareTo(kb) : string.CompareOrdinal(a.id, b.id);
            });

            foreach (var def in goods)
            {
                var d = def;   // 闭包捕获：foreach 变量在 C# 5 之后是每轮独立，这里显式拷贝更保险
                int have = Inventory.Count(d.id);
                string sub = d.kind == ItemKind.Seed
                    ? $"{d.buyPrice} 金币 / 包   ·   已有 {have} 包   ·   成熟后卖 {ItemDatabase.Find(d.seedOf)?.sellPrice ?? 0} 金币"
                    : $"{d.buyPrice} 金币 / 个   ·   已有 {have} 个" + (d.sellPrice > 0 ? $"   ·   回收价 {d.sellPrice}" : "");

                AddRow(d, sub, new[]
                {
                    ("买 1", (System.Action)(() => Buy(d.id, 1))),
                    ("买 5", (System.Action)(() => Buy(d.id, 5))),
                });
            }
        }

        /// <summary>收购页：只列背包里真实持有、且物品表标了收购价的。</summary>
        void BuildSellRows()
        {
            var rows = new List<KeyValuePair<string, int>>();
            foreach (var kv in Inventory.Entries())
                if (ItemDatabase.IsSellable(kv.Key)) rows.Add(kv);

            if (rows.Count == 0)
            {
                AddEmptyRow("背包里没有能卖的东西 —— 砍树开垦、种地、钓鱼去吧");
                return;
            }

            // 鱼排在作物前面：刚钓上来的顺手就想卖掉
            rows.Sort((a, b) =>
            {
                int ka = ItemDatabase.Find(a.Key)?.kind == ItemKind.Fish ? 0 : 1;
                int kb = ItemDatabase.Find(b.Key)?.kind == ItemKind.Fish ? 0 : 1;
                return ka != kb ? ka.CompareTo(kb) : string.CompareOrdinal(a.Key, b.Key);
            });

            foreach (var kv in rows)
            {
                var def = ItemDatabase.Find(kv.Key);
                string id = kv.Key;
                int count = kv.Value;
                string sub = $"{def.sellPrice} 金币 / 个   ·   持有 {count} 个";

                AddRow(def, sub, new[]
                {
                    ("卖 1", (System.Action)(() => Sell(id, 1))),
                    ("全卖", (System.Action)(() => Sell(id, count))),
                });
            }
        }

        void AddEmptyRow(string message)
        {
            var row = new GameObject("Empty", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(_listContent, false);
            row.GetComponent<LayoutElement>().preferredHeight = RowH;

            var text = UiFactory.MakeText(row.transform, message, 32, TextAnchor.MiddleCenter,
                                new Color32(120, 88, 52, 255));
            UiFactory.Stretch(text.rectTransform, 0f);
        }

        /// <summary>一行：图标 + 名称/说明 + 右侧一两个操作按钮。</summary>
        void AddRow(ItemDef def, string subtitle, (string label, System.Action act)[] actions)
        {
            var row = new GameObject("Row_" + def.id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_listContent, false);

            var rowImg = row.GetComponent<Image>();
            rowImg.sprite = SlotSprite;
            rowImg.type = Image.Type.Sliced;
            rowImg.pixelsPerUnitMultiplier = UiPpu;
            rowImg.color = new Color(1f, 0.99f, 0.95f, 0.9f);

            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowH;
            le.minHeight = RowH;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(18, 18, 12, 12);
            hlg.spacing = 18f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // ---- 图标
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(row.transform, false);
            var iconImg = icon.GetComponent<Image>();
            iconImg.sprite = def.icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var iconLe = icon.GetComponent<LayoutElement>();
            iconLe.preferredWidth = 84f;
            iconLe.preferredHeight = 84f;

            // ---- 文字块
            var textBlock = new GameObject("Texts", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            textBlock.transform.SetParent(row.transform, false);
            var tbLe = textBlock.GetComponent<LayoutElement>();
            tbLe.flexibleWidth = 1f;
            var tbVlg = textBlock.GetComponent<VerticalLayoutGroup>();
            tbVlg.spacing = 2f;
            tbVlg.childControlHeight = true;
            tbVlg.childControlWidth = true;
            tbVlg.childForceExpandWidth = true;
            tbVlg.childForceExpandHeight = false;

            var nameText = UiFactory.MakeText(textBlock.transform, def.displayName, 34, TextAnchor.MiddleLeft,
                                    new Color32(74, 42, 12, 255));
            nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var subText = UiFactory.MakeText(textBlock.transform, subtitle, 26, TextAnchor.MiddleLeft,
                                   new Color32(126, 92, 52, 255));
            subText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            // ---- 按钮组
            var btnGroup = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            btnGroup.transform.SetParent(row.transform, false);
            var bgLe = btnGroup.GetComponent<LayoutElement>();
            bgLe.preferredWidth = actions.Length * 160f + (actions.Length - 1) * 12f;
            var bgHlg = btnGroup.GetComponent<HorizontalLayoutGroup>();
            bgHlg.spacing = 12f;
            bgHlg.childAlignment = TextAnchor.MiddleRight;
            bgHlg.childControlWidth = true;
            bgHlg.childControlHeight = true;
            bgHlg.childForceExpandWidth = false;
            bgHlg.childForceExpandHeight = false;

            foreach (var (label, act) in actions)
            {
                UiFactory.MakeButton(btnGroup.transform, label, new Vector2(160f, 70f), act, true);
            }
        }

        // ------------------------------------------------------------ 买卖

        void Buy(string seedId, int count)
        {
            var def = ItemDatabase.Find(seedId);
            if (def == null) return;

            // 先探背包空间再扣钱：反过来的话「钱扣了、东西装不下」就是白花
            if (!Inventory.CanAdd(seedId, count))
            {
                Toast.Show(Inventory.IsFull
                    ? "背包满了 —— 先卖掉一些再来买"
                    : $"{def.displayName} 一格最多放 {Inventory.StackMax} 包");
                AudioManager.Click();
                return;
            }

            int cost = def.buyPrice * count;
            if (!Inventory.TrySpendGold(cost))
            {
                Toast.Show($"金币不足，{count} 包 {def.displayName} 要 {cost} 金币");
                AudioManager.Click();
                return;
            }

            Inventory.Add(seedId, count);
            Toast.Show($"买入 {def.displayName} ×{count}（-{cost} 金币）");
            AudioManager.Confirm();
            Refresh();
        }

        void Sell(string itemId, int count)
        {
            var def = ItemDatabase.Find(itemId);
            if (def == null || count <= 0) return;

            count = Mathf.Min(count, Inventory.Count(itemId));   // 防止"全卖"点了两次
            if (count <= 0) return;

            if (!Inventory.TryConsume(itemId, count)) return;

            int income = def.sellPrice * count;
            Inventory.AddGold(income);
            Toast.Show($"卖出 {def.displayName} ×{count}（+{income} 金币）");
            AudioManager.Confirm();
            Refresh();
        }

        // ------------------------------------------------------------ UI 小工具

        void Update()
        {
            // ESC 关闭（Esc 在 PC 上是关闭弹窗的通用键）
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();

            // 兜底：外部直接 SetActive(false) / 背包面板还开着时，时间不能永久冻住
            if (!IsOpen) UiModal.Apply();
        }

        // UI 铺排工具统一在 UiFactory（MakeText / MakeButton / AddTabUnderline …），
        // 背包面板与商店面板共用同一套，改一处两边都生效。

        // 底图统一从 UiTheme 拿（素材只有一份，见 UiTheme 的注释）
        static Sprite PanelSprite => UiTheme.Instance != null ? UiTheme.Instance.panel : null;
        static Sprite ButtonSprite => UiTheme.Instance != null ? UiTheme.Instance.button : null;
        static Sprite SlotSprite => UiTheme.Instance != null ? UiTheme.Instance.slot : null;
        static Sprite GoldPlateSprite => UiTheme.Instance != null ? UiTheme.Instance.goldPlate : null;
        static Sprite CoinSprite => UiTheme.Instance != null ? UiTheme.Instance.coinIcon : null;

    }
}
