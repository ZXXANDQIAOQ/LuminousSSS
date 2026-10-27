using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 上鱼结算卡片：鱼图标 + 星标 + 名称 + 重量 + 售价，破纪录时多一行红色「新纪录！」。
    ///
    /// 为什么不复用 <see cref="Toast"/>：
    ///   Toast 是一行纯文字，钓到一条 ★5 的湖主和「背包只装下 1 个」用同一种呈现太亏；
    ///   而且这里要摆图标、星标和多行数字，必须有自己的排版。
    ///
    /// 刻意**不**做模态（没有遮罩、不改 timeScale、所有图 raycastTarget = false）：
    ///   结算是一段奖励演出，玩家这时候应该能立刻继续走动 / 继续抛竿。
    ///   做成模态会再引入一处「timeScale 与输入锁」的配对风险 —— 而这块地方已经够敏感了。
    ///
    /// 层级 280：压在 QTE 面板(250) 之上、Toast(300) 之下。
    /// 时间基准一律 unscaledDeltaTime：卡片可能在其他面板冻着时间时被弹出来（例如商店里卖鱼）。
    /// </summary>
    public class FishingCatchCard : MonoBehaviour
    {
        const int SortingOrder = 280;
        const float CardW = 620f;
        const float CardH = 300f;

        static FishingCatchCard _inst;

        [Header("节奏（秒）")]
        [Tooltip("淡入时长")]
        public float fadeIn = 0.2f;

        [Tooltip("停留时长")]
        public float hold = 1.4f;

        [Tooltip("淡出时长")]
        public float fadeOut = 0.3f;

        [Tooltip("入场时的缩放脉冲幅度（1.0 = 不做）")]
        public float popScale = 0.12f;

        CanvasGroup _group;
        RectTransform _cardRt;
        Image _icon;
        Text _nameText, _starsText, _weightText, _priceText, _recordText;

        Coroutine _running;

        // ================================================================
        // 对外入口
        // ================================================================

        /// <summary>
        /// 弹一张结算卡。<paramref name="isRecord"/> 为真时多一行「新纪录！」。
        /// 连钓两条时后一条直接顶掉前一条（和 Toast 一致：不做队列，避免积压把玩家淹了）。
        /// </summary>
        public static void Show(string fishId, float weight, bool isRecord)
        {
            if (string.IsNullOrEmpty(fishId)) return;

            if (_inst == null)
            {
                var go = new GameObject("[FishingCatchCard]");
                Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<FishingCatchCard>();
                _inst.Build();
            }

            _inst.Fill(fishId, weight, isRecord);
        }

        // ================================================================
        // 内容
        // ================================================================

        void Fill(string fishId, float weight, bool isRecord)
        {
            var def = ItemDatabase.Find(fishId);
            int rarity = def != null ? Mathf.Clamp(def.rarity, 1, 5) : 1;

            _icon.sprite = ItemDatabase.IconOf(fishId);
            _icon.enabled = _icon.sprite != null;      // 素材缺失时留白，别显示成白方块
            _nameText.text = ItemDatabase.NameOf(fishId);
            _starsText.text = new string('★', rarity);
            _weightText.text = $"{weight:F2} kg";
            _priceText.text = def != null && def.sellPrice > 0
                ? $"售价 {def.sellPrice} 金币"
                : "";

            // 新纪录单独一行红字：这是「同一杆还能再刷一次」的唯一驱动力
            _recordText.gameObject.SetActive(isRecord);

            _group.gameObject.SetActive(true);
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PlayRoutine());
        }

        IEnumerator PlayRoutine()
        {
            // ---- 淡入 + 轻微上浮 + 缩放脉冲（固定用 unscaled）
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeIn);
                _group.alpha = k;
                _cardRt.localScale = Vector3.one * (1f + popScale * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            _group.alpha = 1f;
            _cardRt.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(hold);

            // ---- 淡出
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
                yield return null;
            }
            _group.alpha = 0f;
            _group.gameObject.SetActive(false);
            _running = null;
        }

        // ================================================================
        // 构建
        // ================================================================

        void Build()
        {
            var canvas = UiFactory.CreateCanvas(transform, "Canvas", SortingOrder);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;      // 不吃点击：卡片是演出，不是交互
            _group.interactable = false;

            // ---- 卡片底板（居中偏上：不挡屏幕下方的摇杆/交互按钮）
            var panel = UiFactory.MakePanel(canvas.transform, "Card",
                UiTheme.Instance != null ? UiTheme.Instance.panel : null,
                new Vector2(CardW, CardH));
            var rt = panel.GetComponent<RectTransform>();
            UiFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(CardW, CardH), new Vector2(0f, 180f));
            panel.raycastTarget = false;
            _cardRt = rt;

            // ---- 图标（左边一张大的）
            _icon = UiFactory.MakeIcon(rt, null, new Vector2(150f, 150f), new Vector2(-200f, 20f));
            _icon.preserveAspect = true;

            // ---- 文本列（右边）
            _nameText = UiFactory.MakeText(rt, "", 46, TextAnchor.MiddleLeft,
                new Color32(255, 243, 208, 255));
            UiFactory.Place(_nameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(340f, 56f), new Vector2(-90f, 80f));

            _starsText = UiFactory.MakeText(rt, "", 34, TextAnchor.MiddleLeft,
                new Color32(255, 206, 92, 255));
            UiFactory.Place(_starsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(340f, 44f), new Vector2(-90f, 26f));

            _weightText = UiFactory.MakeText(rt, "", 34, TextAnchor.MiddleLeft,
                new Color32(220, 235, 220, 255));
            UiFactory.Place(_weightText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(340f, 44f), new Vector2(-90f, -24f));

            _priceText = UiFactory.MakeText(rt, "", 30, TextAnchor.MiddleLeft,
                new Color32(255, 214, 120, 255));
            UiFactory.Place(_priceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(340f, 40f), new Vector2(-90f, -70f));

            // ---- 新纪录（跨底边一行，红色高亮）
            _recordText = UiFactory.MakeText(rt, "新纪录！", 34, TextAnchor.MiddleCenter,
                new Color32(255, 96, 84, 255));
            UiFactory.Place(_recordText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(CardW - 80f, 44f), new Vector2(0f, 26f));
            // 深色描边：羊皮纸底上纯红字对比度不够，描一圈深棕才看得清
            var outline = _recordText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.29f, 0.12f, 0.05f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            _recordText.gameObject.SetActive(false);
            _group.gameObject.SetActive(false);
        }
    }
}
