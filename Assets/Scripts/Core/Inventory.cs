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
    ///
    /// 所有写操作都会触发 <see cref="Changed"/>，UI 订阅它刷新，不用轮询。
    /// </summary>
    public static class Inventory
    {
        public const string PrefGold = "LuminousSSS.Gold";
        public const string PrefItems = "LuminousSSS.Items";

        /// <summary>首次进入游戏的启动资金：够买 5~7 包种子，又不至于一开始就能躺平。</summary>
        public const int StartGold = 150;

        /// <summary>金币或物品发生变化时触发（参数无，订阅方自己读最新值）。</summary>
        public static event Action Changed;

        static Dictionary<string, int> _items;
        static int _gold = int.MinValue;

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

        public static void Add(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count == 0) return;
            EnsureLoaded();
            int next = Count(id) + count;
            if (next <= 0) _items.Remove(id);
            else _items[id] = next;
            SaveItems();
            Changed?.Invoke();
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
            PlayerPrefs.SetInt(PrefGold, _gold);
            PlayerPrefs.SetString(PrefItems, "");
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
