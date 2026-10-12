// 登录界面：星空 + 标题 + 昵称输入 + 进入按钮（全部运行时构建）
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginSceneBuilder : MonoBehaviour
{
    InputField nameInput;

    void Awake()
    {
        AndroidFullscreen.Apply();
        var canvas = UiFactory.CreateCanvas("LoginCanvas", new Vector2(1920f, 1080f));

        // —— 背景渐变 ——
        var bg = UiFactory.CreateImage(canvas.transform, "BG", Color.white);
        bg.sprite = SpriteFactory.VerticalGradient(
            new Color(0.13f, 0.09f, 0.20f),   // 顶部：深紫黑
            new Color(0.05f, 0.04f, 0.08f));  // 底部更深
        UiFactory.Stretch(UiFactory.Rect(bg));

        // —— 星空 ——
        var stars = new GameObject("Stars");
        stars.transform.SetParent(canvas.transform, false);
        UiFactory.Stretch(UiFactory.Rect(stars.AddComponent<Image>()));
        stars.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        for (int i = 0; i < 48; i++)
        {
            var s = UiFactory.CreateImage(stars.transform, "star" + i, Color.white);
            var rt = UiFactory.Rect(s);
            rt.anchorMin = rt.anchorMax = new Vector2(Random.value, Random.value);
            float size = Random.Range(3f, 7f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            s.raycastTarget = false;
            var tw = s.gameObject.AddComponent<StarTwinkle>();
            tw.phase = Random.Range(0f, Mathf.PI * 2f);
            tw.speed = Random.Range(0.7f, 1.8f);
        }

        // —— 标题 ——
        var pixel = UiFactory.PixelFont;
        var cjk = UiFactory.CjkFont;

        var title = UiFactory.CreateText(canvas.transform, "Title", "LUMINOUS", 150,
                                         new Color(1f, 0.85f, 0.25f), pixel);
        UiFactory.Anchor(UiFactory.Rect(title), new Vector2(0.5f, 0.5f), new Vector2(1600f, 200f), new Vector2(0f, 250f));

        var titleShadow = UiFactory.CreateText(title.transform, "Shadow", "LUMINOUS", 150,
                                               new Color(0.48f, 0.29f, 0.07f, 0.85f), pixel);
        UiFactory.Stretch(UiFactory.Rect(titleShadow));
        titleShadow.transform.SetAsFirstSibling();

        var subtitle = UiFactory.CreateText(canvas.transform, "Subtitle", "微 光 · 荒 原 物 语", 46,
                                            new Color(0.73f, 0.66f, 0.88f), cjk);
        UiFactory.Anchor(UiFactory.Rect(subtitle), new Vector2(0.5f, 0.5f), new Vector2(1200f, 80f), new Vector2(0f, 130f));

        // —— 面板 ——
        var panelBorder = UiFactory.CreateImage(canvas.transform, "PanelBorder", new Color(0.55f, 0.36f, 0.96f));
        UiFactory.Anchor(UiFactory.Rect(panelBorder), new Vector2(0.5f, 0.5f), new Vector2(780f, 470f), new Vector2(0f, -110f));
        var panel = UiFactory.CreateImage(panelBorder.transform, "Panel", new Color(0.18f, 0.12f, 0.24f));
        UiFactory.Stretch(UiFactory.Rect(panel), 7f, 7f, 7f, 7f);

        var label = UiFactory.CreateText(panel.transform, "Label", "旅人代号", 32,
                                         new Color(0.73f, 0.66f, 0.88f), cjk, TextAnchor.MiddleLeft);
        UiFactory.Anchor(UiFactory.Rect(label), new Vector2(0f, 1f), new Vector2(600f, 50f), new Vector2(330f, -70f));

        nameInput = UiFactory.CreateInputField(panel.transform, "NameInput", "输入你的名字…", 34, cjk,
                                               new Vector2(660f, 86f), new Vector2(0f, 10f));
        nameInput.characterLimit = 12;
        nameInput.text = PlayerPrefs.GetString("luminous_name", "");

        var btn = UiFactory.CreateButton(panel.transform, "StartButton", new Color(1f, 0.78f, 0.28f),
                                         new Color(0.17f, 0.10f, 0f), "进 入 荒 原", 40, cjk,
                                         new Vector2(660f, 96f), new Vector2(0f, -110f));
        btn.onClick.AddListener(OnStart);

        var tip = UiFactory.CreateText(panel.transform, "Tip", "v0.1 · 单机体验 · 进度保存在本机", 24,
                                       new Color(0.43f, 0.36f, 0.58f), cjk);
        UiFactory.Anchor(UiFactory.Rect(tip), new Vector2(0.5f, 0f), new Vector2(700f, 40f), new Vector2(0f, 34f));

        var version = UiFactory.CreateText(canvas.transform, "Version", "LuminousSSS", 24,
                                           new Color(0.33f, 0.27f, 0.43f), cjk, TextAnchor.LowerRight);
        UiFactory.Anchor(UiFactory.Rect(version), new Vector2(1f, 0f), new Vector2(400f, 50f), new Vector2(-220f, 40f));

        // —— 竖屏提示（Android 横屏应用，兜底）——
        var hint = UiFactory.CreateText(canvas.transform, "RotateHint", "请横屏游玩", 60,
                                        new Color(1f, 1f, 1f, 0.85f), cjk);
        UiFactory.Stretch(UiFactory.Rect(hint));
        hint.gameObject.SetActive(false);
        rotateHint = hint;

        AudioManager.Instance.PlayLogin();
    }

    Text rotateHint;

    void Update()
    {
        if (rotateHint != null)
        {
            bool portrait = Screen.height > Screen.width;
            if (rotateHint.gameObject.activeSelf != portrait) rotateHint.gameObject.SetActive(portrait);
        }
    }

    void OnStart()
    {
        string name = (nameInput != null && nameInput.text != null) ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name)) name = "无名旅人";
        PlayerPrefs.SetString("luminous_name", name);
        PlayerPrefs.Save();
        SceneManager.LoadScene("Wasteland");
    }
}
