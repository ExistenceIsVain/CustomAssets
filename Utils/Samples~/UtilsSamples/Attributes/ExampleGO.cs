using UnityEngine;

namespace Utils.Samples.Attributes
{
    /// <summary>
    /// Префаб AttributesExample — все атрибуты инспектора на одном компоненте: выдели объект
    /// и посмотри инспектор. Apply Effect (кнопка на панели демо или ПКМ по компоненту) применяет
    /// выбранные эффекты к объекту — в Play Mode и без.
    /// </summary>
    [AddComponentMenu("Utils/Samples/Attributes Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        [Header("EditorReadOnly — видно, но не правится")]
        [EditorReadOnly] [SerializeField] private int appliedCount;

        [Header("MinMaxRange — диапазон одним слайдером")]
        [MinMaxRange(0f, 5f)] public Vector2 attackDelay = new Vector2(0.5f, 1.5f);
        [MinMaxRange(1, 20)] public Vector2Int goldDrop = new Vector2Int(3, 8);

        [Header("SteppedRange — слайдер с шагом")]
        [SteppedRange(0f, 100f, 5f)] public float critChance = 15f;
        [SteppedRange(0, 50, 10)] public int armor = 20;

        [Header("StringConstant — выбор из const string")]
        [StringConstant(typeof(ExampleConstants))] public string animatorTrigger = ExampleConstants.Attack;

        [Header("SubclassDrawer — выбор наследника")]
        [SerializeReference, SubclassDrawer] public ExampleClass effect = new HealEffect();
        [SerializeReference, SubclassDrawer] public ExampleClass[] extraEffects = new ExampleClass[0];

        [Header("SOGroup + SOSubGroup — группа в Tools > Scriptable Object Editor")]
        public ExampleSO config;

        [ContextMenu("Apply Effect")]
        private void ApplyEffect()
        {
            effect?.Apply(gameObject);

            foreach (ExampleClass extra in extraEffects)
                extra?.Apply(gameObject);

            appliedCount++;

            float delay = Random.Range(attackDelay.x, attackDelay.y);
            int gold = Random.Range(goldDrop.x, goldDrop.y + 1);
            Debug.Log($"Триггер '{animatorTrigger}', задержка {delay:F2} с, золото {gold}, крит {critChance}%", this);
        }

        public string Title => "Attributes";

        public void DrawPanel()
        {
            GUILayout.Label("Атрибуты видны в инспекторе: объект выделен.\nПоменяй значения и нажми Apply Effect.");
            GUILayout.Label($"Effect: {effect?.GetType().Name ?? "—"}, extra: {extraEffects.Length}");
            GUILayout.Label($"Trigger: {animatorTrigger}, crit {critChance}%, armor {armor}");
            GUILayout.Label($"Applied: {appliedCount}");

            if (DemoGUI.Button("Apply Effect")) ApplyEffect();
        }
    }
}
