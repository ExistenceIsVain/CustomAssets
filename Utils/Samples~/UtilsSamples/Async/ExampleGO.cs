using System.Threading;
using UnityEngine;
using Utils.Async;

namespace Utils.Samples.Async
{
    /// <summary>
    /// Префаб GlobalAsyncExample. В демо-сцене — кнопки на панели, в своей сцене — ПКМ по
    /// компоненту → Hint / Save / Blink / Delayed Explosion / Wait For Flag / Stop All.
    /// Уничтожь объект во время Blink — задача остановится сама через destroyCancellationToken.
    /// </summary>
    [AddComponentMenu("Utils/Samples/GlobalAsync Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        private const string BlinkTag = "blink";

        [Tooltip("Отметь в Play Mode (или Raise Flag) — сработает Wait For Flag.")]
        [SerializeField] private bool flag;

        private ExampleClass hints;
        private AsyncHandle explosion;

        private void Awake() => hints = new ExampleClass();

        private void OnDestroy() => hints.Dispose();

        [ContextMenu("Hint")]
        private void Hint() => hints.ShowHint($"Подсказка {Time.frameCount}", 2f);

        [ContextMenu("Save")]
        private void Save() => hints.Save();

        [ContextMenu("Blink")]
        private void Blink() => GlobalAsync.Run(this, BlinkAsync, BlinkTag, TagConflict.Skip);

        [ContextMenu("Delayed Explosion")]
        private void DelayedExplosion()
        {
            // Повторный вызов отменяет прошлый отсчёт.
            explosion.Stop();
            explosion = GlobalAsync.Delay(this, 3f, () => Debug.Log("Бум", this));
        }

        [ContextMenu("Wait For Flag")]
        private void WaitForFlag()
        {
            flag = false;

            GlobalAsync.WaitUntil(this, () => flag,
                () => Debug.Log("Флаг поднят", this),
                timeout: 10f,
                onTimeout: () => Debug.Log("Флаг так и не подняли", this),
                tag: "flag", onConflict: TagConflict.Restart);
        }

        [ContextMenu("Stop All")]
        private void StopAll() => GlobalAsync.StopAllFor(this);

        [ContextMenu("Raise Flag")]
        private void RaiseFlag() => flag = true;

        private async Awaitable BlinkAsync(CancellationToken token)
        {
            for (int i = 0; i < 6; i++)
            {
                transform.localScale = i % 2 == 0 ? Vector3.one * 1.3f : Vector3.one;
                await Awaitable.WaitForSecondsAsync(0.25f, token);
            }

            transform.localScale = Vector3.one;
        }

        public string Title => "GlobalAsync";

        public void DrawPanel()
        {
            GUILayout.Label($"Hint: {hints.CurrentHint}");
            GUILayout.Label($"Saving: {hints.IsSaving}");
            GUILayout.Label($"Blinking: {GlobalAsync.IsRunningWithTag(this, BlinkTag)}");
            GUILayout.Label($"Explosion pending: {explosion.IsRunning}");
            GUILayout.Label($"Flag: {flag}");
            GUILayout.Label($"Running tasks: {GlobalAsync.RunningCount}");

            if (DemoGUI.Button("Hint (Restart)")) Hint();
            if (DemoGUI.Button("Save (Skip)")) Save();
            if (DemoGUI.Button("Blink")) Blink();
            if (DemoGUI.Button("Delayed Explosion (3 s)")) DelayedExplosion();
            if (DemoGUI.Button("Wait For Flag (10 s)")) WaitForFlag();
            if (DemoGUI.Button("Raise Flag")) RaiseFlag();
            if (DemoGUI.Button("Stop All")) StopAll();
        }
    }
}
