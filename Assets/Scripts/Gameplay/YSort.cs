using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 俯视角 2D 的 Y 轴深度排序：位置越靠下（y 越小）越晚渲染（盖住上方物件）。
    /// 挂在会移动的物件上（如玩家）每帧刷新；静止物件在生成场景时由
    /// GameSceneBuilder 按同一公式（offset - y * multiplier）烘焙排序值。
    /// </summary>
    [DisallowMultipleComponent]
    public class YSort : MonoBehaviour
    {
        [Tooltip("要排序的渲染器，留空则自动取子物体里的 SpriteRenderer")]
        public SpriteRenderer target;

        [Tooltip("每单位 y 对应的排序步进（与场景生成器的烘焙公式一致）")]
        public float multiplier = 10f;

        [Tooltip("排序基准偏移（与场景生成器一致：地图高 x multiplier）")]
        public int offset = 240;

        private void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            target.sortingOrder = offset + Mathf.RoundToInt(-transform.position.y * multiplier);
        }
    }
}
