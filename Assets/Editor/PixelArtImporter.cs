using UnityEditor;
using UnityEngine;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 让 Assets/Art/ 下的所有贴图按像素游戏规范导入：
    /// Sprite 类型、Point 过滤（不糊）、PPU=32、不做压缩、无 mipmap、FullRect 网格。
    /// 这样 1 个 32x32 的 tile 正好等于 1 个世界单位。
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const int PixelsPerUnit = 32;
        private const string ArtRoot = "Assets/Art/";

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(ArtRoot)) return;
            Apply((TextureImporter)assetImporter);
        }

        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
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
                    PixelArtImporter.Apply(imp);
                    EditorUtility.SetDirty(imp);
                    n++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[LuminousSSS] 已按像素规范重新导入 {n} 张贴图（PPU={PixelArtImporter.PixelsPerUnit}，Point，无压缩）。");
        }
    }
}
