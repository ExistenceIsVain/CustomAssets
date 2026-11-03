using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
    public class MinMaxRangeDrawer : PropertyDrawer
    {
        private const float FieldWidth = 50f;
        private const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isInt = property.propertyType == SerializedPropertyType.Vector2Int;

            if (!isInt && property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.LabelField(position, label.text, "Use MinMaxRange with Vector2 or Vector2Int.");
                return;
            }

            MinMaxRangeAttribute range = (MinMaxRangeAttribute)attribute;

            // BeginProperty даёт жирный шрифт для переопределений префаба и меню по правому клику.
            label = EditorGUI.BeginProperty(position, label, property);
            Rect content = EditorGUI.PrefixLabel(position, label);

            // PrefixLabel уже учёл отступ; без сброса поля внутри сдвинулись бы ещё раз.
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float sliderWidth = content.width - (FieldWidth * 2f) - (Spacing * 2f);
            Rect minFieldRect = new Rect(content.x, content.y, FieldWidth, content.height);
            Rect sliderRect = new Rect(minFieldRect.xMax + Spacing, content.y, sliderWidth, content.height);
            Rect maxFieldRect = new Rect(sliderRect.xMax + Spacing, content.y, FieldWidth, content.height);

            Vector2 value = isInt ? (Vector2)property.vector2IntValue : property.vector2Value;

            EditorGUI.BeginChangeCheck();
            value.x = isInt
                ? EditorGUI.IntField(minFieldRect, Mathf.RoundToInt(value.x))
                : EditorGUI.FloatField(minFieldRect, value.x);
            EditorGUI.MinMaxSlider(sliderRect, ref value.x, ref value.y, range.Min, range.Max);
            value.y = isInt
                ? EditorGUI.IntField(maxFieldRect, Mathf.RoundToInt(value.y))
                : EditorGUI.FloatField(maxFieldRect, value.y);

            if (EditorGUI.EndChangeCheck())
            {
                value.x = Mathf.Clamp(value.x, range.Min, value.y);
                value.y = Mathf.Clamp(value.y, value.x, range.Max);

                if (isInt)
                    property.vector2IntValue = new Vector2Int(Mathf.RoundToInt(value.x), Mathf.RoundToInt(value.y));
                else
                    property.vector2Value = value;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
