using System;
using UnityEngine;

namespace Utils.Tick
{
    [Serializable]
    public class TickChannel
    {
        public const float MinRate = 0.01f;
        public const int MaxStepsPerFrame = 8;

        [SerializeField] private string _id;
        [SerializeField, Min(MinRate)] private float _rate = 0.5f;
        [SerializeField] private bool _isPaused;
        [SerializeField] private bool _useUnscaledTime;
        public bool UseUnscaledTime
        {
            get => _useUnscaledTime;
            set => _useUnscaledTime = value;
        }
        
        [NonSerialized] private float _accumulator;

        public event Action<float> OnTick;

        internal bool IsRemoved;

        public string Id => _id;

        public float Rate
        {
            get => _rate;
            set => _rate = Mathf.Max(MinRate, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public float Accumulator => _accumulator;

        // Normalized progress towards the next tick, useful for UI bars
        public float Progress => Mathf.Clamp01(_accumulator / Mathf.Max(MinRate, _rate));

        public TickChannel(string id, float rate, bool startPaused = false)
        {
            _id = id;
            _rate = Mathf.Max(MinRate, rate);
            _isPaused = startPaused;
        }

        internal void Advance(float dt)
        {
            if (_isPaused || IsRemoved) return;

            _accumulator += dt;

            int steps = 0;
            while (_accumulator >= _rate)
            {
                _accumulator -= _rate;
                steps++;

                OnTick?.Invoke(_rate);

                // Safety net against a huge frame spike or a very small rate
                if (steps >= MaxStepsPerFrame)
                {
                    _accumulator = 0f;
                    break;
                }
            }
        }

        internal void ResetAccumulator() => _accumulator = 0f;

        internal void ValidateId(string fallbackId)
        {
            if (string.IsNullOrEmpty(_id)) _id = fallbackId;
            _rate = Mathf.Max(MinRate, _rate);
        }

        internal void ClearSubscribers() => OnTick = null;
    }
}
