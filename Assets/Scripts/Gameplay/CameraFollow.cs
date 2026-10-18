using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 正交相机平滑跟随玩家，并限制在地图范围内（设计文档 4.6）。
    ///
    /// v0.13 手感调整（用户反馈「进游戏就是全景，人物太小」）：
    ///   1) 视野从 20 格高收到 13 格（orthographicSize 10 → 6.5，见 GameSceneBuilder），
    ///      玩家占屏高从 8.8% 提到 13.5%，不再是「全景小人」；
    ///   2) 跟随点从脚底抬到胸口（+0.6 格），角色不贴画面下沿；
    ///   3) 朝向预判（look-ahead 1.2 格）：往哪走就往哪侧多看一点，
    ///      但用指数平滑收敛，转身不会左右甩；
    ///   4) 进场直接吸到目标，不再从场景中心滑过去；
    ///   5) 输出位置对齐 1/128 单位（≈ 半屏幕像素），压掉 Point 过滤下的像素游走；
    ///   6) 视野比地图大时居中，而不是让 Clamp 的 min/max 反相（旧写法会固定到错误位置）。
    ///
    /// 注意：本类必须单独放在 CameraFollow.cs 里。Unity 要求 MonoBehaviour 的类名与文件名
    /// 完全一致才能生成 MonoScript 资产；以前它和 PlayerAppearance 挤在同一个文件里，
    /// 序列化出来的 m_Script 只有 fileID、没有 guid，进场景就是 Missing Script。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;

        [Header("跟随")]
        [Tooltip("平滑时间（秒）。0.15 ≈ 5 格/秒下滞后 0.75 格，起步/停步有轻微缓动但不飘")]
        public float smoothTime = 0.15f;

        [Tooltip("跟随点相对玩家脚底的偏移。+0.6 = 抬到胸口，角色不贴画面下沿")]
        public Vector2 targetOffset = new Vector2(0f, 0.6f);

        [Tooltip("进场直接吸到目标位置，避免第一帧从场景中心滑过去")]
        public bool snapOnStart = true;

        [Header("朝向预判")]
        [Tooltip("朝移动方向前移的距离（世界单位 / 格）。0 = 关闭")]
        public float lookAhead = 1.2f;

        [Tooltip("预判偏移的收敛速度（越大越跟手，越小越柔）")]
        public float lookAheadSharpness = 4f;

        [Header("边界（世界坐标，相机中心可到达的范围）")]
        public bool useBounds = true;
        public Vector2 min = new Vector2(-100f, -100f);
        public Vector2 max = new Vector2(100f, 100f);

        [Header("像素对齐")]
        [Tooltip("位置量化网格（世界单位）。1/128 ≈ 半屏幕像素：压掉浮点噪声与像素游走，又不会跳格")]
        public float pixelGrid = 1f / 128f;

        private Vector3 _vel;
        private PlayerController _pc;
        private Vector2 _look;

        private void Start()
        {
            if (target != null) _pc = target.GetComponent<PlayerController>();

            if (snapOnStart && target != null)
            {
                _look = Vector2.zero;
                _vel = Vector3.zero;
                transform.position = ComputeDesired();
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 朝向预判：只取主轴朝向（PlayerController.Facing 已是单位主轴向量），
            // 指数平滑收敛 —— 线性 Lerp 的收敛速度随帧率变化，指数式与帧率无关。
            Vector2 wantLook = (_pc != null && _pc.IsMoving) ? _pc.Facing * lookAhead : Vector2.zero;
            float k = 1f - Mathf.Exp(-lookAheadSharpness * Time.deltaTime);
            _look = Vector2.Lerp(_look, wantLook, k);

            Vector3 desired = ComputeDesired();
            Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
            next.z = transform.position.z;   // z 只影响 2D 裁剪，别参与平滑

            if (pixelGrid > 0f)
            {
                next.x = Mathf.Round(next.x / pixelGrid) * pixelGrid;
                next.y = Mathf.Round(next.y / pixelGrid) * pixelGrid;
            }

            transform.position = next;
        }

        /// <summary>目标位置：玩家 + 胸口偏移 + 朝向预判，最后按地图边界 clamp。</summary>
        private Vector3 ComputeDesired()
        {
            var desired = new Vector3(target.position.x + targetOffset.x + _look.x,
                                      target.position.y + targetOffset.y + _look.y,
                                      transform.position.z);

            if (!useBounds) return desired;

            var cam = GetComponent<Camera>();
            float halfH = cam != null ? cam.orthographicSize : 0f;
            float halfW = halfH * (cam != null ? cam.aspect : 1.777f);

            // 视野比地图大：居中显示全图（Clamp 在 min>max 时会返回错误值，
            // 旧版 size=10 时地图宽 40 < 视野宽 35.6，中心就偏到 22.25 而不是 20）
            desired.x = halfW * 2f >= max.x - min.x
                ? (min.x + max.x) * 0.5f
                : Mathf.Clamp(desired.x, min.x + halfW, max.x - halfW);
            desired.y = halfH * 2f >= max.y - min.y
                ? (min.y + max.y) * 0.5f
                : Mathf.Clamp(desired.y, min.y + halfH, max.y - halfH);

            return desired;
        }
    }
}
