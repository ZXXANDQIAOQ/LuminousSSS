using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// 场景间传送：黑场 → 异步加载 → 把玩家摆到出生点 → 淡入。
    ///
    /// 为什么要这层而不是直接 SceneManager.LoadScene：
    ///   1) 进屋/上楼瞬间硬切会闪一帧黑，手机上尤其难看 —— 这里自己控淡入淡出；
    ///   2) 目标场景里的玩家是构建器生成在固定坐标的，传送后必须按「从哪来」摆到门口/楼梯口，
    ///      否则从二楼下来会掉在一楼门口；
    ///   3) 相机要在摆位之后重新吸附，不然会从场景中心滑一段。
    ///
    /// 用法：<c>SceneFlow.Travel(GameData.SceneHouse1F, GameData.SpawnStairs)</c>。
    /// 跨场景常驻对象（[SceneFlow] + 全屏黑幕）由本类惰性创建，单个场景里不需要预置任何东西。
    /// </summary>
    public static class SceneFlow
    {
        public static bool Busy => _runner != null && _runner.Busy;

        private static SceneFlowRunner _runner;

        public static void Travel(string sceneName, string spawnPointId = GameData.SpawnDefault)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (_runner == null)
            {
                var go = new GameObject("[SceneFlow]");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<SceneFlowRunner>();
            }

            _runner.Go(sceneName, spawnPointId);
        }
    }

    /// <summary>SceneFlow 的执行体：持有黑幕 Canvas，跑加载协程。只有一个，随 DontDestroyOnLoad 常驻。</summary>
    public class SceneFlowRunner : MonoBehaviour
    {
        private const float FadeOutTime = 0.16f;
        private const float FadeInTime = 0.20f;

        public bool Busy { get; private set; }

        private Image _fade;

        private void Awake()
        {
            BuildFadeCanvas();
        }

        private void BuildFadeCanvas()
        {
            var canvasGO = new GameObject("Fader", typeof(Canvas));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;          // 压在整个 HUD 之上

            var imgGO = new GameObject("Black", typeof(Image));
            imgGO.transform.SetParent(canvasGO.transform, false);
            var rt = imgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;           // 全屏拉伸，不依赖分辨率与 CanvasScaler
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _fade = imgGO.GetComponent<Image>();
            _fade.color = new Color(0f, 0f, 0f, 0f);
            _fade.raycastTarget = true;           // 淡出期间吃掉触摸，防止连点触发第二次传送
            imgGO.SetActive(false);
        }

        public void Go(string sceneName, string spawnPointId)
        {
            if (Busy) return;
            StartCoroutine(Run(sceneName, spawnPointId));
        }

        private IEnumerator Run(string sceneName, string spawnPointId)
        {
            Busy = true;
            yield return Fade(0f, 1f, FadeOutTime);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneFlow] 目标场景无法加载：{sceneName}（是否漏了 Build Settings？）");
                yield return Fade(1f, 0f, FadeInTime);
                Busy = false;
                yield break;
            }
            while (!op.isDone) yield return null;

            // 新场景的 Awake 已跑；等一帧让 Start（CameraFollow / PlayerAppearance）也跑完再摆位
            yield return null;
            PlacePlayer(spawnPointId);
            yield return null;

            yield return Fade(1f, 0f, FadeInTime);
            Busy = false;
        }

        /// <summary>把玩家摆到目标场景里 id 匹配的出生点，并让相机立刻吸过去。</summary>
        private static void PlacePlayer(string spawnPointId)
        {
            var player = Object.FindObjectOfType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[SceneFlow] 目标场景里没有 PlayerController，出生点设置跳过");
                return;
            }

            var spawn = FindSpawn(spawnPointId);
            if (spawn == null)
            {
                Debug.LogWarning($"[SceneFlow] 目标场景没有 PlayerSpawnPoint(id={spawnPointId})，玩家留在原位");
                return;
            }

            var pos = spawn.transform.position;
            pos.z = player.transform.position.z;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = pos;          // 带 Rigidbody2D：同步物理位置，否则会被插值拉回
            player.transform.position = pos;
            player.SetFacing(spawn.facing);

            var follow = Object.FindObjectOfType<CameraFollow>();
            if (follow != null) follow.SnapNow();
        }

        private static PlayerSpawnPoint FindSpawn(string id)
        {
            var all = Object.FindObjectsOfType<PlayerSpawnPoint>();
            foreach (var p in all)
                if (p.id == id) return p;
            // 兜底：目标场景没配对应 id 时用第一个，至少不要把人丢在场景原点
            return all.Length > 0 ? all[0] : null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_fade == null) yield break;

            _fade.gameObject.SetActive(true);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k));
                yield return null;
            }
            _fade.color = new Color(0f, 0f, 0f, to);
            if (to <= 0.001f) _fade.gameObject.SetActive(false);
        }
    }
}
