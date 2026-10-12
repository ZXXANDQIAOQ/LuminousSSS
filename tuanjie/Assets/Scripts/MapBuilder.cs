// 地图生成：荒地地面 + 小屋 + 栅栏院子 + 散布装饰（与网页版同一套规则）
using System.Collections.Generic;
using UnityEngine;

public class MapBuilder : MonoBehaviour
{
    public TileRef[,] Ground;      // [ty, tx] 行自上而下
    public bool[,] Solid;
    public readonly List<(int tx, int ty, TileRef tile, bool solidTile)> Decor =
        new List<(int, int, TileRef, bool)>();

    const int W = GameConfig.MapW;
    const int H = GameConfig.MapH;

    public static float WorldX(int tx) => tx + 0.5f;
    public static float WorldY(int ty) => H - ty - 0.5f;

    public void Build()
    {
        Ground = new TileRef[H, W];
        Solid = new bool[H, W];

        for (int ty = 0; ty < H; ty++)
            for (int tx = 0; tx < W; tx++)
                Ground[ty, tx] = GroundTile(tx, ty);

        ScatterDecor();
        BuildHouse();
        BuildYard();

        CreateGroundObject();
        CreateDecorObjects();
        CreateColliders();
    }

    static float Hash2(int x, int y)
    {
        unchecked
        {
            int h = (x * 374761393 + y * 668265263) ^ 0x5bf03635;
            h = h * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    static TileRef GroundTile(int tx, int ty)
    {
        float h = Hash2(tx, ty);
        if (h < 0.75f) return Tiles.DirtPlain;
        if (h < 0.82f) return Tiles.DirtPlain2;
        if (h < 0.88f) return Tiles.DirtTuft1;
        if (h < 0.94f) return Tiles.DirtTuft2;
        return Tiles.DirtTuft3;
    }

    // 与网页版一致的确定性随机（同种子同布局）
    class Rng
    {
        uint s;
        public Rng(uint seed) { s = seed; }
        public float Next()
        {
            s += 0x6D2B79F5u;
            uint t = s;
            t = (uint)((t ^ (t >> 15)) * (t | 1u));
            t ^= t + (uint)((t ^ (t >> 7)) * (t | 61u));
            return ((t ^ (t >> 14)) & 0xffffff) / (float)0xffffff;
        }
    }

    bool Reserved(int tx, int ty)
    {
        int hx = GameConfig.HouseX, hy = GameConfig.HouseY;
        return tx >= hx - 3 && tx <= hx + 7 && ty >= hy - 2 && ty <= hy + 9;
    }

    void ScatterDecor()
    {
        var rng = new Rng(20261012u);
        for (int i = 0; i < 90; i++)
        {
            int tx = Mathf.FloorToInt(rng.Next() * W);
            int ty = Mathf.FloorToInt(rng.Next() * (H - 1));
            if (Reserved(tx, ty)) continue;
            if (Decor.Exists(d => d.tx == tx && d.ty == ty)) continue;
            float roll = rng.Next();
            if (roll < 0.30f && !Decor.Exists(d => d.tx == tx && (d.ty == ty || d.ty == ty + 1)))
            {
                bool fall = rng.Next() < 0.5f;
                Decor.Add((tx, ty, fall ? Tiles.TreeFallTop : Tiles.TreePineTop, false));
                Decor.Add((tx, ty + 1, fall ? Tiles.TreeFallBot : Tiles.TreePineBot, true));
            }
            else if (roll < 0.5f) Decor.Add((tx, ty, Tiles.Bush, false));
            else if (roll < 0.65f) Decor.Add((tx, ty, Tiles.Mushroom, false));
            else if (roll < 0.75f) Decor.Add((tx, ty, Tiles.Rock, true));
        }
    }

    void BuildHouse()
    {
        for (int dy = 0; dy < 4; dy++)
            for (int dx = 0; dx < 4; dx++)
                Decor.Add((GameConfig.HouseX + dx, GameConfig.HouseY + dy, Tiles.House(dx, dy), true));
    }

    void BuildYard()
    {
        int hx = GameConfig.HouseX, hy = GameConfig.HouseY;
        for (int dx = -2; dx <= 6; dx++)
        {
            if (dx == 1 || dx == 2) continue;               // 门口留缺口
            Decor.Add((hx + dx, hy + 7, Tiles.FenceH, true));
        }
        for (int dy = 5; dy <= 6; dy++)
        {
            Decor.Add((hx - 2, hy + dy, Tiles.FenceV, true));
            Decor.Add((hx + 6, hy + dy, Tiles.FenceV, true));
        }
        Decor.Add((hx + 5, hy + 5, Tiles.Well, true));
        Decor.Add((hx - 1, hy + 6, Tiles.Hay, true));
        Decor.Add((hx + 3, hy + 6, Tiles.Sign, true));
    }

    void CreateColliders()
    {
        foreach (var d in Decor)
            if (d.solidTile && d.tx >= 0 && d.ty >= 0 && d.tx < W && d.ty < H)
                Solid[d.ty, d.tx] = true;
    }

    // 地面：整张地图烘成一张贴图（一个 draw call）
    void CreateGroundObject()
    {
        var atlas = Resources.Load<Texture2D>("Art/tilemap");
        int aH = atlas.height;
        var atlasPx = atlas.GetPixels32();

        int gw = W * GameConfig.TilePx, gh = H * GameConfig.TilePx;
        var px = new Color32[gw * gh];

        for (int ty = 0; ty < H; ty++)
        {
            for (int tx = 0; tx < W; tx++)
            {
                var t = Ground[ty, tx];
                int sx = t.C * GameConfig.TilePx;
                int sy = aH - (t.R + 1) * GameConfig.TilePx;
                int dx = tx * GameConfig.TilePx;
                int dy = (H - ty - 1) * GameConfig.TilePx;
                for (int y = 0; y < GameConfig.TilePx; y++)
                {
                    int srcRow = (sy + y) * atlas.width + sx;
                    int dstRow = (dy + y) * gw + dx;
                    for (int x = 0; x < GameConfig.TilePx; x++)
                        px[dstRow + x] = atlasPx[srcRow + x];
                }
            }
        }

        var tex = new Texture2D(gw, gh, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels32(px);
        tex.Apply();

        var go = new GameObject(Layers.Ground);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, gw, gh), new Vector2(0.5f, 0.5f),
                                  GameConfig.TilePx, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = -10000;
        go.transform.position = new Vector3(W * 0.5f, H * 0.5f, 0f);
    }

    void CreateDecorObjects()
    {
        var root = new GameObject(Layers.Decor);
        root.transform.SetParent(transform, false);
        foreach (var d in Decor)
        {
            var sp = SpriteFactory.Tile(d.tile);
            if (sp == null) continue;
            var go = new GameObject("d_" + d.tx + "_" + d.ty);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(WorldX(d.tx), WorldY(d.ty), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = SortingForY(WorldY(d.ty));
        }
    }

    /// <summary>Y 排序：越靠下越靠前（sortingOrder 越大越在上层）</summary>
    public static int SortingForY(float worldY) => Mathf.RoundToInt(-worldY * 100f);
}
