using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 一键生成两个场景（登录界面 + 初始农场场景）。
    /// 注意：团结引擎在本机用 -batchmode 会卡在资源导入，必须由用户在编辑器里点这个菜单执行。
    /// </summary>
    public static class GameSceneBuilder
    {
        const string Art = "Assets/Art/";
        const string AudioDir = "Assets/Audio/";
        const string AnimDir = "Assets/Animations/";
        const string WitchCtrlPath = AnimDir + "WitchPlayerController.controller";
        const string WitchIdleSprite = "V2/Characters/v2_witch_idle.png";
        const string WitchIdleBobSprite = "V2/Characters/v2_witch_idle_bob.png";
        const string TilesDir = "Assets/Tiles";
        const string ScenesDir = "Assets/Scenes";

        // ------------------------------------------------------------ UI 素材
        // 登录界面全部使用 Art/V2/UI 下现成的像素 UI 素材，不再用纯色方块拼。
        const string UiRoot = "V2/UI/";
        const string UiLoginBg = UiRoot + "v2_ui_login_bg.png";              // 全屏底图（程序化生成）
        const string UiPanelTall = UiRoot + "v2_ui_dialog_panel_tall.png";   // 纵向羊皮纸面板
        const string UiDialog = UiRoot + "v2_ui_dialog_panel.png";           // 横向羊皮纸面板（弹窗用）
        const string UiBtnWood = UiRoot + "v2_ui_button_wood.png";           // 木纹按钮底（已清理残字）
        const string UiBtnOn = UiRoot + "v2_ui_button_selected.png";         // 木纹按钮·选中态（黄框）
        const string UiIconGear = UiRoot + "v2_ui_icon_gear.png";            // 齿轮
        const string UiArrowLeft = UiRoot + "v2_ui_icon_arrow_left.png";
        const string UiArrowRight = UiRoot + "v2_ui_icon_arrow_right.png";
        const string UiPortraitShadow = UiRoot + "v2_ui_portrait_shadow.png"; // 立绘脚下投影

        /// <summary>
        /// 九宫格边界的 PPU 补偿系数。
        /// UI 素材 PPU=64，Canvas.referencePixelsPerUnit=100，不补偿的话
        /// spriteBorder 会被放大 100/64≈1.5625 倍，边框变粗、像素不再是 1:1。
        /// 补偿后边框按素材原始像素显示，硬边不糊。
        /// </summary>
        const float UiPpu = 100f / 64f;

        // 界面文字色：木纹/木牌上用暖白，羊皮纸上用深棕（保证对比度）
        static readonly Color WoodTextColor = new Color32(255, 243, 208, 255);
        static readonly Color ParchmentTextColor = new Color32(74, 42, 12, 255);
        static readonly Color WoodTextOutline = new Color32(74, 38, 8, 220);

        // 农场地图（tile 单位）：40 x 24，1 tile = 1 世界单位
        const int MapW = 40;
        const int MapH = 24;
        const int WallRows = 2;              // 顶部石墙占两行
        static readonly RectInt DirtRect = new RectInt(6, 4, 22, 12);  // 中央土黄空地（x 6..27, y 4..15）
        const int PathLeft = 29;             // 土路两列（木屋门口 -> 底部出口）
        const int PathTop = 14;
        const float HouseDoorX = 30f;        // 木屋门（底部中心）的世界 x

        static Sprite S(string rel) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + rel);
        static T A<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        [MenuItem("Tools/LuminousSSS/一键生成（资源 + 场景）", false, 0)]
        public static void OneClick()
        {
            PixelArtTools.ReimportArt();
            BuildAll();
        }

        [MenuItem("Tools/LuminousSSS/2. 生成游戏场景", false, 200)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();
            EnsureTiles();
            EnsureWitchController();
            BuildLoginScene();
            BuildFarmScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(ScenesDir + "/LoginScene.scene");
            Debug.Log("[LuminousSSS] 场景生成完成：Assets/Scenes/LoginScene.scene 与 FarmScene.scene（已为你打开 LoginScene，直接点播放即可）。");
        }

        static void EnsureFolders()
        {
            foreach (var d in new[] { "Assets/Art", "Assets/Tiles", "Assets/Scripts", ScenesDir })
                if (!AssetDatabase.IsValidFolder(d))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(d).Replace('\\', '/'),
                                              System.IO.Path.GetFileName(d));
        }

        // ------------------------------------------------------------ Tile 资产
        static void EnsureTiles()
        {
            if (!AssetDatabase.IsValidFolder(TilesDir))
                AssetDatabase.CreateFolder("Assets", "Tiles");

            MakeTile("tile_grass", Art + "Tiles/tile_grass.png");
            MakeTile("tile_dirt", Art + "Tiles/tile_dirt.png");
            MakeTile("tile_path", Art + "Tiles/tile_path.png");
            MakeTile("tile_stonewall", Art + "Tiles/tile_stonewall.png");

            // V2 素材（64px / PPU 64，1 tile = 1 世界单位）
            // fill_* 是 Tools/gen_fill_tiles.py 用镜像四拼做的无缝填充块
            // （素材集原格自带描边/苗行，直接平铺会有接缝，详见该脚本注释）
            MakeTile("v2_grass_a", Art + "V2/Tiles/fill_grass_a.png"); // 草地填充
            MakeTile("v2_grass_b", Art + "V2/Tiles/fill_grass_b.png"); // 草地填充（变体）
            MakeTile("v2_dirt_a", Art + "V2/Tiles/fill_dirt_a.png");   // 土块填充
            MakeTile("v2_dirt_b", Art + "V2/Tiles/fill_dirt_b.png");   // 土块填充（变体）
            MakeTile("v2_sand", Art + "V2/Tiles/fill_sand.png");       // 浅沙土（土路）
            MakeTile("v2_cobble", Art + "V2/Tiles/tile_r06c05.png");   // 鹅卵石（石墙）
        }

        static Tile MakeTile(string name, string spritePath)
        {
            string path = $"{TilesDir}/{name}.asset";
            var tile = A<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = name;
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = S(spritePath.Substring(Art.Length));
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        // ------------------------------------------------------------ 女巫动画资产
        // 旧版 witch_idle / witch_walk_down 引用的 sprite GUID 已不在仓库中（悬空引用，
        // 播放时 SpriteRenderer 会被动画置空）。这里用 V2/Characters 下现成的素材
        // 整体重写：idle 两帧循环 + 四方向各 6 帧行走（文件名保持不变，场景里的
        // 引用不会断）。
        static void EnsureWitchAnimations()
        {
            // 待机：idle + 呼吸 bob 两帧循环
            var idleFrames = new[]
            {
                S("V2/Characters/v2_witch_idle.png"),
                S("V2/Characters/v2_witch_idle_bob.png"),
            };
            if (idleFrames[0] == null || idleFrames[1] == null)
            {
                Debug.LogWarning("[LuminousSSS] 缺少女巫待机帧（v2_witch_idle / v2_witch_idle_bob）。");
            }
            else
            {
                WriteSpriteClip(AnimDir + "witch_idle.anim", idleFrames, 2f);
            }

            // 行走：四方向各 6 帧（v2_walk_down/up/left/right_01..06）
            foreach (var dir in new[] { "down", "up", "left", "right" })
                EnsureWalkClip(dir);
        }

        static void EnsureWalkClip(string dir)
        {
            var frames = new Sprite[6];
            for (int i = 0; i < 6; i++)
            {
                frames[i] = S($"V2/Characters/v2_walk_{dir}_{i + 1:00}.png");
                if (frames[i] == null)
                {
                    Debug.LogWarning($"[LuminousSSS] 缺少行走帧: V2/Characters/v2_walk_{dir}_{i + 1:00}.png");
                    return;
                }
            }
            WriteSpriteClip(AnimDir + $"witch_walk_{dir}.anim", frames, 8f);
        }

        /// <summary>
        /// 创建（或整体重写）一个「按时间切换 SpriteRenderer.sprite」的循环动画剪辑。
        /// PPtr 曲线整体重设，旧文件里的悬空 sprite 引用会被一并清掉。
        /// </summary>
        static void WriteSpriteClip(string path, Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0) return;

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();

            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            clip.frameRate = fps;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
        }

        // ------------------------------------------------------------ 女巫动画控制器
        // 四方向状态机：Idle + WalkDown/Up/Left/Right。
        // 参数由 CharacterAnimDriver 每帧写入（PlayerController 记录的朝向是主轴
        // 单位向量，所以 MoveX/MoveY 同一时刻只有一个非零）：
        //   Speed  0/1   是否在移动
        //   MoveX -1/0/1 主轴朝向（水平优先）
        //   MoveY -1/0/1 主轴朝向
        // 旧版只有 Idle/Walk 两个状态、FacingUp 是死参数，这里幂等重建。
        static void EnsureWitchController()
        {
            EnsureWitchAnimations();

            var idle = A<AnimationClip>(AnimDir + "witch_idle.anim");
            var walkDown = A<AnimationClip>(AnimDir + "witch_walk_down.anim");
            var walkUp = A<AnimationClip>(AnimDir + "witch_walk_up.anim");
            var walkLeft = A<AnimationClip>(AnimDir + "witch_walk_left.anim");
            var walkRight = A<AnimationClip>(AnimDir + "witch_walk_right.anim");
            if (idle == null || walkDown == null || walkUp == null || walkLeft == null || walkRight == null)
            {
                Debug.LogWarning("[LuminousSSS] 行走动画剪辑不完整，跳过控制器生成。");
                return;
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(WitchCtrlPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(WitchCtrlPath);

            EnsureParam(ctrl, "Speed", AnimatorControllerParameterType.Float);
            EnsureParam(ctrl, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParam(ctrl, "MoveY", AnimatorControllerParameterType.Float);
            RemoveParam(ctrl, "FacingUp");   // 旧版遗留的死参数

            var sm = GetOrCreateStateMachine(ctrl);

            // 清掉旧版遗留的状态（旧版叫 Walk），防止状态机里留孤儿
            var keep = new HashSet<string> { "Idle", "WalkDown", "WalkUp", "WalkLeft", "WalkRight" };
            foreach (var s in sm.states)
                if (s.state != null && !keep.Contains(s.state.name))
                    sm.RemoveState(s.state);

            var idleState = FindState(sm, "Idle") ?? sm.AddState("Idle");
            idleState.motion = idle;
            idleState.transitions = new AnimatorStateTransition[0];

            var sDown = FindState(sm, "WalkDown") ?? sm.AddState("WalkDown");
            var sUp = FindState(sm, "WalkUp") ?? sm.AddState("WalkUp");
            var sLeft = FindState(sm, "WalkLeft") ?? sm.AddState("WalkLeft");
            var sRight = FindState(sm, "WalkRight") ?? sm.AddState("WalkRight");
            sDown.motion = walkDown;
            sUp.motion = walkUp;
            sLeft.motion = walkLeft;
            sRight.motion = walkRight;
            foreach (var s in new[] { sDown, sUp, sLeft, sRight })
                s.transitions = new AnimatorStateTransition[0];

            sm.defaultState = idleState;

            // (状态, 参数名, 符号)：-1 表示该参数 < -0.5，+1 表示 > 0.5
            var walks = new (AnimatorState st, string axis, int sign)[]
            {
                (sDown, "MoveY", -1),
                (sUp, "MoveY", +1),
                (sLeft, "MoveX", -1),
                (sRight, "MoveX", +1),
            };
            AnimatorConditionMode ModeOf(int sign) =>
                sign < 0 ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater;
            float ThresholdOf(int sign) => 0.5f;

            // Idle → 各方向：在走 && 朝向匹配
            foreach (var w in walks)
                AddTrans(idleState, w.st, 0.06f,
                    ("Speed", AnimatorConditionMode.Greater, 0.01f),
                    (w.axis, ModeOf(w.sign), ThresholdOf(w.sign)));

            // 各方向 → Idle：停下
            foreach (var w in walks)
                AddTrans(w.st, idleState, 0.08f, ("Speed", AnimatorConditionMode.Less, 0.01f));

            // 各方向互转：边走边换方向不停步。
            // 上面已把 5 个状态的 transitions 全部清空，这里是统一重建，重复执行不会累积。
            foreach (var from in walks)
                foreach (var to in walks)
                {
                    if (from.st == to.st) continue;
                    AddTrans(from.st, to.st, 0.06f,
                        ("Speed", AnimatorConditionMode.Greater, 0.01f),
                        (to.axis, ModeOf(to.sign), ThresholdOf(to.sign)));
                }

            EditorUtility.SetDirty(ctrl);
            Debug.Log("[LuminousSSS] 女巫 AnimatorController 已就绪（Idle + 四方向行走）：" + WitchCtrlPath);
        }

        static void AddTrans(AnimatorState from, AnimatorState to, float duration,
            params (string name, AnimatorConditionMode mode, float threshold)[] conds)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach (var c in conds)
                t.AddCondition(c.mode, c.threshold, c.name);
        }

        static AnimatorStateMachine GetOrCreateStateMachine(AnimatorController ctrl)
        {
            var layers = ctrl.layers;
            if (layers.Length > 0 && layers[0].stateMachine != null)
                return layers[0].stateMachine;

            // 控制器是空壳（手建的）时给它补一个状态机
            var sm = new AnimatorStateMachine { name = "Base Layer", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, ctrl);
            var layer = layers.Length == 0
                ? new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f }
                : layers[0];
            layer.stateMachine = sm;
            ctrl.layers = new[] { layer };
            return sm;
        }

        static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var s in sm.states)
                if (s.state != null && s.state.name == name) return s.state;
            return null;
        }

        static void EnsureParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in ctrl.parameters)
                if (p.name == name) return;
            ctrl.AddParameter(name, type);
        }

        static void RemoveParam(AnimatorController ctrl, string name)
        {
            var list = new List<AnimatorControllerParameter>(ctrl.parameters);
            if (list.RemoveAll(p => p.name == name) > 0)
                ctrl.parameters = list.ToArray();
        }

        // ------------------------------------------------------------ 全局音频
        static AudioManager EnsureAudioManager()
        {
            var go = new GameObject("AudioManager", typeof(AudioSource), typeof(AudioSource), typeof(AudioManager));
            var am = go.GetComponent<AudioManager>();
            var sources = go.GetComponents<AudioSource>();
            am.musicSource = sources[0];
            am.sfxSource = sources[1];
            am.bgm = A<AudioClip>(AudioDir + "bgm_main.wav");
            am.clickClip = A<AudioClip>(AudioDir + "sfx_click.wav");
            am.confirmClip = A<AudioClip>(AudioDir + "sfx_confirm.wav");
            return am;
        }

        // ------------------------------------------------------------ 登录场景
        // 版式（1920x1080 参考分辨率，坐标为「距屏幕左上角」的像素）：
        //   标题木牌   中心 (960, 120)      660x110
        //   主面板     中心 (960, 620)      820x840   → 占 y 200..1040
        //   立绘       中心 (960, 510)      512 高    → 对 1024 高的素材做 1:2 整数缩放
        //   切换按钮   中心 (736 / 1184, 510)  128x128
        //   角色名     中心 (960, 845)
        //   按钮行     中心 y 960，320x104，两个，各距中线 190
        static void BuildLoginScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.07f, 0.05f);
            cam.orthographic = true;
            camGO.transform.position = new Vector3(0, 0, -10);

            EnsureAudioManager();

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // 横屏游戏按「高度」适配：所有 16:9 分辨率都映射成 1920x1080 虚拟画布，
            // 更宽的屏（21:9）左右留白、更窄的屏（4:3）也左右留白，内容永远完整不裁。
            // 原来的 0.5 在 21:9 下虚拟高只剩 935，底部按钮会被裁掉。
            scaler.matchWidthOrHeight = 1.0f;
            canvasGO.AddComponent<UIFontFixer>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var root = canvasGO.transform;

            // ---- 背景：暗色渐变 + 中心辉光（Simple 全屏拉伸，任意分辨率都铺满）
            var bg = CreateUI<Image>("Background", root, new Vector2(960, 540), new Vector2(1920, 1080));
            bg.sprite = S(UiLoginBg);
            bg.type = Image.Type.Simple;
            bg.raycastTarget = false;

            // ---- 主面板：羊皮纸九宫格，其余元素全部压在它上面
            MakeSliced("MainPanel", root, UiPanelTall, new Vector2(960, 620), new Vector2(820, 840));

            // ---- 标题木牌（图上不放文字：标题交给 Text 渲染，方便换语种与换字体）
            var plate = MakeSliced("TitlePlate", root, UiBtnWood, new Vector2(960, 120), new Vector2(660, 110));
            var title = CreateUI<Text>("Title", plate.transform, new Vector2(330, 55), new Vector2(660, 110));
            Style(title, "LuminousSSS", 56, TextAnchor.MiddleCenter, WoodTextColor);
            AddOutline(title, WoodTextOutline, 2f);

            // ---- 立绘：脚下先铺一层投影，避免角色"飘"在纸上
            var shadow = CreateUI<Image>("PortraitShadow", root, new Vector2(960, 764), new Vector2(280, 70));
            shadow.sprite = S(UiPortraitShadow);
            shadow.type = Image.Type.Simple;
            shadow.raycastTarget = false;

            var portrait = CreateUI<Image>("Portrait", root, new Vector2(960, 510), new Vector2(260, 512));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var preview = portrait.gameObject.AddComponent<CharacterPreview>();
            preview.target = portrait;

            // ---- 左右切换
            var prevBtn = MakeIconButton(root, "PrevButton", UiArrowLeft, new Vector2(736, 510), new Vector2(128, 128));
            var nextBtn = MakeIconButton(root, "NextButton", UiArrowRight, new Vector2(1184, 510), new Vector2(128, 128));

            // ---- 角色名
            var nameText = CreateUI<Text>("CharacterName", root, new Vector2(960, 845), new Vector2(700, 80));
            Style(nameText, "农 夫", 44, TextAnchor.MiddleCenter, ParchmentTextColor);

            // ---- 开始（主）/ 设置（次）：同一套底板，靠 tint 与图标分主次
            var startBtn = MakeButton(root, "StartButton", "开 始 游 戏",
                                      new Vector2(770, 960), new Vector2(320, 104), Color.white);
            var settingsBtn = MakeButton(root, "SettingsButton", "设 置",
                                         new Vector2(1150, 960), new Vector2(320, 104),
                                         new Color(0.84f, 0.82f, 0.78f), UiIconGear);

            // ---- 设置弹窗（初始隐藏，绘制在最上层）
            var settingsPanel = BuildSettingsPanel(root);

            // 逻辑挂载
            var selector = canvasGO.AddComponent<CharacterSelector>();
            selector.portraitImage = portrait;
            selector.preview = preview;
            selector.nameLabel = nameText;
            selector.prevButton = prevBtn.GetComponent<Button>();
            selector.nextButton = nextBtn.GetComponent<Button>();
            selector.startButton = startBtn.GetComponent<Button>();
            selector.settingsButton = settingsBtn.GetComponent<Button>();
            selector.settingsPanel = settingsPanel;
            selector.nextSceneName = GameData.SceneFarm;
            selector.characters = new[]
            {
                NewChar("001", "农夫", "v2_portrait_001_farmer.png", "char_001_farmer.png"),
                NewChar("002", "雨女", "v2_portrait_002_rain_maiden.png", "char_002_rain_maiden.png"),
                NewChar("003", "冒险者", "v2_portrait_003_adventurer.png", "char_003_adventurer.png"),
                new CharacterInfo
                {
                    id = "004",
                    displayName = "女巫",
                    portrait = S(WitchIdleSprite),
                    worldSprite = S(WitchIdleSprite),
                    idleFrames = new[]
                    {
                        S(WitchIdleSprite),
                        S(WitchIdleBobSprite),
                        S(WitchIdleSprite),
                    },
                },
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/LoginScene.scene");
        }

        // ------------------------------------------------------------ 设置弹窗
        // 面板 900x600 居中，内部元素一律用「面板局部坐标（距面板左上角）」。
        //   标题 局部 y 100 ／ 音乐行 y 260 ／ 音效行 y 390 ／ 关闭 y 515
        static SettingsPanel BuildSettingsPanel(Transform root)
        {
            var rootGO = new GameObject("SettingsPanel", typeof(RectTransform), typeof(SettingsPanel));
            rootGO.transform.SetParent(root, false);
            var rrt = (RectTransform)rootGO.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            // 遮罩：必须吃射线，否则点击会穿透到底下的「开始游戏」
            var overlay = CreateUI<Image>("Overlay", rootGO.transform, new Vector2(960, 540), new Vector2(1920, 1080));
            overlay.color = new Color(0f, 0f, 0f, 0.62f);

            // 面板本体
            var box = MakeSliced("Box", rootGO.transform, UiDialog, new Vector2(960, 540), new Vector2(900, 600));

            var title = CreateUI<Text>("Title", box.transform, new Vector2(450, 100), new Vector2(700, 90));
            Style(title, "设 置", 48, TextAnchor.MiddleCenter, ParchmentTextColor);

            var musicLabel = CreateUI<Text>("MusicLabel", box.transform, new Vector2(190, 260), new Vector2(300, 70));
            Style(musicLabel, "音 乐", 36, TextAnchor.MiddleCenter, ParchmentTextColor);
            var musicToggle = MakeButton(box.transform, "MusicToggle", "开", new Vector2(640, 260), new Vector2(300, 88), Color.white);
            var musicText = musicToggle.transform.Find("Label").GetComponent<Text>();

            var sfxLabel = CreateUI<Text>("SfxLabel", box.transform, new Vector2(190, 390), new Vector2(300, 70));
            Style(sfxLabel, "音 效", 36, TextAnchor.MiddleCenter, ParchmentTextColor);
            var sfxToggle = MakeButton(box.transform, "SfxToggle", "开", new Vector2(640, 390), new Vector2(300, 88), Color.white);
            var sfxText = sfxToggle.transform.Find("Label").GetComponent<Text>();

            var closeBtn = MakeButton(box.transform, "CloseButton", "关 闭", new Vector2(450, 515), new Vector2(300, 100), Color.white);

            var sp = rootGO.GetComponent<SettingsPanel>();
            sp.panelRoot = rootGO;
            sp.musicToggle = musicToggle.GetComponent<Button>();
            sp.musicStateText = musicText;
            sp.sfxToggle = sfxToggle.GetComponent<Button>();
            sp.sfxStateText = sfxText;
            sp.closeButton = closeBtn.GetComponent<Button>();
            // 开 / 关换底板（黄框=开）：比单纯改颜色更一眼可辨，也是这套素材的既定用法
            sp.onSprite = S(UiBtnOn);
            sp.offSprite = S(UiBtnWood);
            sp.ppuMultiplier = UiPpu;

            rootGO.SetActive(false);
            return sp;
        }

        static CharacterInfo NewChar(string id, string label, string portraitFile, string worldFile)
            => new CharacterInfo
            {
                id = id,
                displayName = label,
                // 登录界面改用 V2 立绘：高 1024，正好能按 1:2 整数缩放显示（旧版 760 高只能做非整数缩放，会糊）
                portrait = S("V2/Portraits/" + portraitFile),
                worldSprite = S("Characters/" + worldFile),
            };

        // ------------------------------------------------------------ 农场场景
        // 用 V2 素材按参考图（Docs/Refs/v2/scene_01_farm.jpg）布局：
        // 右上红顶木屋、中央土黄空地、左侧阔叶树+石头+树桩、右侧松树、
        // 顶部石墙+灌木、土路从木屋门口拐向底部出口。
        static void BuildFarmScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.24f, 0.14f);
            camGO.transform.position = new Vector3(20f, 12f, -10f);

            EnsureAudioManager();

            // ---- 地面 Tilemap
            var gridGO = new GameObject("Grid", typeof(Grid));
            var tmGO = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            tmGO.transform.SetParent(gridGO.transform, false);
            var tm = tmGO.GetComponent<Tilemap>();
            var tmr = tmGO.GetComponent<TilemapRenderer>();
            tmr.sortingOrder = -100;

            var grassA = A<Tile>(TilesDir + "/v2_grass_a.asset");
            var grassB = A<Tile>(TilesDir + "/v2_grass_b.asset");
            var dirtA = A<Tile>(TilesDir + "/v2_dirt_a.asset");
            var dirtB = A<Tile>(TilesDir + "/v2_dirt_b.asset");
            var sand = A<Tile>(TilesDir + "/v2_sand.asset");
            var cobble = A<Tile>(TilesDir + "/v2_cobble.asset");

            // 草地铺满（多铺一圈防穿帮），两种无缝填充块按坐标哈希混铺
            for (int x = -3; x < MapW + 3; x++)
                for (int y = -3; y < MapH + 3; y++)
                {
                    int h = Mathf.Abs(x * 73856093 ^ y * 19349663) % 100;
                    tm.SetTile(new Vector3Int(x, y, 0), h < 62 ? grassA : grassB);
                }

            // 中央土黄空地（纯土块，两种交替防重复感）
            for (int x = DirtRect.xMin; x < DirtRect.xMax; x++)
                for (int y = DirtRect.yMin; y < DirtRect.yMax; y++)
                    tm.SetTile(new Vector3Int(x, y, 0), ((x + y) & 1) == 0 ? dirtA : dirtB);

            // 土路（浅沙土）：木屋门口 -> 底部出口
            for (int y = 0; y <= PathTop; y++)
            {
                tm.SetTile(new Vector3Int(PathLeft, y, 0), sand);
                tm.SetTile(new Vector3Int(PathLeft + 1, y, 0), sand);
            }

            // 顶部石墙（鹅卵石，两行）
            for (int x = -3; x < MapW + 3; x++)
                for (int r = 0; r < WallRows; r++)
                    tm.SetTile(new Vector3Int(x, MapH - 1 - r, 0), cobble);

            // ---- 物件（collider 给出底部碰撞盒的宽 x 高；无则纯装饰）
            var props = new GameObject("Props").transform;

            // 红顶木屋（右上，底部中心即门口，对准土路顶端）
            PlaceProp(props, "V2/Buildings/v2_building_house_main.png", HouseDoorX, 14f, new Vector2(7.6f, 1.6f));

            // 木屋旁：木箱 + 木桶
            PlaceProp(props, "V2/Nature/v2_nature_crate.png", 24.4f, 14.5f, new Vector2(1.1f, 1.0f));
            PlaceProp(props, "V2/Nature/v2_nature_barrel.png", 25.3f, 13.2f, new Vector2(1.1f, 1.0f));

            // 顶部石墙前的灌木带
            foreach (float bx in new[] { 4f, 8f, 12f, 16f, 20f, 24f, 36f })
                PlaceProp(props, "V2/Nature/v2_nature_bush.png", bx, MapH - WallRows - 0.5f, new Vector2(1.6f, 0.9f));

            // 左侧：阔叶树 / 石头 / 树桩
            foreach (var p in new[] { (2.0f, 6.0f), (2.4f, 10.5f), (3.0f, 15.5f), (1.8f, 19.2f) })
                PlaceProp(props, "V2/Nature/v2_nature_tree_broadleaf.png", p.Item1, p.Item2, new Vector2(1.0f, 0.8f));
            PlaceProp(props, "V2/Nature/v2_nature_rock.png", 3.6f, 8.4f, new Vector2(1.5f, 0.9f));
            PlaceProp(props, "V2/Nature/v2_nature_rock.png", 3.0f, 12.8f, new Vector2(1.5f, 0.9f));
            PlaceProp(props, "V2/Nature/v2_nature_stump.png", 5.0f, 10.0f, new Vector2(1.5f, 0.8f));
            PlaceProp(props, "V2/Nature/v2_nature_stump.png", 4.4f, 18.0f, new Vector2(1.5f, 0.8f));

            // 右侧：松树
            foreach (var p in new[] { (37.2f, 5.0f), (37.8f, 11.0f), (37.2f, 17.5f) })
                PlaceProp(props, "V2/Nature/v2_nature_tree_pine.png", p.Item1, p.Item2, new Vector2(0.9f, 0.7f));

            // 底部：木头 / 石头 / 树桩
            PlaceProp(props, "V2/Nature/v2_nature_log.png", 9.0f, 2.4f, new Vector2(1.5f, 0.6f));
            PlaceProp(props, "V2/Nature/v2_nature_rock.png", 13.6f, 2.6f, new Vector2(1.5f, 0.9f));
            PlaceProp(props, "V2/Nature/v2_nature_stump.png", 17.0f, 2.4f, new Vector2(1.5f, 0.8f));
            PlaceProp(props, "V2/Nature/v2_nature_rock.png", 21.5f, 2.8f, new Vector2(1.5f, 0.9f));
            PlaceProp(props, "V2/Nature/v2_nature_stump.png", 24.5f, 2.5f, new Vector2(1.5f, 0.8f));

            // 土路底部出口旁的木牌
            PlaceProp(props, "V2/Nature/v2_nature_sign.png", 31.4f, 1.4f, new Vector2(0.7f, 0.6f));

            // 纯装饰：草丛 + 野花（避开空地和土路）
            foreach (var p in new[] {
                (4.5f, 7.0f), (5.0f, 12.5f), (33.5f, 6.0f), (35.0f, 17.0f),
                (10.0f, 2.2f), (20.0f, 2.6f), (31.5f, 8.5f), (36.5f, 3.5f) })
                PlaceProp(props, "V2/Nature/v2_nature_grass_tuft.png", p.Item1, p.Item2, null);
            PlaceProp(props, "V2/Nature/v2_nature_flower_pink.png", 4.0f, 9.5f, null);
            PlaceProp(props, "V2/Nature/v2_nature_flower_white.png", 5.2f, 16.5f, null);
            PlaceProp(props, "V2/Nature/v2_nature_flower_yellow.png", 34.0f, 10.0f, null);
            PlaceProp(props, "V2/Nature/v2_nature_flower_pink.png", 32.5f, 16.5f, null);
            PlaceProp(props, "V2/Nature/v2_nature_flower_white.png", 12.0f, 2.8f, null);
            PlaceProp(props, "V2/Nature/v2_nature_flower_yellow.png", 37.0f, 8.0f, null);

            // ---- 地图边界碰撞（设计文档 4.5）
            var bounds = new GameObject("Bounds").transform;
            MakeWall(bounds, "WallTop", new Vector2(MapW / 2f, MapH - WallRows + 0.5f), new Vector2(MapW + 4, WallRows));
            MakeWall(bounds, "WallBottom", new Vector2(MapW / 2f, -0.5f), new Vector2(MapW + 4, 1));
            MakeWall(bounds, "WallLeft", new Vector2(-0.5f, MapH / 2f), new Vector2(1, MapH + 4));
            MakeWall(bounds, "WallRight", new Vector2(MapW + 0.5f, MapH / 2f), new Vector2(1, MapH + 4));

            // ---- 玩家（出生在木屋门口土路上，面向下方）
            var player = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                                        typeof(PlayerController), typeof(PlayerAppearance),
                                        typeof(YSort), typeof(CharacterAnimDriver));
            player.transform.position = new Vector3(HouseDoorX, 12.5f, 0f);

            var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var sr = visual.GetComponent<SpriteRenderer>();
            sr.sprite = S("Characters/char_001_farmer.png");
            sr.sortingOrder = 10;

            // 动画：默认静态角色先停用，选了女巫时由 PlayerAppearance 启用
            var anim = visual.GetComponent<Animator>();
            var witchCtrl = A<RuntimeAnimatorController>(WitchCtrlPath);
            anim.runtimeAnimatorController = witchCtrl;
            anim.enabled = false;

            var cc = player.GetComponent<CircleCollider2D>();
            cc.radius = 0.32f;
            cc.offset = new Vector2(0f, 0.45f);

            // CharacterAnimDriver 自己会在子物体里找 SpriteRenderer / Animator / PlayerController：
            // 女巫（有 controller）走四方向动画；静态角色走 flipX + 走路小跳。
            var driver = player.GetComponent<CharacterAnimDriver>();
            driver.bobHeight = 0.06f;   // 约 4 像素（V2 素材 64 PPU）
            driver.bobFrequency = 8f;

            var pa = player.GetComponent<PlayerAppearance>();
            pa.target = sr;
            pa.animator = anim;
            pa.entries = new[]
            {
                new PlayerAppearance.Entry { id = "001", sprite = S("Characters/char_001_farmer.png") },
                new PlayerAppearance.Entry { id = "002", sprite = S("Characters/char_002_rain_maiden.png") },
                new PlayerAppearance.Entry { id = "003", sprite = S("Characters/char_003_adventurer.png") },
                new PlayerAppearance.Entry { id = "004", sprite = S(WitchIdleSprite), controller = witchCtrl },
            };

            // Y 轴动态排序（与物件烘焙公式一致：MapH * 10 - y * 10）
            var ys = player.GetComponent<YSort>();
            ys.target = sr;
            ys.multiplier = 10f;
            ys.offset = MapH * 10;

            var follow = camGO.GetComponent<CameraFollow>();
            follow.target = player.transform;
            follow.useBounds = true;
            follow.min = new Vector2(0f, 0f);
            follow.max = new Vector2(MapW, MapH);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/FarmScene.scene");
        }

        /// <summary>
        /// 摆一个物件：sprite 按「底部对齐到 groundY」放置，排序按 groundY 烘焙
        /// （与玩家的 YSort 公式一致）。collider 非空时加一个底部碰撞盒（宽 x 高）。
        /// </summary>
        static void PlaceProp(Transform parent, string rel, float tileX, float groundY, Vector2? collider)
        {
            var sp = S(rel);
            if (sp == null) { Debug.LogWarning("[LuminousSSS] 缺少素材: " + rel); return; }
            var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(rel), typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sp;
            // sprite 是 Center 轴心，这里按「底部对齐到 groundY」摆放
            float h = sp.rect.height / sp.pixelsPerUnit;
            go.transform.position = new Vector3(tileX, groundY + h * 0.5f, 0f);
            // 越靠下越晚渲染，避免互相穿插时穿帮
            sr.sortingOrder = Mathf.RoundToInt((MapH - groundY) * 10f);

            if (collider.HasValue)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = collider.Value;
                box.offset = new Vector2(0f, -h * 0.5f + collider.Value.y * 0.5f);
            }
        }

        static void MakeWall(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name, typeof(BoxCollider2D));
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.GetComponent<BoxCollider2D>().size = size;
        }

        // ------------------------------------------------------------ UI 小工具
        static T CreateUI<T>(string name, Transform parent, Vector2 centerPos, Vector2 size)
            where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 以左上角为参考
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(centerPos.x, -centerPos.y);
            return go.GetComponent<T>();
        }

        static void Style(Text t, string content, int size, TextAnchor anchor, Color color)
        {
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }

        /// <summary>给文字加一圈描边，保证叠在木纹/羊皮纸上都读得清。</summary>
        static void AddOutline(Graphic g, Color color, float dist)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
        }

        /// <summary>
        /// 九宫格底板。像素 UI 素材必须走 Sliced，否则每种按钮宽度都得单独切一张图。
        /// pixelsPerUnitMultiplier 做 PPU 补偿（素材 64 vs Canvas 100），让边框按素材原始像素 1:1 显示。
        /// </summary>
        static Image MakeSliced(string name, Transform parent, string spriteRel, Vector2 centerPos, Vector2 size)
        {
            var img = CreateUI<Image>(name, parent, centerPos, size);
            img.sprite = S(spriteRel);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiPpu;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// 按钮四态：常态 / 悬停 / 按下 / 禁用。
        /// 缺态是 UI 上线后最常见的返工点，这里一次性给全。
        /// 颜色是「乘」到底板上的，所以主/次按钮共用一套，靠 Image.color 区分层级。
        /// </summary>
        static ColorBlock MakeButtonColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1f, 0.97f, 0.88f);
            c.pressedColor = new Color(0.76f, 0.74f, 0.68f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(0.50f, 0.50f, 0.50f, 0.50f);
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            return c;
        }

        static GameObject MakeButton(Transform parent, string name, string label, Vector2 centerPos, Vector2 size,
                                     Color tint, string iconRel = null)
        {
            var img = MakeSliced(name, parent, UiBtnWood, centerPos, size);
            img.raycastTarget = true;
            img.color = tint;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = MakeButtonColors();

            var txt = CreateUI<Text>("Label", img.transform, new Vector2(size.x * 0.5f, size.y * 0.5f), size);
            Style(txt, label, 36, TextAnchor.MiddleCenter, WoodTextColor);
            AddOutline(txt, WoodTextOutline, 2f);

            if (!string.IsNullOrEmpty(iconRel))
            {
                // 「图标 + 文字」整组居中：先估文字宽度，再把两者一起摆正
                const float iconSize = 46f, gap = 20f;
                float textW = EstimateTextWidth(label, 36);
                float left = size.x * 0.5f - (iconSize + gap + textW) * 0.5f;

                var icon = CreateUI<Image>("Icon", img.transform,
                                           new Vector2(left + iconSize * 0.5f, size.y * 0.5f),
                                           new Vector2(iconSize, iconSize));
                icon.sprite = S(iconRel);
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var trt = (RectTransform)txt.transform;
                trt.anchoredPosition = new Vector2(left + iconSize + gap + textW * 0.5f, -size.y * 0.5f);
            }
            return img.gameObject;
        }

        /// <summary>只有图标、没有文字的按钮（左右切换角色）。</summary>
        static GameObject MakeIconButton(Transform parent, string name, string iconRel, Vector2 centerPos, Vector2 size)
        {
            var img = MakeSliced(name, parent, UiBtnWood, centerPos, size);
            img.raycastTarget = true;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = MakeButtonColors();

            var icon = CreateUI<Image>("Icon", img.transform, new Vector2(size.x * 0.5f, size.y * 0.5f), size * 0.5f);
            icon.sprite = S(iconRel);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return img.gameObject;
        }

        /// <summary>粗估一行文字的像素宽度：中日韩全角算 1 个字号，其余算 0.55 个。</summary>
        static float EstimateTextWidth(string s, int fontSize)
        {
            float w = 0f;
            foreach (char ch in s)
                w += (ch > 0x2E80) ? fontSize : fontSize * 0.55f;
            return w;
        }

        // ------------------------------------------------------------ Build Settings
        static void RegisterBuildScenes()
        {
            var wanted = new[] { ScenesDir + "/LoginScene.scene", ScenesDir + "/FarmScene.scene" };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var w in wanted)
                if (System.IO.File.Exists(w))
                    list.Add(new EditorBuildSettingsScene(w, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (!wanted.Contains(s.path))
                    list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
