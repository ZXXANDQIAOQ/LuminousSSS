using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 按登录界面保存的选择，替换场景内玩家的 Sprite。
    /// 若该角色带 AnimatorController（如女巫），改用动画；否则停用 Animator、只换静态图。
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
            public RuntimeAnimatorController controller;   // 可选：有则播放动画
        }

        public SpriteRenderer target;
        public Animator animator;
        public Entry[] entries;

        private void Start()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            string id = GameData.SelectedCharacterId;
            if (entries == null || target == null) return;
            foreach (var e in entries)
            {
                if (e.id != id) continue;
                if (e.sprite != null) target.sprite = e.sprite;
                if (animator != null)
                {
                    if (e.controller != null)
                    {
                        animator.runtimeAnimatorController = e.controller;
                        animator.enabled = true;
                    }
                    else
                    {
                        animator.enabled = false;   // 防止动画控制器把静态图刷成别人的帧
                    }
                }
                return;
            }
        }
    }

    /// <summary>正交相机平滑跟随玩家，并限制在地图范围内（设计文档 4.6）。</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.18f;

        [Header("边界（世界坐标，相机中心可到达的范围）")]
        public bool useBounds = true;
        public Vector2 min = new Vector2(-100f, -100f);
        public Vector2 max = new Vector2(100f, 100f);

        private Vector3 _vel;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            if (useBounds)
            {
                var cam = GetComponent<Camera>();
                float halfH = cam != null ? cam.orthographicSize : 0f;
                float halfW = halfH * (cam != null ? cam.aspect : 1.777f);
                desired.x = Mathf.Clamp(desired.x, min.x + halfW, max.x - halfW);
                desired.y = Mathf.Clamp(desired.y, min.y + halfH, max.y - halfH);
            }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
        }
    }
}
