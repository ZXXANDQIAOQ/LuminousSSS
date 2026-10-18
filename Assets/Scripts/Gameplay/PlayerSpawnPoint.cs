using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 场景里的「出生点」标记：从别的场景传送过来时，玩家被摆到这个位置。
    ///
    /// id 用语义名（Default / Stairs / Door）而不是坐标 —— 场景重建、微调家具位置时
    /// 只要出生点跟着走，传送逻辑不用改。SceneFlow 按 id 查找，找不到退回第一个。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("出生点标识。农场出门用 Door，一楼/二楼都各有一个 Default 与 Stairs")]
        public string id = GameData.SpawnDefault;

        [Tooltip("传送落地后玩家的朝向（主轴向量：上/下/左/右）")]
        public Vector2 facing = Vector2.down;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position,
                            transform.position + (Vector3)(facing * 0.8f));
        }
    }
}
