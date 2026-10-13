using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
        const string TilesDir = "Assets/Tiles";
        const string ScenesDir = "Assets/Scenes";

        // 农场地图（tile 单位）：40 x 24，1 tile = 1 世界单位
        const int MapW = 40;
        const int MapH = 24;
        const int WallRows = 2;              // 顶部石墙占两行
        static readonly RectInt DirtRect = new RectInt(6, 5, 20, 11);   // 中央土黄空地
        const int PathLeft = 28;             // 土路两列
        const int PathTop = 14;

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
            BuildLoginScene();
            BuildFarmScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(ScenesDir + "/LoginScene.unity");
            Debug.Log("[LuminousSSS] 场景生成完成：Assets/Scenes/LoginScene.unity 与 FarmScene.unity（已为你打开 LoginScene，直接点播放即可）。");
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

            // 角色立绘
            var portrait = CreateUI<Image>("Portrait", root, new Vector2(960, 560), new Vector2(560, 620));
            portrait.preserveAspect = true;
            portrait.sprite = S("Portraits/portrait_001_farmer.png");

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

            // 逻辑挂载
            var selector = canvasGO.AddComponent<CharacterSelector>();
            selector.portraitImage = portrait;
            selector.nameLabel = nameText;
            selector.prevButton = prevBtn.GetComponent<Button>();
            selector.nextButton = nextBtn.GetComponent<Button>();
            selector.startButton = startBtn.GetComponent<Button>();
            selector.settingsButton = settingsBtn.GetComponent<Button>();
            selector.nextSceneName = GameData.SceneFarm;
            selector.characters = new[]
            {
                NewChar("001", "农夫", "portrait_001_farmer.png", "char_001_farmer.png"),
                NewChar("002", "雨女", "portrait_002_rain_maiden.png", "char_002_rain_maiden.png"),
                NewChar("003", "冒险者", "portrait_003_adventurer.png", "char_003_adventurer.png"),
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/LoginScene.unity");
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

            // ---- 地面 Tilemap
            var gridGO = new GameObject("Grid", typeof(Grid));
            var tmGO = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            tmGO.transform.SetParent(gridGO.transform, false);
            var tm = tmGO.GetComponent<Tilemap>();
            var tmr = tmGO.GetComponent<TilemapRenderer>();
            tmr.sortingOrder = -100;

            var grass = A<Tile>(TilesDir + "/tile_grass.asset");
            var dirt = A<Tile>(TilesDir + "/tile_dirt.asset");
            var path = A<Tile>(TilesDir + "/tile_path.asset");
            var wall = A<Tile>(TilesDir + "/tile_stonewall.asset");

            // 草铺满（多铺一圈，避免相机贴边看到空）
            for (int x = -3; x < MapW + 3; x++)
                for (int y = -3; y < MapH + 3; y++)
                    tm.SetTile(new Vector3Int(x, y, 0), grass);

            // 中央土黄空地
            for (int x = DirtRect.xMin; x < DirtRect.xMax; x++)
                for (int y = DirtRect.yMin; y < DirtRect.yMax; y++)
                    tm.SetTile(new Vector3Int(x, y, 0), dirt);

            // 土路：木屋门口 -> 底部出口
            for (int y = 0; y <= PathTop; y++)
            {
                tm.SetTile(new Vector3Int(PathLeft, y, 0), path);
                tm.SetTile(new Vector3Int(PathLeft + 1, y, 0), path);
            }
            for (int x = PathLeft - 2; x < PathLeft; x++)
                tm.SetTile(new Vector3Int(x, PathTop, 0), path);

            // 顶部石墙
            for (int x = 0; x < MapW; x++)
                for (int r = 0; r < WallRows; r++)
                    tm.SetTile(new Vector3Int(x, MapH - 1 - r, 0), wall);

            // ---- 物件
            var props = new GameObject("Props").transform;

            // 木屋（右上，底部落在 y=14）
            PlaceProp(props, "Buildings/building_house.png", 30f, 14f, 0);

            // 顶部灌木带
            foreach (float bx in new[] { 8f, 11f, 14f, 17f, 20f, 36f })
                PlaceProp(props, "Props/prop_bush.png", bx, MapH - WallRows - 0.4f, 0);

            // 左侧：阔叶树 / 石头 / 树桩
            foreach (var p in new[] {
                (1.2f, 5.5f), (1.8f, 9.5f), (3.2f, 16.0f), (1.4f, 19.5f)
            }) PlaceProp(props, "Props/prop_tree_broadleaf.png", p.Item1, p.Item2, 0);

            PlaceProp(props, "Props/prop_rock_small.png", 2.6f, 7.6f, 0);
            PlaceProp(props, "Props/prop_rock_large.png", 2.2f, 12.4f, 0);
            PlaceProp(props, "Props/prop_rock_large.png", 3.6f, 21.0f, 0);
            PlaceProp(props, "Props/prop_stump.png", 4.8f, 9.8f, 0);
            PlaceProp(props, "Props/prop_stump.png", 4.4f, 17.8f, 0);

            // 右侧：松树 / 树桩
            foreach (var p in new[] {
                (37.0f, 4.0f), (37.6f, 10.5f), (37.0f, 17.5f)
            }) PlaceProp(props, "Props/prop_tree_pine.png", p.Item1, p.Item2, 0);

            PlaceProp(props, "Props/prop_stump.png", 35.6f, 9.4f, 0);
            PlaceProp(props, "Props/prop_stump.png", 36.2f, 14.4f, 0);

            // 底部：木头 / 石头 / 树桩 / 杂草
            PlaceProp(props, "Props/prop_log.png", 9.0f, 2.2f, 0);
            PlaceProp(props, "Props/prop_rock_small.png", 13.6f, 2.6f, 0);
            PlaceProp(props, "Props/prop_stump.png", 16.6f, 2.2f, 0);
            PlaceProp(props, "Props/prop_rock_small.png", 21.2f, 2.8f, 0);
            PlaceProp(props, "Props/prop_stump.png", 23.6f, 2.4f, 0);
            PlaceProp(props, "Props/prop_rock_large.png", 26.2f, 2.0f, 0);

            foreach (var p in new[] {
                (7.5f, 8.0f), (10.5f, 3.5f), (18.5f, 8.0f), (24.5f, 3.2f), (33.5f, 3.2f), (5.5f, 13.5f)
            }) PlaceProp(props, "Props/prop_grass_tuft.png", p.Item1, p.Item2, 0);

            // ---- 地图边界碰撞（设计文档 4.5）
            var bounds = new GameObject("Bounds").transform;
            MakeWall(bounds, "WallTop", new Vector2(MapW / 2f, MapH - WallRows + 0.5f), new Vector2(MapW + 4, 1));
            MakeWall(bounds, "WallBottom", new Vector2(MapW / 2f, -0.5f), new Vector2(MapW + 4, 1));
            MakeWall(bounds, "WallLeft", new Vector2(-0.5f, MapH / 2f), new Vector2(1, MapH + 4));
            MakeWall(bounds, "WallRight", new Vector2(MapW + 0.5f, MapH / 2f), new Vector2(1, MapH + 4));

            // ---- 玩家（出生在木屋门口土路上，面向下方）
            var player = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                                        typeof(PlayerController), typeof(PlayerAppearance));
            player.transform.position = new Vector3(PathLeft + 1f, 13f, 0f);

            var visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var sr = visual.GetComponent<SpriteRenderer>();
            sr.sprite = S("Characters/char_001_farmer.png");
            sr.sortingOrder = 10;

            var cc = player.GetComponent<CircleCollider2D>();
            cc.radius = 0.32f;
            cc.offset = new Vector2(0f, 0.45f);

            var pc = player.GetComponent<PlayerController>();
            pc.bodyRenderer = sr;

            var pa = player.GetComponent<PlayerAppearance>();
            pa.target = sr;
            pa.entries = new[]
            {
                new PlayerAppearance.Entry { id = "001", sprite = S("Characters/char_001_farmer.png") },
                new PlayerAppearance.Entry { id = "002", sprite = S("Characters/char_002_rain_maiden.png") },
                new PlayerAppearance.Entry { id = "003", sprite = S("Characters/char_003_adventurer.png") },
            };

            var follow = camGO.GetComponent<CameraFollow>();
            follow.target = player.transform;
            follow.useBounds = true;
            follow.min = new Vector2(0f, 0f);
            follow.max = new Vector2(MapW, MapH);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/FarmScene.unity");
        }

        static void PlaceProp(Transform parent, string rel, float tileX, float groundY, int order)
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
            sr.sortingOrder = order + Mathf.RoundToInt((MapH - groundY) * 10f);
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
            var wanted = new[] { ScenesDir + "/LoginScene.unity", ScenesDir + "/FarmScene.unity" };
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
