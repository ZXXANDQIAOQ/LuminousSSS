using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 场景传送门：农场木屋门口「进入」、房间楼梯「上楼 / 下楼」、室内大门「出门」都用它。
    ///
    /// 带一个 isTrigger 的 BoxCollider2D 作为接近检测区 —— 不是碰撞体，玩家可以正常走过去，
    /// 只是 PlayerInteractor 的 OverlapCircle 会扫到它，从而让 HUD 按钮亮起并显示 prompt。
    ///
    /// 为什么用「靠近 + 交互键」而不是「踩上去就传送」：
    ///   踩上去就切场景，玩家路过门口会被打断（星露谷也是按确认，不自动进门）；
    ///   而且手机端误触率更高，多一次确认更稳。
    /// </summary>
    [DisallowMultipleComponent]
    public class ScenePortal : MonoBehaviour, IInteractable
    {
        [Header("显示")]
        [Tooltip("HUD 交互按钮上的动词：进入 / 上楼 / 下楼 / 出门")]
        public string prompt = "进入";

        [Header("目标")]
        [Tooltip("目标场景名（必须是 Build Settings 里的场景）")]
        public string targetScene = "";

        [Tooltip("目标场景里的出生点 id（PlayerSpawnPoint.id）")]
        public string spawnPointId = GameData.SpawnDefault;

        [Header("反馈")]
        [Tooltip("传送前播一声确认音（开门感）。没配 AudioManager 时静默跳过")]
        public bool playSfx = true;

        public string Prompt => prompt;

        public bool CanInteract
        {
            get
            {
                // 传送途中锁住：黑场还没结束就再点一次会重入
                if (SceneFlow.Busy) return false;
                return isActiveAndEnabled && !string.IsNullOrEmpty(targetScene);
            }
        }

        public void Interact(PlayerInteractor who)
        {
            if (!CanInteract) return;
            if (playSfx) AudioManager.Confirm();
            SceneFlow.Travel(targetScene, spawnPointId);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.35f);
            if (box != null)
            {
                var c = transform.position + (Vector3)box.offset;
                Gizmos.DrawWireCube(c, new Vector3(box.size.x, box.size.y, 0.1f));
            }
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
