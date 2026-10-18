using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 玩家交互：扫描身旁最近的可交互物，响应 E 键（PC）与 HUD 交互按钮（手机）。
    /// 只做「找目标 + 转发交互」，具体行为由目标自己实现 <see cref="IInteractable"/>。
    ///
    /// 扫描用 0.1s 轮询而不是每帧 OverlapCircle：场景里树木上百棵，
    /// 每帧全扫浪费；0.1s 的延迟远低于人手反应，HUD 按钮的亮/灰看不出差别。
    ///
    /// v0.14 起从「只认 TreeChop」改成「认 IInteractable」—— 门、楼梯、商店
    /// 都是这套体系，新增交互物不用再动这个类和 HUD。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("交互")]
        [Tooltip("玩家中心到交互物中心的最大距离（世界单位 ≈ 格）。1.6 = 树碰撞盒外约 0.6 格可触及")]
        public float interactRange = 1.6f;

        [Tooltip("PC 交互键。E 是农场/生存游戏的约定键；避开 WASD 与空格（空格常用于对话框）")]
        public KeyCode interactKey = KeyCode.E;

        [Tooltip("扫描间隔（秒）")]
        public float scanInterval = 0.1f;

        [Tooltip("没有目标时 HUD 按钮上的文案")]
        public string idlePrompt = "交互";

        /// <summary>当前最近的可交互物（没有则为 null）。HUD 按钮据此亮/灰、改文案。</summary>
        public IInteractable Current { get; private set; }

        /// <summary>HUD 按钮该显示的文字：有目标用目标自己的动词，没有就用 idlePrompt。</summary>
        public string CurrentPrompt => Current != null ? Current.Prompt : idlePrompt;

        private float _nextScan;
        private readonly Collider2D[] _hits = new Collider2D[32];

        private void Update()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + scanInterval;
                Scan();
            }

            if (Input.GetKeyDown(interactKey)) Interact();
        }

        private void Scan()
        {
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, interactRange, _hits);
            IInteractable best = null;
            float bestSqr = interactRange * interactRange;

            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                // 接口挂在碰撞体所在物体或其父级（门/楼梯常常把 collider 放子节点）
                var it = col.GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract) continue;

                float sqr = ((Vector2)(col.transform.position - transform.position)).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = it;
                }
            }

            Current = best;
        }

        /// <summary>
        /// 交互当前目标。HUD 按钮的 onClick 绑这个方法。
        /// 返回 true = 确实执行了一次交互。
        /// </summary>
        public bool Interact()
        {
            var target = Current;
            if (target == null || !target.CanInteract) return false;

            // 先清空：砍倒树 / 进屋后，HUD 按钮要立刻变灰，不能等下一次扫描
            Current = null;
            target.Interact(this);
            return true;
        }
    }
}
