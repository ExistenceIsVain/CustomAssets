using UnityEngine;
using Utils.Cooldowns;

namespace Utils.Samples.GlobalCooldowns
{
    /// <summary>
    /// Префаб GlobalCooldownExample — сундук с ежедневной наградой. В демо-сцене — кнопки на
    /// панели, в своей сцене — ПКМ по компоненту → Claim Daily / Spend Energy / Reset.
    /// Состояние пишется в PlayerPrefs, так что переживает перезапуск Play Mode.
    /// </summary>
    [AddComponentMenu("Utils/Samples/GlobalCooldown Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        [Tooltip("Крышка сундука: открыта, пока награду можно забрать.")]
        [SerializeField] private Transform lid;

        private readonly ExampleClass rewards = new ExampleClass();

        private void Update()
        {
            if (lid != null)
                lid.localRotation = Quaternion.Euler(rewards.CanClaimDaily ? 60f : 0f, 0f, 0f);
        }

        [ContextMenu("Claim Daily")]
        private void ClaimDaily() => rewards.TryClaimDaily();

        [ContextMenu("Spend Energy")]
        private void SpendEnergy() => rewards.TrySpendEnergy();

        [ContextMenu("Reset")]
        private void ResetCooldowns() => rewards.ResetAll();

        public string Title => "GlobalCooldown";

        public void DrawPanel()
        {
            GUILayout.Label($"Daily: {rewards.DailyStatus}");
            GUILayout.Label($"Energy: {rewards.EnergyStatus}");

            // Игрок перевёл часы назад — время кулдаунов не откатывается, а замирает.
            if (GlobalCooldown.ClockRolledBack)
                GUILayout.Label("Часы переведены назад");

            if (DemoGUI.Button("Claim Daily")) ClaimDaily();
            if (DemoGUI.Button("Spend Energy")) SpendEnergy();
            if (DemoGUI.Button("Reset")) ResetCooldowns();
        }
    }
}
