using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class EditorContextClickGuard
    {
        private readonly List<Rect> _blockedRects = new();

        public void BeginFrame()
        {
            _blockedRects.Clear();
        }

        public void RegisterBlocker(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            _blockedRects.Add(rect);
        }

        public void HandleContextClick(Action<Rect> onContextClick)
        {
            if (Event.current.type != EventType.ContextClick || GUIUtility.hotControl != 0)
                return;

            Vector2 mousePosition = Event.current.mousePosition;

            if (IsMouseOverAnyRect(mousePosition))
                return;

            onContextClick?.Invoke(new Rect(mousePosition, Vector2.one));
            Event.current.Use();
        }

        private bool IsMouseOverAnyRect(Vector2 mousePosition)
        {
            foreach (var rect in _blockedRects)
            {
                if (rect.Contains(mousePosition))
                    return true;
            }

            return false;
        }
    }
}
