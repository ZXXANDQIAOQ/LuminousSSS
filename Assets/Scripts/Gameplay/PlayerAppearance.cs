using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 按登录界面保存的选择，替换场景内玩家的 Sprite，并把这只脚「踩」到地面上。
    ///
    /// 两种角色走两条路：
    ///   1) 有 AnimatorController 的（银骑士 / 森之猎手 / 炼金术士）—— 播四方向行走动画；
    ///   2) 没有行走帧的特殊角色（烁）—— 停用 Animator，改用 <see cref="CharacterAnimDriver"/>
    ///      的飘浮表现（悬浮起伏 + 身体轻摆 + flipX 转向），形象完全按原画走，不做帧重绘。
    ///
    /// 为什么这里要管 Visual 的高度：
    ///   不同角色的素材尺寸不一样（战斗小人 64×112，烁 54×101）。原来 Visual 的
    ///   localPosition.y 是场景生成器写死的 0.875（= 112/2/64），换成 101 高的图就会
    ///   悬空约 0.09 格 —— 看起来像"飘在影子上"。这里统一按 sprite 实际高度的一半重算，
    ///   脚底就永远贴着 transform 的位置。
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
            public RuntimeAnimatorController controller;   // 可选：有则播四方向行走

            [Tooltip("在半高之外再微调脚底高度（格）。素材本身留白时才需要，通常 0")]
            public float visualOffsetY;

            [Tooltip("无行走帧的特殊角色：用飘浮表现代替走路动画")]
            public bool floaty;
        }

        public SpriteRenderer target;
        public Animator animator;
        public Entry[] entries;

        private CharacterAnimDriver _driver;

        private void Awake()
        {
            _driver = GetComponent<CharacterAnimDriver>();
        }

        private void Start()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (_driver == null) _driver = GetComponent<CharacterAnimDriver>();

            string id = GameData.SelectedCharacterId;
            if (entries == null || target == null) return;

            foreach (var e in entries)
            {
                if (e.id != id) continue;
                Apply(e);
                return;
            }
        }

        /// <summary>把某个角色套到玩家身上（换图 + 换控制器 + 对齐脚底 + 设移动表现）。</summary>
        private void Apply(Entry e)
        {
            if (e.sprite != null) target.sprite = e.sprite;

            // 脚底对齐：sprite 中心到脚底 = 高度的一半
            var vis = target.transform;
            float halfH = 0.5f;
            if (target.sprite != null && target.sprite.pixelsPerUnit > 0.01f)
                halfH = target.sprite.rect.height / target.sprite.pixelsPerUnit * 0.5f;
            vis.localPosition = new Vector3(vis.localPosition.x, halfH + e.visualOffsetY, vis.localPosition.z);

            if (animator != null)
            {
                if (e.controller != null)
                {
                    animator.runtimeAnimatorController = e.controller;
                    animator.enabled = true;
                }
                else
                {
                    animator.enabled = false;   // 防止动画控制器把静态图刷成别人的帧
                }
            }

            if (_driver != null)
            {
                _driver.SetFloaty(e.floaty);
                // driver 在 Awake 里记过基准位置，这里换了高度必须让它重取，
                // 否则走路小跳会把角色又拉回旧高度
                _driver.RefreshBase();
            }
        }
    }
}
