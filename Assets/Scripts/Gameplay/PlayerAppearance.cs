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
}
