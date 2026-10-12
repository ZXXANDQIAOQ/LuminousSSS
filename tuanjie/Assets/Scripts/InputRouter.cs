// 输入：键盘（编辑器/PC 调试）+ 触屏虚拟摇杆（左半屏）
using UnityEngine;
using UnityEngine.UI;

public class InputRouter : MonoBehaviour
{
    public Canvas canvas;

    RectTransform baseRect, knobRect;
    Vector2 joyCenter, joyValue;
    bool joyActive;
    int joyFingerId = -1;
    const float BaseRadius = 105f;   // 画布像素
    const float KnobRadius = 46f;
    const float KnobTravel = 74f;

    public Vector2 Direction
    {
        get
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (joyActive) { x += joyValue.x; y += joyValue.y; }
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }

    public void Init(Canvas targetCanvas)
    {
        canvas = targetCanvas;
        baseRect = CreateCircle("JoyBase", new Color(1f, 1f, 1f, 0.22f), BaseRadius * 2f);
        knobRect = CreateCircle("JoyKnob", new Color(1f, 1f, 1f, 0.55f), KnobRadius * 2f);
        SetVisible(false);
    }

    RectTransform CreateCircle(string name, Color color, float size)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = SpriteFactory.Ellipse(Color.white, 64, 64);
        img.color = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        return rt;
    }

    void SetVisible(bool v)
    {
        if (baseRect != null) baseRect.gameObject.SetActive(v);
        if (knobRect != null) knobRect.gameObject.SetActive(v);
    }

    void PlaceAt(Vector2 screenPos)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenPos, null, out var local))
        {
            baseRect.anchoredPosition = local;
            knobRect.anchoredPosition = local;
            joyCenter = local;
        }
    }

    void Update()
    {
        // —— 触屏 ——
        bool handled = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began && !joyActive && t.position.x < Screen.width * 0.5f)
            {
                joyActive = true;
                joyFingerId = t.fingerId;
                PlaceAt(t.position);
                joyValue = Vector2.zero;
                SetVisible(true);
            }
            else if (joyActive && t.fingerId == joyFingerId)
            {
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    joyActive = false;
                    joyFingerId = -1;
                    SetVisible(false);
                }
                else
                {
                    UpdateKnob(t.position);
                }
            }
            handled = true;
        }

        // —— 编辑器：鼠标模拟 ——
        if (!handled && !joyActive)
        {
            if (Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width * 0.5f)
            {
                joyActive = true;
                PlaceAt(Input.mousePosition);
                joyValue = Vector2.zero;
                SetVisible(true);
            }
            else if (joyActive && Input.GetMouseButton(0))
            {
                UpdateKnob(Input.mousePosition);
            }
            else if (joyActive && Input.GetMouseButtonUp(0))
            {
                joyActive = false;
                SetVisible(false);
            }
        }
    }

    void UpdateKnob(Vector2 screenPos)
    {
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(null, canvas.transform.TransformPoint(joyCenter));
        Vector2 delta = (screenPos - screenCenter) / canvas.scaleFactor;
        Vector2 v = delta / KnobTravel;
        if (v.magnitude > 1f) v = v.normalized;
        joyValue = v;
        knobRect.anchoredPosition = joyCenter + v * KnobTravel;
    }
}
