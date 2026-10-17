using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
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
        const string WitchIdleSprite = "V2/Characters/v2_witch_idle.png";
        const string WitchIdleBobSprite = "V2/Characters/v2_witch_idle_bob.png";
        const string TilesDir = "Assets/Tiles";
        const string ScenesDir = "Assets/Scenes";

        /// <summary>
        /// 可玩角色的行走素材命名段（方案 A：命名带角色段，每角色独立剪辑 + 控制器）。
        ///
        /// 素材约定：v2_walk_{Key}_{dir}_{01..06}.png
        /// 生成产物：{Key}_walk_{dir}.anim（4 个）+ {Key}_idle.anim + {Pascal}PlayerController.controller
        ///
        /// 缺帧的角色会被跳过（生成器 warning），该角色在游戏里由 CharacterAnimDriver 走
        /// flipX + bob 兜底 —— 所以四个角色可以陆续交付，不影响已交付的角色。
        /// </summary>
        static readonly (string Id, string Key, string Pascal, string Label)[] WalkChars =
        {
            ("001", "farmer",     "Farmer",     "农夫"),
            ("002", "rain",       "Rain",       "雨女"),
            ("003", "adventurer", "Adventurer", "冒险者"),
            ("004", "witch",      "Witch",      "女巫"),
        };

        static string CtrlPathOf(string pascal) => AnimDir + pascal + "PlayerController.controller";
        static string IdleClipPathOf(string key) => AnimDir + key + "_idle.anim";
        static string WalkClipPathOf(string key, string dir) => AnimDir + key + "_walk_" + dir + ".anim";

        /// <summary>取某角色 id 的动画控制器（该角色缺帧、控制器未生成时返回 null）。</summary>
        static RuntimeAnimatorController CtrlOf(string id)
        {
            foreach (var c in WalkChars)
                if (c.Id == id) return A<RuntimeAnimatorController>(CtrlPathOf(c.Pascal));
            return null;
        }

        /// <summary>
        /// 场景内小人的静态立绘：优先用该角色 V2 行走帧的首帧（与动画帧同尺寸同 PPU，
        /// 避免「静止一尺寸、走动另一尺寸」的跳变）；素材未交付时退回旧版小图。
        /// </summary>
        static Sprite WorldSpriteOf(string key, string fallbackRel)
        {
            var v2 = S($"V2/Characters/v2_walk_{key}_down_01.png");
            return v2 != null ? v2 : S(fallbackRel);
        }

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
        // 农场 HUD 的虚拟摇杆（Tools/gen_ui_joystick.py 程序化生成）
        const string UiJoystickBase = UiRoot + "v2_ui_joystick_base.png";      // 空心木纹底圈
        const string UiJoystickKnob = UiRoot + "v2_ui_joystick_knob.png";      // 实心木纹摇杆头

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

    // ---- 农场 HUD（虚拟摇杆）
    // 尺寸与素材 1:1（gen_ui_joystick.py 出 288 / 128），Point 过滤下不会出现非整数缩放毛边
    const float JoystickTouchSize = 480f;   // 触摸热区边长（隐形，比底圈大一圈，手指不用瞄准）
    const float JoystickBaseSize = 288f;    // 底圈显示尺寸
    const float JoystickKnobSize = 128f;    // 摇杆头显示尺寸
    const float JoystickInset = 40f;        // 距屏幕左 / 下边缘

    // ---- 农场 HUD（砍伐按钮，v0.12）
    // 依据：点击区下限 88x88（移动端建议值），360x120 远超下限；
    // 与左下摇杆底圈（288 直径）视觉重量平衡；木纹底板走九宫格，宽高任意拉伸不糊。
    const float ChopButtonW = 360f;
    const float ChopButtonH = 120f;

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
            EnsureCharacterAnimations();     // 剪辑：每角色 idle + 四方向行走（缺帧的角色跳过）
            EnsureCharacterControllers();    // 控制器：每角色一个四方向状态机
            BuildLoginScene();
            BuildFarmScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(ScenesDir + "/LoginScene.scene");
            Debug.Log("[LuminousSSS] 场景生成完成：Assets/Scenes/LoginScene.scene 与 FarmScene.scene（已为你打开 LoginScene，直接点播放即可）。");
        }

        /// <summary>
        /// 只重建农场场景（v0.12：AutoBuildScenes 增量用）。
        /// 前置资产（tile / 动画 / 控制器）缺一个农场场景就是残的，所以先补齐再建；
        /// 与 <see cref="BuildAll"/> 的区别是绝不碰 LoginScene —— 登录界面允许手动改过。
        /// </summary>
        internal static void BuildFarmSceneOnly()
        {
            EnsureFolders();
            EnsureTiles();
            EnsureCharacterAnimations();
            EnsureCharacterControllers();
            BuildFarmScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LuminousSSS] 已重建 Assets/Scenes/FarmScene.scene（登录场景未动）。");
        }

        /// <summary>
        /// 只重建登录场景（补充 AutoBuildScenes 的对称缺口）。
        /// 原先「缺登录、农场在」的分支调的是 <see cref="OneClick"/>，而 OneClick → BuildAll
        /// 会把已存在的 FarmScene 一并重建，与「绝不重建已存在的场景」的承诺相反。
        /// 登录场景只依赖 UI 素材与立绘，不需要 tile / 动画 / 控制器。
        /// </summary>
        internal static void BuildLoginSceneOnly()
        {
            EnsureFolders();
            BuildLoginScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LuminousSSS] 已重建 Assets/Scenes/LoginScene.scene（农场场景未动）。");
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

        // ------------------------------------------------------------ 角色动画资产
        // 按角色生成：每角色 4 个行走剪辑 + 1 个待机剪辑 + 1 个控制器。
        // 素材缺失的角色会被跳过（警告），由 CharacterAnimDriver 的 flipX + bob 兜底。
        //
        // 历史：旧版所有角色共用一套 v2_walk_{dir}_{nn}.png（内容是农夫形象），
        // 女巫走的是农夫的动作。v0.10 起按角色分段命名（v2_walk_{key}_{dir}_{nn}.png），
        // 现有那 24 张已重命名为 v2_walk_farmer_*，回归农夫。
        static void EnsureCharacterAnimations()
        {
            int ok = 0, skipped = 0;
            foreach (var c in WalkChars)
            {
                if (EnsureCharacterAnim(c.Key, c.Label)) ok++;
                else skipped++;
            }
            Debug.Log($"[LuminousSSS] 角色动画：{ok} 个角色已生成剪辑，{skipped} 个因缺帧跳过（走 bob 兜底）。");
        }

        /// <summary>生成单个角色的 idle + 4 方向行走剪辑。素材不全时返回 false 且不生成任何剪辑。</summary>
        static bool EnsureCharacterAnim(string key, string label)
        {
            // 先整体检查四方向素材，缺任何一个就整角色跳过（避免生成半套动画）
            var walk = new Dictionary<string, Sprite[]>();
            foreach (var dir in new[] { "down", "up", "left", "right" })
            {
                var frames = new Sprite[6];
                for (int i = 0; i < 6; i++)
                {
                    string rel = $"V2/Characters/v2_walk_{key}_{dir}_{i + 1:00}.png";
                    frames[i] = S(rel);
                    if (frames[i] == null)
                    {
                        Debug.Log($"[LuminousSSS] {label} 缺行走帧 {rel} —— 该角色暂用 flipX + bob 兜底。");
                        return false;
                    }
                }
                walk[dir] = frames;
            }

            foreach (var kv in walk)
                WriteSpriteClip(WalkClipPathOf(key, kv.Key), kv.Value, 8f);

            // 待机：有专属 idle 素材（如女巫 idle + bob 两帧）就呼吸循环，
            // 否则拿「向下走」第 1 帧做静态待机（不是循环行走，避免原地踏步）
            var idleA = S($"V2/Characters/v2_{key}_idle.png");
            var idleB = S($"V2/Characters/v2_{key}_idle_bob.png");
            if (idleA != null && idleB != null)
                WriteSpriteClip(IdleClipPathOf(key), new[] { idleA, idleB }, 2f);
            else
                WriteSpriteClip(IdleClipPathOf(key), new[] { walk["down"][0] }, 1f);

            return true;
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

        // ------------------------------------------------------------ 角色动画控制器
        // 四方向状态机：Idle + WalkDown/Up/Left/Right，**每个角色一个控制器**。
        // 参数由 CharacterAnimDriver 每帧写入（PlayerController 记录的朝向是主轴
        // 单位向量，所以 MoveX/MoveY 同一时刻只有一个非零）：
        //   Speed  0/1   是否在移动
        //   MoveX -1/0/1 主轴朝向（水平优先）
        //   MoveY -1/0/1 主轴朝向
        // 旧版只有 Idle/Walk 两个状态、FacingUp 是死参数，这里幂等重建。
        static void EnsureCharacterControllers()
        {
            int ok = 0, cleaned = 0;
            foreach (var c in WalkChars)
            {
                if (EnsureCharacterController(c.Key, c.Pascal, c.Label))
                {
                    ok++;
                    continue;
                }

                // 素材不全（如女巫还没交付帧）却存在旧控制器 —— 上一版遗留的控制器引用的是
                // 过期的剪辑（女巫曾借用农夫帧）。必须删掉，否则运行时会被挂上，
                // 导致角色播放别人的动作。删掉后该角色由 CharacterAnimDriver 走 flipX + bob。
                string stale = CtrlPathOf(c.Pascal);
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(stale) != null)
                {
                    AssetDatabase.DeleteAsset(stale);
                    cleaned++;
                    Debug.Log($"[LuminousSSS] {c.Label} 素材不全，已清理过期控制器：{stale}");
                }
            }
            Debug.Log($"[LuminousSSS] 角色控制器：{ok}/{WalkChars.Length} 个已就绪，"
                      + $"清理过期 {cleaned} 个（缺帧的角色走 flipX + bob 兜底）。");
        }

        /// <summary>为单个角色生成四方向状态机控制器。剪辑不全时返回 false（不建控制器）。</summary>
        static bool EnsureCharacterController(string key, string pascal, string label)
        {
            var idle = A<AnimationClip>(IdleClipPathOf(key));
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var dir in new[] { "down", "up", "left", "right" })
            {
                var clip = A<AnimationClip>(WalkClipPathOf(key, dir));
                if (idle == null || clip == null) return false;
                clips[dir] = clip;
            }

            string ctrlPath = CtrlPathOf(pascal);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

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
            sDown.motion = clips["down"];
            sUp.motion = clips["up"];
            sLeft.motion = clips["left"];
            sRight.motion = clips["right"];
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
            Debug.Log($"[LuminousSSS] {label} 动画控制器已就绪（Idle + 四方向行走）：{ctrlPath}");
            return true;
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
            am.chopClip = A<AudioClip>(AudioDir + "sfx_chop.wav");   // 砍树命中（缺失时 PlayChop 退回 click）
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
                    // 女巫素材是 54×101 的 Q 版小像素人，人形立绘高 1024：
                    // 不压回来的话会被 preserveAspect 放大到 260×486，比农夫（199×512）还宽，
                    // 而且是大颗粒像素块，和精细立绘并列很割裂。
                    // 0.7 → 实际显示约 182×340（Q 版小人略矮于人手立绘，宽度相近）。
                    // 嫌高/矮改这一个数即可。
                    displayScale = 0.7f,
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
            // v0.13：视野高 13 格（原 20 格）。玩家 1.75 格高 → 占屏高 13.5%（原 8.8%）。
            // 依据：树 4.7 格占 36%、木屋 9.59 格占 74%（地标感），
            // 相机中心可移动余量 x 16.9 格 / y 11 格（再小就到边，跟随会「顶住」）。
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.24f, 0.14f);
            // 出生点（木屋门口）+ 胸口偏移；CameraFollow.Start 会 snap 到按边界 clamp 后的位置
            camGO.transform.position = new Vector3(HouseDoorX, 12.5f + 0.6f, -10f);

            EnsureAudioManager();

            // 农场场景原本没有任何 Canvas —— 手机端因此拿不到任何移动输入（v0.10 的已知缺陷）。
            BuildFarmHud();

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

            // ---- v0.12 田地种树：砍伐前田地不可见，玩家砍树后才暴露田地（开垦玩法）
            PlantFieldTrees(props);

            // ---- v0.12 空地加密：原本只有零星几棵树，房子两侧的大片草地空得跑肚皮
            DecorateOpenGround(props);

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
                                        typeof(YSort), typeof(CharacterAnimDriver),
                                        typeof(PlayerInteractor));   // v0.12：砍树交互（E 键 / HUD 按钮）
            player.transform.position = new Vector3(HouseDoorX, 12.5f, 0f);

            var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var sr = visual.GetComponent<SpriteRenderer>();
            sr.sprite = WorldSpriteOf("farmer", "Characters/char_001_farmer.png");
            sr.sortingOrder = 10;

            // 动画：先停用，由 PlayerAppearance 按所选角色启用对应控制器。
            // 每个角色都有自己的控制器（见 EnsureCharacterControllers），
            // 缺帧的角色 controller 为 null → CharacterAnimDriver 走 flipX + bob 兜底。
            var anim = visual.GetComponent<Animator>();
            anim.runtimeAnimatorController = CtrlOf("001");
            anim.enabled = false;

            var cc = player.GetComponent<CircleCollider2D>();
            cc.radius = 0.32f;
            cc.offset = new Vector2(0f, 0.45f);

            // CharacterAnimDriver 自己会在子物体里找 SpriteRenderer / Animator / PlayerController：
            // 有 controller 的角色走四方向动画；无 controller 的走 flipX + 走路小跳。
            var driver = player.GetComponent<CharacterAnimDriver>();
            driver.bobHeight = 0.06f;   // 约 4 像素（V2 素材 64 PPU）
            driver.bobFrequency = 8f;

            var pa = player.GetComponent<PlayerAppearance>();
            pa.target = sr;
            pa.animator = anim;
            pa.entries = new[]
            {
                new PlayerAppearance.Entry { id = "001", sprite = WorldSpriteOf("farmer", "Characters/char_001_farmer.png"),
                                             controller = CtrlOf("001") },
                new PlayerAppearance.Entry { id = "002", sprite = WorldSpriteOf("rain", "Characters/char_002_rain_maiden.png"),
                                             controller = CtrlOf("002") },
                new PlayerAppearance.Entry { id = "003", sprite = WorldSpriteOf("adventurer", "Characters/char_003_adventurer.png"),
                                             controller = CtrlOf("003") },
                new PlayerAppearance.Entry { id = "004", sprite = S(WitchIdleSprite), controller = CtrlOf("004") },
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
            // v0.13 跟随手感：胸口跟随 + 朝向预判 + 进场吸附 + 半像素对齐
            follow.snapOnStart = true;
            follow.targetOffset = new Vector2(0f, 0.6f);   // 脚底 → 胸口，角色不贴画面下沿
            follow.lookAhead = 1.2f;                       // 朝移动方向多看 1.2 格
            follow.lookAheadSharpness = 4f;
            follow.smoothTime = 0.15f;                     // 5 格/秒下滞后约 0.75 格
            follow.pixelGrid = 1f / 128f;                  // ≈ 半屏幕像素，压掉像素游走

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/FarmScene.scene");
        }

        /// <summary>
        /// 农场场景的 HUD 层（Canvas + EventSystem + 虚拟摇杆）。
        ///
        /// 为什么必须单独建：农场场景在 v0.10 之前**完全没有 Canvas 也没有 EventSystem**，
        /// 移动全靠键盘轴，安卓包里既没有按键也收不到指针事件 —— 玩家在手机上无法移动。
        ///
        /// Canvas 规范与登录界面完全一致（1920×1080、matchWidthOrHeight = 1.0 按高度适配），
        /// 摇杆锚定左下角，因此在 16:9 / 21:9 / 4:3 下都贴角不跑位。
        /// 后续的背包 / 体力条按 Docs/LuminousSSS-UI规格.md 的「层 2 HUD」加在这里。
        ///
        /// 横屏是硬性前提（v0.13.1 起由工程设置保证）：matchWidthOrHeight = 1.0 按高度适配，
        /// 竖屏 1080x1920 时画布宽只剩 1080×(1080/1920) ≈ 608 ——
        /// 摇杆热区（480，占 x 40..520）会和右下砍伐按钮（360，左边界 608-56-360=192）直接重叠，
        /// 而且两个触摸热区会互相抢事件。ProjectSettings 已锁「AutoRotation + 只允许左右横屏」。
        /// 若将来要支持竖屏，HUD 必须重排（摇杆与按钮不能同时贴底），不是调一个尺寸能解决的。
        /// </summary>
        static void BuildFarmHud()
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;                       // HUD 永远压在场景之上

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1.0f;
            canvasGO.AddComponent<UIFontFixer>();

            // 没有 EventSystem 就收不到任何指针 / 触摸事件，摇杆会「看得见按不动」
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var root = canvasGO.transform;

            // ---- 触摸热区：一张全透明 Image。alpha=0 不影响射线拾取，
            //      所以它不可见但能接住指针事件，比底圈大一圈，手指不用瞄准圆环。
            var areaGO = new GameObject("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            areaGO.transform.SetParent(root, false);
            var area = areaGO.GetComponent<RectTransform>();
            area.anchorMin = area.anchorMax = new Vector2(0f, 0f);   // 锚定左下角
            area.pivot = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(JoystickTouchSize, JoystickTouchSize);
            area.anchoredPosition = new Vector2(JoystickInset + JoystickTouchSize * 0.5f,
                                                JoystickInset + JoystickTouchSize * 0.5f);
            var hit = areaGO.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            // ---- 底圈（空心环，几乎不遮农场画面）
            var baseImg = CreateCentered<Image>("Base", area, new Vector2(JoystickBaseSize, JoystickBaseSize));
            baseImg.sprite = S(UiJoystickBase);
            baseImg.preserveAspect = true;
            baseImg.raycastTarget = false;

            // ---- 摇杆头：热区的轴心在 (0.5,0.5)，所以 anchoredPosition 就是「相对底圈圆心的偏移」，
            //      正好等于 VirtualJoystick 算出来的 local 向量，不用再做任何坐标换算。
            var knobImg = CreateCentered<Image>("Knob", area, new Vector2(JoystickKnobSize, JoystickKnobSize));
            knobImg.sprite = S(UiJoystickKnob);
            knobImg.preserveAspect = true;
            knobImg.raycastTarget = false;

            var joy = areaGO.GetComponent<VirtualJoystick>();
            joy.knob = knobImg.rectTransform;
            joy.radius = JoystickBaseSize * 0.5f;       // 底圈半径 = 行程基准
            joy.knobRadius = JoystickKnobSize * 0.5f;   // 可用行程 = 144 - 64 = 80 UI 像素

            // ---- 砍伐按钮（v0.12）：右下角，与左下摇杆对称
            // 田地初始被树木覆盖，砍伐是进场景后的第一个核心操作，按钮必须常驻 HUD。
            // 进场时没有可砍目标 → interactable=false（半透明），ChopButtonDriver 每 0.1s 按目标刷新。
            var chopGO = new GameObject("ChopButton",
                                        typeof(RectTransform), typeof(Image), typeof(Button),
                                        typeof(ChopButtonDriver));
            chopGO.transform.SetParent(root, false);
            var chopRt = chopGO.GetComponent<RectTransform>();
            chopRt.anchorMin = chopRt.anchorMax = new Vector2(1f, 0f);   // 锚右下角
            chopRt.pivot = new Vector2(1f, 0f);
            chopRt.sizeDelta = new Vector2(ChopButtonW, ChopButtonH);
            chopRt.anchoredPosition = new Vector2(-(JoystickInset + 16f), JoystickInset);

            var chopImg = chopGO.GetComponent<Image>();
            chopImg.sprite = S(UiBtnWood);
            chopImg.type = Image.Type.Sliced;
            chopImg.pixelsPerUnitMultiplier = UiPpu;    // 与登录按钮一致：边框按素材原始像素 1:1
            chopImg.raycastTarget = true;

            var chopBtn = chopGO.GetComponent<Button>();
            chopBtn.targetGraphic = chopImg;
            chopBtn.colors = MakeButtonColors();
            chopBtn.interactable = false;

            var chopLabelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            chopLabelGO.transform.SetParent(chopGO.transform, false);
            var chopLabelRt = chopLabelGO.GetComponent<RectTransform>();
            chopLabelRt.anchorMin = chopLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            chopLabelRt.pivot = new Vector2(0.5f, 0.5f);
            chopLabelRt.sizeDelta = new Vector2(ChopButtonW - 24f, ChopButtonH - 24f);
            chopLabelRt.anchoredPosition = Vector2.zero;
            Style(chopLabelGO.GetComponent<Text>(), "砍  伐", 44, TextAnchor.MiddleCenter, WoodTextColor);
            AddOutline(chopLabelGO.GetComponent<Text>(), WoodTextOutline, 2f);

            // 持久监听：运行时 onClick.AddListener 不会被序列化，保存场景后丢失，
            // 必须用 UnityEventTools 写进 m_OnClick.m_PersistentCalls。
            // 用 AddVoidPersistentListener（无参 void 方法的追加式重载）—— 
            // RegisterPersistentListener 只有 (UnityEvent, int, UnityAction) 这个 3 参版本，没有「目标+方法名」的 4 参重载。
            var chopDriver = chopGO.GetComponent<ChopButtonDriver>();
            UnityEventTools.AddVoidPersistentListener(
                chopBtn.onClick, chopDriver.OnClickChop);
        }

        /// <summary>锚点与轴心都取 (0.5,0.5) 的 UI 元素 —— 相对父节点居中。</summary>
        static T CreateCentered<T>(string name, Transform parent, Vector2 size) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return go.GetComponent<T>();
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

        /// <summary>
        /// v0.12 田地种树：砍伐前田地不可见，玩家砍树后田地才暴露（开垦玩法）。
        ///
        /// 梅花型种植：行内隔 2 格、相邻行错开 1 格（每 2x2 格一棵），
        /// 树间留 1 格通道；碰撞盒 1.0 格宽占格心，玩家（半径 0.32）刚好能从树缝走到田中央。
        /// 范围 = DirtRect 内缩 1 格，四周留通道进田（不内缩的话边缘树会把田堵死）。
        /// 树种按坐标哈希 6:4 混栽（阔叶 60% / 松树 40%），防止出现条纹状纯色带。
        ///
        /// 已砍记录由 TreeChop 读 PlayerPrefs（键 LuminousSSS.ChoppedTrees），
        /// 重进场景不长回来 —— 开垦进度不丢。
        /// </summary>
        static void PlantFieldTrees(Transform parent)
        {
            var broad = S("V2/Nature/v2_nature_tree_broadleaf.png");
            var pine = S("V2/Nature/v2_nature_tree_pine.png");
            var stump = S("V2/Nature/v2_nature_stump.png");
            if (broad == null || pine == null)
            {
                Debug.LogWarning("[LuminousSSS] 缺少树木素材（v2_nature_tree_*），田地种树跳过");
                return;
            }

            int planted = 0;
            for (int y = DirtRect.yMin + 1; y <= DirtRect.yMax - 1; y += 2)
            {
                // y 为奇数时整行右移半格（错开 1 格），形成梅花型
                for (int x = DirtRect.xMin + 1 + (y & 1); x <= DirtRect.xMax - 1; x += 2)
                {
                    bool isPine = Mathf.Abs(x * 31 + y * 17) % 10 >= 6;   // 阔叶 60% / 松树 40%
                    PlaceChopTree(parent,
                                  isPine ? pine : broad, stump,
                                  x + 0.5f, y + 0.5f,
                                  $"tree_{x}_{y}",
                                  isPine ? new Vector2(0.9f, 0.7f) : new Vector2(1.0f, 0.8f));
                    planted++;
                }
            }
            Debug.Log($"[LuminousSSS] 田地种树 {planted} 棵（砍伐前田地不可见，砍树后暴露）");
        }

        /// <summary>
        /// 种一棵可砍伐的树：与 <see cref="PlaceProp"/> 同一套「底部对齐 + Y 排序」公式，
        /// 额外挂 TreeChop 并写入 treeId / stumpSprite / groundY（砍倒后换树桩要按底部重新对齐）。
        /// </summary>
        static void PlaceChopTree(Transform parent, Sprite tree, Sprite stump, float cx, float groundY,
                                  string treeId, Vector2 collider)
        {
            if (tree == null) { Debug.LogWarning("[LuminousSSS] 缺少树木素材"); return; }

            var go = new GameObject("Tree_" + treeId,
                                    typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(TreeChop));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = tree;

            // sprite 是 Center 轴心：按「底部对齐到 groundY」摆放（与 PlaceProp 一致）
            float h = tree.rect.height / tree.pixelsPerUnit;
            go.transform.position = new Vector3(cx, groundY + h * 0.5f, 0f);
            sr.sortingOrder = Mathf.RoundToInt((MapH - groundY) * 10f);

            var box = go.GetComponent<BoxCollider2D>();
            box.size = collider;
            box.offset = new Vector2(0f, -h * 0.5f + collider.y * 0.5f);

            var chop = go.GetComponent<TreeChop>();
            chop.treeId = treeId;
            chop.stumpSprite = stump;
            chop.groundY = groundY;
        }

        /// <summary>
        /// v0.12 空地加密：木屋两侧与四角补树补灌木，让农场「住得进人」。
        /// 全部避开：木屋本体（x 25.9..34.1, y 14..23.6）、土路两列（x 29/30）、
        /// 已有物件坐标。位置硬编码 + 确定性，重新生成场景结果不变。
        /// </summary>
        static void DecorateOpenGround(Transform parent)
        {
            // 左下空地（木屋左侧、土路下方的大片草坡）
            PlaceProp(parent, "V2/Nature/v2_nature_tree_broadleaf.png", 7f, 18.8f, new Vector2(1.0f, 0.8f));
            PlaceProp(parent, "V2/Nature/v2_nature_tree_broadleaf.png", 13f, 18.5f, new Vector2(1.0f, 0.8f));
            PlaceProp(parent, "V2/Nature/v2_nature_tree_broadleaf.png", 19f, 20f, new Vector2(1.0f, 0.8f));
            PlaceProp(parent, "V2/Nature/v2_nature_tree_pine.png", 10.5f, 20.5f, new Vector2(0.9f, 0.7f));
            PlaceProp(parent, "V2/Nature/v2_nature_tree_pine.png", 22f, 18.5f, new Vector2(0.9f, 0.7f));
            PlaceProp(parent, "V2/Nature/v2_nature_bush.png", 5.5f, 17.5f, new Vector2(1.6f, 0.9f));
            PlaceProp(parent, "V2/Nature/v2_nature_bush.png", 16f, 20f, new Vector2(1.6f, 0.9f));
            PlaceProp(parent, "V2/Nature/v2_nature_bush.png", 23.5f, 20f, new Vector2(1.6f, 0.9f));
            PlaceProp(parent, "V2/Nature/v2_nature_rock.png", 9f, 20.3f, new Vector2(1.5f, 0.9f));
            // 花草点缀（无碰撞，纯装饰）
            PlaceProp(parent, "V2/Nature/v2_nature_grass_tuft.png", 12f, 19.5f, null);
            PlaceProp(parent, "V2/Nature/v2_nature_flower_yellow.png", 17.5f, 17.8f, null);
            PlaceProp(parent, "V2/Nature/v2_nature_flower_white.png", 21f, 21.2f, null);
            PlaceProp(parent, "V2/Nature/v2_nature_grass_tuft.png", 8f, 21.8f, null);

            // 右上空地（木屋右侧，x 35..38 窄条）
            PlaceProp(parent, "V2/Nature/v2_nature_tree_pine.png", 36f, 20.5f, new Vector2(0.9f, 0.7f));
            PlaceProp(parent, "V2/Nature/v2_nature_tree_pine.png", 35.5f, 18f, new Vector2(0.9f, 0.7f));

            // 左上角（左侧树列与墙之间的空隙）
            PlaceProp(parent, "V2/Nature/v2_nature_tree_broadleaf.png", 5.5f, 19.8f, new Vector2(1.0f, 0.8f));
            PlaceProp(parent, "V2/Nature/v2_nature_tree_pine.png", 2.5f, 21.3f, new Vector2(0.9f, 0.7f));
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
