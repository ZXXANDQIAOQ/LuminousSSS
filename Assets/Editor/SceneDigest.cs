using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LuminousSSS.EditorTools
{
    /// <summary>
    /// 把场景里的「地面瓦片 + 所有 SpriteRenderer」导出成 JSON，
    /// 交给 Tools/preview_scene.py 离线合成布局预览图。
    ///
    /// 用法：菜单 Tools/LuminousSSS/5. 导出场景布局 JSON，然后
    ///   python Tools/preview_scene.py .workbuddy/digest_village.json 34 20 Docs/preview_v2/scene_village.png
    ///
    /// 为什么不用引擎渲染截图：本机 GUI 编辑器进程起不来（受限环境），
    /// batchmode 又是 -nographics 没有 GPU。导出坐标 + 素材路径再离线合成，
    /// 拿到的是同一份真实数据（世界坐标、尺寸、排序），只是换了个渲染器。
    ///
    /// </summary>
    public static class SceneDigest
    {
        [MenuItem("Tools/LuminousSSS/5. 导出场景布局 JSON（离线预览用）", false, 203)]
        public static void RunAll()
        {
            Dump("Assets/Scenes/FarmScene.scene", "farm");
            Dump("Assets/Scenes/VillageScene.scene", "village");
            Debug.Log("[Digest] 完成：.workbuddy/digest_farm.json / digest_village.json");
        }

        static void Dump(string scenePath, string tag)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);

            var sb = new StringBuilder();
            sb.Append("{\"objects\":[");

            bool first = true;

            // ---- 地面瓦片（Tilemap）
            foreach (var tm in Object.FindObjectsOfType<Tilemap>())
            {
                var bounds = tm.cellBounds;
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                    for (int x = bounds.xMin; x < bounds.xMax; x++)
                    {
                        var tile = tm.GetTile(new Vector3Int(x, y, 0));
                        if (tile == null) continue;

                        var sprite = tm.GetSprite(new Vector3Int(x, y, 0));
                        string spritePath = sprite != null ? AssetDatabase.GetAssetPath(sprite) : "";

                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append("{\"k\":\"tile\",\"x\":").Append(x)
                          .Append(",\"y\":").Append(y)
                          .Append(",\"map\":\"").Append(tm.gameObject.name).Append('"')
                          .Append(",\"order\":").Append(tm.GetComponent<TilemapRenderer>().sortingOrder)
                          .Append(",\"sprite\":\"").Append(spritePath).Append("\"}");
                    }
            }

            // ---- SpriteRenderer 物件
            foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr.sprite == null) continue;
                var b = sr.bounds;
                string spritePath = AssetDatabase.GetAssetPath(sr.sprite);

                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"k\":\"sprite\",\"name\":\"").Append(sr.gameObject.name).Append('"')
                  .Append(",\"x\":").Append(b.min.x.ToString("F3"))
                  .Append(",\"y\":").Append(b.min.y.ToString("F3"))
                  .Append(",\"w\":").Append(b.size.x.ToString("F3"))
                  .Append(",\"h\":").Append(b.size.y.ToString("F3"))
                  .Append(",\"order\":").Append(sr.sortingOrder)
                  .Append(",\"flipX\":").Append(sr.flipX ? 1 : 0)
                  // 染色（村民 tint）也带上，否则预览里所有村民长一个样
                  .Append(",\"r\":").Append(sr.color.r.ToString("F2"))
                  .Append(",\"g\":").Append(sr.color.g.ToString("F2"))
                  .Append(",\"b\":").Append(sr.color.b.ToString("F2"))
                  .Append(",\"sprite\":\"").Append(spritePath).Append("\"}");
            }

            sb.Append("]}");
            Directory.CreateDirectory(".workbuddy");
            File.WriteAllText(".workbuddy/digest_" + tag + ".json", sb.ToString());
            Debug.Log($"[Digest] {scene.name}: 导出 {sb.Length} 字节 -> .workbuddy/digest_{tag}.json");
        }
    }
}
