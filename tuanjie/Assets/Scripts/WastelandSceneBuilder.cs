// 荒原主场景：地图 + 玩家 + 相机 + HUD + 摇杆（全部运行时构建）
using UnityEngine;
using UnityEngine.UI;

public class WastelandSceneBuilder : MonoBehaviour
{
    MapBuilder map;
    PlayerController player;
    InputRouter input;
    Text hintText;
    Image hintBg;
    float hintTimer = 6f;
    Text muteLabel;

    void Awake()
    {
        AndroidFullscreen.Apply();

        // —— 地图 ——
        var mapGo = new GameObject("Map");
        map = mapGo.AddComponent<MapBuilder>();
        map.Build();

        // —— UI 画布（HUD + 摇杆共用）——
        var canvas = UiFactory.CreateCanvas("GameCanvas", new Vector2(1920f, 1080f), 10);

        // —— 输入 ——
        var inputGo = new GameObject("Input");
        input = inputGo.AddComponent<InputRouter>();
        input.Init(canvas);

        // —— 玩家 ——
        var playerGo = new GameObject("Player");
        player = playerGo.AddComponent<PlayerController>();
        player.Init(map, input, PlayerPrefs.GetString("luminous_name", "无名旅人"));

        // —— 相机 ——
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            cam = camGo.GetComponent<Camera>();
        }
        var follow = cam.gameObject.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.Init(playerGo.transform);

        BuildHud(canvas);

        AudioManager.Instance.PlayTown();
    }

    void BuildHud(Canvas canvas)
    {
        var cjk = UiFactory.CjkFont;

        // 左上角：地点 + 玩家名
        var infoBg = UiFactory.CreateImage(canvas.transform, "InfoBg", new Color(0.05f, 0.04f, 0.08f, 0.55f));
        UiFactory.Anchor(UiFactory.Rect(infoBg), new Vector2(0f, 1f), new Vector2(430f, 120f), new Vector2(250f, -72f));

        var loc = UiFactory.CreateText(infoBg.transform, "Location", "荒原 · 初始之地", 34,
                                       new Color(1f, 0.77f, 0.26f), cjk, TextAnchor.MiddleLeft);
        UiFactory.Anchor(UiFactory.Rect(loc), new Vector2(0f, 1f), new Vector2(390f, 44f), new Vector2(215f, -34f));

        var who = UiFactory.CreateText(infoBg.transform, "Who", "旅人：" + player.PlayerName, 30,
                                       new Color(0.94f, 0.90f, 1f), cjk, TextAnchor.MiddleLeft);
        UiFactory.Anchor(UiFactory.Rect(who), new Vector2(0f, 1f), new Vector2(390f, 40f), new Vector2(215f, -78f));

        // 底部操作提示（6 秒后淡出）
        hintBg = UiFactory.CreateImage(canvas.transform, "HintBg", new Color(0.05f, 0.04f, 0.08f, 0.55f));
        UiFactory.Anchor(UiFactory.Rect(hintBg), new Vector2(0.5f, 0f), new Vector2(720f, 60f), new Vector2(0f, 60f));
        hintText = UiFactory.CreateText(hintBg.transform, "Hint", "WASD / 方向键移动 · 触屏拖左半屏", 28,
                                        new Color(0.73f, 0.66f, 0.88f), cjk);
        UiFactory.Stretch(UiFactory.Rect(hintText));

        // 右上角：BGM 开关
        var mute = UiFactory.CreateButton(canvas.transform, "MuteButton", new Color(0.05f, 0.04f, 0.08f, 0.75f),
                                          new Color(0.94f, 0.90f, 1f), "音乐 开", 26, cjk,
                                          new Vector2(180f, 64f), new Vector2(0f, 0f));
        UiFactory.Anchor(UiFactory.Rect(mute.image), new Vector2(1f, 1f), new Vector2(180f, 64f), new Vector2(-118f, -50f));
        if (mute.transform.childCount > 0)
        {
            var lrt = UiFactory.Rect(mute.transform.GetChild(0).GetComponent<Text>());
            UiFactory.Anchor(lrt, new Vector2(1f, 1f), new Vector2(180f, 64f), new Vector2(-118f, -50f));
        }
        muteLabel = mute.GetComponentInChildren<Text>();
        mute.onClick.AddListener(() =>
        {
            AudioManager.Instance.ToggleMute();
            if (muteLabel != null)
                muteLabel.text = AudioManager.Instance.Muted ? "音乐 关" : "音乐 开";
        });
    }

    void Update()
    {
        if (hintTimer > 0f)
        {
            hintTimer -= Time.deltaTime;
            float a = Mathf.Clamp01(hintTimer / 1.5f);
            if (hintBg != null)
            {
                var c = hintBg.color; hintBg.color = new Color(c.r, c.g, c.b, 0.55f * a);
                var t = hintText.color; hintText.color = new Color(t.r, t.g, t.b, a);
                if (hintTimer <= 0f)
                {
                    hintBg.gameObject.SetActive(false);
                    hintText.gameObject.SetActive(false);
                }
            }
        }
    }
}
