using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 锻造概率签筒配置（ScriptableObject）。放在 Assets/Resources/ForgeConfig.asset，
    /// 运行时用 <see cref="Instance"/> 惰性加载 —— ForgeSession / ForgePanel 都从这里读概率。
    ///
    /// 5 档结果（参考 ForgeTax 的"价值递增 + 破碎风险博弈"骨架）：
    ///   破坏 Break   → 剑碎，价值归零
    ///   损伤 Damage  → 价值 ×damageMul
    ///   维持 Keep    → 价值不变
    ///   成功 Success → 价值 ×successMul
    ///   大成功 Crit  → 价值 ×critMul
    ///
    /// 权重总和不必为 100，内部归一化。遗物（<see cref="RelicDatabase"/>）可在运行时
    /// 临时修改这些权重，实现"构筑流派"。
    /// </summary>
    [CreateAssetMenu(menuName = "LuminousSSS/ForgeConfig", fileName = "ForgeConfig")]
    public class ForgeConfig : ScriptableObject
    {
        public const string ResourcePath = "ForgeConfig";

        [Header("5 档结果权重（总和不必为100，内部归一化）")]
        [Tooltip("破坏：剑碎，价值归零")]
        public int breakWeight = 5;

        [Tooltip("损伤：价值下降")]
        public int damageWeight = 15;

        [Tooltip("维持：价值不变")]
        public int keepWeight = 30;

        [Tooltip("成功：价值上升")]
        public int successWeight = 40;

        [Tooltip("大成功：价值大幅上升")]
        public int critWeight = 10;

        [Header("价值系数")]
        [Tooltip("损伤时价值乘这个系数")]
        public float damageMul = 0.9f;

        [Tooltip("成功时价值乘这个系数")]
        public float successMul = 1.2f;

        [Tooltip("大成功时价值乘这个系数")]
        public float critMul = 1.5f;

        [Header("贪心风险（价值越高破碎概率越高）")]
        [Tooltip("基础破碎概率（0~1）")]
        public float baseBreakChance = 0.05f;

        [Tooltip("每点价值额外增加的破碎概率")]
        public float breakRisePerValue = 0.00002f;

        [Tooltip("破碎概率上限（0~1）")]
        public float maxBreakChance = 0.35f;

        [Header("入神机制")]
        [Tooltip("连敲 N 次触发入神")]
        public int frenzyHits = 10;

        [Tooltip("入神期间下锤必不碎（每把剑限 1 次）")]
        public bool frenzyGuaranteesSuccess = true;

        [Header("基础剑参数")]
        [Tooltip("剑坯初始价值")]
        public int baseSwordValue = 100;

        [Header("锻造材料")]
        [Tooltip("每次入炉消耗的铁矿 id（空=不消耗）")]
        public string oreItemId = "iron_ore";

        [Tooltip("每次入炉消耗的铁矿数量")]
        public int oreCost = 1;

        [Tooltip("每次入炉消耗的煤炭 id（空=不消耗）")]
        public string coalItemId = "coal";

        [Tooltip("每次入炉消耗的煤炭数量")]
        public int coalCost = 1;

        static ForgeConfig _cached;

        /// <summary>惰性加载并缓存。Resources 里没有资产时返回 null（调用方需判空）。</summary>
        public static ForgeConfig Instance
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<ForgeConfig>(ResourcePath);
                return _cached;
            }
        }

        /// <summary>当前破碎概率（随剑价值动态上升）。</summary>
        public float BreakChance(float currentValue)
        {
            return Mathf.Min(baseBreakChance + currentValue * breakRisePerValue, maxBreakChance);
        }

        /// <summary>按权重抽一档结果。</summary>
        public ForgeResult Roll()
        {
            int total = breakWeight + damageWeight + keepWeight + successWeight + critWeight;
            if (total <= 0) return ForgeResult.Keep;

            int roll = Random.Range(0, total);
            if (roll < breakWeight) return ForgeResult.Break;
            roll -= breakWeight;
            if (roll < damageWeight) return ForgeResult.Damage;
            roll -= damageWeight;
            if (roll < keepWeight) return ForgeResult.Keep;
            roll -= keepWeight;
            if (roll < successWeight) return ForgeResult.Success;
            return ForgeResult.Critical;
        }

        /// <summary>归一化后的各档概率（0~1），供 UI 显示。</summary>
        public void GetProbabilities(out float breakP, out float damageP, out float keepP,
                                     out float successP, out float critP)
        {
            int total = breakWeight + damageWeight + keepWeight + successWeight + critWeight;
            if (total <= 0) { breakP = damageP = keepP = successP = critP = 0f; return; }
            float inv = 1f / total;
            breakP = breakWeight * inv;
            damageP = damageWeight * inv;
            keepP = keepWeight * inv;
            successP = successWeight * inv;
            critP = critWeight * inv;
        }
    }

    /// <summary>锻造一锤的 5 档结果。</summary>
    public enum ForgeResult
    {
        Break,      // 破坏：剑碎
        Damage,     // 损伤：价值下降
        Keep,       // 维持：价值不变
        Success,    // 成功：价值上升
        Critical    // 大成功：价值大幅上升
    }
}
