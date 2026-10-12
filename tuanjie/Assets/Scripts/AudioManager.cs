// BGM 管理：登录曲 / 场景曲，跨场景常驻，可静音
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    static AudioManager _inst;
    AudioSource loginSrc, townSrc;

    public static AudioManager Instance
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("AudioManager");
                _inst = go.AddComponent<AudioManager>();
                _inst.Init();
                DontDestroyOnLoad(go);
            }
            return _inst;
        }
    }

    public bool Muted { get; private set; }

    void Init()
    {
        loginSrc = gameObject.AddComponent<AudioSource>();
        townSrc = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { loginSrc, townSrc })
        {
            s.loop = true;
            s.volume = GameConfig.BgmVolume;
            s.playOnAwake = false;
        }
        loginSrc.clip = Resources.Load<AudioClip>("Audio/login");
        townSrc.clip = Resources.Load<AudioClip>("Audio/town");
    }

    public void PlayLogin()
    {
        if (townSrc != null && townSrc.isPlaying) townSrc.Stop();
        if (loginSrc != null && loginSrc.clip != null && !loginSrc.isPlaying) loginSrc.Play();
    }

    public void PlayTown()
    {
        if (loginSrc != null && loginSrc.isPlaying) loginSrc.Stop();
        if (townSrc != null && townSrc.clip != null && !townSrc.isPlaying) townSrc.Play();
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        float v = Muted ? 0f : GameConfig.BgmVolume;
        if (loginSrc != null) loginSrc.volume = v;
        if (townSrc != null) townSrc.volume = v;
    }
}
