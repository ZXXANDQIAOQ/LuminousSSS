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
    }
}
