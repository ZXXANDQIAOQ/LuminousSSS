using System.Collections;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 可砍伐树木：默认 3 下砍倒。砍倒后树隐藏、原位置留下树桩、
    /// 并把 treeId 写进 PlayerPrefs —— 重进场景时树不会长回来，
    /// 玩家开垦过的田地保持暴露（开垦进度不丢）。
    ///
    /// 摆动/倒下全部用 transform 做，不依赖 Animator：
    /// 场景里的树是 SpriteRenderer 裸对象，挂 Animator 反而要多养一个控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public class TreeChop : MonoBehaviour
    {
        /// <summary>已砍树木 id 列表在 PlayerPrefs 里的键（逗号分隔的 "x_y" 坐标）。</summary>
        public const string PrefChopped = "LuminousSSS.ChoppedTrees";

        [Header("砍伐")]
        [Tooltip("砍倒所需次数。3 下 = 有反馈感的开垦节奏，1 下太轻、5 下以上砍完一片田太熬人")]
        public int chopHits = 3;

        [Tooltip("两次砍击的最小间隔（秒）。防住连点器一帧砍完，也给抖动动画留出演完的时间")]
        public float hitInterval = 0.35f;

        [Tooltip("场景生成器写入的唯一 id：tree_{x}_{y}（种植格坐标）")]
        public string treeId;

        [Header("砍倒后")]
        [Tooltip("树桩贴图。砍倒后留在原地，既是「这里砍过了」的视觉记录，也是纯装饰")]
        public Sprite stumpSprite;

        [Tooltip("砍倒后是否留树桩。留：开垦痕迹可见；不留：田地直接干净")]
        public bool leaveStump = true;

        /// <summary>树底对齐的世界 y（由场景生成器写入），换树桩贴图时重新按底部对齐用。</summary>
        [HideInInspector] public float groundY;

        /// <summary>是否已砍倒。</summary>
        public bool IsChopped { get; private set; }

        /// <summary>剩余可砍次数。</summary>
        public int HitsLeft { get; private set; }

        private SpriteRenderer _sr;
        private BoxCollider2D _col;
        private float _lastHitTime = -999f;
        private bool _shaking;

        // ------------------------------------------------------------ 已砍记录（静态）

        /// <summary>这棵树在之前的游戏进程里是否已被砍倒。</summary>
        public static bool WasChopped(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var list = PlayerPrefs.GetString(PrefChopped, "");
            return list.Length > 0 && ("," + list + ",").Contains("," + id + ",");
        }

        /// <summary>把一棵树的 id 追加进已砍列表（去重，追加前先查重，避免重复字符串变长）。</summary>
        public static void MarkChopped(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var list = PlayerPrefs.GetString(PrefChopped, "");
            if (WasChopped(id)) return;
            PlayerPrefs.SetString(PrefChopped, list.Length == 0 ? id : list + "," + id);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------ 生命周期

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            _col = GetComponent<BoxCollider2D>();
            HitsLeft = chopHits;

            // 之前砍过：直接进入砍倒态（不播抖动、不发音，进场景就是现状）
            if (WasChopped(treeId)) Fall(playFeedback: false);
        }

        /// <summary>是否可以砍（冷却中或已砍倒时不可）。</summary>
        public bool CanChop() => !IsChopped && Time.time - _lastHitTime >= hitInterval;

        /// <summary>
        /// 砍一下。返回 true = 这一下把树砍倒了。
        /// 冷却中 / 已砍倒时返回 false（调用方区分「没砍中」与「已砍倒」用
        /// <see cref="IsChopped"/>，不需要解析返回值）。
        /// </summary>
        public bool Chop()
        {
            if (!CanChop()) return false;

            _lastHitTime = Time.time;
            HitsLeft--;

            if (HitsLeft > 0)
            {
                if (!_shaking) StartCoroutine(ShakeRoutine());
                AudioManager.Instance?.PlayChop();
                return false;
            }

            Fall(playFeedback: true);
            return true;
        }

        // ------------------------------------------------------------ 表现

        /// <summary>命中抖动：绕 z 轴 ±4° + x 压扁到 0.92，0.12s 内回弹。</summary>
        private IEnumerator ShakeRoutine()
        {
            _shaking = true;
            var t = transform;
            var restRot = Quaternion.identity;
            var restScale = Vector3.one;
            const float dur = 0.12f;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                float k = 1f - elapsed / dur;              // 1 -> 0 衰减
                float angle = 4f * k * Mathf.Sin(elapsed / dur * Mathf.PI * 2f);
                t.rotation = restRot * Quaternion.Euler(0f, 0f, angle);
                t.localScale = new Vector3(1f - 0.08f * k, 1f + 0.04f * k, 1f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            t.rotation = restRot;
            t.localScale = restScale;
            _shaking = false;
        }

        /// <summary>
        /// 砍倒：去碰撞（玩家可以直接走过开垦完的地）、隐藏树、留树桩、写存档。
        /// playFeedback = false 用于 Awake 的补状态（进场景时已经砍过的树）。
        /// </summary>
        private void Fall(bool playFeedback)
        {
            if (IsChopped) return;
            IsChopped = true;
            HitsLeft = 0;

            if (_col != null) Destroy(_col);   // 砍倒的树不再挡路

            if (leaveStump && stumpSprite != null && _sr != null)
            {
                _sr.sprite = stumpSprite;
                // 树桩比树矮：sprite 是 Center 轴心，换图后要按底部重新对齐 groundY
                float h = stumpSprite.rect.height / stumpSprite.pixelsPerUnit;
                transform.position = new Vector3(transform.position.x, groundY + h * 0.5f, transform.position.z);
            }
            else if (_sr != null)
            {
                _sr.enabled = false;   // 不留桩：整棵消失
            }

            if (playFeedback) AudioManager.Instance?.PlayChop();
            MarkChopped(treeId);
        }

        private void OnDrawGizmosSelected()
        {
            // 编辑器里选中树时画一个交互范围圈，便于核对「玩家站在哪能砍到」
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, 1.6f);
        }
    }
}
