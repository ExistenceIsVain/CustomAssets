using UnityEngine;
using Utils.Cooldowns;

namespace Utils.Samples.FastCooldowns
{
    /// <summary>
    /// Префаб FastCooldownExample. В демо-сцене — кнопки на панели, в своей сцене — ПКМ по
    /// компоненту → Attack / Dash / Pause Toggle.
    /// Dash идёт по реальному времени (unscaled) и перезаряжается даже на паузе.
    /// </summary>
    [AddComponentMenu("Utils/Samples/FastCooldown Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float dashCooldown = 3f;

        [Tooltip("Что опустить при ударе — модель меча. Поднимается по мере перезарядки.")]
        [SerializeField] private Transform swordModel;

        private ExampleClass sword;
        private CooldownKey dashKey;

        private void Awake()
        {
            sword = new ExampleClass($"{name}/Sword", attackCooldown);
            dashKey = new CooldownKey(this, "dash");
        }

        private void OnDestroy()
        {
            sword?.Dispose();

            // Обязательно для MonoBehaviour-владельца: ключи живут в статическом словаре.
            FastCooldown.RemoveAllFor(this);
        }

        private void Update()
        {
            if (swordModel != null)
                swordModel.localRotation = Quaternion.Euler(Mathf.Lerp(90f, 0f, sword.Progress), 0f, 0f);
        }

        [ContextMenu("Attack")]
        private void Attack() => sword.TryAttack();

        [ContextMenu("Dash")]
        private void Dash()
        {
            if (FastCooldown.Check(dashKey, dashCooldown, unscaled: true))
                Debug.Log("Рывок", this);
            else
                Debug.Log($"Рывок через {FastCooldown.GetTimeRemaining(dashKey):F1} с", this);
        }

        [ContextMenu("Pause Toggle")]
        private void PauseToggle() => Time.timeScale = Time.timeScale > 0f ? 0f : 1f;

        public string Title => "FastCooldown";

        public void DrawPanel()
        {
            float dash = FastCooldown.GetProgress(dashKey);

            DemoGUI.Bar($"Attack {sword.Progress:P0}", sword.Progress);
            DemoGUI.Bar($"Dash, unscaled {dash:P0}", dash);
            GUILayout.Label($"timeScale: {Time.timeScale}");

            if (DemoGUI.Button("Attack")) Attack();
            if (DemoGUI.Button("Dash")) Dash();
            if (DemoGUI.Button(Time.timeScale > 0f ? "Pause" : "Resume")) PauseToggle();
        }
    }
}
