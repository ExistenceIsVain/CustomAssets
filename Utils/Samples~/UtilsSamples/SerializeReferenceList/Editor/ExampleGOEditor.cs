using UnityEditor;
using Utils.EditorTools;

namespace Utils.Samples.SerializeReferenceList
{
    /// <summary>
    /// Инспектор списка [SerializeReference] через SerializeReferenceListGUI.
    /// Лежит в папке Editor — в сборку игры не попадает.
    /// </summary>
    [CustomEditor(typeof(ExampleGO))]
    public class ExampleGOEditor : Editor
    {
        private SerializedProperty steps;

        private void OnEnable()
        {
            steps = serializedObject.FindProperty("steps");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializeReferenceListGUI.Draw(
                steps,
                typeof(ExampleClass),
                "Steps",
                // «Wait Step» → «Wait»: имя в меню и заголовке без суффикса.
                readableName: type => ObjectNames.NicifyVariableName(type.Name.Replace("Step", "")),
                // Подпись после имени — краткое содержание шага.
                elementLabel: value => $"— {((ExampleClass)value).Describe()}",
                // Кружок: зелёный — шаг включён, красный — выключен.
                elementEnabled: value => ((ExampleClass)value).enabled,
                addButtonLabel: "Add Step");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
