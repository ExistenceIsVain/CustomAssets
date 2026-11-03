using System;
using System.Threading;
using UnityEngine;

namespace Utils.Samples.SerializeReferenceList
{
    /// <summary>
    /// Шаг сценария катсцены. Список таких шагов в [SerializeReference] рисует
    /// SerializeReferenceListGUI: кнопка Add с выбором наследника, перестановка, копирование.
    /// </summary>
    [Serializable]
    public abstract class ExampleClass
    {
        public bool enabled = true;

        public abstract string Describe();

        /// <summary>Выполнить шаг. Катсцена ждёт его завершения и переходит к следующему.</summary>
        public abstract Awaitable Run(ExampleGO cutscene, CancellationToken token);
    }

    [Serializable]
    public class WaitStep : ExampleClass
    {
        [Min(0f)] public float seconds = 1f;

        public override string Describe() => $"ждать {seconds} с";

        public override async Awaitable Run(ExampleGO cutscene, CancellationToken token)
        {
            await Awaitable.WaitForSecondsAsync(seconds, token);
        }
    }

    [Serializable]
    public class SayStep : ExampleClass
    {
        public string speaker = "Knight";
        [TextArea] public string line = "...";
        [Min(0f)] public float duration = 2f;

        public override string Describe() => $"{speaker}: {line}";

        public override async Awaitable Run(ExampleGO cutscene, CancellationToken token)
        {
            cutscene.CurrentLine = Describe();

            try
            {
                await Awaitable.WaitForSecondsAsync(duration, token);
            }
            finally
            {
                cutscene.CurrentLine = "";
            }
        }
    }

    [Serializable]
    public class MoveStep : ExampleClass
    {
        public Transform target;
        [Min(0.1f)] public float speed = 2f;

        public override string Describe() => $"идти к {(target != null ? target.name : "—")}";

        public override async Awaitable Run(ExampleGO cutscene, CancellationToken token)
        {
            if (target == null)
                return;

            Transform actor = cutscene.Actor;

            while (Vector3.Distance(actor.position, target.position) > 0.01f)
            {
                actor.position = Vector3.MoveTowards(actor.position, target.position, speed * Time.deltaTime);
                await Awaitable.NextFrameAsync(token);
            }
        }
    }
}
