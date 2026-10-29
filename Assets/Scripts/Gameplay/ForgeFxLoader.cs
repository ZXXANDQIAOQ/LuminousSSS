using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 打铁玩法素材运行时加载器（v0.20）。
    ///
    /// 解决两个痛点：
    ///  1. AudioManager 的 clip 字段靠 Inspector 手动拖，场景里没拖就"没音效"。
    ///     这里在 Awake 时用 Resources.Load 把 sfx_hammer / sfx_break / ... 自动挂上，
    ///     若场景里没有 AudioManager 则自动补一个。
    ///  2. 像素精灵在 Assets/Resources/Forge/ 下，统一在这里 Resources.Load<Texture2D>
    ///     并 Sprite.Create，避免每个面板自己写一遍加载逻辑。
    ///
    /// 挂载方式：在登录场景或农场场景里挂一个空 GameObject，加这个组件即可。
    /// 也可以直接让 ForgePanel.Open() 首次被调用时自动 EnsureLoaded()。
    /// </summary>
    public static class ForgeFxLoader
    {
        static bool _loaded;

        // ---- 精灵缓存 ----
        public static Sprite Anvil { get; private set; }
        public static Sprite SwordCold { get; private set; }
        public static Sprite SwordHot1 { get; private set; }
        public static Sprite SwordHot2 { get; private set; }
        public static Sprite SwordHot3 { get; private set; }
        public static Sprite SwordBroken { get; private set; }
        public static Sprite[] HammerFrames { get; private set; }
        public static Sprite Spark { get; private set; }
        public static Sprite[] FlameFrames { get; private set; }
        public static Sprite Furnace { get; private set; }

        /// <summary>首次打开锻造面板时调用，幂等。</summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            LoadSprites();
            EnsureAudioManager();
        }

        static void LoadSprites()
        {
            Anvil        = LoadSprite("Forge/anvil", 64, 48, new Vector2(0.5f, 0.0f));
            SwordCold    = LoadSprite("Forge/sword_cold", 56, 12, new Vector2(0.5f, 0.5f));
            SwordHot1    = LoadSprite("Forge/sword_hot1", 56, 12, new Vector2(0.5f, 0.5f));
            SwordHot2    = LoadSprite("Forge/sword_hot2", 56, 12, new Vector2(0.5f, 0.5f));
            SwordHot3    = LoadSprite("Forge/sword_hot3", 56, 12, new Vector2(0.5f, 0.5f));
            SwordBroken  = LoadSprite("Forge/sword_broken", 56, 12, new Vector2(0.5f, 0.5f));
            Spark        = LoadSprite("Forge/spark", 8, 8, new Vector2(0.5f, 0.5f));
            Furnace      = LoadSprite("Forge/furnace", 80, 60, new Vector2(0.5f, 0.5f));

            HammerFrames = new Sprite[3];
            for (int i = 0; i < 3; i++)
                HammerFrames[i] = LoadSprite($"Forge/hammer_f{i+1}", 32, 32, new Vector2(0.5f, 0.5f));

            FlameFrames = new Sprite[4];
            for (int i = 0; i < 4; i++)
                FlameFrames[i] = LoadSprite($"Forge/flame_f{i+1}", 20, 28, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 从 Resources 加载 PNG 并转成 Sprite。
        /// 关键：filterMode = Point，避免像素图被糊成一片；
        ///       wrapMode = Clamp；pixelsPerUnit = 1（UI 里按像素尺寸显示）。
        /// </summary>
        static Sprite LoadSprite(string path, int w, int h, Vector2 pivot)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[ForgeFxLoader] 找不到 Resources/{path}.png，打铁舞台会缺素材");
                return null;
            }
            // 运行时改导入设置（对已加载纹理生效）
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(true);

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 1f, 0,
                SpriteMeshType.FullRect);
        }

        /// <summary>
        /// 确保场景里有 AudioManager，并且所有打铁 clip 都被赋值。
        /// 若 AudioManager 已存在但 clip 为空，补挂。
        /// </summary>
        static void EnsureAudioManager()
        {
            var am = AudioManager.Instance;
            if (am == null)
            {
                // 场景里没有 AudioManager，自动补一个常驻对象
                var go = new GameObject("[AudioManager]");
                var musicSrc = go.AddComponent<AudioSource>();
                var sfxSrc = go.AddComponent<AudioSource>();
                am = go.AddComponent<AudioManager>();
                am.musicSource = musicSrc;
                am.sfxSource = sfxSrc;
                Object.DontDestroyOnLoad(go);
            }

            // 补挂打铁 clip（已有的不覆盖）
            if (am.hammerClip    == null) am.hammerClip    = Resources.Load<AudioClip>("sfx_hammer");
            if (am.breakClip     == null) am.breakClip     = Resources.Load<AudioClip>("sfx_break");
            if (am.successClip   == null) am.successClip   = Resources.Load<AudioClip>("sfx_success");
            if (am.criticalClip  == null) am.criticalClip  = Resources.Load<AudioClip>("sfx_critical");
            if (am.frenzyClip    == null) am.frenzyClip    = Resources.Load<AudioClip>("sfx_frenzy");
            if (am.heatClip      == null) am.heatClip      = Resources.Load<AudioClip>("sfx_heat");
            if (am.damageClip    == null) am.damageClip    = Resources.Load<AudioClip>("sfx_damage");

            // 顺便补 bgm / click / confirm（老项目里可能没拖）
            if (am.musicSource != null && am.musicSource.clip == null && am.bgm == null)
                am.bgm = Resources.Load<AudioClip>("bgm_main");
            if (am.clickClip == null) am.clickClip = Resources.Load<AudioClip>("sfx_click");
            if (am.confirmClip == null) am.confirmClip = Resources.Load<AudioClip>("sfx_confirm");
        }
    }
}
