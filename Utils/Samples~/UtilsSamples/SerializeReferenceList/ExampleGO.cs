using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Utils.Samples.SerializeReferenceList
{
    /// <summary>
    /// Префаб SerializeReferenceListExample — катсцена из шагов-наследников ExampleClass.
    /// Инспектор — Editor/ExampleGOEditor.cs. В демо-сцене — кнопки на панели, в своей сцене — ПКМ
    /// по компоненту → Play / Stop / Print Steps.
    /// </summary>
    [AddComponentMenu("Utils/Samples/SerializeReferenceList Example")]
    public class ExampleGO : MonoBehaviour, IDemoPanel
    {
        [Tooltip("Кого двигают шаги Move. Пусто — сам объект.")]
        [SerializeField] private Transform actor;

        [SerializeReference] private List<ExampleClass> steps = new List<ExampleClass>();

        private CancellationTokenSource playing;
        private Vector3 actorStart;
        private int currentStep = -1;

        public Transform Actor => actor != null ? actor : transform;

        /// <summary>Реплика шага Say, пока он идёт.</summary>
        public string CurrentLine { get; set; } = "";

        public bool IsPlaying => playing != null;

        private void Start() => actorStart = Actor.localPosition;

        private void OnDestroy() => Stop();

        [ContextMenu("Play")]
        private void Play()
        {
            if (IsPlaying)
                return;

            Actor.localPosition = actorStart;
            PlayAsync();
        }

        [ContextMenu("Stop")]
        private void Stop()
        {
            playing?.Cancel();
        }

        [ContextMenu("Print Steps")]
        private void PrintSteps()
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].enabled)
                    Debug.Log($"{i + 1}. {steps[i].Describe()}", this);
            }
        }

        // async void — только у точки запуска: исключения шагов всё равно доходят до консоли.
        private async void PlayAsync()
        {
            using var source = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            playing = source;

            try
            {
                for (currentStep = 0; currentStep < steps.Count; currentStep++)
                {
                    ExampleClass step = steps[currentStep];

                    if (step != null && step.enabled)
                        await step.Run(this, source.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop или уничтожение объекта.
            }
            finally
            {
                currentStep = -1;
                CurrentLine = "";
                playing = null;
            }
        }

        public string Title => "SerializeReferenceList";

        public void DrawPanel()
        {
            GUILayout.Label("Шаги — в инспекторе: Add Step, перетаскивание, копирование.");

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] == null)
                    continue;

                string marker = i == currentStep ? "▶" : steps[i].enabled ? "•" : "×";
                GUILayout.Label($"{marker} {i + 1}. {steps[i].Describe()}");
            }

            GUILayout.Label($"Реплика: {(CurrentLine.Length > 0 ? CurrentLine : "—")}");

            if (IsPlaying)
            {
                if (DemoGUI.Button("Stop")) Stop();
            }
            else if (DemoGUI.Button("Play"))
            {
                Play();
            }
        }
    }
}
