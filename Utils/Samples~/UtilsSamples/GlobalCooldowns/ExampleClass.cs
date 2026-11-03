using System;
using UnityEngine;
using Utils.Cooldowns;

namespace Utils.Samples.GlobalCooldowns
{
    /// <summary>
    /// Ежедневная награда и энергия без MonoBehaviour. Таймеры идут по настенным часам и
    /// переживают закрытие игры: закрой Play Mode, открой снова — время продолжит идти.
    /// Ключи глобальные на всю игру, поэтому их лучше держать константами в одном месте.
    /// </summary>
    public class ExampleClass
    {
        public const string DailyRewardKey = "samples.daily_reward";
        public const string EnergyKey = "samples.energy";

        /// <summary>Сброс награды — в 05:00 по местному времени игрока.</summary>
        private const int DailyResetHour = 5;

        private static readonly TimeSpan EnergyRefill = TimeSpan.FromMinutes(30);

        public bool CanClaimDaily => GlobalCooldown.IsReady(DailyRewardKey);

        public string DailyStatus => GlobalCooldown.GetFormattedTime(DailyRewardKey, CooldownFormat.Auto, "Можно забрать");

        public string EnergyStatus => GlobalCooldown.GetFormattedTime(EnergyKey, CooldownFormat.Auto, "Энергия полная");

        public bool TryClaimDaily()
        {
            if (!CanClaimDaily)
                return false;

            // Не «через 24 часа», а до следующего 05:00: награда не уползает по времени суток.
            GlobalCooldown.SetDailyReset(DailyRewardKey, DailyResetHour);
            Debug.Log("Ежедневная награда получена");
            return true;
        }

        /// <summary>Потратить энергию: запускает восстановление, если оно ещё не идёт.</summary>
        public bool TrySpendEnergy()
        {
            // Check = IsReady + Set: потратить можно, только когда восстановление закончилось.
            if (!GlobalCooldown.Check(EnergyKey, EnergyRefill, out TimeSpan remaining))
            {
                Debug.Log($"Энергия восстановится через {GlobalCooldown.Format(remaining)}");
                return false;
            }

            Debug.Log("Энергия потрачена");
            return true;
        }

        /// <summary>Сбросить оба таймера — для отладки.</summary>
        public void ResetAll()
        {
            GlobalCooldown.Remove(DailyRewardKey);
            GlobalCooldown.Remove(EnergyKey);
        }
    }
}
