// 星星闪烁
using UnityEngine;
using UnityEngine.UI;

public class StarTwinkle : MonoBehaviour
{
    public float phase;
    public float speed = 1f;
    Image img;
    float baseAlpha;

    void Awake()
    {
        img = GetComponent<Image>();
        if (img != null) baseAlpha = img.color.a > 0.01f ? img.color.a : 1f;
    }

    void Update()
    {
        if (img == null) return;
        float a = 0.15f + 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.time * speed * 2f + phase));
        var c = img.color;
        img.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a) * baseAlpha);
    }
}
