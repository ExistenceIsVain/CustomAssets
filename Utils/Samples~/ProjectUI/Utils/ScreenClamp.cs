using TMPro;
using UnityEngine;

namespace ProjectUI
{
    // Keeps pop-ups (tooltips, right-click menus, item lists) inside the screen after they are placed at a cursor or
    // a cell (2026-10-25). For screen-space overlay canvases, where world corners are screen pixels.
    public static class ScreenClamp
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        public static void KeepOnScreen(RectTransform rect, float margin = 4f)
        {
            if (rect == null) return;
            rect.GetWorldCorners(Corners);
            var min = Corners[0];
            var max = Corners[2];
            var shift = Vector3.zero;
            if (max.x > Screen.width - margin) shift.x = Screen.width - margin - max.x;
            if (min.x + shift.x < margin) shift.x = margin - min.x;
            if (max.y > Screen.height - margin) shift.y = Screen.height - margin - max.y;
            if (min.y + shift.y < margin) shift.y = margin - min.y;
            rect.position += shift;
        }

        // Grows or shrinks a box to fit its text (a fixed width, the height the wrapped text needs).
        public static void FitHeight(RectTransform box, TMP_Text text, float padding)
        {
            if (box == null || text == null) return;
            var width = text.rectTransform.rect.width;
            var height = text.GetPreferredValues(text.text, width, 0f).y;
            var size = text.rectTransform.sizeDelta;
            text.rectTransform.sizeDelta = new Vector2(size.x, height);
            box.sizeDelta = new Vector2(box.sizeDelta.x, height + padding);
        }
    }
}
