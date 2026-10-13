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
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const int PixelsPerUnit = 32;        // 旧素材（gen_art.py 生成）
        public const int PixelsPerUnitV2 = 64;      // v0.2 素材（AI 素材归一化后）
        private const string ArtRoot = "Assets/Art/";
        private const string ArtV2Root = "Assets/Art/V2/";

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
