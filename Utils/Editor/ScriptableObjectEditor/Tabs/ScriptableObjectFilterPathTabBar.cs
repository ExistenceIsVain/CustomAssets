using System;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectFilterPathTabBar
    {
        private const float FilterTabMinWidth = 64f;
        private const float FilterTabButtonWidth = 50f;
        private const float FilterTabRemoveButtonWidth = 65f;
        private const float FilterTabControlSpacing = 2f;

        public void Draw(
            ScriptableObjectFilterPathTabs filterPathTabs,
            Action<int> onSelectTab,
            Action<Rect> onAddTab,
            Action<int, Rect> onEditTab,
            Action<int> onRemoveTab,
            Action<int, Rect> onTabContextMenu,
            Action<Rect> registerContextBlocker)
        {
            Rect panelRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                EditorStyles.toolbar,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (Event.current.type == EventType.Repaint)
                EditorStyles.toolbar.Draw(panelRect, GUIContent.none, false, false, false, false);

            float buttonAreaWidth = FilterTabButtonWidth * 2f +
                                    FilterTabRemoveButtonWidth +
                                    FilterTabControlSpacing * 2f;
            float buttonStartX = panelRect.xMax - buttonAreaWidth;
            float tabMaxX = buttonStartX - FilterTabControlSpacing * 2f;
            float x = panelRect.x;

            bool isRightMouseDown = Event.current.type == EventType.MouseDown && Event.current.button == 1;

            for (int i = 0; i < filterPathTabs.Tabs.Count; i++)
            {
                var tab = filterPathTabs.Tabs[i];
                var content = new GUIContent(tab.Name, tab.Path);
                float tabWidth = Mathf.Max(FilterTabMinWidth, EditorStyles.toolbarButton.CalcSize(content).x + 12f);

                if (x + tabWidth > tabMaxX)
                    tabWidth = Mathf.Max(0f, tabMaxX - x);

                if (tabWidth <= 0f)
                    break;

                Rect tabRect = new Rect(x, panelRect.y, tabWidth, panelRect.height);
                bool mouseIsOverTab = tabRect.Contains(Event.current.mousePosition);
                registerContextBlocker?.Invoke(tabRect);

                if (isRightMouseDown && mouseIsOverTab)
                {
                    onSelectTab?.Invoke(i);
                    onTabContextMenu?.Invoke(i, tabRect);
                    Event.current.Use();
                }

                bool isSelected = i == filterPathTabs.SelectedIndex;
                bool clicked = GUI.Toggle(tabRect, isSelected, content, EditorStyles.toolbarButton);

                if (clicked && !isSelected)
                    onSelectTab?.Invoke(i);

                x += tabWidth;
            }

            Rect addRect = new Rect(buttonStartX, panelRect.y, FilterTabButtonWidth, panelRect.height);
            Rect editRect = new Rect(addRect.xMax + FilterTabControlSpacing, panelRect.y, FilterTabButtonWidth, panelRect.height);
            Rect removeRect = new Rect(editRect.xMax + FilterTabControlSpacing, panelRect.y, FilterTabRemoveButtonWidth, panelRect.height);

            registerContextBlocker?.Invoke(addRect);
            registerContextBlocker?.Invoke(editRect);
            registerContextBlocker?.Invoke(removeRect);

            if (GUI.Button(addRect, "Add", EditorStyles.toolbarButton))
                onAddTab?.Invoke(addRect);

            using (new EditorGUI.DisabledScope(filterPathTabs.Tabs.Count == 0))
            {
                if (GUI.Button(editRect, "Edit", EditorStyles.toolbarButton))
                    onEditTab?.Invoke(filterPathTabs.SelectedIndex, editRect);
            }

            using (new EditorGUI.DisabledScope(filterPathTabs.Tabs.Count <= 1))
            {
                if (GUI.Button(removeRect, "Remove", EditorStyles.toolbarButton))
                    onRemoveTab?.Invoke(filterPathTabs.SelectedIndex);
            }
        }
    }
}
