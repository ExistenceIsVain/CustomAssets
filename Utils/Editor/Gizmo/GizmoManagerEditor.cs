using System;
using UnityEditor;
using UnityEngine;
using Utils.Gizmo;

namespace Utils.EditorTools
{
    [CustomEditor(typeof(GizmosObject))]
    [CanEditMultipleObjects]
    public class GizmoManagerEditor : Editor
    {
        private const string DrawerSuffix = "GizmoDrawer";

        private SerializedProperty gizmosDrawersProperty;

        private void OnEnable()
        {
            gizmosDrawersProperty = serializedObject.FindProperty("gizmosDrawers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializeReferenceListGUI.Draw(
                gizmosDrawersProperty,
                typeof(GizmosDrawer),
                "Gizmo Drawers",
                type => GetReadableName(type.Name),
                DrawResetButton,
                elementEnabled: value => ((GizmosDrawer)value).enabled,
                addButtonLabel: "Add Drawer");

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var gizmosObject = (GizmosObject)target;
            var drawers = gizmosObject.GetDrawers();
            if (drawers == null) return;

            foreach (var drawer in drawers)
            {
                if (drawer != null && drawer.enabled)
                    drawer.OnSceneGUI(gizmosObject);
            }
        }

        // Сброс — новый экземпляр того же типа: значения берутся из инициализаторов полей,
        // и отрисовщику не нужно дублировать их в отдельном методе сброса.
        private static void DrawResetButton(SerializedProperty element, object value)
        {
            if (!GUILayout.Button("Reset", GUILayout.Width(60)))
                return;

            element.managedReferenceValue = Activator.CreateInstance(value.GetType());
            element.serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        private static string GetReadableName(string typeName)
        {
            if (typeName.EndsWith(DrawerSuffix, StringComparison.Ordinal) && typeName.Length > DrawerSuffix.Length)
                typeName = typeName.Substring(0, typeName.Length - DrawerSuffix.Length);

            return SerializeReferenceListGUI.SplitWords(typeName);
        }
    }
}
