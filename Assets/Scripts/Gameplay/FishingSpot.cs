using System.Collections;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 钓鱼点：放在池塘岸边，玩家走近按交互键（或点 HUD 按钮）开始钓鱼。
    ///
    /// 节奏（整个过程都是可被打断的协程，换场景/销毁对象时自动收尾）：
    ///   抛竿 0.55s → 等待 1.5~3.8s（水面随机）→ 咬钩 1.4s 窗口 → 窗口内再交互一次 = 上鱼
    ///
    /// 为什么咬钩要「再点一次」而不是自动上鱼：
    ///   自动上鱼等于挂机刷钱，种子钱就没意义了；一次点击的成本刚好是「留意水面」的代价，
    ///   和星露谷的手感一致（那边是看浮标下沉按一下）。
    ///
    /// 等待期间也是可交互的（Prompt 变「收竿」），点了就是主动放弃，避免玩家被锁在状态里。
    /// </summary>
    [DisallowMultipleComponent]
    public class FishingSpot : MonoBehaviour, IInteractable
    {
        [Header("节奏")]
        [Tooltip("抛竿动画时长（秒）")]
        public float castSeconds = 0.55f;

        [Tooltip("咬钩前的最短等待（秒）")]
        public float minWait = 1.5f;

        [Tooltip("咬钩前的最长等待（秒）")]
        public float maxWait = 3.8f;

        [Tooltip("咬钩窗口（秒）。这段时间内再交互一次才能上鱼，超时鱼跑")]
        public float biteWindow = 1.4f;

        [Header("产出")]
        [Tooltip("可能钓到的鱼 id，重复写就是提高权重。蓝鱼 2 : 金鱼 1")]
        public string[] fishIds = { "fish_blue", "fish_blue", "fish_gold" };

        [Tooltip("浮标相对本物体的偏移（指向水面中心）")]
        public Vector2 bobberOffset = new Vector2(0f, 1.4f);

        enum State { Idle, Casting, Waiting, Biting }

        State _state = State.Idle;
        Coroutine _routine;
        Transform _bobber;
        SpriteRenderer _bobberSr;
        Vector3 _bobberHome;

        // ------------------------------------------------------------ IInteractable

        public string Prompt
        {
            get
            {
                switch (_state)
                {
                    case State.Casting: return "抛竿中";
                    case State.Waiting: return "收竿";
                    case State.Biting: return "收杆！";
                    default: return "钓鱼";
                }
            }
        }

        /// <summary>抛竿动画那半秒不给交互，其余状态都可（等待=放弃，咬钩=上鱼）。</summary>
        public bool CanInteract => _state != State.Casting;

        public void Interact(PlayerInteractor who)
        {
            switch (_state)
            {
                case State.Idle:
                    _routine = StartCoroutine(FishRoutine());
                    break;

                case State.Waiting:
                    StopFishing("空竿… 下次看准浮标");
                    break;

                case State.Biting:
                    Catch();
                    break;
            }
        }

        // ------------------------------------------------------------ 流程

        IEnumerator FishRoutine()
        {
            _state = State.Casting;
            ShowBobber(false);

            yield return new WaitForSeconds(castSeconds);

            _state = State.Waiting;
            ShowBobber(true);
            Toast.Show("抛竿了，等浮标动");

            yield return new WaitForSeconds(Random.Range(minWait, maxWait));

            // ---- 咬钩
            _state = State.Biting;
            Toast.Show("咬钩了！快收杆");
            AudioManager.Confirm();

            float t = 0f;
            while (t < biteWindow)
            {
                t += Time.deltaTime;
                if (_bobber != null)
                {
                    // 浮标上下急跳，眼睛能抓住
                    float k = Mathf.Sin(t * 26f) * 0.12f;
                    _bobber.position = _bobberHome + new Vector3(0f, k, 0f);
                }
                yield return null;
            }

            StopFishing("鱼跑了…");
        }

        /// <summary>收到鱼：随机挑一种写进背包，然后回到空闲态。</summary>
        void Catch()
        {
            string fishId = PickFish();
            StopFishing(null);
            if (string.IsNullOrEmpty(fishId)) return;

            // 背包满了就直接放生，不硬塞（Inventory.Add 会拒绝，这里先说清楚）
            if (!Inventory.CanAdd(fishId, 1))
            {
                Toast.Show("背包满了，鱼挣脱跑了 —— 去集市卖掉一些");
                AudioManager.Click();
                return;
            }

            Inventory.Add(fishId, 1);
            Toast.Show($"钓到 {ItemDatabase.NameOf(fishId)} ×1");
            AudioManager.Confirm();
        }

        string PickFish()
        {
            if (fishIds == null || fishIds.Length == 0) return null;

            // 从随机位置开始转一圈，挑第一个物品表里真实存在的鱼 ——
            // 防止物品表改过之后钓出「不存在的东西」进背包
            int start = Random.Range(0, fishIds.Length);
            for (int i = 0; i < fishIds.Length; i++)
            {
                string id = fishIds[(start + i) % fishIds.Length];
                if (ItemDatabase.Find(id) != null) return id;
            }
            return null;
        }

        /// <summary>收尾：停协程、回 Idle、收起浮标，可选弹一句提示。</summary>
        void StopFishing(string message)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _state = State.Idle;
            ShowBobber(false);
            if (!string.IsNullOrEmpty(message)) Toast.Show(message);
        }

        // ------------------------------------------------------------ 浮标

        void ShowBobber(bool visible)
        {
            if (_bobber == null && visible) CreateBobber();
            if (_bobberSr != null) _bobberSr.enabled = visible;
            if (_bobber != null) _bobber.position = _bobberHome;
        }

        void CreateBobber()
        {
            var go = new GameObject("Bobber", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            _bobber = go.transform;
            _bobberHome = transform.position + (Vector3)bobberOffset;
            _bobber.position = _bobberHome;

            _bobberSr = go.GetComponent<SpriteRenderer>();
            _bobberSr.sprite = BuildBobberSprite();
            _bobberSr.sortingOrder = 0;      // 与水面上方的装饰同层即可
        }

        /// <summary>
        /// 程序化生成一个 6x10 的红白浮标（上红下白的小椭圆）。
        /// 不引素材文件：浮标只在运行时出现，为 60 像素专门放一张 png 不值当。
        /// </summary>
        static Sprite BuildBobberSprite()
        {
            const int w = 6, h = 10;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var white = new Color32(246, 241, 226, 255);
            var red = new Color32(198, 58, 48, 255);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                    float ny = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                    bool inside = nx * nx + ny * ny <= 1f;
                    px[y * w + x] = inside ? (ny > 0.12f ? red : white) : clear;
                }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)bobberOffset);
            Gizmos.DrawWireSphere(transform.position + (Vector3)bobberOffset, 0.2f);
        }
    }
}
