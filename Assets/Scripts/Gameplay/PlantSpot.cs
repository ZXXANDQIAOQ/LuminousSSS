using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 一块可耕种的格子：砍树把树位置变成耕地之后，由 <see cref="TreeChop"/> 在同一格生成。
    ///
    /// 三种状态，全部共用同一个交互体系（PlayerInteractor 扫 IInteractable）：
    ///   空地  → Prompt「播种」：从背包取一包种子种下去（没有种子就提示，不消耗）
    ///   生长中 → 不可交互（按钮变灰），按真实时间推进 3~4 个贴图阶段
    ///   成熟   → Prompt「收获」：作物进背包，格子回到空地，可以接着种
    ///
    /// 生长用「真实时间戳」而不是累加 deltaTime：
    ///   玩家砍完树种下去就走了，回来时已经在别处待了几分钟 —— 时间戳能算出
    ///   「离开期间也在长」，累加式计时一旦场景卸载就断了。存档写 PlayerPrefs，
    ///   重进农场作物不会消失（每格一个键，见 <see cref="PrefPrefix"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlantSpot : MonoBehaviour, IInteractable
    {
        /// <summary>每格播种状态的存档键前缀，完整键 = 前缀 + "x_y"。</summary>
        public const string PrefPrefix = "LuminousSSS.Plant.";

        [Header("格子")]
        public int cellX;
        public int cellY;

        [Tooltip("作物 sprite 的显示缩放。素材 128px @ PPU 64 = 2 格，缩到 0.55 约 1.1 格高")]
        public float visualScale = 0.55f;

        /// <summary>每个生长阶段占用的真实秒数（三段共 3 倍）。</summary>
        [Tooltip("每个生长阶段占用的真实秒数。30s 一段 → 约 1.5 分钟成熟，测试和手感都够用")]
        public float stageSeconds = 30f;

        enum State { Empty, Growing, Ripe }

        State _state = State.Empty;
        string _seedId;          // 种下去的是什么种子
        long _plantedAt;         // 播种时的 UTC 秒
        SpriteRenderer _sr;
        int _shownFrame = -1;
        float _nextTick;

        // ------------------------------------------------------------ IInteractable

        public string Prompt
        {
            get
            {
                if (_state == State.Ripe) return "收获";
                if (_state == State.Growing) return "生长中";
                return "播种";
            }
        }

        public bool CanInteract => _state != State.Growing;

        public void Interact(PlayerInteractor who)
        {
            if (_state == State.Ripe) Harvest();
            else if (_state == State.Empty) Sow();
        }

        // ------------------------------------------------------------ 创建与恢复

        /// <summary>
        /// 在指定格生成一个播种点（同名已存在则直接返回它，保证幂等）。
        /// <paramref name="sortBase"/> 是这一格原本的排序值（树用的 (地图高 - y) * 10），
        /// 作物沿用同一基准，玩家站到作物前后时的遮挡关系才和树一致。
        /// </summary>
        public static PlantSpot SpawnAt(int cellX, int cellY, int sortBase)
        {
            string name = "PlantSpot_" + cellX + "_" + cellY;
            var existing = GameObject.Find(name);
            if (existing != null) return existing.GetComponent<PlantSpot>();

            var go = new GameObject(name, typeof(BoxCollider2D), typeof(PlantSpot));
            go.transform.position = new Vector3(cellX + 0.5f, cellY + 0.5f, 0f);

            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;                 // 只做交互检测，不挡路（耕地上要能走）
            col.size = new Vector2(0.9f, 0.9f);

            var visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(go.transform, false);

            var spot = go.GetComponent<PlantSpot>();
            spot.cellX = cellX;
            spot.cellY = cellY;
            spot._sr = visual.GetComponent<SpriteRenderer>();
            spot._sr.sortingOrder = sortBase;
            spot._sr.enabled = false;

            spot.Restore();
            return spot;
        }

        string PrefKey => PrefPrefix + cellX + "_" + cellY;

        /// <summary>从存档恢复（没有存档就是空地）。</summary>
        void Restore()
        {
            _state = State.Empty;
            _seedId = null;
            _plantedAt = 0;

            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                // 格式 "seedId|播种时的 UTC 秒"
                var parts = raw.Split('|');
                if (parts.Length == 2 && ItemDatabase.Find(parts[0]) != null &&
                    long.TryParse(parts[1], out long t))
                {
                    _seedId = parts[0];
                    _plantedAt = t;
                }
            }

            if (_seedId != null) RefreshGrowthState(notify: false);
            ApplyVisual();
        }

        // ------------------------------------------------------------ 播种 / 生长 / 收获

        /// <summary>播种：挑背包里第一包有库存的种子，消耗 1 个并开始计时。</summary>
        void Sow()
        {
            var seed = PickSeed();
            if (seed == null)
            {
                Toast.Show("没有种子了 —— 去村口集市买点吧");
                return;
            }

            if (!Inventory.TryConsume(seed.id, 1)) return;

            _seedId = seed.id;
            _plantedAt = NowUnix();
            _state = State.Growing;
            SaveState();
            ApplyVisual();

            Toast.Show($"播下了 {seed.displayName}");
            AudioManager.Confirm();
        }

        /// <summary>
        /// 挑种子：优先用背包里选定的「待播种子」，没有选（或那种种子用完了）
        /// 就按物品表顺序取第一包有库存的（胡萝卜 → 萝卜 → 番茄）。
        /// </summary>
        ItemDef PickSeed()
        {
            var picked = ItemDatabase.Find(Inventory.SelectedSeedId);
            if (picked != null && picked.kind == ItemKind.Seed && Inventory.Has(picked.id))
                return picked;

            foreach (var def in ItemDatabase.Purchasable(ItemKind.Seed))
                if (Inventory.Has(def.id)) return def;
            return null;
        }

        /// <summary>收获：产物进背包，格子清空。</summary>
        void Harvest()
        {
            var seed = ItemDatabase.Find(_seedId);
            if (seed == null || string.IsNullOrEmpty(seed.seedOf))
            {
                Clear();               // 数据异常（物品表改过）时兜底清空，别卡住格子
                return;
            }

            // 背包满了就让它继续挂在格子上 —— 直接清空等于把玩家的收成凭空丢掉
            if (!Inventory.CanAdd(seed.seedOf, 1))
            {
                Toast.Show("背包满了，先去集市卖掉一些再收");
                AudioManager.Click();
                return;
            }

            Inventory.Add(seed.seedOf, 1);
            Toast.Show($"收获 {ItemDatabase.NameOf(seed.seedOf)} ×1");
            AudioManager.Confirm();
            Clear();
        }

        void Clear()
        {
            _state = State.Empty;
            _seedId = null;
            _plantedAt = 0;
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            ApplyVisual();
        }

        void Update()
        {
            if (_state != State.Growing) return;

            // 0.25s 一次就够：生长阶段以 30s 计，每帧算一遍只是白烧 CPU
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 0.25f;

            RefreshGrowthState(notify: true);
        }

        /// <summary>按经过时间重算阶段（成熟时置 Ripe 并刷新贴图）。</summary>
        void RefreshGrowthState(bool notify)
        {
            var seed = ItemDatabase.Find(_seedId);
            if (seed == null) { Clear(); return; }

            int frames = seed.growthFrames != null ? seed.growthFrames.Length : 0;
            float total = Mathf.Max(1f, (frames + 1) * stageSeconds);   // +1 = 成熟那一段

            double elapsed = NowUnix() - _plantedAt;
            bool ripe = elapsed >= total;

            var next = ripe ? State.Ripe : State.Growing;
            bool changed = next != _state;
            _state = next;

            if (changed && notify)
            {
                if (ripe)
                {
                    Toast.Show($"{seed.displayName} 成熟了，可以收获");
                    AudioManager.Confirm();
                }
                ApplyVisual();
            }
            else if (_state == State.Growing)
            {
                ApplyVisual();   // 阶段可能变了，内部会判重
            }
        }

        // ------------------------------------------------------------ 表现

        void ApplyVisual()
        {
            if (_sr == null) return;

            var seed = ItemDatabase.Find(_seedId);
            Sprite sprite = null;
            int frame = -1;

            if (_state == State.Ripe && seed != null)
            {
                // 成熟时要换成「作物」的图标，不是种子图标 ——
                // seed.icon 画的是种子袋，长成了还挂着种子袋会让人以为没收成。
                sprite = ItemDatabase.IconOf(seed.seedOf);
            }
            else if (_state == State.Growing && seed != null)
            {
                int frames = seed.growthFrames != null ? seed.growthFrames.Length : 0;
                if (frames > 0)
                {
                    double elapsed = NowUnix() - _plantedAt;
                    // 每 stageSeconds 一帧，最后一帧停到成熟
                    frame = Mathf.Clamp((int)(elapsed / stageSeconds), 0, frames - 1);
                    sprite = seed.growthFrames[frame];
                }
            }

            if (sprite == null)
            {
                _sr.enabled = false;
                _shownFrame = -1;
                return;
            }

            // 阶段没变就不重设 sprite / 位置（Update 每 0.25s 会来一次，判重避免无谓的 transform 写入）
            if (_sr.enabled && _sr.sprite == sprite && frame == _shownFrame) return;

            _sr.enabled = true;
            _sr.sprite = sprite;
            _sr.transform.localScale = new Vector3(visualScale, visualScale, 1f);

            // sprite 轴心在中心 → 按「底部贴齐格子下沿」摆，作物才像从地里长出来的
            float h = sprite.rect.height / sprite.pixelsPerUnit * visualScale;
            _sr.transform.localPosition = new Vector3(0f, h * 0.5f - 0.5f, 0f);
            _shownFrame = frame;
        }

        static long NowUnix() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        void SaveState()
        {
            PlayerPrefs.SetString(PrefKey, _seedId + "|" + _plantedAt);
            PlayerPrefs.Save();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.35f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(cellX + 0.5f, cellY + 0.5f, 0f), new Vector3(1f, 1f, 0f));
        }
    }
}
