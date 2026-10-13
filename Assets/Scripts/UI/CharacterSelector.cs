using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LuminousSSS
{
    /// <summary>
    /// 登录 / 角色选择界面控制器。
    /// 交互：◀ ▶ 切换角色（立绘与名称联动）；「开始游戏」写入 PlayerPrefs 并跳转农场场景；「设置」预留。
    /// </summary>
    public class CharacterSelector : MonoBehaviour
    {
        [Header("角色列表（顺序即展示顺序）")]
        public CharacterInfo[] characters;

        [Header("界面引用")]
        public Image portraitImage;
        public Text nameLabel;
        public Button prevButton;
        public Button nextButton;
        public Button startButton;
        public Button settingsButton;

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
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);

            // 恢复了上次选择的话，直接定位过去
            string saved = GameData.SelectedCharacterId;
            _index = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null && characters[i].id == saved) { _index = i; break; }
            }
            Refresh();
        }

        public void Next() => Move(1);
        public void Prev() => Move(-1);

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
            if (portraitImage != null)
            {
                portraitImage.sprite = c.portrait;
                portraitImage.enabled = c.portrait != null;
                portraitImage.preserveAspect = true;
            }
            if (nameLabel != null) nameLabel.text = c.displayName;
        }

        public void StartGame()
        {
            var c = Current;
            if (c == null) return;
            GameData.SelectedCharacterId = c.id;
            Debug.Log($"[LuminousSSS] 选择角色 {c.id} {c.displayName}，进入 {nextSceneName}");
            if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
        }

        private void OnSettings()
        {
            // 设计文档 3.4：设置暂未实现，预留入口
            Debug.Log("[LuminousSSS] 设置界面尚未实现（设计文档 3.4）。");
        }
    }
}
