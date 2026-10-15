using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 设置弹窗：音乐 / 音效开关（设计文档 3.4）。
    /// 开关状态来自 AudioManager（PlayerPrefs 持久化），改动即时生效。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("面板根（初始隐藏）")]
        public GameObject panelRoot;

        [Header("开关按钮与标签")]
        public Button musicToggle;
        public Text musicStateText;
        public Button sfxToggle;
        public Text sfxStateText;
        public Button closeButton;

        [Header("开关底板（可留空，留空则退回纯色方案）")]
        [Tooltip("「开」用带黄框的选中态底板")]
        public Sprite onSprite;
        [Tooltip("「关」用普通木纹底板")]
        public Sprite offSprite;
        [Tooltip("九宫格 PPU 补偿系数，与 GameSceneBuilder.UiPpu 保持一致")]
        public float ppuMultiplier = 100f / 64f;

        private static readonly Color OnColor = new Color(0.35f, 0.62f, 0.29f);
        private static readonly Color OffColor = new Color(0.45f, 0.42f, 0.38f);

        private void Start()
        {
            if (musicToggle != null) musicToggle.onClick.AddListener(ToggleMusic);
            if (sfxToggle != null) sfxToggle.onClick.AddListener(ToggleSfx);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            Refresh();
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            AudioManager.Click();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void ToggleMusic()
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.SetMusicOn(!AudioManager.Instance.MusicOn);
            AudioManager.Click();
            Refresh();
        }

        public void ToggleSfx()
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.SetSfxOn(!AudioManager.Instance.SfxOn);
            AudioManager.Click();   // 刚打开时能听到反馈
            Refresh();
        }

        private void Refresh()
        {
            var am = AudioManager.Instance;
            bool musicOn = am == null || am.MusicOn;
            bool sfxOn = am == null || am.SfxOn;
            SetToggle(musicToggle, musicStateText, musicOn);
            SetToggle(sfxToggle, sfxStateText, sfxOn);
        }

        /// <summary>
        /// 开关视觉：优先换底板（黄框选中态=开），底板没配时退回纯色方案。
        /// 像素风底板上的木纹深浅本来就有变化，只靠 tint 区分开关不够醒目，换图更一眼可辨。
        /// </summary>
        private void SetToggle(Button btn, Text label, bool on)
        {
            if (label != null) label.text = on ? "开" : "关";
            if (btn == null) return;

            var img = btn.targetGraphic as Image;
            if (img == null) return;

            if (onSprite != null && offSprite != null)
            {
                img.sprite = on ? onSprite : offSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = ppuMultiplier;
                img.color = Color.white;
            }
            else
            {
                img.color = on ? OnColor : OffColor;
            }
        }
    }
}
