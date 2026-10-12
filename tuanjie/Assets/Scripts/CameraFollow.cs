// 相机跟随 + 边界钳制 + 像素对齐（避免像素抖动）
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    Camera cam;

    public void Init(Transform followTarget)
    {
        target = followTarget;
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = GameConfig.CamOrthoSize;
        cam.backgroundColor = new Color(0.16f, 0.10f, 0.07f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = -10f;
        cam.farClipPlane = 10f;
        transform.position = new Vector3(target.position.x, target.position.y, -10f);
    }

    void LateUpdate()
    {
        if (target == null) return;
        float halfH = GameConfig.CamOrthoSize;
        float halfW = halfH * ((float)Screen.width / Mathf.Max(1, Screen.height));

        float x = target.position.x;
        float y = target.position.y - 0.4f;   // 略微下移，多看点前方的地面

        float minX = halfW, maxX = GameConfig.MapW - halfW;
        float minY = halfH, maxY = GameConfig.MapH - halfH;
        if (minX < maxX) x = Mathf.Clamp(x, minX, maxX); else x = GameConfig.MapW * 0.5f;
        if (minY < maxY) y = Mathf.Clamp(y, minY, maxY); else y = GameConfig.MapH * 0.5f;

        // 对齐到 1/16 单位（= 1 个美术像素），保证像素风不抖
        const float px = 1f / GameConfig.TilePx;
        x = Mathf.Round(x / px) * px;
        y = Mathf.Round(y / px) * px;

        transform.position = new Vector3(x, y, -10f);
    }
}
