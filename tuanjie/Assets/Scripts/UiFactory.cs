// 运行时创建 uGUI 元素的小工具（避免在编辑器里手摆 UI）
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UiFactory
{
    public static Font PixelFont => Resources.Load<Font>("Fonts/PressStart2P");
    public static Font CjkFont => Resources.Load<Font>("Fonts/NotoSansSC");

    public static Canvas CreateCanvas(string name, Vector2 reference, int sortingOrder = 0)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = reference;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }
        return canvas;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = SpriteFactory.Solid(Color.white);
        return img;
    }

    public static RectTransform Rect(Component c) => c.GetComponent<RectTransform>();

    public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
        return rt;
    }

    public static Text CreateText(Transform parent, string name, string content, int size, Color color,
                                  Font font, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.font = font != null ? font : CjkFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button CreateButton(Transform parent, string name, Color bg, Color textColor, string label,
                                      int fontSize, Font font, Vector2 size, Vector2 position)
    {
        var img = CreateImage(parent, name, bg);
        var rt = Rect(img);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var shadow = CreateImage(parent, name + "Shadow", new Color(0.64f, 0.38f, 0.04f, 1f));
        var srt = Rect(shadow);
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = size;
        srt.anchoredPosition = position - new Vector2(0f, 8f);
        shadow.transform.SetSiblingIndex(img.transform.GetSiblingIndex());

        var text = CreateText(img.transform, "Label", label, fontSize, textColor, font);
        Stretch(Rect(text));
        return btn;
    }

    public static InputField CreateInputField(Transform parent, string name, string placeholder,
                                              int fontSize, Font font, Vector2 size, Vector2 position)
    {
        var bg = CreateImage(parent, name, new Color(0.086f, 0.059f, 0.125f, 1f));
        var rt = Rect(bg);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        var frame = CreateImage(parent, name + "Frame", new Color(0.33f, 0.23f, 0.48f, 1f));
        var frt = Rect(frame);
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = size + new Vector2(12f, 12f);
        frt.anchoredPosition = position;
        frame.transform.SetSiblingIndex(bg.transform.GetSiblingIndex());

        var input = bg.gameObject.AddComponent<InputField>();
        input.targetGraphic = bg;

        var text = CreateText(bg.transform, "Text", "", fontSize, new Color(0.94f, 0.90f, 1f), font, TextAnchor.MiddleLeft);
        var trt = Stretch(Rect(text), 18f, 18f, 0f, 0f);
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        var ph = CreateText(bg.transform, "Placeholder", placeholder, fontSize,
                            new Color(0.43f, 0.36f, 0.58f), font, TextAnchor.MiddleLeft);
        Stretch(Rect(ph), 18f, 18f, 0f, 0f);
        ph.fontStyle = FontStyle.Italic;

        input.textComponent = text;
        input.placeholder = ph;
        return input;
    }
}
