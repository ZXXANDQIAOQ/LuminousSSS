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

        // ------------------------------------------------------------ 女巫动画控制器
        // 用户在编辑器里手建的 WitchPlayerController.controller 可能只有参数、没有状态机，
        // 那样 Play 模式不会播任何动画。这里用编辑器 API 原地修复（保持资产 guid 不变，
        // SampleScene 里的引用不会断）。
        static void EnsureWitchController()
        {
            var idle = A<AnimationClip>(AnimDir + "witch_idle.anim");
            var walk = A<AnimationClip>(AnimDir + "witch_walk_down.anim");
            if (idle == null || walk == null)
            {
                Debug.LogWarning("[LuminousSSS] 未找到女巫动画剪辑（witch_idle / witch_walk_down），跳过控制器生成。");
                return;
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(WitchCtrlPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(WitchCtrlPath);

            EnsureParam(ctrl, "Speed", AnimatorControllerParameterType.Float);
            EnsureParam(ctrl, "FacingUp", AnimatorControllerParameterType.Bool);

            // 拿到底层状态机；控制器是空壳（手建的）时给它补一个
            var layers = ctrl.layers;
            AnimatorStateMachine sm;
            if (layers.Length == 0 || layers[0].stateMachine == null)
            {
                sm = new AnimatorStateMachine { name = "Base Layer", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(sm, ctrl);
                var layer = layers.Length == 0
                    ? new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f }
                    : layers[0];
                layer.stateMachine = sm;
                ctrl.layers = new[] { layer };
            }
            else
            {
                sm = layers[0].stateMachine;
            }

            // 幂等：复用同名状态、清掉旧过渡后重建
            var idleState = FindState(sm, "Idle") ?? sm.AddState("Idle");
            idleState.motion = idle;
            idleState.transitions = new AnimatorStateTransition[0];

            var walkState = FindState(sm, "Walk") ?? sm.AddState("Walk");
            walkState.motion = walk;
            walkState.transitions = new AnimatorStateTransition[0];

            sm.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.08f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.08f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");

            EditorUtility.SetDirty(ctrl);
            Debug.Log("[LuminousSSS] 女巫 AnimatorController 已就绪：" + WitchCtrlPath);
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
        static void BuildLoginScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.145f, 0.125f, 0.11f);
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
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<UIFontFixer>();

            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var root = canvasGO.transform;

            // 背景底色
            var bg = CreateUI<Image>("Background", root, new Vector2(960, 540), new Vector2(1920, 1080));
            bg.color = new Color(0.176f, 0.152f, 0.129f);

            // 顶部装饰条
            var bar = CreateUI<Image>("TopBar", root, new Vector2(960, 1000), new Vector2(1920, 160));
            bar.color = new Color(0.278f, 0.216f, 0.145f);

            // 标题
            var title = CreateUI<Text>("Title", root, new Vector2(960, 960), new Vector2(1200, 140));
            Style(title, "LuminousSSS", 96, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.75f));

            // 角色待机预览（多帧角色轮播 idle，单帧角色上下浮动）
            var portrait = CreateUI<Image>("Portrait", root, new Vector2(960, 560), new Vector2(560, 620));
            portrait.preserveAspect = true;
            var preview = portrait.gameObject.AddComponent<CharacterPreview>();
            preview.target = portrait;

            // ◀ ▶
            var prevBtn = MakeButton(root, "PrevButton", "◀", new Vector2(340, 560), new Vector2(120, 120));
            var nextBtn = MakeButton(root, "NextButton", "▶", new Vector2(1580, 560), new Vector2(120, 120));

            // 角色名
            var nameText = CreateUI<Text>("CharacterName", root, new Vector2(960, 190), new Vector2(800, 90));
            Style(nameText, "农夫", 60, TextAnchor.MiddleCenter, new Color(1f, 0.96f, 0.88f));

            // 开始 / 设置
            var startBtn = MakeButton(root, "StartButton", "开 始 游 戏", new Vector2(780, 90), new Vector2(340, 96));
            startBtn.GetComponent<Image>().color = new Color(0.35f, 0.62f, 0.29f);
            var settingsBtn = MakeButton(root, "SettingsButton", "设 置", new Vector2(1140, 90), new Vector2(340, 96));
            settingsBtn.GetComponent<Image>().color = new Color(0.42f, 0.35f, 0.28f);

            // 设置弹窗（初始隐藏，绘制在最上层）
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
                NewChar("001", "农夫", "portrait_001_farmer.png", "char_001_farmer.png"),
                NewChar("002", "雨女", "portrait_002_rain_maiden.png", "char_002_rain_maiden.png"),
                NewChar("003", "冒险者", "portrait_003_adventurer.png", "char_003_adventurer.png"),
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
        static SettingsPanel BuildSettingsPanel(Transform root)
        {
            var rootGO = new GameObject("SettingsPanel", typeof(RectTransform), typeof(SettingsPanel));
            rootGO.transform.SetParent(root, false);
            var rrt = (RectTransform)rootGO.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            // 遮罩：挡住背后的按钮
            var overlay = CreateUI<Image>("Overlay", rootGO.transform, new Vector2(960, 540), new Vector2(1920, 1080));
            overlay.color = new Color(0f, 0f, 0f, 0.55f);

            // 面板本体（子元素用面板内坐标：760x600）
            var box = CreateUI<Image>("Box", rootGO.transform, new Vector2(960, 540), new Vector2(760, 600));
            box.color = new Color(0.23f, 0.19f, 0.15f);

            var title = CreateUI<Text>("Title", box.transform, new Vector2(380, 70), new Vector2(400, 84));
            Style(title, "设 置", 56, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.75f));

            var musicLabel = CreateUI<Text>("MusicLabel", box.transform, new Vector2(190, 200), new Vector2(260, 76));
            Style(musicLabel, "音 乐", 44, TextAnchor.MiddleRight, new Color(1f, 0.96f, 0.88f));
            var musicToggle = MakeButton(box.transform, "MusicToggle", "开", new Vector2(540, 200), new Vector2(200, 88));
            var musicText = musicToggle.transform.Find("Label").GetComponent<Text>();

            var sfxLabel = CreateUI<Text>("SfxLabel", box.transform, new Vector2(190, 330), new Vector2(260, 76));
            Style(sfxLabel, "音 效", 44, TextAnchor.MiddleRight, new Color(1f, 0.96f, 0.88f));
            var sfxToggle = MakeButton(box.transform, "SfxToggle", "开", new Vector2(540, 330), new Vector2(200, 88));
            var sfxText = sfxToggle.transform.Find("Label").GetComponent<Text>();

            var closeBtn = MakeButton(box.transform, "CloseButton", "关 闭", new Vector2(380, 490), new Vector2(280, 96));

            var sp = rootGO.GetComponent<SettingsPanel>();
            sp.panelRoot = rootGO;
            sp.musicToggle = musicToggle.GetComponent<Button>();
            sp.musicStateText = musicText;
            sp.sfxToggle = sfxToggle.GetComponent<Button>();
            sp.sfxStateText = sfxText;
            sp.closeButton = closeBtn.GetComponent<Button>();

            rootGO.SetActive(false);
            return sp;
        }

        static CharacterInfo NewChar(string id, string label, string portraitFile, string worldFile)
            => new CharacterInfo
            {
                id = id,
                displayName = label,
                portrait = S("Portraits/" + portraitFile),
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
                                        typeof(PlayerController), typeof(PlayerAppearance), typeof(YSort));
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

            var pc = player.GetComponent<PlayerController>();
            pc.bodyRenderer = sr;
            pc.animator = anim;

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

        static GameObject MakeButton(Transform parent, string name, string label, Vector2 centerPos, Vector2 size)
        {
            var img = CreateUI<Image>("BtnBg_" + name, parent, centerPos, size);
            img.gameObject.name = name;
            img.color = new Color(0.35f, 0.30f, 0.24f);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var txt = CreateUI<Text>("Label", img.transform, new Vector2(size.x * 0.5f, size.y * 0.5f), size);
            Style(txt, label, 40, TextAnchor.MiddleCenter, new Color(1f, 0.98f, 0.94f));
            return img.gameObject;
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
