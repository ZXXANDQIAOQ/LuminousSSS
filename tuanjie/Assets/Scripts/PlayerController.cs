// 玩家：脚底锚点、AABB 碰撞、行走动画、朝向翻转、影子
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public MapBuilder map;
    public InputRouter input;
    public string PlayerName = "旅人";

    SpriteRenderer sr;
    SpriteRenderer shadow;
    Sprite idle, walk;
    float animT;
    bool moving;

    const float HalfW = 5f / 16f;     // 碰撞盒半宽（与网页版一致）
    const float BoxTop = 7f / 16f;
    const float BoxBottom = 1f / 16f;

    public Vector2 Position => new Vector2(transform.position.x, transform.position.y);

    public void Init(MapBuilder mapBuilder, InputRouter router, string playerName)
    {
        map = mapBuilder;
        input = router;
        PlayerName = playerName;

        idle = SpriteFactory.Hero("hero_0", GameConfig.PlayerHeight);
        walk = SpriteFactory.Hero("hero_1", GameConfig.PlayerHeight);

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = idle;

        var sh = new GameObject("Shadow");
        sh.transform.SetParent(transform, false);
        sh.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        sh.transform.localScale = new Vector3(GameConfig.PlayerHeight * 0.65f, GameConfig.PlayerHeight * 0.2f, 1f);
        shadow = sh.AddComponent<SpriteRenderer>();
        shadow.sprite = SpriteFactory.Ellipse(new Color(0f, 0f, 0f, 0.25f));

        // 出生在房门口，整座小屋在视野内
        transform.position = new Vector3(GameConfig.HouseX + 1.5f, MapBuilder.WorldY(GameConfig.HouseY + 5), 0f);
    }

    bool Collides(Vector2 p)
    {
        float l = p.x - HalfW, r = p.x + HalfW;
        float b = p.y - BoxBottom, t = p.y + BoxTop;
        int x0 = Mathf.FloorToInt(l), x1 = Mathf.FloorToInt(r);
        int y0 = Mathf.FloorToInt(GameConfig.MapH - t);          // 世界 y → 行
        int y1 = Mathf.FloorToInt(GameConfig.MapH - b - 0.0001f);
        for (int ty = y0; ty <= y1; ty++)
        {
            for (int tx = x0; tx <= x1; tx++)
            {
                if (tx < 0 || ty < 0 || tx >= GameConfig.MapW || ty >= GameConfig.MapH) return true;
                if (map.Solid[ty, tx]) return true;
            }
        }
        return false;
    }

    void Update()
    {
        Vector2 dir = input != null ? input.Direction : Vector2.zero;
        float len = dir.magnitude;
        if (len > 1f) dir /= len;
        moving = len > 0.01f;

        if (moving)
        {
            if (dir.x < -0.01f) sr.flipX = true;
            else if (dir.x > 0.01f) sr.flipX = false;

            animT += Time.deltaTime;
            float step = GameConfig.PlayerSpeed * Time.deltaTime;
            Vector2 p = Position;
            Vector2 nx = new Vector2(p.x + dir.x * step, p.y);
            if (!Collides(nx)) p.x = nx.x;
            Vector2 ny = new Vector2(p.x, p.y + dir.y * step);
            if (!Collides(ny)) p.y = ny.y;
            transform.position = new Vector3(p.x, p.y, 0f);

            sr.sprite = (Mathf.FloorToInt(animT / GameConfig.WalkFrameTime) % 2 == 1 && walk != null) ? walk : idle;
        }
        else
        {
            animT = 0f;
            sr.sprite = idle;
        }

        int order = MapBuilder.SortingForY(transform.position.y);
        sr.sortingOrder = order;
        shadow.sortingOrder = order - 1;
    }
}
