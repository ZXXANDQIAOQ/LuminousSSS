// 运行时 sprite 工厂：把 Resources 里的图集切成瓦片 sprite（无需美术资源导入切片）
using System.Collections.Generic;
using UnityEngine;

public static class SpriteFactory
{
    static Texture2D _tileAtlas;
    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    static Texture2D TileAtlas
    {
        get
        {
            if (_tileAtlas == null) _tileAtlas = Resources.Load<Texture2D>("Art/tilemap");
            return _tileAtlas;
        }
    }

    /// <summary>取图集瓦片（行自顶部数），PPU=16 即 1 瓦片 = 1 世界单位</summary>
    public static Sprite Tile(int col, int row)
    {
        return Tile(new TileRef(col, row));
    }

    public static Sprite Tile(TileRef t)
    {
        if (!t.Valid) return null;
        string key = "t" + t.C + "_" + t.R;
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var atlas = TileAtlas;
        if (atlas == null) return null;
        int aH = atlas.height;
        var rect = new Rect(t.C * GameConfig.TilePx, aH - (t.R + 1) * GameConfig.TilePx,
                            GameConfig.TilePx, GameConfig.TilePx);
        var sp = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f),
                               GameConfig.TilePx, 0, SpriteMeshType.FullRect);
        _cache[key] = sp;
        return sp;
    }

    /// <summary>取角色 sprite：按目标世界高度换算 PPU</summary>
    public static Sprite Hero(string name, float worldHeight)
    {
        string key = "hero_" + name;
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var tex = Resources.Load<Texture2D>("Art/" + name);
        if (tex == null) return null;
        float ppu = tex.height / worldHeight;
        var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                               new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
        _cache[key] = sp;
        return sp;
    }

    /// <summary>纯色 / 渐变 sprite（UI 用）</summary>
    public static Sprite Solid(Color c)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < 16; i++) px[i] = c;
        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
    }

    /// <summary>竖直渐变（登录界面背景）</summary>
    public static Sprite VerticalGradient(Color top, Color bottom, int h = 128)
    {
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        var px = new Color32[4 * h];
        for (int y = 0; y < h; y++)
        {
            Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
            for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
        }
        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
    }

    /// <summary>椭圆阴影</summary>
    public static Sprite Ellipse(Color c, int w = 32, int h = 12)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / cx, dy = (y - cy) / cy;
                px[y * w + x] = (dx * dx + dy * dy <= 1f) ? c : new Color(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32, 0, SpriteMeshType.FullRect);
    }
}
