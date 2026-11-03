using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectListPanel
    {
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
                SetAllFoldouts(catalog.GroupedScriptableObjects, true);
            if (GUILayout.Button("Collapse All", EditorStyles.miniButtonRight))
                SetAllFoldouts(catalog.GroupedScriptableObjects, false);
            GUILayout.EndHorizontal();
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            _scrollList = GUILayout.BeginScrollView(_scrollList);

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                DrawSearchResults(catalog.ScriptableObjects, selectedSO, onSelect, registerContextBlocker);
            }
            else
            {
                DrawGroupedList(catalog.GroupedScriptableObjects, selectedSO, onSelect, registerContextBlocker);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void SetAllFoldouts(IReadOnlyDictionary<string, List<ScriptableObject>> groupedSOs, bool expanded)
        {
            foreach (var group in groupedSOs.Keys)
                _foldouts[group] = expanded;
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
            IReadOnlyDictionary<string, List<ScriptableObject>> groupedSOs,
            ScriptableObject selectedSO,
            Action<ScriptableObject> onSelect,
            Action<Rect> registerContextBlocker)
        {
            foreach (var group in groupedSOs.Keys.OrderBy(g => g))
            {
                var items = groupedSOs[group];

                if (!_foldouts.ContainsKey(group))
                    _foldouts[group] = true;

                _foldouts[group] = EditorGUILayout.Foldout(_foldouts[group], group, true, EditorStyles.foldoutHeader);
                registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

                if (!_foldouts[group])
                    continue;

                EditorGUI.indentLevel++;
                foreach (var so in items)
                {
                    if (so == null)
                        continue;

                    DrawSOButton(so, selectedSO, onSelect, registerContextBlocker);
                }
                EditorGUI.indentLevel--;
            }
        }

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
