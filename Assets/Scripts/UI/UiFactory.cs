using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 运行时铺 UI 的共用小工具（商店面板 / 背包面板 / 以后的对话框都用它）。
    ///
    /// 为什么 UI 全在运行时用代码搭、不做预制体：
    ///   商店与背包的内容完全由 <see cref="ItemDatabase"/> 和 <see cref="Inventory"/> 决定，
    ///   加一种作物不该要求回编辑器里手动复制一行 UI；代码铺行，加物品只改物品表。
    ///   预制体还得为「每个场景各放一份」或「做成常驻」多维护一层生命周期。
    ///
    /// 九宫格缩放的 <see cref="UiPpu"/>：Canvas 的 referencePixelsPerUnit 默认 100，
    /// 而 V2 素材是 PPU 64。乘 100/64 之后边框在 UI 里按素材原始像素 1:1 显示，
    /// 不会被拉成粗黑边（与登录界面 / HUD 的算法一致）。
    /// </summary>
    public static class UiFactory
    {
        public const float UiPpu = 100f / 64f;

        /// <summary>
        /// 建一个 ScreenSpaceOverlay 画布（1920×1080、按高度适配）并补一个 EventSystem。
        /// 场景里已有 EventSystem 时不会重复创建 —— 两个 EventSystem 会互相抢输入。
        /// </summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            return canvas;
        }

        /// <summary>全屏半透明遮罩：吃掉面板外的点击，防止误触后面的摇杆。</summary>
        public static Image MakeDim(Transform parent, float alpha = 0.55f)
        {
            var go = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), 0f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = true;      // 必须为 true，否则点击会穿透到场景
            return img;
        }

        /// <summary>居中的九宫格底板。</summary>
        public static Image MakePanel(Transform parent, string name, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiPpu;
            return img;
        }

        public static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>按左上/右上等角锚定摆放，省掉每次手写四行 anchor/pivot。</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot,
                                          Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static Text MakeText(Transform parent, string content, int size, TextAnchor align, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = GameData.GetUIFont();          // 内置字体没有中文字形，必须用系统动态字体
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button MakeButton(Transform parent, string label, Vector2 size,
                                        Action onClick, bool addLayout)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = UiTheme.Instance != null ? UiTheme.Instance.button : null;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UiPpu;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = ButtonColors();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            if (addLayout)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = size.x;
                le.preferredHeight = size.y;
            }

            var text = MakeText(go.transform, label, 32, TextAnchor.MiddleCenter,
                                new Color32(255, 243, 208, 255));
            Stretch(text.rectTransform, 4f);

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.29f, 0.15f, 0.03f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return btn;
        }

        public static ColorBlock ButtonColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1f, 0.95f, 0.86f);
            c.pressedColor = new Color(0.86f, 0.78f, 0.66f);
            c.disabledColor = new Color(0.62f, 0.58f, 0.52f, 0.6f);
            return c;
        }

        /// <summary>
        /// 页签底部的高亮条（选中的才显示）。
        ///
        /// 不用改 Image.color 来区分选中：Button 的 ColorTint 过渡每帧会把 color
        /// 写回 normalColor，手动压暗会被立刻覆盖 —— 挂一条独立的小图更稳。
        /// </summary>
        public static void AddTabUnderline(Button tab)
        {
            if (tab == null) return;
            var go = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(tab.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.09f, 0f);
            rt.anchorMax = new Vector2(0.91f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 7f);
            rt.anchoredPosition = new Vector2(0f, 9f);

            var img = go.GetComponent<Image>();
            img.color = new Color32(255, 206, 92, 255);
            img.raycastTarget = false;
        }

        public static void TintTab(Button tab, bool active)
        {
            if (tab == null) return;
            var line = tab.transform.Find("Underline");
            if (line != null) line.gameObject.SetActive(active);
        }

        /// <summary>居中摆放的图片（图标 / 装饰）。</summary>
        public static Image MakeIcon(Transform parent, Sprite sprite, Vector2 size, Vector2 offset = default)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }
    }
}
