using UnityEngine;

namespace Utils.Samples
{
    /// <summary>Мелочи для панелей демо: полоска прогресса и кнопка во всю ширину.</summary>
    public static class DemoGUI
    {
        /// <param name="value">0..1</param>
        public static void Bar(string label, float value)
        {
            Rect rect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);

            Rect fill = rect;
            fill.width *= Mathf.Clamp01(value);

            Color color = GUI.color;
            GUI.color = new Color(0.35f, 0.8f, 0.45f);
            GUI.Box(fill, GUIContent.none);
            GUI.color = color;

            GUI.Label(rect, $" {label}");
        }

        public static bool Button(string text) => GUILayout.Button(text, GUILayout.Height(24f));
    }
}
