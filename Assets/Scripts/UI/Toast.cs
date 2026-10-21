using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 屏幕上方的一行临时提示（「获得 蓝鱼 ×1」「金币不足」「咬钩了！」）。
    ///
    /// 为什么不用 UI 预制体：这类提示是纯运行时冒出来的，一条消息、一段淡出，
    /// 放预制体反而要维护一个"谁负责实例化"的引用链。这里惰性建一次 Canvas，
    /// 之后所有提示复用同一个 Text，新消息直接覆盖旧的（同时不会叠成一堆）。
    ///
    /// 层级：sortingOrder = 300，低于 SceneFlow 的黑幕（32000）—— 换场景时黑幕压住提示是对的。
    /// </summary>
    public static class Toast
    {
        const int SortingOrder = 300;

        static ToastView _view;

        public static void Show(string message, float seconds = 1.8f)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_view == null)
            {
                var go = new GameObject("[Toast]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Object.DontDestroyOnLoad(go);

                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;

                // 与 HUD 同一套缩放基准，保证在手机上字号看起来一致
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGO.transform.SetParent(go.transform, false);
                var rt = textGO.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // 顶部居中
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1400f, 90f);
                rt.anchoredPosition = new Vector2(0f, -130f);

                var text = textGO.GetComponent<Text>();
                text.font = GameData.GetUIFont();
                text.fontSize = 40;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 0.96f, 0.85f, 0f);
                text.raycastTarget = false;   // 不挡触摸
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;

                // 描边：亮背景（草地 / 木板）上暖白字也要看得清
                var outline = textGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.22f, 0.11f, 0.03f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);

                _view = go.AddComponent<ToastView>();
                _view.text = text;
            }

            _view.Play(message, seconds);
        }
    }

    /// <summary>Toast 的实际表现：换成新文字 → 淡入 → 停留 → 淡出。同一时刻只显示最新一条。</summary>
    public class ToastView : MonoBehaviour
    {
        public Text text;

        const float FadeIn = 0.12f;

        Coroutine _running;

        public void Play(string message, float holdSeconds)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(message, holdSeconds));
        }

        IEnumerator Run(string message, float hold)
        {
            text.text = message;

            // 淡入
            float t = 0f;
            while (t < FadeIn)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Clamp01(t / FadeIn));
                yield return null;
            }
            SetAlpha(1f);

            yield return new WaitForSecondsRealtime(hold);

            // 淡出
            const float fadeOut = 0.35f;
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(1f - Mathf.Clamp01(t / fadeOut));
                yield return null;
            }
            SetAlpha(0f);
            _running = null;
        }

        void SetAlpha(float a)
        {
            if (text == null) return;
            var c = text.color;
            c.a = a;
            text.color = c;
        }
    }
}
