using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 玩家交互：扫描身旁最近的可砍树，响应 E 键（PC）与 HUD 砍伐按钮（手机）。
    /// 只做「找目标 + 触发砍击」，砍击的数值与表现全在 <see cref="TreeChop"/>。
    ///
    /// 扫描用 0.1s 轮询而不是每帧 OverlapCircle：场景里树木上百棵，
    /// 每帧全扫浪费；0.1s 的延迟远低于人手反应，HUD 按钮的亮/灰看不出差别。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("交互")]
        [Tooltip("玩家中心到树中心的最大交互距离（世界单位 ≈ 格）。1.6 = 树碰撞盒外约 0.6 格可触及")]
        public float interactRange = 1.6f;

        [Tooltip("PC 砍伐键。E 是农场/生存游戏的约定键；避开 WASD 与空格（空格常用于对话框）")]
        public KeyCode chopKey = KeyCode.E;

        [Tooltip("扫描间隔（秒）")]
        public float scanInterval = 0.1f;

        /// <summary>当前可砍的最近树（没有则为 null）。HUD 按钮据此亮/灰。</summary>
        public TreeChop CurrentTarget { get; private set; }

        private float _nextScan;

        private void Update()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + scanInterval;
                Scan();
            }

            if (Input.GetKeyDown(chopKey)) Interact();
        }

        private void Scan()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, interactRange);
            float bestSqr = interactRange * interactRange;
            TreeChop best = null;

            foreach (var h in hits)
            {
                // 碰撞盒与 TreeChop 同挂在树根节点，直接取即可
                var tree = h.GetComponent<TreeChop>();
                if (tree == null || tree.IsChopped) continue;

                float sqr = ((Vector2)(tree.transform.position - transform.position)).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = tree;
                }
            }

            CurrentTarget = best;
        }

        /// <summary>
        /// 砍当前目标一下。HUD 砍伐按钮的 onClick 绑这个方法。
        /// 返回 true = 这一下砍倒了树。
        /// </summary>
        public bool Interact()
        {
            if (CurrentTarget == null || CurrentTarget.IsChopped) return false;

            bool felled = CurrentTarget.Chop();
            if (felled) CurrentTarget = null;   // 立刻清空，HUD 按钮马上变灰
            return felled;
        }
    }
}
