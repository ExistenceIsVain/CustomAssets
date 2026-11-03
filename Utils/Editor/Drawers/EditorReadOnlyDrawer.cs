using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    [CustomPropertyDrawer(typeof(EditorReadOnlyAttribute))]
    public class EditorReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // includeChildren, иначе развёрнутая структура или [SerializeReference] обрежется по высоте.
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(position, property, label, true);
        }
    }
}
