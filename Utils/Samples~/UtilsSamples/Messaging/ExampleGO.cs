using UnityEngine;
using Utils.Messaging;

namespace Utils.Samples.Messaging
{
    /// <summary>
    /// Префаб TransmitterExample — HUD, который слушает EnemyKilled и спрашивает счёт у сервиса,
    /// не зная о нём напрямую. В демо-сцене — кнопки на панели, в своей сцене — ПКМ по
    /// компоненту → Kill Enemy / Ask Score / Ask Missing Provider.
    /// Регистрации видны в Tools > Transmitter Monitor.
    /// </summary>
    [AddComponentMenu("Utils/Samples/Transmitter Example")]
    public class ExampleGO : MonoBehaviour, IReceiver<EnemyKilled>, IDemoPanel
    {
        private ExampleClass scoreService;
        private string lastKill = "—";
        private string lastAnswer = "—";

        private void Awake()
        {
            // В игре сервис создаёт точка входа; здесь — сам пример, чтобы был самодостаточным.
            scoreService = new ExampleClass();
        }

        private void OnDestroy() => scoreService.Dispose();

        // Регистрация на время активности: выключенный HUD событий не получает.
        private void OnEnable() => Transmitter.Register<EnemyKilled>(this);

        private void OnDisable() => Transmitter.Unregister<EnemyKilled>(this);

        public void Receive(EnemyKilled message) => lastKill = $"{message.EnemyName} (+{message.Score})";

        [ContextMenu("Kill Enemy")]
        private void KillEnemy()
        {
            int score = Random.Range(10, 100);
            Transmitter.Send(new EnemyKilled($"Skeleton #{Random.Range(1, 99)}", score));
        }

        [ContextMenu("Ask Score")]
        private void AskScore()
        {
            if (Transmitter.Request<ScoreRequest, int>(default, out int score))
                lastAnswer = $"счёт {score}";
        }

        [ContextMenu("Ask Missing Provider")]
        private void AskMissingProvider()
        {
            // Провайдера string для ScoreRequest нет. silent: true — отсутствие нормально, без ошибки в консоли.
            lastAnswer = Transmitter.Ask<ScoreRequest, string>(default, silent: true) ?? "нет ответа";
        }

        public string Title => "Transmitter";

        public void DrawPanel()
        {
            GUILayout.Label($"Score (Ask каждый кадр): {Transmitter.Ask<ScoreRequest, int>(default, silent: true)}");
            GUILayout.Label($"Last kill: {lastKill}");
            GUILayout.Label($"Last answer: {lastAnswer}");

            if (DemoGUI.Button("Kill Enemy (Send)")) KillEnemy();
            if (DemoGUI.Button("Ask Score (Request)")) AskScore();
            if (DemoGUI.Button("Ask Missing Provider")) AskMissingProvider();
        }
    }
}
