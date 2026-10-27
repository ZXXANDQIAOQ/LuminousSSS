using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 全局音频管理：BGM 循环 + 音效单发，跨场景常驻。
    /// 「音乐 / 音效」开关写入 PlayerPrefs（设计文档 3.4 设置项）。
    /// 场景里各放一个（登录 + 农场），单例去重：从登录场景进入农场时保留登录场景那个。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public const string PrefMusicOn = "LuminousSSS.MusicOn";
        public const string PrefSfxOn = "LuminousSSS.SfxOn";

        [Header("音源")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("音频剪辑")]
        public AudioClip bgm;
        public AudioClip clickClip;
        public AudioClip confirmClip;
        public AudioClip chopClip;   // 砍树命中（程序化合成，gen_audio.py）

        [Header("打铁音效（ForgeTax 风格）")]
        public AudioClip hammerClip;    // 锤击铁砧
        public AudioClip breakClip;     // 剑碎
        public AudioClip successClip;   // 锤击成功
        public AudioClip criticalClip;  // 大成功
        public AudioClip frenzyClip;    // 入神激活
        public AudioClip heatClip;      // 入炉加热
        public AudioClip damageClip;    // 锤击损伤

        [Header("钓鱼音效（v0.21）")]
        public AudioClip splashClip;    // 浮标入水 / 水花（gen_audio.py 生成）
        public AudioClip biteClip;      // 咬钩提示音：比 confirm 更短更「叮」，要能从 BGM 里跳出来

        public bool MusicOn { get; private set; }
        public bool SfxOn { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            MusicOn = PlayerPrefs.GetInt(PrefMusicOn, 1) == 1;
            SfxOn = PlayerPrefs.GetInt(PrefSfxOn, 1) == 1;

            if (musicSource == null || sfxSource == null)
            {
                var sources = GetComponents<AudioSource>();
                if (musicSource == null && sources.Length > 0) musicSource = sources[0];
                if (sfxSource == null && sources.Length > 1) sfxSource = sources[1];
            }
            if (musicSource != null)
            {
                if (musicSource.clip == null && bgm != null) musicSource.clip = bgm;
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            ApplyMusicState();
        }

        public void SetMusicOn(bool on)
        {
            MusicOn = on;
            PlayerPrefs.SetInt(PrefMusicOn, on ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMusicState();
        }

        public void SetSfxOn(bool on)
        {
            SfxOn = on;
            PlayerPrefs.SetInt(PrefSfxOn, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyMusicState()
        {
            if (musicSource == null) return;
            if (MusicOn && musicSource.clip != null)
            {
                if (!musicSource.isPlaying) musicSource.Play();
            }
            else
            {
                musicSource.Stop();
            }
        }

        /// <summary>普通按钮音（设置项切换、角色切换）。</summary>
        public static void Click()
        {
            if (Instance != null && Instance.SfxOn && Instance.sfxSource != null && Instance.clickClip != null)
                Instance.sfxSource.PlayOneShot(Instance.clickClip);
        }

        /// <summary>「开始游戏」确认音。</summary>
        public static void Confirm()
        {
            if (Instance != null && Instance.SfxOn && Instance.sfxSource != null && Instance.confirmClip != null)
                Instance.sfxSource.PlayOneShot(Instance.confirmClip);
        }

        /// <summary>砍树命中音。没有 chopClip 时退回 clickClip，保证老场景也有反馈。</summary>
        public static void PlayChop()
        {
            if (Instance == null || !Instance.SfxOn || Instance.sfxSource == null) return;
            var clip = Instance.chopClip != null ? Instance.chopClip : Instance.clickClip;
            if (clip != null) Instance.sfxSource.PlayOneShot(clip);
        }

        // ---- 打铁音效（ForgeTax 风格）v0.19 ----

        /// <summary>锤击铁砧「铛」。</summary>
        public static void Hammer()
        {
            PlayForge(Instance?.hammerClip);
        }

        /// <summary>剑碎。</summary>
        public static void Break()
        {
            PlayForge(Instance?.breakClip);
        }

        /// <summary>锤击成功（上行铃）。</summary>
        public static void Success()
        {
            PlayForge(Instance?.successClip);
        }

        /// <summary>大成功（琶音爆发）。</summary>
        public static void Critical()
        {
            PlayForge(Instance?.criticalClip);
        }

        /// <summary>入神激活（上升扫频）。</summary>
        public static void Frenzy()
        {
            PlayForge(Instance?.frenzyClip);
        }

        /// <summary>入炉加热（火焰呼啸）。</summary>
        public static void Heat()
        {
            PlayForge(Instance?.heatClip);
        }

        /// <summary>锤击损伤（闷响下行）。</summary>
        public static void Damage()
        {
            PlayForge(Instance?.damageClip);
        }

        // ---- 钓鱼音效（v0.21）

        /// <summary>
        /// 浮标入水 / 水花。缺 splashClip 时退回 clickClip —— 老场景（没重跑一键生成）
        /// 至少还有一声响，不至于"抛竿完全没反馈"。
        /// </summary>
        public static void PlaySplash()
        {
            if (Instance == null) return;
            var clip = Instance.splashClip != null ? Instance.splashClip : Instance.clickClip;
            PlaySfx(clip);
        }

        /// <summary>
        /// 咬钩「叮」。这是玩家判断「真咬钩 vs 假咬钩」最重要的线索（假咬钩故意不出声），
        /// 所以缺失时退回 confirmClip —— 两个都是短促上行的提示音，语义对得上。
        /// </summary>
        public static void PlayBite()
        {
            if (Instance == null) return;
            var clip = Instance.biteClip != null ? Instance.biteClip : Instance.confirmClip;
            PlaySfx(clip);
        }

        /// <summary>打铁音效统一出口：查开关 + 播放。</summary>
        static void PlayForge(AudioClip clip)
        {
            PlaySfx(clip);
        }

        /// <summary>所有音效的统一出口：开关 / 音源 / 剪辑三者任一缺失就静默跳过。</summary>
        static void PlaySfx(AudioClip clip)
        {
            if (Instance == null || !Instance.SfxOn || Instance.sfxSource == null || clip == null) return;
            Instance.sfxSource.PlayOneShot(clip);
        }
    }
}
