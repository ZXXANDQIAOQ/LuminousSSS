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

        /// <summary>打铁音效统一出口：查开关 + 播放。</summary>
        static void PlayForge(AudioClip clip)
        {
            if (Instance == null || !Instance.SfxOn || Instance.sfxSource == null || clip == null) return;
            Instance.sfxSource.PlayOneShot(clip);
        }
    }
}
