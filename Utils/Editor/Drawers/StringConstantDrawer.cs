using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Utils.EditorTools
{
    [CustomPropertyDrawer(typeof(StringConstantAttribute))]
    public class StringConstantDrawer : PropertyDrawer
    {
        private class ConstantInfo
        {
            public string name;
            public string value;
            public string region;
        }

        private static Dictionary<Type, List<ConstantInfo>> cachedConstantsByType = new();

        private List<ConstantInfo> GetConstantsForType(Type type)
        {
            if (cachedConstantsByType.TryGetValue(type, out var cached))
                return cached;

            var constants = new List<ConstantInfo>();
            CollectConstants(type, "Other", constants);
            cachedConstantsByType[type] = constants;
            return constants;
        }

        private void CollectConstants(Type type, string currentRegion, List<ConstantInfo> constants)
        {
            // Get constants directly on this type
            var fields = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .OrderBy(f => f.MetadataToken);

            string region = currentRegion;

            foreach (var field in fields)
            {
                var regionAttr = field.GetCustomAttribute<RegionAttribute>();
                if (regionAttr != null)
                    region = regionAttr.Name;

                constants.Add(new ConstantInfo
                {
                    name = field.Name,
                    value = (string)field.GetRawConstantValue(),
                    region = region
                });
            }

            // Recurse into nested classes
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
            {
                CollectConstants(nested, nested.Name, constants);
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [StringConstant] with string fields only.");
                return;
            }

            var attr = attribute as StringConstantAttribute;
            var constantsType = attr.ConstantsType;

            if (constantsType == null)
            {
                EditorGUI.LabelField(position, label.text, "Constants type is null");
                return;
            }

            var constants = GetConstantsForType(constantsType);

            if (constants.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, $"No constants found in {constantsType.Name}");
                return;
            }

            // BeginProperty даёт жирный шрифт для переопределений префаба и меню по правому клику.
            label = EditorGUI.BeginProperty(position, label, property);
            Rect buttonRect = EditorGUI.PrefixLabel(position, label);

            string currentValue = property.stringValue;
            var currentConstant = constants.FirstOrDefault(c => c.value == currentValue);
            string displayText = currentConstant != null ? currentConstant.value : currentValue;

            if (string.IsNullOrEmpty(displayText))
                displayText = "(none)";

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayText), FocusType.Keyboard))
            {
                ShowCategorizedMenu(property, constants, buttonRect);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private void ShowCategorizedMenu(SerializedProperty property, List<ConstantInfo> constants, Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();

            // Меню срабатывает после OnGUI, когда property уже может быть недействительным:
            // запоминаем объект и путь и находим свойство заново.
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;

            foreach (var constant in constants)
            {
                string menuPath = constant.region != "Other"
                    ? $"{constant.region}/{constant.value}"
                    : constant.value;

                bool isSelected = property.stringValue == constant.value;

                menu.AddItem(
                    new GUIContent(menuPath),
                    isSelected,
                    () =>
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty(path).stringValue = constant.value;
                        serializedObject.ApplyModifiedProperties();
                    }
                );
            }

            menu.DropDown(buttonRect);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
