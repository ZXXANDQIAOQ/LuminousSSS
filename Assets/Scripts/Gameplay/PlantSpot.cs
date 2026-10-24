using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 一块可耕种的格子：砍树把树位置变成耕地之后，由 <see cref="TreeChop"/> 在同一格生成。
    ///
    /// 六种状态（v0.17 扩展），全部共用同一个交互体系（PlayerInteractor 扫 IInteractable）：
    ///   空地  → Prompt「播种」：从背包取一包种子种下去
    ///   刚种  → Prompt「浇水」：刚播下，土壤湿润
    ///   生长中 → Prompt「浇水」/「施肥」：按真实时间推进，缺水会暂停
    ///   缺水  → Prompt「浇水」：土色变干，生长暂停，继续不浇会枯萎
    ///   枯萎  → Prompt「铲除」：作物死亡，铲掉才能重种
    ///   成熟  → Prompt「收获」：作物进背包（有品质/数量判定）
    ///
    /// 生长用「真实时间戳」而不是累加 deltaTime：玩家离开期间也在长。
    /// 缺水暂停生长、施肥加速 1.5 倍、金色品质随机 —— 见 <see cref="ItemDef"/> 的种植扩展字段。
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

        [Tooltip("每生长阶段占用的真实秒数（0=用 ItemDef.stageSeconds）")]
        public float stageSeconds = 30f;

        [Tooltip("枯萎所需秒数（缺水持续多久后作物死亡）")]
        public float witherSeconds = 240f;

        /// <summary>六种状态。</summary>
        enum State { Empty, Seeded, Growing, Dry, Withered, Ripe }

        State _state = State.Empty;
        string _seedId;
        long _plantedAt;
        long _lastWateredAt;
        int _fertilizerUsed;
        bool _isGolden;
        int _harvestYield;
        SpriteRenderer _sr;
        int _shownFrame = -1;
        float _nextTick;

        // ------------------------------------------------------------ IInteractable

        public string Prompt
        {
            get
            {
                switch (_state)
                {
                    case State.Ripe: return "收获";
                    case State.Withered: return "铲除";
                    case State.Dry: return "浇水";
                    case State.Seeded:
                    case State.Growing:
                        return "浇水";   // 生长中也可浇水/施肥，统一显示「浇水」
                    default: return "播种";
                }
            }
        }

        public bool CanInteract => true;   // 所有状态都可交互（播种/浇水/收获/铲除）

        public void Interact(PlayerInteractor who)
        {
            switch (_state)
            {
                case State.Ripe: Harvest(); break;
                case State.Withered: Shovel(); break;
                case State.Empty: Sow(); break;
                case State.Seeded:
                case State.Growing:
                case State.Dry:
                    Water(); break;
            }
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
            _lastWateredAt = 0;
            _fertilizerUsed = 0;
            _isGolden = false;
            _harvestYield = 0;

            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                // 格式 "seedId|plantedAt|lastWateredAt|fertilizerUsed|isGolden"
                var parts = raw.Split('|');
                if (parts.Length >= 2 && ItemDatabase.Find(parts[0]) != null &&
                    long.TryParse(parts[1], out long t))
                {
                    _seedId = parts[0];
                    _plantedAt = t;
                    if (parts.Length >= 3) long.TryParse(parts[2], out _lastWateredAt);
                    if (parts.Length >= 4) int.TryParse(parts[3], out _fertilizerUsed);
                    if (parts.Length >= 5) _isGolden = parts[4] == "1";
                }
            }

            if (_seedId != null) RefreshGrowthState(notify: false);
            ApplyVisual();
        }

        // ------------------------------------------------------------ 播种 / 浇水 / 施肥 / 收获 / 铲除

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
            _lastWateredAt = NowUnix();   // 播种时土壤是湿润的
            _fertilizerUsed = 0;
            _isGolden = false;
            _harvestYield = seed.harvestYield;
            if (seed.harvestYieldBonus > 0)
                _harvestYield += Random.Range(0, seed.harvestYieldBonus + 1);
            _state = State.Seeded;
            SaveState();
            ApplyVisual();

            Toast.Show($"播下了 {seed.displayName}");
            AudioManager.Confirm();
        }

        /// <summary>浇水：恢复湿润，解除缺水暂停。</summary>
        void Water()
        {
            if (_state == State.Withered || _state == State.Empty || _state == State.Ripe) return;

            _lastWateredAt = NowUnix();
            if (_state == State.Dry) _state = State.Growing;
            SaveState();
            ApplyVisual();

            Toast.Show("浇水完成");
            AudioManager.Click();
        }

        /// <summary>施肥：加速生长 1.5 倍（消耗肥料）。</summary>
        public void Fertilize()
        {
            if (_state != State.Growing && _state != State.Seeded) return;
            var seed = ItemDatabase.Find(_seedId);
            if (seed == null || !seed.fertilizable) return;

            if (!Inventory.TryConsume("fertilizer", 1))
            {
                Toast.Show("没有肥料了");
                return;
            }

            _fertilizerUsed++;
            SaveState();
            Toast.Show("施肥完成，生长加速");
            AudioManager.Confirm();
        }

        /// <summary>收获：产物进背包，有品质/数量判定。</summary>
        void Harvest()
        {
            var seed = ItemDatabase.Find(_seedId);
            if (seed == null || string.IsNullOrEmpty(seed.seedOf))
            {
                Clear();
                return;
            }

            int yield = _harvestYield > 0 ? _harvestYield : 1;
            string prefix = _isGolden ? "金色 " : "";

            if (!Inventory.CanAdd(seed.seedOf, yield))
            {
                Toast.Show($"背包满了，先去集市卖掉一些再收（需要 {yield} 格）");
                AudioManager.Click();
                return;
            }

            Inventory.Add(seed.seedOf, yield);
            Toast.Show($"收获 {prefix}{ItemDatabase.NameOf(seed.seedOf)} ×{yield}");
            AudioManager.Confirm();
            Clear();
        }

        /// <summary>铲除枯萎的作物。</summary>
        void Shovel()
        {
            if (_state != State.Withered) return;
            Clear();
            Toast.Show("铲除了枯萎的作物");
            AudioManager.Click();
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

        void Clear()
        {
            _state = State.Empty;
            _seedId = null;
            _plantedAt = 0;
            _lastWateredAt = 0;
            _fertilizerUsed = 0;
            _isGolden = false;
            _harvestYield = 0;
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            ApplyVisual();
        }

        void Update()
        {
            if (_state != State.Growing && _state != State.Seeded && _state != State.Dry) return;

            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 0.25f;

            RefreshGrowthState(notify: true);
        }

        /// <summary>按经过时间重算阶段（含缺水/枯萎判定）。</summary>
        void RefreshGrowthState(bool notify)
        {
            var seed = ItemDatabase.Find(_seedId);
            if (seed == null) { Clear(); return; }

            long now = NowUnix();
            double sinceWatered = now - _lastWateredAt;

            // 枯萎判定：缺水持续过久
            if (sinceWatered > witherSeconds && _state != State.Withered && _state != State.Ripe)
            {
                _state = State.Withered;
                if (notify) Toast.Show($"{seed.displayName} 枯萎了...");
                ApplyVisual();
                return;
            }

            // 缺水判定：超过 waterNeed 倍的 stageSeconds 没浇
            float stageSec = seed.stageSeconds > 0 ? seed.stageSeconds : stageSeconds;
            float dryThreshold = stageSec * Mathf.Max(1, seed.waterNeed);
            if (sinceWatered > dryThreshold && _state != State.Dry && _state != State.Ripe)
            {
                _state = State.Dry;
                if (notify) Toast.Show("土地干了，快浇水");
                ApplyVisual();
                return;
            }

            // 生长推进（缺水暂停，施肥加速）
            int frames = seed.growthFrames != null ? seed.growthFrames.Length : 0;
            float total = Mathf.Max(1f, (frames + 1) * stageSec);

            // 施肥加速 1.5 倍
            float speedMul = _fertilizerUsed > 0 ? 1.5f : 1f;
            double elapsed = (now - _plantedAt) * speedMul;

            bool ripe = elapsed >= total;
            var next = ripe ? State.Ripe : (sinceWatered <= dryThreshold ? State.Growing : State.Dry);
            bool changed = next != _state;
            _state = next;

            if (changed && notify)
            {
                if (ripe)
                {
                    // 收获品质判定
                    if (seed.goldenChance > 0 && Random.value < seed.goldenChance)
                        _isGolden = true;
                    Toast.Show($"{seed.displayName} 成熟了，可以收获");
                    AudioManager.Confirm();
                }
                ApplyVisual();
            }
            else if (_state == State.Growing || _state == State.Seeded)
            {
                ApplyVisual();
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
                sprite = ItemDatabase.IconOf(seed.seedOf);
            }
            else if ((_state == State.Growing || _state == State.Seeded || _state == State.Dry) && seed != null)
            {
                int frames = seed.growthFrames != null ? seed.growthFrames.Length : 0;
                if (frames > 0)
                {
                    float stageSec = seed.stageSeconds > 0 ? seed.stageSeconds : stageSeconds;
                    double elapsed = NowUnix() - _plantedAt;
                    frame = Mathf.Clamp((int)(elapsed / stageSec), 0, frames - 1);
                    sprite = seed.growthFrames[frame];
                }
            }

            if (sprite == null)
            {
                _sr.enabled = false;
                _shownFrame = -1;
                return;
            }

            // 状态变化也要刷新（颜色不同），不只是帧变化
            bool stateChanged = _sr.enabled && _sr.sprite == sprite && frame == _shownFrame;
            if (stateChanged && _sr.color == StateColor()) return;

            _sr.enabled = true;
            _sr.sprite = sprite;
            _sr.color = StateColor();
            _sr.transform.localScale = new Vector3(visualScale, visualScale, 1f);

            float h = sprite.rect.height / sprite.pixelsPerUnit * visualScale;
            _sr.transform.localPosition = new Vector3(0f, h * 0.5f - 0.5f, 0f);
            _shownFrame = frame;
        }

        /// <summary>按状态给作物染色（枯萎=灰棕、缺水=偏黄、金色=金黄）。</summary>
        Color StateColor()
        {
            if (_isGolden && _state == State.Ripe) return new Color(1f, 0.9f, 0.3f, 1f);
            if (_state == State.Withered) return new Color(0.55f, 0.45f, 0.35f, 1f);
            if (_state == State.Dry) return new Color(0.85f, 0.8f, 0.6f, 1f);
            return Color.white;
        }

        static long NowUnix() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        void SaveState()
        {
            PlayerPrefs.SetString(PrefKey,
                $"{_seedId}|{_plantedAt}|{_lastWateredAt}|{_fertilizerUsed}|{(_isGolden ? 1 : 0)}");
            PlayerPrefs.Save();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.35f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(cellX + 0.5f, cellY + 0.5f, 0f), new Vector3(1f, 1f, 0f));
        }
    }
}
