using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 玩家背包与金币（跨场景常驻的静态存档）。
    ///
    /// 为什么用静态类而不是场景里的单例：
    ///   背包要被商店 UI、播种点、钓鱼点、HUD 金币条同时读写，而这几样东西
    ///   有的在场景里（HUD）、有的是运行时才生成的（钓鱼提示）。挂场景对象会让
    ///   「换场景后谁来持有它」变成一堆 DontDestroyOnLoad 生命周期问题；
    ///   静态类 + PlayerPrefs 更直接，进场景时读完就是唯一真相。
    ///
    /// 存档格式（都是有意的简单格式，避免引 JSON 依赖）：
    ///   金币：PlayerPrefs int，键 LuminousSSS.Gold
    ///   物品：PlayerPrefs string，"id:数量;id:数量"（id 只含字母数字下划线，不需要转义）
    ///   待播种子：PlayerPrefs string，键 LuminousSSS.SelectedSeed
    ///
    /// v0.16 起加了容量概念：<see cref="SlotCount"/> 个格子，同一种物品堆到
    /// <see cref="StackMax"/> 为止占一格。这样背包 UI 才有「装满」可言，
    /// 而不是无限字典 —— 有上限才会让玩家去集市卖东西，经济循环才转得起来。
    ///
    /// 所有写操作都会触发 <see cref="Changed"/>，UI 订阅它刷新，不用轮询。
    /// </summary>
    public static class Inventory
    {
        public const string PrefGold = "LuminousSSS.Gold";
        public const string PrefItems = "LuminousSSS.Items";
        public const string PrefSelectedSeed = "LuminousSSS.SelectedSeed";

        /// <summary>首次进入游戏的启动资金：够买 5~7 包种子，又不至于一开始就能躺平。</summary>
        public const int StartGold = 150;

        /// <summary>背包格子数（6 列 × 4 行）。一种物品占一格，堆叠上限见 <see cref="StackMax"/>。</summary>
        public const int SlotCount = 24;

        /// <summary>单格堆叠上限。99 是这类游戏的通用值，也够玩家一整轮卖一次。</summary>
        public const int StackMax = 99;

        /// <summary>金币或物品发生变化时触发（参数无，订阅方自己读最新值）。</summary>
        public static event Action Changed;

        static Dictionary<string, int> _items;
        static int _gold = int.MinValue;
        static string _selectedSeed;
        static bool _selectedSeedLoaded;

        // ------------------------------------------------------------ 读

        public static int Gold
        {
            get { EnsureLoaded(); return _gold; }
        }

        /// <summary>持有数量（没有则 0）。</summary>
        public static int Count(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            EnsureLoaded();
            return _items.TryGetValue(id, out int n) ? n : 0;
        }

        public static bool Has(string id, int n = 1) => Count(id) >= n;

        /// <summary>背包里的全部条目（按加入顺序），商店的「收购」列表据此生成。</summary>
        public static List<KeyValuePair<string, int>> Entries()
        {
            EnsureLoaded();
            return new List<KeyValuePair<string, int>>(_items);
        }

        /// <summary>
        /// 按「种子 → 作物 → 鱼」分组、组内按物品表顺序排好的条目。
        /// 背包格子的排列用这个：Dictionary 的枚举顺序在增删几次后会乱，
        /// 玩家每次打开背包看到的物品位置都不一样会很难找。
        /// </summary>
        public static List<KeyValuePair<string, int>> EntriesSorted()
        {
            var list = Entries();
            list.Sort((a, b) =>
            {
                var da = ItemDatabase.Find(a.Key);
                var db = ItemDatabase.Find(b.Key);
                int ka = da != null ? (int)da.kind : 99;
                int kb = db != null ? (int)db.kind : 99;
                if (ka != kb) return ka.CompareTo(kb);

                int ia = ItemDatabase.IndexOf(a.Key);
                int ib = ItemDatabase.IndexOf(b.Key);
                if (ia != ib) return ia.CompareTo(ib);

                return string.CompareOrdinal(a.Key, b.Key);
            });
            return list;
        }

        // ------------------------------------------------------------ 容量

        /// <summary>已占用的格子数（每种有库存的物品算一格）。</summary>
        public static int UsedSlots
        {
            get
            {
                EnsureLoaded();
                int n = 0;
                foreach (var kv in _items) if (kv.Value > 0) n++;
                return n;
            }
        }

        public static bool IsFull => UsedSlots >= SlotCount;

        /// <summary>
        /// 能否再装下 count 个。买了东西再扣钱的调用方必须先用这个探一下，
        /// 否则会出现「钱扣了、东西没进包」。
        /// </summary>
        public static bool CanAdd(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return false;
            EnsureLoaded();
            if (!_items.ContainsKey(id) && UsedSlots >= SlotCount) return false;
            return Count(id) + count <= StackMax;
        }

        // ------------------------------------------------------------ 待播种子

        /// <summary>
        /// 玩家在背包里选定「下次种这个」的种子 id（空 = 没选，播种时自动挑）。
        /// 存 PlayerPrefs，进场景 / 重开游戏都还在 —— 不然每次进农场都要重新选一遍。
        /// </summary>
        public static string SelectedSeedId
        {
            get
            {
                if (!_selectedSeedLoaded)
                {
                    _selectedSeed = PlayerPrefs.GetString(PrefSelectedSeed, "");
                    _selectedSeedLoaded = true;
                }
                return string.IsNullOrEmpty(_selectedSeed) ? null : _selectedSeed;
            }
        }

        /// <summary>设为待播种子（传空 = 取消选择）。</summary>
        public static void SelectSeed(string id)
        {
            _selectedSeed = string.IsNullOrEmpty(id) ? "" : id;
            _selectedSeedLoaded = true;
            PlayerPrefs.SetString(PrefSelectedSeed, _selectedSeed);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ 金币

        public static void AddGold(int amount)
        {
            if (amount == 0) return;
            EnsureLoaded();
            _gold = Mathf.Max(0, _gold + amount);
            SaveGold();
            Changed?.Invoke();
        }

        /// <summary>扣钱，钱不够返回 false 且不做任何改动（调用方据此提示「金币不足」）。</summary>
        public static bool TrySpendGold(int amount)
        {
            if (amount <= 0) return true;
            EnsureLoaded();
            if (_gold < amount) return false;
            _gold -= amount;
            SaveGold();
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ 物品

        /// <summary>
        /// 往背包里放东西，返回**实际**放进去的数量（背包满 / 堆叠满时会少于 count）。
        /// count 为负表示扣除（供 <see cref="TryConsume"/> 复用），走同一套存档逻辑。
        /// </summary>
        public static int Add(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count == 0) return 0;
            EnsureLoaded();

            // ---- 负数：扣除，直接落库（调用方 TryConsume 已保证不会扣成负数）
            if (count < 0)
            {
                int next0 = Count(id) + count;
                if (next0 <= 0) _items.Remove(id);
                else _items[id] = next0;
                SaveItems();
                Changed?.Invoke();
                return count;
            }

            // ---- 新物品要占一格：格子满了就装不进来
            if (!_items.ContainsKey(id) && UsedSlots >= SlotCount)
            {
                Toast.Show("背包满了 —— 去村口集市卖掉一些吧");
                return 0;
            }

            int room = StackMax - Count(id);
            if (room <= 0)
            {
                Toast.Show($"{ItemDatabase.NameOf(id)} 已经堆满一格了");
                return 0;
            }

            int added = Mathf.Min(count, room);
            _items[id] = Count(id) + added;
            SaveItems();
            Changed?.Invoke();

            if (added < count) Toast.Show($"背包只装下 {added} 个 {ItemDatabase.NameOf(id)}");
            return added;
        }

        /// <summary>消耗物品，不够返回 false 且不扣（避免出现负数）。</summary>
        public static bool TryConsume(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return true;
            EnsureLoaded();
            if (Count(id) < count) return false;
            Add(id, -count);       // Add 内部已 Save + 触发事件
            return true;
        }

        /// <summary>清空存档（调试 / 新游戏用）。</summary>
        public static void ResetAll()
        {
            _items = new Dictionary<string, int>();
            _gold = StartGold;
            _selectedSeed = "";
            _selectedSeedLoaded = true;
            PlayerPrefs.SetInt(PrefGold, _gold);
            PlayerPrefs.SetString(PrefItems, "");
            PlayerPrefs.SetString(PrefSelectedSeed, "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ 存档

        static void EnsureLoaded()
        {
            if (_items != null && _gold != int.MinValue) return;

            _items = new Dictionary<string, int>();
            _gold = PlayerPrefs.GetInt(PrefGold, StartGold);   // 首次进游戏给启动资金
            if (!PlayerPrefs.HasKey(PrefGold)) { PlayerPrefs.SetInt(PrefGold, _gold); PlayerPrefs.Save(); }

            string raw = PlayerPrefs.GetString(PrefItems, "");
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var pair in raw.Split(';'))
            {
                if (pair.Length == 0) continue;
                int sep = pair.IndexOf(':');
                if (sep <= 0) continue;
                string id = pair.Substring(0, sep);
                if (int.TryParse(pair.Substring(sep + 1), out int n) && n > 0) _items[id] = n;
            }
        }

        static void SaveGold()
        {
            PlayerPrefs.SetInt(PrefGold, _gold);
            PlayerPrefs.Save();
        }

        static void SaveItems()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _items)
            {
                if (kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            PlayerPrefs.SetString(PrefItems, sb.ToString());
            PlayerPrefs.Save();
        }
    }
}
