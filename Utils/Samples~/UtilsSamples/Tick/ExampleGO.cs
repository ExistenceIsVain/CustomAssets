using UnityEngine;
using Utils.Tick;

namespace Utils.Samples.Tick
{
    /// <summary>
    /// Префаб GameplayTickExample. Яд на стандартном канале Gameplay (шаг 0.5 с) и регенерация
    /// на своём канале "Regen". Объект GameplayTick создастся сам при первой подписке.
    /// В демо-сцене — кнопки на панели, в своей сцене — ПКМ по компоненту →
    /// Poison / Pause Regen Toggle / Pause All Toggle.
    /// </summary>
    [AddComponentMenu("Utils/Samples/GameplayTick Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        private const int MaxHealth = 100;

        [SerializeField] private int poisonDamagePerSecond = 4;
        [SerializeField] private float poisonDuration = 3f;

        [Tooltip("Полоска здоровья над объектом: масштаб по X = доля здоровья.")]
        [SerializeField] private Transform healthBar;

        private ExampleClass health;
        private float poisonLeft;

        private void OnEnable()
        {
            health = new ExampleClass(startHealth: 50, maxHealth: MaxHealth, regenPerTick: 2);
            GameplayTick.OnTick += OnGameplayTick;
        }

        private void OnDisable()
        {
            // Отписка без создания объекта тика: безопасна и при выходе из Play Mode.
            GameplayTick.OnTick -= OnGameplayTick;
            health.Dispose();
        }

        private void Update()
        {
            if (healthBar == null)
                return;

            Vector3 scale = healthBar.localScale;
            scale.x = (float)health.Health / MaxHealth;
            healthBar.localScale = scale;
        }

        private void OnGameplayTick(float tickDuration)
        {
            if (poisonLeft <= 0f)
                return;

            poisonLeft -= tickDuration;

            // Урон «в секунду» умножаем на длину шага: смена TickRate не меняет баланс.
            health.Damage(Mathf.RoundToInt(poisonDamagePerSecond * tickDuration));
        }

        [ContextMenu("Poison")]
        private void Poison() => poisonLeft = poisonDuration;

        [ContextMenu("Pause Regen Toggle")]
        private void PauseRegenToggle()
        {
            TickChannel regen = GameplayTick.GetChannel(ExampleClass.RegenChannelId);

            if (regen != null)
                regen.IsPaused = !regen.IsPaused;
        }

        [ContextMenu("Pause All Toggle")]
        private void PauseAllToggle()
        {
            if (GameplayTick.IsPaused)
                GameplayTick.Resume();
            else
                GameplayTick.Pause();
        }

        public string Title => "GameplayTick";

        public void DrawPanel()
        {
            TickChannel regen = GameplayTick.GetChannel(ExampleClass.RegenChannelId);

            DemoGUI.Bar($"HP {health.Health}/{MaxHealth}", (float)health.Health / MaxHealth);
            GUILayout.Label($"Poison: {Mathf.Max(0f, poisonLeft):F1} s");

            if (regen != null)
                DemoGUI.Bar($"Regen tick {regen.Progress:P0}{(regen.IsPaused ? " (pause)" : "")}", regen.Progress);

            if (DemoGUI.Button("Poison")) Poison();
            if (DemoGUI.Button(regen != null && regen.IsPaused ? "Resume Regen" : "Pause Regen")) PauseRegenToggle();
            if (DemoGUI.Button(GameplayTick.IsPaused ? "Resume All" : "Pause All")) PauseAllToggle();
        }
    }
}
