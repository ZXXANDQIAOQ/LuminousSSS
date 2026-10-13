using UnityEngine;
using UnityEngine.UI;

namespace LuminousSSS
{
    /// <summary>
    /// UI 文本用动态系统字体，保证「农夫 / 雨女 / 冒险者」等中文正常显示
    /// （内置的 LegacyRuntime.ttf 不含中文字形）。
    /// 动态字体对象无法序列化进场景，所以必须在运行时赋值。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UIFontFixer : MonoBehaviour
    {
        private void Awake()
        {
            var font = GameData.GetUIFont();
            if (font == null)
            {
                Debug.LogWarning("[LuminousSSS] 没找到可用的中文字体，UI 文字可能显示为方块。");
                return;
            }
            foreach (var t in GetComponentsInChildren<Text>(true))
                t.font = font;
        }
    }
}
