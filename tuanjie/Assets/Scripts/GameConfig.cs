// 全局配置与素材索引（对应网页版 CONFIG / TILES）
using UnityEngine;

public static class GameConfig
{
    public const int TilePx = 16;          // 图集瓦片边长（像素）
    public const int MapW = 64;            // 地图宽（瓦片）
    public const int MapH = 48;            // 地图高（瓦片）
    public const float PlayerSpeed = 3.625f;   // 单位/秒（= 网页 58px/s ÷ 16）
    public const float PlayerHeight = 1.25f;   // 主角显示高度（单位）
    public const float WalkFrameTime = 0.16f;
    public const float CamOrthoSize = 5.625f;  // 视野 11.25 单位高 = 180 世界像素
    public const int HouseX = 30;          // 小屋左上角（列）
    public const int HouseY = 10;          // 小屋左上角（行，自上而下）
    public const float BgmVolume = 0.5f;
}

// 图集里的瓦片坐标（列, 行 — 行自图集顶部数）
public struct TileRef
{
    public int C, R;
    public TileRef(int c, int r) { C = c; R = r; }
    public bool Valid => C >= 0 && R >= 0;
    public static readonly TileRef None = new TileRef(-1, -1);
}

public static class Tiles
{
    public static readonly TileRef DirtPlain  = new TileRef(3, 3);
    public static readonly TileRef DirtPlain2 = new TileRef(1, 2);
    public static readonly TileRef DirtTuft1  = new TileRef(4, 3);
    public static readonly TileRef DirtTuft2  = new TileRef(5, 3);
    public static readonly TileRef DirtTuft3  = new TileRef(6, 3);

    public static readonly TileRef TreeFallTop = new TileRef(3, 0);
    public static readonly TileRef TreeFallBot = new TileRef(3, 1);
    public static readonly TileRef TreePineTop = new TileRef(4, 0);
    public static readonly TileRef TreePineBot = new TileRef(4, 1);

    public static readonly TileRef Bush     = new TileRef(5, 0);
    public static readonly TileRef Mushroom = new TileRef(5, 2);
    public static readonly TileRef Rock     = new TileRef(9, 8);
    public static readonly TileRef Well     = new TileRef(8, 8);
    public static readonly TileRef Hay      = new TileRef(8, 7);
    public static readonly TileRef Sign     = new TileRef(11, 6);
    public static readonly TileRef FenceH   = new TileRef(9, 6);
    public static readonly TileRef FenceV   = new TileRef(11, 4);

    // 木屋：图集 (0..3, 4..7)
    public static TileRef House(int dx, int dy) => new TileRef(dx, 4 + dy);
}

public static class Layers
{
    public const string Ground = "Ground";
    public const string Decor = "Decor";
    public const string Player = "Player";
}
