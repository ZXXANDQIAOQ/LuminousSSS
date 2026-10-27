using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 钓鱼图鉴 / 成就记录（静态类 + PlayerPrefs）。
    ///
    /// 记录每种鱼的：钓到次数、最重重量。用 PlayerPrefs 持久化，
    /// 键格式：LuminousSSS.Fish.{fishId} = "次数|最重kg"
    ///
    /// 另有「跑掉的次数」：LuminousSSS.FishEscaped.{fishId} = 次数（int）。
    /// v0.21 加了上鱼 QTE 之后，「差一点钓上来」也是有价值的信息（能算成功率），
    /// 但没有并进上面那个 "次数|最重kg" 串 —— 改存档格式会让老玩家已有的记录解析失败，
    /// 新开一个键最省事，代价只是多一条 PlayerPrefs。
    ///
    /// 集合驱动是钓鱼玩法的核心钩子："这次能不能钓到没见过的鱼"。
    /// </summary>
    public static class FishingLog
    {
        const string PrefPrefix = "LuminousSSS.Fish.";
        const string EscapedPrefix = "LuminousSSS.FishEscaped.";

        /// <summary>记录一次钓获。</summary>
        public static void Record(string fishId, float weight)
        {
            if (string.IsNullOrEmpty(fishId)) return;

            string key = PrefPrefix + fishId;
            string raw = PlayerPrefs.GetString(key, "0|0");
            var parts = raw.Split('|');
            int count = 1;
            float maxWeight = weight;

            if (parts.Length >= 2)
            {
                int.TryParse(parts[0], out count);
                float.TryParse(parts[1], out maxWeight);
                count++;
                if (weight > maxWeight) maxWeight = weight;
            }

            PlayerPrefs.SetString(key, $"{count}|{maxWeight:F3}");
            PlayerPrefs.Save();
        }

        /// <summary>获取某鱼的钓获次数（没钓过返回 0）。</summary>
        public static int GetCount(string fishId)
        {
            string raw = PlayerPrefs.GetString(PrefPrefix + fishId, "0|0");
            var parts = raw.Split('|');
            return parts.Length >= 1 && int.TryParse(parts[0], out int c) ? c : 0;
        }

        /// <summary>获取某鱼的最重记录（没钓过返回 0）。</summary>
        public static float GetMaxWeight(string fishId)
        {
            string raw = PlayerPrefs.GetString(PrefPrefix + fishId, "0|0");
            var parts = raw.Split('|');
            return parts.Length >= 2 && float.TryParse(parts[1], out float w) ? w : 0f;
        }

        // ------------------------------------------------------------ 跑掉的次数（v0.21）

        /// <summary>记录一次「鱼跑了」（QTE 进度清零或超时）。</summary>
        public static void RecordEscaped(string fishId)
        {
            if (string.IsNullOrEmpty(fishId)) return;
            PlayerPrefs.SetInt(EscapedPrefix + fishId, GetEscapedCount(fishId) + 1);
            PlayerPrefs.Save();
        }

        /// <summary>某鱼跑掉过几次（没跑过返回 0）。</summary>
        public static int GetEscapedCount(string fishId)
        {
            if (string.IsNullOrEmpty(fishId)) return 0;
            return PlayerPrefs.GetInt(EscapedPrefix + fishId, 0);
        }

        /// <summary>已钓到多少种不同的鱼（图鉴完成度）。</summary>
        public static int UniqueSpecies()
        {
            int count = 0;
            var db = ItemDatabase.Instance;
            if (db == null || db.items == null) return 0;
            foreach (var it in db.items)
                if (it != null && it.kind == ItemKind.Fish && GetCount(it.id) > 0) count++;
            return count;
        }

        /// <summary>总共有多少种鱼（图鉴总数）。</summary>
        public static int TotalSpecies()
        {
            var db = ItemDatabase.Instance;
            if (db == null || db.items == null) return 0;
            int count = 0;
            foreach (var it in db.items)
                if (it != null && it.kind == ItemKind.Fish) count++;
            return count;
        }
    }
}
