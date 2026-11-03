using System;
using System.Threading;
using UnityEngine;
using Utils.Async;

namespace Utils.Samples.Async
{
    /// <summary>
    /// Сервис подсказок без MonoBehaviour. Новая подсказка заменяет старую (TagConflict.Restart),
    /// автосохранение не запускается повторно, пока идёт (TagConflict.Skip).
    /// Владелец не MonoBehaviour, поэтому Dispose обязан остановить свои задачи сам.
    /// </summary>
    public class ExampleClass : IDisposable
    {
        private const string HintTag = "hint";
        private const string SaveTag = "save";

        public string CurrentHint { get; private set; } = "";

        public bool IsSaving => GlobalAsync.IsRunningWithTag(this, SaveTag);

        public void ShowHint(string text, float seconds)
        {
            GlobalAsync.Run(this, token => ShowHintAsync(text, seconds, token), HintTag, TagConflict.Restart);
        }

        public void Save()
        {
            AsyncHandle handle = GlobalAsync.Run(this, SaveAsync, SaveTag, TagConflict.Skip);

            if (!handle.IsValid)
                Debug.Log("Сохранение уже идёт");
        }

        public void Dispose() => GlobalAsync.StopAllFor(this);

        private async Awaitable ShowHintAsync(string text, float seconds, CancellationToken token)
        {
            CurrentHint = text;

            try
            {
                // unscaled: подсказка исчезает и на паузе.
                await GlobalAsync.WaitSeconds(seconds, unscaled: true, token);
            }
            finally
            {
                // finally срабатывает и при отмене — Restart новой подсказкой тоже сюда.
                if (CurrentHint == text)
                    CurrentHint = "";
            }
        }

        private async Awaitable SaveAsync(CancellationToken token)
        {
            Debug.Log("Сохранение…");

            // Тяжёлую работу — в фоновый поток, результат — обратно в главный.
            await Awaitable.BackgroundThreadAsync();
            Thread.Sleep(1000);
            await Awaitable.MainThreadAsync();

            token.ThrowIfCancellationRequested();
            Debug.Log("Сохранено");
        }
    }
}
