using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    [CustomPropertyDrawer(typeof(SteppedRangeAttribute))]
    public class SteppedRangeDrawer : PropertyDrawer
    {
        /// <summary>Высота плашки с ошибкой: две строки текста плюс отступ.</summary>
        private const float ErrorBoxHeight = 32f;

        private const float Spacing = 2f;

        /// <summary>
        /// Поле с ошибкой занимает больше места: над ним рисуется плашка с объяснением.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label, true);

            if (attribute is SteppedRangeAttribute { HasError: true })
                height += ErrorBoxHeight + Spacing;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SteppedRangeAttribute range = (SteppedRangeAttribute)attribute;

            // Ошибку показываем в инспекторе, а не бросаем: конструктор атрибута зовётся
            // из OnGUI, и исключение оттуда сорвало бы отрисовку всего объекта.
            if (range.HasError)
            {
                Rect errorRect = new Rect(position.x, position.y, position.width, ErrorBoxHeight);
                EditorGUI.HelpBox(errorRect, range.ErrorMessage, MessageType.Error);

                // Поле всё равно рисуем ниже: значение должно остаться редактируемым.
                position = new Rect(
                    position.x,
                    errorRect.yMax + Spacing,
                    position.width,
                    position.height - ErrorBoxHeight - Spacing);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    DrawFloat(position, property, label, range);
                    break;

                case SerializedPropertyType.Integer:
                    DrawInt(position, property, label, range);
                    break;

                default:
                    EditorGUI.LabelField(position, label.text, "SteppedRange работает только с float и int.");
                    break;
            }
        }

        private static void DrawFloat(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            SteppedRangeAttribute range)
        {
            EditorGUI.BeginChangeCheck();

            float value = EditorGUI.Slider(position, label, property.floatValue, range.Min, range.Max);

            // Округляем только при реальном изменении: иначе дровер молча переписал бы
            // некратное значение, выставленное из кода или старого ассета.
            if (EditorGUI.EndChangeCheck())
                property.floatValue = Snap(value, range);
        }

        private static void DrawInt(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            SteppedRangeAttribute range)
        {
            EditorGUI.BeginChangeCheck();

            int value = EditorGUI.IntSlider(
                position,
                label,
                property.intValue,
                Mathf.RoundToInt(range.Min),
                Mathf.RoundToInt(range.Max));

            if (EditorGUI.EndChangeCheck())
                property.intValue = Mathf.RoundToInt(Snap(value, range));
        }

        /// <summary>
        /// Ближайшее кратное шагу значение, отсчитываемое от Min: шкала должна начинаться
        /// с самого Min, а не с нуля, иначе диапазон 3-100 с шагом 5 не дал бы выбрать 3.
        ///
        /// Max прилипает отдельно: если шаг не делит диапазон нацело (0-100 с шагом 30),
        /// последняя ступень не достаёт до Max, и потянув слайдер вправо до упора,
        /// дизайнер получил бы 90 вместо 100 без всякого объяснения.
        /// </summary>
        private static float Snap(float value, SteppedRangeAttribute range)
        {
            if (range.Step <= 0f)
                return Mathf.Clamp(value, range.Min, range.Max);

            float steps = Mathf.Round((value - range.Min) / range.Step);
            float snapped = range.Min + (steps * range.Step);

            // Ближе к краю диапазона, чем к своей ступени — значит целились в край.
            if (range.Max - value < value - snapped)
                return range.Max;

            return Mathf.Clamp(snapped, range.Min, range.Max);
        }
    }
}
