using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 登录界面角色待机动画预览（挂在 UI Image 上）。
    /// - 多帧角色（女巫）：按 fps 轮播 idle 帧；
    /// - 单帧角色（农夫等）：RectTransform 上下轻浮（bob）。
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        public Image target;
        public float fps = 2f;

        [Header("单帧角色的浮动参数（UI 像素）")]
        public float bobPixels = 10f;
        public float bobPeriod = 1.6f;

        private Sprite[] _frames;
        private RectTransform _rt;
        private Vector2 _basePos;
        private float _t;

        private void Awake()
        {
            if (target == null) target = GetComponent<Image>();
            if (target != null) _rt = target.rectTransform;
            if (_rt != null) _basePos = _rt.anchoredPosition;
        }

        /// <summary>切换展示的角色。frames 为 null/空时隐藏。</summary>
        public void SetFrames(Sprite[] frames)
        {
            _frames = frames;
            _t = 0f;
            bool has = frames != null && frames.Length > 0 && frames[0] != null;
            if (target != null)
            {
                target.enabled = has;
                if (has) target.sprite = frames[0];
            }
        }

        private void Update()
        {
            if (target == null || !target.enabled) return;
            _t += Time.unscaledDeltaTime;

            if (_frames != null && _frames.Length > 1)
            {
                int i = Mathf.FloorToInt(_t * fps) % _frames.Length;
                var s = _frames[i];
                if (s != null && target.sprite != s) target.sprite = s;
                if (_rt != null) _rt.anchoredPosition = _basePos;
            }
            else if (_rt != null && bobPeriod > 0.01f)
            {
                float y = Mathf.Sin(_t * Mathf.PI * 2f / bobPeriod) * bobPixels;
                _rt.anchoredPosition = _basePos + new Vector2(0f, y);
            }
        }
    }
}
