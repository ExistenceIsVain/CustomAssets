#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Utils.Async
{
    /// <summary>Что делать, если у владельца уже идёт задача с тем же тегом.</summary>
    public enum TagConflict
    {
        /// <summary>Бросить InvalidOperationException: два одинаковых тега — почти всегда ошибка.</summary>
        Throw,

        /// <summary>Отменить старую задачу и запустить новую.</summary>
        Restart,

        /// <summary>Не запускать новую, вернуть пустой handle.</summary>
        Skip
    }

    /// <summary>Ссылка на запущенную задачу. default — пустой handle, Stop на нём ничего не делает.</summary>
    public readonly struct AsyncHandle : IEquatable<AsyncHandle>
    {
        internal AsyncHandle(int id) => Id = id;

        internal int Id { get; }

        public bool IsValid => Id != 0;

        public bool IsRunning => GlobalAsync.IsRunning(Id);

        /// <summary>Отменить задачу. Безопасно звать повторно и после её завершения.</summary>
        public void Stop() => GlobalAsync.Stop(Id);

        public bool Equals(AsyncHandle other) => Id == other.Id;

        public override bool Equals(object? obj) => obj is AsyncHandle other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString() => Id == 0 ? "AsyncHandle.None" : $"AsyncHandle({Id})";

        public static bool operator ==(AsyncHandle left, AsyncHandle right) => left.Equals(right);

        public static bool operator !=(AsyncHandle left, AsyncHandle right) => !left.Equals(right);
    }

    /// <summary>Снимок запущенной задачи для диагностики.</summary>
    public readonly struct AsyncJobInfo
    {
        public AsyncJobInfo(AsyncHandle handle, object owner, string? tag, string name, float startTime)
        {
            Handle = handle;
            Owner = owner;
            Tag = tag;
            Name = name;
            StartTime = startTime;
        }

        public AsyncHandle Handle { get; }
        public object Owner { get; }
        public string? Tag { get; }
        public string Name { get; }
        public float StartTime { get; }
        public float Age => Time.unscaledTime - StartTime;
    }

    /// <summary>
    /// Запуск асинхронных задач на Awaitable с владельцем и тегом — для сервисов без
    /// MonoBehaviour и для задач, которые нужно останавливать группой.
    ///
    /// Задача получает CancellationToken, который отменяется:
    /// — при Stop у handle, StopByTag или StopAllFor владельца;
    /// — при уничтожении владельца, если он MonoBehaviour (destroyCancellationToken);
    /// — при выходе из Play Mode или игры (Application.exitCancellationToken).
    /// Владелец не MonoBehaviour живёт в реестре, пока его задачи не закончатся или их не остановят.
    ///
    /// Только главный поток: продолжения Awaitable и так выполняются на нём.
    /// </summary>
    public static class GlobalAsync
    {
        private sealed class Job
        {
            public Job(int id, object owner, string? tag, CancellationTokenSource cancellation, string name)
            {
                Id = id;
                Owner = owner;
                Tag = tag;
                Cancellation = cancellation;
                Name = name;
                StartTime = Time.unscaledTime;
            }

            public int Id { get; }
            public object Owner { get; }
            public string? Tag { get; }
            public CancellationTokenSource Cancellation { get; }
            public string Name { get; }
            public float StartTime { get; }
        }

        // Владельцы сравниваются по ссылке: UnityEngine.Object переопределяет Equals/GetHashCode.
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static ReferenceComparer Instance { get; } = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static readonly Dictionary<int, Job> Jobs = new();
        private static readonly Dictionary<object, List<Job>> JobsByOwner = new(ReferenceComparer.Instance);
        private static readonly List<Job> Scratch = new();

        // Не сбрасывается между запусками Play Mode: старый handle не должен указать на новую задачу.
        private static int nextId;

        // Domain Reload может быть выключен, поэтому статику чистим явно. Задачи прошлого
        // запуска к этому моменту уже отменены через Application.exitCancellationToken.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Jobs.Clear();
            JobsByOwner.Clear();
            Scratch.Clear();
        }

        #region Run

        /// <param name="owner">Ссылочный объект, к которому привязана задача. MonoBehaviour — отмена при уничтожении.</param>
        /// <param name="work">Сама задача. Токен нужно передавать во все ожидания внутри неё.</param>
        /// <param name="tag">Имя задачи у этого владельца, например "reload". Уникально в пределах владельца.</param>
        /// <param name="onConflict">Что делать, если задача с таким тегом у владельца уже идёт.</param>
        /// <param name="debugName">Имя для диагностики. По умолчанию — имя метода задачи.</param>
        public static AsyncHandle Run(
            object owner,
            Func<CancellationToken, Awaitable> work,
            string? tag = null,
            TagConflict onConflict = TagConflict.Throw,
            string? debugName = null)
        {
            ValidateOwner(owner);

            if (work == null)
                throw new ArgumentNullException(nameof(work));

            if (!Application.isPlaying)
                throw new InvalidOperationException("[GlobalAsync] Tasks can only be started in Play Mode.");

            if (tag != null && TryFind(owner, tag, out Job? running))
            {
                switch (onConflict)
                {
                    case TagConflict.Skip:
                        return default;

                    case TagConflict.Restart:
                        Cancel(running!);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"[GlobalAsync] Tag '{tag}' is already running for {DescribeOwner(owner)}. " +
                            "Use TagConflict.Restart or TagConflict.Skip if that is expected.");
                }
            }

            CancellationTokenSource cancellation = owner is MonoBehaviour behaviour
                ? CancellationTokenSource.CreateLinkedTokenSource(behaviour.destroyCancellationToken, Application.exitCancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);

            var job = new Job(NextId(), owner, tag, cancellation, debugName ?? work.Method.Name);
            Add(job);

            // Первая часть задачи до первого await выполняется синхронно прямо здесь, поэтому
            // регистрация — до запуска: задача может сама себя остановить или запустить соседа.
            Execute(job, work);

            return new AsyncHandle(job.Id);
        }

        /// <summary>Вызвать action через seconds секунд.</summary>
        public static AsyncHandle Delay(
            object owner,
            float seconds,
            Action action,
            bool unscaled = false,
            string? tag = null,
            TagConflict onConflict = TagConflict.Throw)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return Run(owner, async token =>
            {
                await WaitSeconds(seconds, unscaled, token);
                action();
            }, tag, onConflict, action.Method.Name);
        }

        /// <summary>Вызвать action в следующем кадре.</summary>
        public static AsyncHandle NextFrame(
            object owner,
            Action action,
            string? tag = null,
            TagConflict onConflict = TagConflict.Throw)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return Run(owner, async token =>
            {
                await Awaitable.NextFrameAsync(token);
                action();
            }, tag, onConflict, action.Method.Name);
        }

        /// <summary>
        /// Проверять condition каждый кадр и вызвать action, когда он станет true.
        /// С timeout больше нуля по истечении времени вызывается onTimeout, а action — нет.
        /// </summary>
        public static AsyncHandle WaitUntil(
            object owner,
            Func<bool> condition,
            Action action,
            float timeout = -1f,
            Action? onTimeout = null,
            bool unscaled = false,
            string? tag = null,
            TagConflict onConflict = TagConflict.Throw)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return Run(owner, async token =>
            {
                float deadline = timeout > 0f ? Now(unscaled) + timeout : float.PositiveInfinity;

                while (!condition())
                {
                    if (Now(unscaled) >= deadline)
                    {
                        onTimeout?.Invoke();
                        return;
                    }

                    await Awaitable.NextFrameAsync(token);
                }

                action();
            }, tag, onConflict, action.Method.Name);
        }

        /// <summary>
        /// Ожидание в секундах внутри своей задачи. Awaitable.WaitForSecondsAsync идёт по
        /// игровому времени, поэтому для unscaled ждём по кадрам.
        /// </summary>
        public static async Awaitable WaitSeconds(float seconds, bool unscaled, CancellationToken token)
        {
            if (!unscaled)
            {
                await Awaitable.WaitForSecondsAsync(seconds, token);
                return;
            }

            float end = Time.unscaledTime + seconds;

            while (Time.unscaledTime < end)
                await Awaitable.NextFrameAsync(token);
        }

        #endregion

        #region Stop

        public static void StopAllFor(object owner)
        {
            if (owner == null || !JobsByOwner.TryGetValue(owner, out var list))
                return;

            Scratch.Clear();
            Scratch.AddRange(list);

            foreach (Job job in Scratch)
                Cancel(job);

            Scratch.Clear();
        }

        public static void StopByTag(object owner, string tag)
        {
            if (owner != null && tag != null && TryFind(owner, tag, out Job? job))
                Cancel(job!);
        }

        /// <summary>Остановить вообще всё. Для смены сцены или сброса игры.</summary>
        public static void StopAll()
        {
            Scratch.Clear();
            Scratch.AddRange(Jobs.Values);

            foreach (Job job in Scratch)
                Cancel(job);

            Scratch.Clear();
        }

        internal static void Stop(int id)
        {
            if (id != 0 && Jobs.TryGetValue(id, out var job))
                Cancel(job);
        }

        #endregion

        #region Query

        public static bool IsRunningWithTag(object owner, string tag)
        {
            return owner != null && tag != null && TryFind(owner, tag, out _);
        }

        public static int RunningCount => Jobs.Count;

        internal static bool IsRunning(int id) => id != 0 && Jobs.ContainsKey(id);

        public static void GetRunning(List<AsyncJobInfo> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();

            foreach (Job job in Jobs.Values)
                buffer.Add(new AsyncJobInfo(new AsyncHandle(job.Id), job.Owner, job.Tag, job.Name, job.StartTime));
        }

        /// <summary>Предупреждение в консоль о задачах, которые идут дольше olderThanSeconds.</summary>
        public static void LogStale(float olderThanSeconds)
        {
            float now = Time.unscaledTime;

            foreach (Job job in Jobs.Values)
            {
                float age = now - job.StartTime;

                if (age < olderThanSeconds)
                    continue;

                Debug.LogWarning(
                    $"[GlobalAsync] Long-running task '{job.Name}'" +
                    (job.Tag != null ? $" (tag: {job.Tag})" : "") +
                    $" of {DescribeOwner(job.Owner)} — {age:F1}s.",
                    job.Owner as UnityEngine.Object);
            }
        }

        #endregion

        #region Internals

        private static async void Execute(Job job, Func<CancellationToken, Awaitable> work)
        {
            try
            {
                await work(job.Cancellation.Token);
            }
            catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
            {
                // Нормальная остановка.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, job.Owner as UnityEngine.Object);
            }
            finally
            {
                Remove(job);
                job.Cancellation.Dispose();
            }
        }

        private static void Cancel(Job job)
        {
            // Из реестра — сразу: Restart запускает задачу с тем же тегом, пока старая
            // ещё доходит до своего finally.
            if (!Remove(job))
                return;

            job.Cancellation.Cancel();
        }

        private static void Add(Job job)
        {
            Jobs.Add(job.Id, job);

            if (!JobsByOwner.TryGetValue(job.Owner, out var list))
            {
                list = new List<Job>();
                JobsByOwner.Add(job.Owner, list);
            }

            list.Add(job);
        }

        private static bool Remove(Job job)
        {
            if (!Jobs.Remove(job.Id))
                return false;

            if (JobsByOwner.TryGetValue(job.Owner, out var list))
            {
                list.Remove(job);

                if (list.Count == 0)
                    JobsByOwner.Remove(job.Owner);
            }

            return true;
        }

        private static bool TryFind(object owner, string tag, out Job? job)
        {
            job = null;

            if (!JobsByOwner.TryGetValue(owner, out var list))
                return false;

            foreach (Job candidate in list)
            {
                if (string.Equals(candidate.Tag, tag, StringComparison.Ordinal))
                {
                    job = candidate;
                    return true;
                }
            }

            return false;
        }

        private static int NextId()
        {
            nextId = nextId == int.MaxValue ? 1 : nextId + 1;
            return nextId;
        }

        private static float Now(bool unscaled) => unscaled ? Time.unscaledTime : Time.time;

        private static void ValidateOwner(object owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner is UnityEngine.Object unityObject && unityObject == null)
                throw new ArgumentException("[GlobalAsync] Owner is a destroyed UnityEngine.Object.", nameof(owner));

            // Упакованный value type — каждый раз новая коробка, StopAllFor её никогда не найдёт.
            if (owner.GetType().IsValueType)
                throw new ArgumentException("[GlobalAsync] Owner must be a reference type, not a boxed value type.", nameof(owner));
        }

        private static string DescribeOwner(object owner)
        {
            return owner is UnityEngine.Object unityObject && unityObject != null
                ? $"{owner.GetType().Name} '{unityObject.name}'"
                : owner.GetType().Name;
        }

        #endregion
    }
}
