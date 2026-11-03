using System;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    public class FilterPathTabPopup : PopupWindowContent
    {
        private readonly string _title;
        private readonly Action<string, string> _onSave;
        private string _name;
        private string _path;

        public FilterPathTabPopup(string title, string name, string path, Action<string, string> onSave)
        {
            _title = title;
            _name = name;
            _path = path;
            _onSave = onSave;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(320, 104);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label(_title, EditorStyles.boldLabel);
            _name = EditorGUILayout.TextField("Name", _name);
            _path = EditorGUILayout.TextField("Path", _path);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(70)))
                editorWindow.Close();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_name) || string.IsNullOrWhiteSpace(_path)))
            {
                if (GUILayout.Button("Save", GUILayout.Width(70)))
                {
                    _onSave?.Invoke(_name.Trim(), ScriptableObjectFilterPathTabs.NormalizePath(_path));
                    editorWindow.Close();
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}
