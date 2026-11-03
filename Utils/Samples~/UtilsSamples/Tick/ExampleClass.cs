using System;
using UnityEngine;
using Utils.Tick;

namespace Utils.Samples.Tick
{
    /// <summary>
    /// Регенерация без MonoBehaviour на своём канале "Regen" раз в секунду. Канал создаётся
    /// при первой подписке. Dispose отписывает — без этого статический канал держал бы объект.
    /// </summary>
    public class ExampleClass : IDisposable
    {
        public const string RegenChannelId = "Regen";

        private readonly int maxHealth;

        public ExampleClass(int startHealth, int maxHealth, int regenPerTick)
        {
            Health = startHealth;
            this.maxHealth = maxHealth;
            RegenPerTick = regenPerTick;

            GameplayTick.Subscribe(RegenChannelId, OnRegenTick, rate: 1f);
        }

        public int Health { get; private set; }

        public int RegenPerTick { get; set; }

        public void Damage(int amount) => Health = Mathf.Max(0, Health - amount);

        /// <param name="tickDuration">Длина шага канала в секундах — для формул «в секунду».</param>
        private void OnRegenTick(float tickDuration)
        {
            if (Health <= 0 || Health >= maxHealth)
                return;

            Health = Mathf.Min(maxHealth, Health + RegenPerTick);
        }

        public void Dispose() => GameplayTick.Unsubscribe(RegenChannelId, OnRegenTick);
    }
}
