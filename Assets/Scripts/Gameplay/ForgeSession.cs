using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 单次锻造的状态机（Idle → Heating → Ready → Striking → Done/Broken）。
    ///
    /// 纯 C# 类（不挂场景对象），由 <see cref="ForgePanel"/> 持有并驱动。
    /// 概率计算、价值更新、遗物效果都在这里，UI 只负责显示和收按钮事件。
    ///
    /// 核心博弈：每次锤击抽签 → 价值变化 → 玩家决定"收剑"还是"再敲"。
    /// 价值越高破碎概率越高（贪心的代价），连敲 N 次触发"入神"（下锤必不碎）。
    /// </summary>
    public class ForgeSession
    {
        /// <summary>锻造状态。</summary>
        public enum State
        {
            Idle,       // 未开始
            Heating,    // 入炉加热中
            Ready,      // 可锤击
            Striking,   // 锤击动画中（短暂）
            Done,       // 收剑成功
            Broken      // 剑碎了
        }

        // ---- 当前状态 ----
        public State Current { get; private set; } = State.Idle;
        public float Value { get; private set; }
        public int HitCount { get; private set; }
        public int FrenzyProgress { get; private set; }
        public bool FrenzyActive { get; private set; }
        public float BreakChance => _config != null ? _config.BreakChance(Value) : 0.05f;

        // ---- 遗物效果 ----
        readonly List<string> _activeRelics = new();
        int _damageStreak;          // 连续损伤次数（巨人脊骨用）
        int _stackCount;            // 损伤积攒层数（咬合砝码用）
        bool _breakPrevented;       // 龙鳞护符：防一次破碎
        public int RewindAvailable { get; private set; }  // 悔恨发条：可撤销次数

        // ---- 上一锤快照（悔恨发条用） ----
        float _prevValue;
        int _prevHitCount;
        int _prevDamageStreak;
        int _prevStackCount;
        bool _prevFrenzyActive;

        // ---- 配置 ----
        readonly ForgeConfig _config;

        // ---- 事件 ----
        public event Action<ForgeResult, float> OnStrike;       // (结果, 新价值)
        public event Action OnBroken;
        public event Action<float> OnDone;                      // (最终价值)
        public event Action<string> OnFrenzy;                   // (提示文本)

        public ForgeSession(ForgeConfig config = null)
        {
            _config = config != null ? config : ForgeConfig.Instance;
        }

        /// <summary>开始新的一把剑（入炉加热）。</summary>
        public void StartForge()
        {
            Value = _config != null ? _config.baseSwordValue : 100;
            HitCount = 0;
            FrenzyProgress = 0;
            FrenzyActive = false;
            _damageStreak = 0;
            _stackCount = 0;
            _breakPrevented = false;
            RewindAvailable = 0;
            Current = State.Heating;
        }

        /// <summary>加热完成，进入可锤击状态。</summary>
        public void FinishHeating()
        {
            if (Current == State.Heating) Current = State.Ready;
        }

        /// <summary>锤击一锤。返回是否成功执行（非 Ready 状态不行）。</summary>
        public bool Strike()
        {
            if (Current != State.Ready) return false;

            // 保存快照（悔恨发条）
            _prevValue = Value;
            _prevHitCount = HitCount;
            _prevDamageStreak = _damageStreak;
            _prevStackCount = _stackCount;
            _prevFrenzyActive = FrenzyActive;

            // 计算动态破碎概率
            float breakChance = BreakChance;
            bool canBreak = true;

            // 龙鳞护符：防一次破碎
            if (_breakPrevented)
            {
                canBreak = false;
                _breakPrevented = false;
            }

            // 入神：下锤必不碎
            if (FrenzyActive && _config != null && _config.frenzyGuaranteesSuccess)
            {
                canBreak = false;
                FrenzyActive = false;   // 每把剑限一次
            }

            // 抽签
            ForgeResult result;
            if (canBreak && UnityEngine.Random.value < breakChance)
            {
                result = ForgeResult.Break;
            }
            else
            {
                result = _config != null ? _config.Roll() : ForgeResult.Keep;
                if (result == ForgeResult.Break && !canBreak) result = ForgeResult.Keep;
            }

            // 巨人脊骨：连续 N 次损伤 → 下次必大成功
            if (_damageStreak > 0 && _config != null)
            {
                var giantBone = FindRelic("relic_giant_bone");
                if (giantBone != null && _damageStreak >= giantBone.damageStreakToCrit && giantBone.damageStreakToCrit > 0)
                {
                    result = ForgeResult.Critical;
                    _damageStreak = 0;
                }
            }

            // 红粉笔：大成功概率翻倍（在 Roll 之后再判一次）
            if (result == ForgeResult.Success)
            {
                var redChalk = FindRelic("relic_red_chalk");
                if (redChalk != null && redChalk.critChanceMul > 1f && UnityEngine.Random.value < 0.5f)
                    result = ForgeResult.Critical;
            }

            // 应用结果
            switch (result)
            {
                case ForgeResult.Break:
                    Value = 0;
                    Current = State.Broken;
                    OnBroken?.Invoke();
                    break;

                case ForgeResult.Damage:
                    Value = Mathf.RoundToInt(Value * (_config?.damageMul ?? 0.9f));
                    _damageStreak++;
                    // 咬合砝码：损伤积攒强化
                    if (FindRelic("relic_scale") != null) _stackCount++;
                    break;

                case ForgeResult.Keep:
                    _damageStreak = 0;
                    break;

                case ForgeResult.Success:
                    Value = Mathf.RoundToInt(Value * (_config?.successMul ?? 1.2f));
                    _damageStreak = 0;
                    // 咬合砝码：大成功时爆发
                    if (_stackCount > 0)
                    {
                        Value += _stackCount * 50;
                        _stackCount = 0;
                    }
                    break;

                case ForgeResult.Critical:
                    Value = Mathf.RoundToInt(Value * (_config?.critMul ?? 1.5f));
                    _damageStreak = 0;
                    if (_stackCount > 0)
                    {
                        Value += _stackCount * 80;
                        _stackCount = 0;
                    }
                    break;
            }

            HitCount++;
            FrenzyProgress++;

            // 入神充能
            if (_config != null && FrenzyProgress >= _config.frenzyHits && !FrenzyActive)
            {
                FrenzyActive = true;
                OnFrenzy?.Invoke("入神！下锤必不碎");
            }

            Current = State.Ready;
            OnStrike?.Invoke(result, Value);
            return true;
        }

        /// <summary>收剑（结算价值）。</summary>
        public float Collect()
        {
            if (Current != State.Ready) return 0;
            Current = State.Done;
            OnDone?.Invoke(Value);
            return Value;
        }

        /// <summary>悔恨发条：撤销上一锤。</summary>
        public bool Rewind()
        {
            if (RewindAvailable <= 0 || Current != State.Ready) return false;
            if (HitCount == 0) return false;

            RewindAvailable--;
            Value = _prevValue;
            HitCount = _prevHitCount;
            _damageStreak = _prevDamageStreak;
            _stackCount = _prevStackCount;
            FrenzyActive = _prevFrenzyActive;
            return true;
        }

        /// <summary>使用龙鳞护符（防下一次破碎）。</summary>
        public bool UseBreakPrevent()
        {
            if (_breakPrevented) return false;
            _breakPrevented = true;
            return true;
        }

        /// <summary>启用一件遗物。</summary>
        public void ActivateRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId) || _activeRelics.Contains(relicId)) return;
            _activeRelics.Add(relicId);

            var def = RelicDatabase.Find(relicId);
            if (def == null) return;

            if (def.canRewind) RewindAvailable++;
            if (def.breakReduction > 0.5f) _breakPrevented = true;
        }

        /// <summary>双生印：收剑时概率复制。</summary>
        public bool RollDuplicate()
        {
            var twin = FindRelic("relic_twin_seal");
            return twin != null && UnityEngine.Random.value < twin.duplicateChance;
        }

        RelicDef FindRelic(string id)
        {
            return _activeRelics.Contains(id) ? RelicDatabase.Find(id) : null;
        }

        /// <summary>当前入神充能（0~1），供 UI 画进度条。</summary>
        public float FrenzyProgress01
        {
            get
            {
                if (_config == null || _config.frenzyHits <= 0) return 0f;
                return Mathf.Clamp01((float)FrenzyProgress / _config.frenzyHits);
            }
        }
    }
}
