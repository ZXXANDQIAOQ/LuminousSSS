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
    /// 打开时冻结 timeScale：面板是模态的，玩家不该在数钱时被怪物/摇杆带着跑。
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
        const float UiPpu = 100f / 64f;    // 与 HUD 一致：九宫格边框按素材原始像素 1:1

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
            Time.timeScale = 0f;      // 模态：冻结游戏时间
        }

        public static void Close()
        {
            if (_inst == null || _inst._root == null) return;
            _inst._root.SetActive(false);
            Time.timeScale = 1f;
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
            Stretch(dimRt, 0f);
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
            _titleText = MakeText(panel.transform, title, 46, TextAnchor.MiddleLeft,
                                  new Color32(74, 42, 12, 255));
            var titleRt = _titleText.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(600f, 70f);
            titleRt.anchoredPosition = new Vector2(56f, -32f);

            // ---- 关闭按钮（右上角）
            var closeBtn = MakeButton(panel.transform, "✕", new Vector2(76f, 76f), () => Close(), false);
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

            _goldText = MakeText(coinPlate.transform, "0", 40, TextAnchor.MiddleLeft,
                                 new Color32(74, 42, 12, 255));
            var goldRt = _goldText.rectTransform;
            goldRt.anchorMin = goldRt.anchorMax = new Vector2(0f, 0.5f);
            goldRt.pivot = new Vector2(0f, 0.5f);
            goldRt.sizeDelta = new Vector2(170f, 60f);
            goldRt.anchoredPosition = new Vector2(78f, 0f);

            // ---- 两个页签
            _tabBuy = MakeButton(panel.transform, "买种子", new Vector2(220f, 72f), () => SwitchTab(true), false);
            AddTabUnderline(_tabBuy);
            var buyRt = _tabBuy.GetComponent<RectTransform>();
            buyRt.anchorMin = buyRt.anchorMax = new Vector2(1f, 1f);
            buyRt.pivot = new Vector2(1f, 1f);
            buyRt.anchoredPosition = new Vector2(-330f, -118f);

            _tabSell = MakeButton(panel.transform, "卖东西", new Vector2(220f, 72f), () => SwitchTab(false), false);
            AddTabUnderline(_tabSell);
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
            TintTab(_tabBuy, _buyTab);
            TintTab(_tabSell, !_buyTab);

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

        /// <summary>购买页：物品表里所有带 buyPrice 的种子。</summary>
        void BuildBuyRows()
        {
            var seeds = ItemDatabase.Purchasable(ItemKind.Seed);
            if (seeds.Count == 0)
            {
                AddEmptyRow("物品表里还没有可出售的种子（检查 ItemDB）");
                return;
            }

            foreach (var def in seeds)
            {
                var d = def;   // 闭包捕获：foreach 变量在 C# 5 之后是每轮独立，这里显式拷贝更保险
                int have = Inventory.Count(d.id);
                string sub = $"{d.buyPrice} 金币 / 包   ·   已有 {have} 包   ·   成熟后卖 {ItemDatabase.Find(d.seedOf)?.sellPrice ?? 0} 金币";

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

            var text = MakeText(row.transform, message, 32, TextAnchor.MiddleCenter,
                                new Color32(120, 88, 52, 255));
            Stretch(text.rectTransform, 0f);
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

            var nameText = MakeText(textBlock.transform, def.displayName, 34, TextAnchor.MiddleLeft,
                                    new Color32(74, 42, 12, 255));
            nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var subText = MakeText(textBlock.transform, subtitle, 26, TextAnchor.MiddleLeft,
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
                MakeButton(btnGroup.transform, label, new Vector2(160f, 70f), act, true);
            }
        }

        // ------------------------------------------------------------ 买卖

        void Buy(string seedId, int count)
        {
            var def = ItemDatabase.Find(seedId);
            if (def == null) return;

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
            // 关闭时保证时间恢复：哪怕外部直接 SetActive(false) 也不会把游戏永久冻住
            if (!IsOpen && Time.timeScale == 0f) Time.timeScale = 1f;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Text MakeText(Transform parent, string content, int size, TextAnchor align, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = GameData.GetUIFont();          // 内置字体没有中文字形，必须用系统动态字体
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button MakeButton(Transform parent, string label, Vector2 size, System.Action onClick, bool addLayout)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = ButtonSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiPpu;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = ButtonColors();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            if (addLayout)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = size.x;
                le.preferredHeight = size.y;
            }

            var text = MakeText(go.transform, label, 32, TextAnchor.MiddleCenter,
                                new Color32(255, 243, 208, 255));
            Stretch(text.rectTransform, 4f);

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.29f, 0.15f, 0.03f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return btn;
        }

        static ColorBlock ButtonColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1f, 0.95f, 0.86f);
            c.pressedColor = new Color(0.86f, 0.78f, 0.66f);
            c.disabledColor = new Color(0.62f, 0.58f, 0.52f, 0.6f);
            return c;
        }

        /// <summary>
        /// 页签底部的高亮条（选中的才显示）。
        ///
        /// 不用改 Image.color 来区分选中：Button 的 ColorTint 过渡每帧会把 color
        /// 写回 normalColor，手动压暗会被立刻覆盖 —— 挂一条独立的小图更稳。
        /// </summary>
        static void AddTabUnderline(Button tab)
        {
            if (tab == null) return;
            var go = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(tab.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.09f, 0f);
            rt.anchorMax = new Vector2(0.91f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 7f);
            rt.anchoredPosition = new Vector2(0f, 9f);

            var img = go.GetComponent<Image>();
            img.color = new Color32(255, 206, 92, 255);
            img.raycastTarget = false;
        }

        static void TintTab(Button tab, bool active)
        {
            if (tab == null) return;
            var line = tab.transform.Find("Underline");
            if (line != null) line.gameObject.SetActive(active);
        }

        // 底图统一从 UiTheme 拿（素材只有一份，见 UiTheme 的注释）
        static Sprite PanelSprite => UiTheme.Instance != null ? UiTheme.Instance.panel : null;
        static Sprite ButtonSprite => UiTheme.Instance != null ? UiTheme.Instance.button : null;
        static Sprite SlotSprite => UiTheme.Instance != null ? UiTheme.Instance.slot : null;
        static Sprite GoldPlateSprite => UiTheme.Instance != null ? UiTheme.Instance.goldPlate : null;
        static Sprite CoinSprite => UiTheme.Instance != null ? UiTheme.Instance.coinIcon : null;

    }
}
