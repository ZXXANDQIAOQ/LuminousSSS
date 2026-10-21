using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LuminousSSS
{
    /// <summary>
    /// 可砍伐树木：默认 3 下砍倒。砍倒后树消失，**原位直接铺一块耕地瓦片**
    /// （不留树桩），并把 treeId 写进 PlayerPrefs —— 重进场景时树不会长回来、
    /// 耕地也照原样铺回去，开垦进度不丢。
    ///
    /// 为什么不留树桩：树桩只是「这里砍过了」的标记，但开垦玩法真正要的是
    /// 「这里变成田了」。耕地瓦片本身既是进度记录又是可耕种的地面，
    /// 比树桩信息量更大，也不会和周围地块格格不入。
    ///
    /// 摆动/倒下全部用 transform 做，不依赖 Animator：
    /// 场景里的树是 SpriteRenderer 裸对象，挂 Animator 反而要多养一个控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public class TreeChop : MonoBehaviour, IInteractable
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

        [Header("砍倒后变成耕地")]
        [Tooltip("耕地层（Farm 场景里 sortingOrder=-90 的 Tilemap，在 Ground 之上、物件之下）")]
        public Tilemap farmland;

        [Tooltip("主耕地瓦片（v2_tilled_a）")]
        public Tile tilledTile;

        [Tooltip("耕地瓦片变体（v2_tilled_b）；按格子坐标奇偶混铺，避免整片纹理重复")]
        public Tile tilledTileAlt;

        [Tooltip("树所在的种植格坐标（场景生成器写入；不是世界坐标）")]
        public int cellX;

        [Tooltip("树所在的种植格坐标（场景生成器写入；不是世界坐标）")]
        public int cellY;

        [Tooltip("砍倒后在原位生成播种点：耕地不只是背景，可以直接播种")]
        public bool spawnPlantSpot = true;

        /// <summary>是否已砍倒。</summary>
        public bool IsChopped { get; private set; }

        /// <summary>剩余可砍次数。</summary>
        public int HitsLeft { get; private set; }

        // ------------------------------------------------------------ IInteractable
        // 砍树走的是与门/楼梯同一套交互体系（PlayerInteractor 扫 IInteractable）。
        // 冷却（hitInterval）也算进 CanInteract：砍击动画期间按钮灰一下，
        // 玩家能看出「这一下已经在砍了」，不会以为丢输入。

        public string Prompt => "砍伐";

        public bool CanInteract => !IsChopped && CanChop();

        public void Interact(PlayerInteractor who) => Chop();

        private SpriteRenderer _sr;
        private BoxCollider2D _col;
        private float _lastHitTime = -999f;
        private bool _shaking;

        /// <summary>原树的排序基准（(地图高 - y) * 10）。播种点沿用同一个值，遮挡关系与树一致。</summary>
        private int _baseOrder;

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
            if (_sr != null) _baseOrder = _sr.sortingOrder;

            // 之前砍过：直接进入砍倒态（不播抖动、不发音，进场景就是现状）
            if (WasChopped(treeId)) Fall(playFeedback: false);
        }

        private void Start()
        {
            // 兜底：Awake 阶段 Tilemap 未必已经启用完，已砍倒的树在这里再铺一次。
            // PaintFarmland / SpawnPlantSpot 都是幂等的（SetTile 重设同值、同名物体直接复用）。
            if (IsChopped)
            {
                PaintFarmland();
                SpawnPlantSpot();
            }
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
                AudioManager.PlayChop();   // 静态入口，内部自带 Instance 判空
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
        /// 砍倒：去碰撞（玩家可以直接走过开垦完的地）、隐藏树、把原位铺成耕地、写存档。
        /// playFeedback = false 用于 Awake 的补状态（进场景时已经砍过的树）。
        /// </summary>
        private void Fall(bool playFeedback)
        {
            if (IsChopped) return;
            IsChopped = true;
            HitsLeft = 0;

            if (_col != null) Destroy(_col);       // 砍倒的树不再挡路
            if (_sr != null) _sr.enabled = false;  // 整棵消失（不留树桩，原位换成耕地）

            PaintFarmland();
            SpawnPlantSpot();

            if (playFeedback) AudioManager.PlayChop();   // 静态入口，内部自带 Instance 判空
            MarkChopped(treeId);
        }

        /// <summary>
        /// 把树所在的这一格铺成耕地。
        ///
        /// 幂等：重复调用只是重设同一个瓦片，Awake / Start 各调一次也不会出问题。
        /// 变体按格子坐标奇偶挑选，相邻格纹理不同，成片开垦时不会看出重复。
        /// farmland / tilledTile 缺失时静默跳过（老场景没有这两个引用也能正常跑，
        /// 只是砍完树那块保持原土色）。
        /// </summary>
        private void PaintFarmland()
        {
            if (farmland == null || tilledTile == null) return;

            var pick = (((cellX + cellY) & 1) == 0 || tilledTileAlt == null)
                       ? tilledTile
                       : tilledTileAlt;
            farmland.SetTile(new Vector3Int(cellX, cellY, 0), pick);
        }

        /// <summary>
        /// 在耕地这一格生成播种点：耕地是"能种的地"而不只是变色地面，
        /// 玩家砍完树就能原地播种（播种 → 生长 → 收获 → 拿去集市卖）。
        /// </summary>
        private void SpawnPlantSpot()
        {
            if (!spawnPlantSpot) return;
            PlantSpot.SpawnAt(cellX, cellY, _baseOrder);
        }

        private void OnDrawGizmosSelected()
        {
            // 编辑器里选中树时画一个交互范围圈，便于核对「玩家站在哪能砍到」
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, 1.6f);
        }
    }
}
