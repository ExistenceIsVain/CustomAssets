using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectListPanel
    {
        private const float IndentWidth = 14f;

        private readonly Dictionary<string, bool> _foldouts = new();
        private Vector2 _scrollList;

        public string SearchQuery { get; set; } = "";

        public void Draw(
            float width,
            ScriptableObjectAssetCatalogResult catalog,
            ScriptableObject selectedSO,
            Action<ScriptableObject> onSelect,
            Action<Rect> registerContextBlocker)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Scriptable Objects ({catalog.ScriptableObjects.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand All", EditorStyles.miniButtonLeft))
                SetAllFoldouts(catalog.Groups, true);
            if (GUILayout.Button("Collapse All", EditorStyles.miniButtonRight))
                SetAllFoldouts(catalog.Groups, false);
            GUILayout.EndHorizontal();
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            _scrollList = GUILayout.BeginScrollView(_scrollList);

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                DrawSearchResults(catalog.ScriptableObjects, selectedSO, onSelect, registerContextBlocker);
            }
            else
            {
                DrawGroupedList(catalog.Groups, selectedSO, onSelect, registerContextBlocker);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void SetAllFoldouts(IReadOnlyList<ScriptableObjectGroup> groups, bool expanded)
        {
            foreach (var group in groups)
            {
                _foldouts[group.Name] = expanded;

                foreach (var subGroup in group.SubGroups.Keys)
                    _foldouts[SubGroupKey(group.Name, subGroup)] = expanded;
            }
        }

        private void DrawSearchResults(
            IReadOnlyList<ScriptableObject> scriptableObjects,
            ScriptableObject selectedSO,
            Action<ScriptableObject> onSelect,
            Action<Rect> registerContextBlocker)
        {
            string searchQuery = SearchQuery.ToLower();

            foreach (var so in scriptableObjects)
            {
                if (so == null)
                    continue;

                if (!so.name.ToLower().Contains(searchQuery))
                    continue;

                DrawSOButton(so, selectedSO, onSelect, registerContextBlocker);
            }
        }

        private void DrawGroupedList(
            IReadOnlyList<ScriptableObjectGroup> groups,
            ScriptableObject selectedSO,
            Action<ScriptableObject> onSelect,
            Action<Rect> registerContextBlocker)
        {
            foreach (var group in groups)
            {
                if (!DrawFoldout(group.Name, group.Name, 0, EditorStyles.foldoutHeader, registerContextBlocker))
                    continue;

                // Как в окне Project: сначала сабгруппы, потом ассеты без сабгруппы.
                foreach (var subGroup in group.SubGroups)
                {
                    string key = SubGroupKey(group.Name, subGroup.Key);
                    if (!DrawFoldout(key, subGroup.Key, 1, EditorStyles.foldout, registerContextBlocker))
                        continue;

                    DrawItems(subGroup.Value, 2, selectedSO, onSelect, registerContextBlocker);
                }

                DrawItems(group.Items, 1, selectedSO, onSelect, registerContextBlocker);
            }
        }

        private bool DrawFoldout(string key, string label, int indent, GUIStyle style, Action<Rect> registerContextBlocker)
        {
            if (!_foldouts.TryGetValue(key, out bool expanded))
                expanded = true;

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent * IndentWidth);
            expanded = EditorGUILayout.Foldout(expanded, label, true, style);
            GUILayout.EndHorizontal();
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            _foldouts[key] = expanded;
            return expanded;
        }

        private static void DrawItems(
            IReadOnlyList<ScriptableObject> items,
            int indent,
            ScriptableObject selectedSO,
            Action<ScriptableObject> onSelect,
            Action<Rect> registerContextBlocker)
        {
            foreach (var so in items)
            {
                if (so == null)
                    continue;

                GUILayout.BeginHorizontal();
                GUILayout.Space(indent * IndentWidth);
                DrawSOButton(so, selectedSO, onSelect, registerContextBlocker);
                GUILayout.EndHorizontal();
            }
        }

        // Перевода строки нет в именах групп из атрибутов: ключи групп и сабгрупп не пересекутся.
        private static string SubGroupKey(string group, string subGroup) => group + '\n' + subGroup;

        private static void DrawSOButton(
            ScriptableObject so,
            ScriptableObject selectedSO,
            Action<ScriptableObject> onSelect,
            Action<Rect> registerContextBlocker)
        {
            bool isSelected = so == selectedSO;
            bool clicked = GUILayout.Toggle(isSelected, so.name, "Button", GUILayout.Height(22));
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            if (clicked && !isSelected)
                onSelect?.Invoke(so);
        }
    }
}
