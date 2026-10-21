using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 背包界面（v0.16）：6 列 × 4 行格子 + 右侧详情。
    ///
    /// 为什么背包要有格子上限：
    ///   之前的 <see cref="Inventory"/> 是个无限字典，东西只会越攒越多，
    ///   玩家没有「该去集市卖掉一批」的动机，经济循环是空转的。
    ///   有了 <see cref="Inventory.SlotCount"/> 格 + 每格 99 上限，
    ///   收获塞不下时才会真的需要卖东西换钱、再买种子 —— 这才闭环。
    ///
    /// 和商店的关系：商店负责「钱 ↔ 物品」，背包负责「看 / 选种子 / 丢弃」。
    ///   卖东西仍然只在集市做，避免玩家在田里一键清仓，跑腿就没意义了。
    ///
    /// 排序固定为「种子 → 作物 → 鱼」，组内按物品表顺序：
    ///   Dictionary 的枚举顺序在增删几次后会乱，每次打开位置都变会很难找。
    ///
    /// UI 全在运行时用代码铺（理由见 <see cref="UiFactory"/>），
    /// 打开时冻结 timeScale，与商店共用 <see cref="UiModal"/> 的模态计数。
    /// </summary>
    public class BackpackPanel : MonoBehaviour
    {
        public static bool IsOpen => _inst != null && _inst._root != null && _inst._root.activeSelf;

        const int SortingOrder = 200;      // 与商店同层（两者互斥）；HUD 是 100，Toast 300

        const float PanelW = 1320f;
        const float PanelH = 840f;
        const float SlotCell = 132f;
        const float SlotGap = 12f;
        const int Columns = 6;
        const int Rows = 4;                // 6×4 = 24，必须与 Inventory.SlotCount 一致

        const float GridTop = -206f;
        const float GridW = Columns * SlotCell + (Columns - 1) * SlotGap;   // 852
        const float GridH = Rows * SlotCell + (Rows - 1) * SlotGap;         // 564

        static readonly Color InkDark = new Color32(74, 42, 12, 255);
        static readonly Color InkMid = new Color32(126, 92, 52, 255);
        static readonly Color GoldInk = new Color32(232, 168, 42, 255);

        static BackpackPanel _inst;

        enum Tab { All, Seed, Harvest }

        GameObject _root;              // 整个 Canvas（连遮罩一起开关）
        RectTransform _gridRoot;
        RectTransform _detailContent;
        Text _capacityText;
        Text _goldText;
        Button _tabAll, _tabSeed, _tabHarvest;

        Tab _tab = Tab.All;
        string _detailId;              // 详情区正在展示的物品 id（null = 没选）

        // ------------------------------------------------------------ 对外入口

        /// <summary>打开背包（已打开则只刷新）。</summary>
        public static void Open()
        {
            if (ShopPanel.IsOpen) ShopPanel.Close();   // 两个面板互斥，否则会叠在一起

            if (_inst == null)
            {
                var go = new GameObject("[BackpackPanel]");
                Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<BackpackPanel>();
                _inst.Build();
            }

            _inst._root.SetActive(true);
            _inst.Refresh();
            UiModal.Apply();
        }

        public static void Close()
        {
            if (_inst == null || _inst._root == null) return;
            _inst._root.SetActive(false);
            UiModal.Apply();
        }

        /// <summary>Tab / B 键用：开着就关，关着就开。</summary>
        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        // ------------------------------------------------------------ 构建

        void Build()
        {
            var canvas = UiFactory.CreateCanvas(transform, "Canvas", SortingOrder);
            var canvasRt = canvas.GetComponent<RectTransform>();
            _root = canvas.gameObject;
            _root.SetActive(false);         // 建完先关着，等 Open()

            UiFactory.MakeDim(canvasRt);

            var panel = UiFactory.MakePanel(canvasRt, "Panel",
                                            Theme?.panelInventory, new Vector2(PanelW, PanelH));
            panel.color = new Color(1f, 0.98f, 0.94f, 1f);
            var panelTf = panel.transform;

            BuildHeader(panelTf);

            // ---- 三个页签（筛选）
            _tabAll = MakeTab(panelTf, "全部", 56f, () => SwitchTab(Tab.All));
            _tabSeed = MakeTab(panelTf, "种子", 244f, () => SwitchTab(Tab.Seed));
            _tabHarvest = MakeTab(panelTf, "收获", 432f, () => SwitchTab(Tab.Harvest));

            // ---- 容量（页签行右侧）
            _capacityText = UiFactory.MakeText(panelTf, "0 / 24 格", 30, TextAnchor.MiddleRight, InkMid);
            UiFactory.Place(_capacityText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(320f, 66f), new Vector2(-56f, -118f));

            BuildGrid(panelTf);
            BuildDetail(panelTf);
        }

        void BuildHeader(Transform panelTf)
        {
            var title = UiFactory.MakeText(panelTf, "背包", 46, TextAnchor.MiddleLeft, InkDark);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(420f, 70f), new Vector2(56f, -32f));

            // 关闭按钮（右上角）
            var close = UiFactory.MakeButton(panelTf, "×", new Vector2(76f, 76f), Close, false);
            UiFactory.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(76f, 76f), new Vector2(-34f, -28f));

            // 金币条（关闭按钮左侧）：面板是模态的，会盖住 HUD 上的金币，这里得有一份
            var plate = UiFactory.MakePanel(panelTf, "Gold", Theme?.goldPlate, new Vector2(240f, 70f));
            UiFactory.Place(plate.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(240f, 70f), new Vector2(-126f, -31f));

            var coin = UiFactory.MakeIcon(plate.transform, Theme?.coinIcon, new Vector2(50f, 50f));
            UiFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(50f, 50f), new Vector2(14f, 0f));

            _goldText = UiFactory.MakeText(plate.transform, "0", 34, TextAnchor.MiddleLeft, InkDark);
            UiFactory.Place(_goldText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(160f, 60f), new Vector2(72f, 0f));
        }

        Button MakeTab(Transform panelTf, string label, float x, System.Action onClick)
        {
            var btn = UiFactory.MakeButton(panelTf, label, new Vector2(176f, 66f), onClick, false);
            UiFactory.Place(btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(176f, 66f), new Vector2(x, -118f));
            UiFactory.AddTabUnderline(btn);
            return btn;
        }

        void BuildGrid(Transform panelTf)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(panelTf, false);

            _gridRoot = UiFactory.Place(go.GetComponent<RectTransform>(),
                                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        new Vector2(GridW, GridH), new Vector2(56f, GridTop));

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(SlotCell, SlotCell);
            grid.spacing = new Vector2(SlotGap, SlotGap);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
        }

        void BuildDetail(Transform panelTf)
        {
            var bg = UiFactory.MakePanel(panelTf, "Detail", Theme?.tooltipPanel, new Vector2(320f, GridH));
            var bgRt = UiFactory.Place(bg.GetComponent<RectTransform>(),
                                       new Vector2(1f, 1f), new Vector2(1f, 1f),
                                       new Vector2(320f, GridH), new Vector2(-56f, GridTop));
            bg.color = new Color(1f, 0.99f, 0.95f, 0.96f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content.transform.SetParent(bgRt, false);
            _detailContent = content.GetComponent<RectTransform>();
            UiFactory.Stretch(_detailContent, 18f);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        // ------------------------------------------------------------ 刷新

        void SwitchTab(Tab tab)
        {
            _tab = tab;
            Refresh();
        }

        /// <summary>重建格子 + 刷新金币 / 容量 / 页签 / 详情。任何改动之后都要调。</summary>
        void Refresh()
        {
            if (_gridRoot == null) return;

            _goldText.text = Inventory.Gold.ToString();
            _capacityText.text = $"{Inventory.UsedSlots} / {Inventory.SlotCount} 格";

            UiFactory.TintTab(_tabAll, _tab == Tab.All);
            UiFactory.TintTab(_tabSeed, _tab == Tab.Seed);
            UiFactory.TintTab(_tabHarvest, _tab == Tab.Harvest);

            BuildSlots();
            RefreshDetail();
        }

        void BuildSlots()
        {
            ClearChildren(_gridRoot);

            var shown = new List<KeyValuePair<string, int>>();
            foreach (var kv in Inventory.EntriesSorted())
            {
                var kind = ItemDatabase.KindOf(kv.Key);
                if (_tab == Tab.Seed && kind != ItemKind.Seed) continue;
                if (_tab == Tab.Harvest && kind == ItemKind.Seed) continue;
                shown.Add(kv);
            }

            // 格子数恒定（空格也画出来），玩家一眼能看出还剩多少位置
            for (int i = 0; i < Columns * Rows; i++)
            {
                bool filled = i < shown.Count;
                BuildSlot(filled ? shown[i].Key : null, filled ? shown[i].Value : 0);
            }
        }

        /// <summary>一个格子。id 为空 = 空格（只有底图，点它等于取消选择）。</summary>
        void BuildSlot(string id, int count)
        {
            string name = id != null ? "Slot_" + id : "Slot_Empty";

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_gridRoot, false);

            var img = go.GetComponent<Image>();
            // 选中态直接换底图，而不是在上面叠一层 ring：
            // v2_ui_slot_selected 是**不透明**的（实测中心 alpha=255），
            // 叠在上层会把图标整个盖住，只剩一个空金框。
            bool ring = id != null && id == _detailId;
            img.sprite = ring ? Theme?.slotSelected : (id != null ? Theme?.slot : Theme?.slotEmpty);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiFactory.UiPpu;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = UiFactory.ButtonColors();
            btn.onClick.AddListener(() => OnSlotClicked(id));

            if (id == null) return;

            var icon = UiFactory.MakeIcon(go.transform, ItemDatabase.IconOf(id), new Vector2(96f, 96f));

            var num = UiFactory.MakeText(go.transform, "×" + count, 26, TextAnchor.LowerRight,
                                         new Color32(255, 246, 222, 255));
            UiFactory.Place(num.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(110f, 34f), new Vector2(-8f, 8f));
            var numOutline = num.gameObject.AddComponent<Outline>();
            numOutline.effectColor = new Color(0.16f, 0.09f, 0.02f, 0.95f);
            numOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 待播种子：左上角一个金点。不做成文字是因为 132px 的格子里放「播」太挤
            if (id == Inventory.SelectedSeedId)
            {
                var dot = UiFactory.MakeText(go.transform, "●", 26, TextAnchor.UpperLeft, GoldInk);
                UiFactory.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                new Vector2(34f, 34f), new Vector2(8f, -8f));
                var dotOutline = dot.gameObject.AddComponent<Outline>();
                dotOutline.effectColor = new Color(0.24f, 0.12f, 0.02f, 0.95f);
                dotOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 当前查看的格子：底图已在上面换成 slot_selected，这里不用再叠东西
        }

        void OnSlotClicked(string id)
        {
            _detailId = id;            // 点空格 = 清空详情
            AudioManager.Click();
            Refresh();
        }

        // ------------------------------------------------------------ 详情

        void RefreshDetail()
        {
            if (_detailContent == null) return;
            ClearChildren(_detailContent);

            var def = _detailId != null ? ItemDatabase.Find(_detailId) : null;
            int count = def != null ? Inventory.Count(def.id) : 0;

            // 物品被丢弃 / 卖光后详情要自动收起，不能挂着一条不存在的记录
            if (def == null || count <= 0)
            {
                _detailId = null;
                AddHint("点一个格子查看详情");
                return;
            }

            var icon = UiFactory.MakeIcon(_detailContent, def.icon, new Vector2(148f, 148f));
            icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 148f;

            var name = UiFactory.MakeText(_detailContent, def.displayName, 36, TextAnchor.MiddleCenter, InkDark);
            name.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;

            var kindLine = UiFactory.MakeText(_detailContent, KindLabel(def), 26, TextAnchor.MiddleCenter, InkMid);
            kindLine.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

            var desc = UiFactory.MakeText(_detailContent, Describe(def), 24, TextAnchor.UpperLeft, InkMid);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            desc.lineSpacing = 1.15f;
            var descLe = desc.gameObject.AddComponent<LayoutElement>();
            descLe.preferredHeight = 100f;
            descLe.flexibleHeight = 1f;

            // ---- 操作按钮
            if (def.kind == ItemKind.Seed)
            {
                bool picked = Inventory.SelectedSeedId == def.id;
                UiFactory.MakeButton(_detailContent,
                                     picked ? "取消待播" : "选为播种",
                                     new Vector2(280f, 66f),
                                     () => ToggleSelect(def.id), true);
            }
            else
            {
                AddHint($"持有 {count} 个  ·  单价 {def.sellPrice} 金币");
            }

            var drop = UiFactory.MakeButton(_detailContent, "丢弃 1 个", new Vector2(280f, 66f),
                                            () => Drop(def.id), true);
            drop.colors = Dim(drop.colors);
        }

        void ToggleSelect(string seedId)
        {
            if (Inventory.SelectedSeedId == seedId)
            {
                Inventory.SelectSeed(null);
                Toast.Show("已取消待播种子，播种时自动挑一种");
            }
            else
            {
                Inventory.SelectSeed(seedId);
                Toast.Show($"待播种子：{ItemDatabase.NameOf(seedId)}");
            }

            AudioManager.Confirm();
            Refresh();
        }

        void Drop(string id)
        {
            if (!Inventory.TryConsume(id, 1)) return;
            Toast.Show($"丢掉了 {ItemDatabase.NameOf(id)} ×1");
            AudioManager.Click();
            Refresh();
        }

        void AddHint(string message)
        {
            var text = UiFactory.MakeText(_detailContent, message, 26, TextAnchor.UpperCenter, InkMid);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 260f;
            le.flexibleHeight = 1f;
        }

        /// <summary>类别 + 价值的一行摘要。</summary>
        static string KindLabel(ItemDef def)
        {
            switch (def.kind)
            {
                case ItemKind.Seed: return $"种子 · {def.buyPrice} 金币 / 包";
                case ItemKind.Fish: return $"鱼 · 收购 {def.sellPrice} 金币";
                default: return $"作物 · 收购 {def.sellPrice} 金币";
            }
        }

        /// <summary>物品说明。不加 description 字段是有意的：价格与生长帧都在表里，能算出来就别让数据两处维护。</summary>
        static string Describe(ItemDef def)
        {
            switch (def.kind)
            {
                case ItemKind.Seed:
                    var crop = ItemDatabase.Find(def.seedOf);
                    string cropName = crop != null ? crop.displayName : "作物";
                    int minutes = Mathf.Max(1, Mathf.RoundToInt(
                        ((def.growthFrames != null ? def.growthFrames.Length : 0) + 1) * 30f / 60f));
                    return $"种在耕地上，约 {minutes} 分钟成熟，收获 {cropName}。" +
                           $"集市上 {cropName} 卖 {(crop != null ? crop.sellPrice : 0)} 金币。";

                case ItemKind.Fish:
                    return $"池塘里钓上来的鲜鱼。拿去村口集市能卖 {def.sellPrice} 金币。";

                default:
                    return $"农场种出来的收成。拿去村口集市能卖 {def.sellPrice} 金币。";
            }
        }

        // ------------------------------------------------------------ 收尾 / 小工具

        void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();

            // 兜底：外面直接把面板 SetActive(false) 时也要把时间放开，
            // 否则游戏会永远停在 timeScale = 0
            if (!IsOpen && _root != null && !_root.activeSelf) UiModal.Apply();
        }

        /// <summary>销毁子物体。先摘出层级再 Destroy —— Destroy 延迟到帧末，本帧布局里还会留着旧格子。</summary>
        static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            while (parent.childCount > 0)
            {
                var child = parent.GetChild(0);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        /// <summary>把按钮调暗一档（丢弃是破坏性操作，不该和主按钮一样显眼）。</summary>
        static ColorBlock Dim(ColorBlock c)
        {
            c.normalColor = new Color(0.82f, 0.76f, 0.68f);
            c.highlightedColor = new Color(0.9f, 0.84f, 0.74f);
            return c;
        }

        static UiTheme Theme => UiTheme.Instance;
    }
}
