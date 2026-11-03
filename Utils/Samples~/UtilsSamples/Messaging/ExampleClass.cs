using System;
using Utils.Messaging;

namespace Utils.Samples.Messaging
{
    /// <summary>Событие: враг убит. Тип сообщения и есть канал — отдельный тип на событие.</summary>
    public readonly struct EnemyKilled
    {
        public EnemyKilled(string enemyName, int score)
        {
            EnemyName = enemyName;
            Score = score;
        }

        public string EnemyName { get; }
        public int Score { get; }
    }

    /// <summary>Запрос текущего счёта. Пустая структура — запрос без параметров.</summary>
    public readonly struct ScoreRequest { }

    /// <summary>
    /// Сервис счёта без MonoBehaviour: слушает EnemyKilled и отвечает на ScoreRequest.
    /// Регистрируется в конструкторе, снимается в Dispose — сам себя он никогда не уничтожит,
    /// и без Dispose так и остался бы в реестре.
    /// </summary>
    public class ExampleClass : IReceiver<EnemyKilled>, IProvider<ScoreRequest, int>, IDisposable
    {
        public ExampleClass()
        {
            Transmitter.Register<EnemyKilled>(this);

            // single: true — авторитетный ответ один; второй сервис счёта не зарегистрируется.
            Transmitter.Register<ScoreRequest, int>(this, single: true);
        }

        public int Score { get; private set; }

        public void Receive(EnemyKilled message) => Score += message.Score;

        public int Provide(ScoreRequest request) => Score;

        public void Dispose()
        {
            Transmitter.Unregister<EnemyKilled>(this);
            Transmitter.Unregister<ScoreRequest, int>(this);
        }
    }
}
