using System;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class ScriptableObjectInspectorPanel
    {
        private Vector2 _scrollSettings;

        public void Draw(
            ScriptableObject selectedSO,
            UnityEditor.Editor cachedEditor,
            Action<Rect> registerContextBlocker)
        {
            GUILayout.BeginVertical();

            if (selectedSO == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a Scriptable Object to view its content.",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(selectedSO.name, EditorStyles.boldLabel);
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            _scrollSettings = GUILayout.BeginScrollView(_scrollSettings);
            cachedEditor?.OnInspectorGUI();
            GUILayout.EndScrollView();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Save", GUILayout.Height(28)))
            {
                EditorUtility.SetDirty(selectedSO);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[SO Editor] Saved: {selectedSO.name}");
            }
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            if (GUILayout.Button("Ping", GUILayout.Width(60), GUILayout.Height(28)))
            {
                EditorGUIUtility.PingObject(selectedSO);
            }
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(28)))
            {
                Selection.activeObject = selectedSO;
            }
            registerContextBlocker?.Invoke(GUILayoutUtility.GetLastRect());

            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            GUILayout.EndVertical();
        }
    }
}
