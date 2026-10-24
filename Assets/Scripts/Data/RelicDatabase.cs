using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 遗物表（ScriptableObject）。放在 Assets/Resources/RelicDB.asset。
    ///
    /// 遗物是铁匠铺的"构筑流派"系统：玩家用高价值剑兑换遗物，遗物改变概率签筒，
    /// 形成不同打法流派（稳健维持流 / 损伤增值流 / 极限赌狗流）。
    ///
    /// 表由 GameSceneBuilder.EnsureRelicDatabase() 生成；改遗物改那里再重跑菜单。
    /// </summary>
    [CreateAssetMenu(menuName = "LuminousSSS/RelicDatabase", fileName = "RelicDB")]
    public class RelicDatabase : ScriptableObject
    {
        public const string ResourcePath = "RelicDB";

        public RelicDef[] relics;

        static RelicDatabase _cached;

        public static RelicDatabase Instance
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<RelicDatabase>(ResourcePath);
                return _cached;
            }
        }

        /// <summary>按 id 查遗物定义。</summary>
        public static RelicDef Find(string id)
        {
            var db = Instance;
            if (db == null || db.relics == null || string.IsNullOrEmpty(id)) return null;
            foreach (var r in db.relics)
                if (r != null && r.id == id) return r;
            return null;
        }

        /// <summary>所有遗物（用于兑换列表）。</summary>
        public static List<RelicDef> All()
        {
            var list = new List<RelicDef>();
            var db = Instance;
            if (db == null || db.relics == null) return list;
            foreach (var r in db.relics)
                if (r != null) list.Add(r);
            return list;
        }
    }

    /// <summary>一件遗物的静态定义。</summary>
    [System.Serializable]
    public class RelicDef
    {
        [Tooltip("唯一 id，存档用它")]
        public string id;

        [Tooltip("界面显示名（中文）")]
        public string displayName;

        [Tooltip("效果描述（显示在遗物面板里）")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("兑换所需剑价值（≥ 这个值才能换）")]
        public int requiredSwordValue = 500;

        [Tooltip("遗物图标（可空）")]
        public Sprite icon;

        [Header("效果参数（不同遗物用不同字段）")]
        [Tooltip("破碎概率减免（0~1，龙鳞护符=1.0 表示防一次破碎）")]
        public float breakReduction;

        [Tooltip("大成功概率倍率（红粉笔=2.0）")]
        public float critChanceMul = 1f;

        [Tooltip("损伤概率倍率（深海之珠=1.5）")]
        public float damageChanceMul = 1f;

        [Tooltip("是否是消耗型（用一次就没了）")]
        public bool consumable;

        [Tooltip("撤销上一锤（悔恨发条）")]
        public bool canRewind;

        [Tooltip("连续 N 次损伤后下次必大成功（巨人脊骨，0=无效果）")]
        public int damageStreakToCrit;

        [Tooltip("每损伤一次积攒强化层数（咬合砝码，大成功时爆发）")]
        public bool stackOnDamage;

        [Tooltip("概率复制成品剑（双生印，0~1）")]
        public float duplicateChance;
    }
}
