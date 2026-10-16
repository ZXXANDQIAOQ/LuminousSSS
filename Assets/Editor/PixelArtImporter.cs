using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 让 Assets/Art/ 下的所有贴图按像素游戏规范导入：
    /// Sprite 类型、Point 过滤（不糊）、不做压缩、无 mipmap、FullRect 网格。
    ///
    /// PPU 分两档，保证两套素材"1 个 tile = 1 个世界单位"完全一致：
    ///   Assets/Art/           旧素材 1 tile = 32 px  -> PPU 32
    ///   Assets/Art/V2/        v0.2 素材 1 tile = 64 px -> PPU 64
    ///
    /// 另外为 UI 素材补上九宫格边界（见 SpriteBorders）——UI 底板必须走 Sliced，
    /// 否则每种宽度都要重画一张图；边界写进导入设置，美术重新导出素材也不会丢。
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const int PixelsPerUnit = 32;        // 旧素材（gen_art.py 生成）
        public const int PixelsPerUnitV2 = 64;      // v0.2 素材（AI 素材归一化后）
        public const int PixelsPerUnitUi = 100;     // 全屏 UI 底图：对齐 Canvas referencePixelsPerUnit，1:1 铺满
        private const string ArtRoot = "Assets/Art/";
        private const string ArtV2Root = "Assets/Art/V2/";

        /// <summary>
        /// UI 九宫格边界，单位=精灵像素，顺序 <b>左 / 下 / 右 / 上</b>
        /// （Unity 的 Sprite Border 就是这个顺序，和 Godot / Unreal 的「左上右下」不同）。
        /// 这里全部取对称值，正是为了避开顺序陷阱。
        /// </summary>
        public static readonly Dictionary<string, Vector4> SpriteBorders = new Dictionary<string, Vector4>
        {
            // 257x85 木纹圆角按钮：圆角到 x=9 结束、描边 6px，取 12 留 3px 余量
            { "v2_ui_button_wood",       new Vector4(12, 12, 12, 12) },
            // 258x94 选中态（多一圈 3px 黄框）：14 覆盖 黄框 + 深棕描边 + 圆角
            { "v2_ui_button_selected",   new Vector4(14, 14, 14, 14) },
            // 1411x372 羊皮纸对话框：26 覆盖木框（约 18px）与四角铆钉（约 22px）
            { "v2_ui_dialog_panel",      new Vector4(26, 26, 26, 26) },
            // 同上的纵向版（1411x1411），供接近方形的容器使用
            { "v2_ui_dialog_panel_tall", new Vector4(26, 26, 26, 26) },
            // v0.8 重绘：圆角 10 + 木框 4 + 内凹区自 14px 起 -> 取 16
            { "v2_ui_slot_empty",        new Vector4(16, 16, 16, 16) },
            { "v2_ui_slot_item",         new Vector4(14, 14, 14, 14) },

            // ---- v0.8 新增：素材补全批次（全部为硬边重绘，边界按「圆角 + 木框 + 铆钉」算）----
            { "v2_ui_slot_selected",     new Vector4(16, 16, 16, 16) },  // 与 slot_empty 同几何，保证叠放不跳位
            { "v2_ui_panel_inventory",   new Vector4(20, 20, 20, 20) },  // 圆角 14 + 木框 12 + 铆钉至 8，取 20 留 6
            { "v2_ui_tooltip_panel",     new Vector4(18, 18, 18, 18) },  // 圆角 12 + 木框 10
            { "v2_ui_dialog_bubble",     new Vector4(28, 28, 28, 28) },  // 圆角 22，四者中最圆
            { "v2_ui_currency_plate",    new Vector4(12, 12, 12, 12) },  // 圆角 10
            { "v2_ui_stamina_bg",        new Vector4(6, 6, 6, 6) },      // 圆角 3×2
            { "v2_ui_stamina_fill",      new Vector4(6, 6, 6, 6) },      // 必须与底槽同值，否则长度对不齐
        };

        /// <summary>按 Canvas referencePixelsPerUnit 导入的全屏底图（否则会被放大 1.5625 倍）。</summary>
        private static readonly HashSet<string> FullScreenUi = new HashSet<string> { "v2_ui_login_bg" };

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(ArtRoot)) return;
            Apply((TextureImporter)assetImporter, ResolvePpu(p));
        }

        /// <summary>按路径决定用哪一档 PPU。</summary>
        public static int ResolvePpu(string assetPath)
        {
            string p = assetPath.Replace('\\', '/');
            if (FullScreenUi.Contains(System.IO.Path.GetFileNameWithoutExtension(p)))
                return PixelsPerUnitUi;
            return p.StartsWith(ArtV2Root) ? PixelsPerUnitV2 : PixelsPerUnit;
        }

        public static void Apply(TextureImporter importer, int ppu = PixelsPerUnit)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // 防止 Tight 网格裁掉透明边
            settings.spriteExtrude = 0;
            settings.spriteAlignment = (int)SpriteAlignment.Center;

            // 九宫格边界：只有列进表的 UI 底板才写，其余保持 0
            var stem = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
            settings.spriteBorder = SpriteBorders.TryGetValue(stem, out var border)
                ? border
                : Vector4.zero;

            importer.SetTextureSettings(settings);
        }
    }

    public static class PixelArtTools
    {
        [MenuItem("Tools/LuminousSSS/1. 重新导入像素资源", false, 100)]
        public static void ReimportArt()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });
            int n = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    PixelArtImporter.Apply(imp, PixelArtImporter.ResolvePpu(path));
                    EditorUtility.SetDirty(imp);
                    n++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[LuminousSSS] 已按像素规范重新导入 {n} 张贴图" +
                      $"（Art/ 用 PPU={PixelArtImporter.PixelsPerUnit}，Art/V2/ 用 PPU={PixelArtImporter.PixelsPerUnitV2}，" +
                      $"Point 过滤、无压缩）。");
        }
    }
}
