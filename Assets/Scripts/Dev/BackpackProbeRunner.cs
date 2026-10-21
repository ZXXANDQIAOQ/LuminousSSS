#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LuminousSSS
{
    /// <summary>
    /// 背包系统的运行时验证（v0.16）。由 <see cref="RuntimeProbe"/> 写的 flag 文件触发。
    ///
    /// 验证的是**逻辑**而不是画面：面板开关、timeScale、格子数量、选中种子优先、
    /// 背包容量上限、与商店的互斥。批处理模式没有渲染，像素效果看不到，
    /// 但「玩家会不会卡住 / 会不会丢东西」这些问题都能跑出来。
    ///
    /// 会临时重置存档，所以开头备份、结尾还原 —— 手工玩过的进度不能被一次探针冲掉。
    /// 另外挂了一个 90 秒硬超时：中途出异常时也要自己退出，
    /// 否则 batchmode 进程会一直挂着等外部 timeout（踩过一次）。
    /// </summary>
    public class BackpackProbeRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string flag = RuntimeProbe.FlagPath;
            if (!File.Exists(flag)) return;      // 普通 Play 不动它
            File.Delete(flag);

            var go = new GameObject("[BackpackProbe]");
            DontDestroyOnLoad(go);
            go.AddComponent<BackpackProbeRunner>();
        }

        readonly StringBuilder _sb = new StringBuilder();
        int _pass, _fail;
        bool _done;

        void Start()
        {
            StartCoroutine(Watchdog(90f));
            StartCoroutine(Run());
        }

        IEnumerator Watchdog(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!_done && Time.realtimeSinceStartup < deadline) yield return null;
            if (!_done)
            {
                _sb.AppendLine("[FAIL] 探针在 " + seconds + "s 内没跑完，强制退出");
                Finish(2);
            }
        }

        IEnumerator Run()
        {
            var backup = BackupPrefs();

            yield return null;
            yield return null;

            Check("当前场景是农场",
                  UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameData.SceneFarm,
                  UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            // ---- 1. HUD 上有背包按钮
            var btn = FindObjectOfType<BackpackButton>();
            Check("HUD 存在 BackpackButton", btn != null, btn != null ? btn.name : "没找到");

            if (btn != null) btn.OnClickOpen();
            yield return null;

            Check("点按钮后背包打开", BackpackPanel.IsOpen, "IsOpen");
            Check("打开时冻结时间", Mathf.Approximately(Time.timeScale, 0f), "timeScale=" + Time.timeScale);

            // ---- 2. 格子数量（读私有字段，不为测试污染生产代码）
            var panel = FindObjectOfType<BackpackPanel>();
            var grid = GetPrivate<RectTransform>(panel, "_gridRoot");
            Check("格子总数为 24", grid != null && grid.childCount == Inventory.SlotCount,
                  grid != null ? grid.childCount.ToString() : "grid 为空");
            Check("6x4 与容量常量一致", 6 * 4 == Inventory.SlotCount, "SlotCount=" + Inventory.SlotCount);

            // ---- 3. 造数据后刷新
            Inventory.ResetAll();
            Inventory.Add("seed_carrot", 3);
            Inventory.Add("seed_radish", 2);
            Inventory.Add("carrot", 4);
            Inventory.Add("fish_gold", 1);
            BackpackPanel.Open();          // 重开一次触发 Refresh
            yield return null;

            Check("4 种物品占 4 格", Inventory.UsedSlots == 4, "UsedSlots=" + Inventory.UsedSlots);
            Check("刷新后格子仍是 24 个", grid != null && grid.childCount == 24,
                  grid != null ? grid.childCount.ToString() : "grid 为空");

            // ---- 4. 选中的种子优先于「物品表里第一包」
            // 胡萝卜在表里排在萝卜前面：不选的话必定种胡萝卜，
            // 这样才证明「选萝卜」真的改变了播种结果。
            Inventory.SelectSeed("seed_radish");
            var spot = PlantSpot.SpawnAt(5, 5, 0);
            spot.Interact(null);
            yield return null;

            Check("播种消耗的是选中的萝卜种子", Inventory.Count("seed_radish") == 1,
                  "萝卜=" + Inventory.Count("seed_radish"));
            Check("胡萝卜没被动过", Inventory.Count("seed_carrot") == 3,
                  "胡萝卜=" + Inventory.Count("seed_carrot"));

            // ---- 5. 背包满：装不下就拒绝，不能悄悄丢东西
            Inventory.ResetAll();
            for (int i = 0; i < Inventory.SlotCount; i++) Inventory.Add("probe_slot_" + i, 1);
            Check("填满后 IsFull 为真", Inventory.IsFull, "Used=" + Inventory.UsedSlots);
            Check("满了以后 CanAdd 返回 false", !Inventory.CanAdd("probe_extra", 1), "CanAdd");
            Check("满了以后 Add 实际加入 0 个", Inventory.Add("probe_extra", 1) == 0, "Add");

            // ---- 6. 单格堆叠上限
            Inventory.ResetAll();
            Inventory.Add("carrot", Inventory.StackMax + 10);
            Check("单格不超过堆叠上限", Inventory.Count("carrot") == Inventory.StackMax,
                  "carrot=" + Inventory.Count("carrot"));
            Check("堆叠满后 CanAdd 为 false", !Inventory.CanAdd("carrot", 1), "CanAdd");

            // ---- 7. 关闭恢复时间
            BackpackPanel.Close();
            yield return null;
            Check("关闭后 IsOpen 为假", !BackpackPanel.IsOpen, "IsOpen");
            Check("关闭后时间恢复", Mathf.Approximately(Time.timeScale, 1f), "timeScale=" + Time.timeScale);

            // ---- 8. 与商店互斥（两个面板叠一起会让时间管理打架）
            BackpackPanel.Open();
            ShopPanel.Open("探针");
            yield return null;
            Check("打开商店时背包自动关掉", !BackpackPanel.IsOpen, "Backpack.IsOpen");
            Check("商店此时是开着的", ShopPanel.IsOpen, "Shop.IsOpen");
            Check("模态时间仍冻结", Mathf.Approximately(Time.timeScale, 0f), "timeScale=" + Time.timeScale);

            ShopPanel.Close();
            yield return null;
            Check("都关掉后时间恢复", Mathf.Approximately(Time.timeScale, 1f), "timeScale=" + Time.timeScale);

            // ---- 收尾：清掉探针造的东西，还原玩家存档
            if (spot != null)
            {
                PlayerPrefs.DeleteKey(PlantSpot.PrefPrefix + "5_5");
                Destroy(spot.gameObject);
            }
            backup.Restore();

            Finish(_fail == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------ 小工具

        void Check(string name, bool ok, string detail)
        {
            if (ok) _pass++;
            else _fail++;

            string line = (ok ? "[ OK ] " : "[FAIL] ") + name + "   (" + detail + ")";
            _sb.AppendLine(line);
            Debug.Log("[Probe] " + line);
        }

        static T GetPrivate<T>(object target, string field) where T : class
        {
            if (target == null) return null;
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(target) as T;
        }

        /// <summary>存档备份。金币是 int 键，其余是 string 键 —— 混用 GetString / SetString 会把类型写坏。</summary>
        class PrefBackup
        {
            public bool hasGold;
            public int gold;
            public string items, seed, character;

            public void Restore()
            {
                if (hasGold) PlayerPrefs.SetInt(Inventory.PrefGold, gold);
                else PlayerPrefs.DeleteKey(Inventory.PrefGold);

                SetOrClear(Inventory.PrefItems, items);
                SetOrClear(Inventory.PrefSelectedSeed, seed);
                SetOrClear(GameData.PrefCharacterId, character);
                PlayerPrefs.Save();
            }

            static void SetOrClear(string key, string value)
            {
                if (value == null) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetString(key, value);
            }
        }

        static PrefBackup BackupPrefs()
        {
            string Read(string key) =>
                PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "") : null;

            return new PrefBackup
            {
                hasGold = PlayerPrefs.HasKey(Inventory.PrefGold),
                gold = PlayerPrefs.GetInt(Inventory.PrefGold, 0),
                items = Read(Inventory.PrefItems),
                seed = Read(Inventory.PrefSelectedSeed),
                character = Read(GameData.PrefCharacterId),
            };
        }

        void Finish(int exitCode)
        {
            if (_done) return;
            _done = true;

            _sb.Insert(0, $"背包系统运行时验证（v0.16）\n通过 {_pass} · 失败 {_fail}\n\n");
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeProbe.ReportPath));
            File.WriteAllText(RuntimeProbe.ReportPath, _sb.ToString(), Encoding.UTF8);

            Debug.Log($"[Probe] 完成：{_pass} 通过 / {_fail} 失败 → {RuntimeProbe.ReportPath}");

            // 编辑器侧在 Play 里收不到 update 回调，只能自己退
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
