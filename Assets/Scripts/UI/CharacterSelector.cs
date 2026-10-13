using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LuminousSSS
{
    /// <summary>
    /// 登录 / 角色选择界面控制器。
    /// 交互：◀ ▶ 切换角色（待机预览与名称联动）；「开始游戏」写入 PlayerPrefs 并跳转农场场景；
    /// 「设置」打开设置弹窗（音乐 / 音效开关）。
    /// </summary>
    public class CharacterSelector : MonoBehaviour
    {
        [Header("角色列表（顺序即展示顺序）")]
        public CharacterInfo[] characters;

        [Header("界面引用")]
        public Image portraitImage;
        public CharacterPreview preview;
        public Text nameLabel;
        public Button prevButton;
        public Button nextButton;
        public Button startButton;
        public Button settingsButton;

        [Header("设置弹窗")]
        public SettingsPanel settingsPanel;

        [Header("跳转")]
        public string nextSceneName = GameData.SceneFarm;

        private int _index;

        public int Index => _index;
        public CharacterInfo Current =>
            (characters != null && characters.Length > 0) ? characters[Mathf.Clamp(_index, 0, characters.Length - 1)] : null;

        private void Start()
        {
            if (characters == null || characters.Length == 0)
            {
                Debug.LogWarning("[LuminousSSS] CharacterSelector 没有配置任何角色。");
                return;
            }

            if (prevButton != null) prevButton.onClick.AddListener(Prev);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);

            // 恢复了上次选择的话，直接定位过去
            string saved = GameData.SelectedCharacterId;
            _index = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null && characters[i].id == saved) { _index = i; break; }
            }
            Refresh();
        }

        public void Next() { AudioManager.Click(); Move(1); }
        public void Prev() { AudioManager.Click(); Move(-1); }

        private void Move(int delta)
        {
            if (characters == null || characters.Length == 0) return;
            _index = (_index + delta + characters.Length) % characters.Length;
            Refresh();
        }

        private void Refresh()
        {
            var c = Current;
            if (c == null) return;

            // 待机预览：有多帧 idle 动画就轮播（女巫），否则单张立绘 + 浮动
            Sprite[] frames = (c.idleFrames != null && c.idleFrames.Length > 0)
                ? c.idleFrames
                : (c.portrait != null ? new[] { c.portrait } : null);

            if (preview != null)
            {
                preview.SetFrames(frames);
            }
            else if (portraitImage != null)
            {
                portraitImage.sprite = frames != null ? frames[0] : null;
                portraitImage.enabled = frames != null;
                portraitImage.preserveAspect = true;
            }
            if (nameLabel != null) nameLabel.text = c.displayName;
        }

        public void StartGame()
        {
            var c = Current;
            if (c == null) return;
            AudioManager.Confirm();
            GameData.SelectedCharacterId = c.id;
            Debug.Log($"[LuminousSSS] 选择角色 {c.id} {c.displayName}，进入 {nextSceneName}");
            if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
        }

        private void OpenSettings()
        {
            AudioManager.Click();
            if (settingsPanel != null) settingsPanel.Open();
        }
    }
}
