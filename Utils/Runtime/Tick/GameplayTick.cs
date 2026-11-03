using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils.Tick
{
    [DefaultExecutionOrder(-100)]
    public class GameplayTick : MonoBehaviour
    {
        public const string GameplayChannelId = "Gameplay";
        public const string SecondChannelId = "Second";
        public const float DefaultTickRate = 0.5f;

        private const float MaxDeltaTime = 0.25f;

        [SerializeField] private bool _createDefaultChannels = true;
        [SerializeField] private List<TickChannel> _channels = new List<TickChannel>();

        private readonly Dictionary<string, TickChannel> _lookup = new Dictionary<string, TickChannel>();
        private readonly List<TickChannel> _pendingAdd = new List<TickChannel>();
        private bool _isTicking;
        private bool _hasPendingRemove;

        public static event Action<float> OnFrame;
        public static event Action<float> OnUnscaledFrame;
        public static event Action OnTickPaused;
        public static event Action OnTickResumed;
        public static event Action OnTickStarted;
        public static event Action OnTickRestarted;
        public static event Action<TickChannel> OnChannelAdded;
        public static event Action<TickChannel> OnChannelRemoved;

        public static bool IsPaused { get; private set; } = true;
        public static bool IsStarted { get; private set; }

        private static GameplayTick _instance;

        public static GameplayTick Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(GameplayTick));
                    _instance = go.AddComponent<GameplayTick>();
                }

                return _instance;
            }
        }

        // True while the tick object exists. Unlike Instance, never creates it.
        public static bool HasInstance => _instance != null;

        public static IReadOnlyList<TickChannel> Channels => Instance._channels;

        // Shortcut to the default gameplay channel rate
        public static float TickRate
        {
            get => GetOrCreateChannel(GameplayChannelId, DefaultTickRate).Rate;
            set => GetOrCreateChannel(GameplayChannelId, DefaultTickRate).Rate = value;
        }

        public static event Action<float> OnTick
        {
            add => GetOrCreateChannel(GameplayChannelId, DefaultTickRate).OnTick += value;
            remove
            {
                var channel = FindExistingChannel(GameplayChannelId);
                if (channel != null) channel.OnTick -= value;
            }
        }

        public static event Action<float> OnSecond
        {
            add => GetOrCreateChannel(SecondChannelId, 1f).OnTick += value;
            remove
            {
                var channel = FindExistingChannel(SecondChannelId);
                if (channel != null) channel.OnTick -= value;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            RebuildLookup();
            if (_createDefaultChannels) CreateDefaultChannels();

            StartTick();
        }

        private void RebuildLookup()
        {
            _lookup.Clear();

            for (int i = _channels.Count - 1; i >= 0; i--)
            {
                var channel = _channels[i];
                if (channel == null)
                {
                    _channels.RemoveAt(i);
                    continue;
                }

                channel.ValidateId($"Channel_{i}");
                channel.IsRemoved = false;

                if (_lookup.ContainsKey(channel.Id))
                {
                    Debug.LogWarning($"[{nameof(GameplayTick)}] Duplicate channel id '{channel.Id}' removed.", this);
                    _channels.RemoveAt(i);
                    continue;
                }

                _lookup.Add(channel.Id, channel);
            }
        }

        private void CreateDefaultChannels()
        {
            EnsureChannel(GameplayChannelId, DefaultTickRate);
            EnsureChannel(SecondChannelId, 1f);
        }

        private TickChannel EnsureChannel(string id, float rate, bool startPaused = false)
        {
            if (_lookup.TryGetValue(id, out var existing)) return existing;

            var channel = new TickChannel(id, rate, startPaused);
            _lookup.Add(id, channel);

            if (_isTicking) _pendingAdd.Add(channel);
            else _channels.Add(channel);

            OnChannelAdded?.Invoke(channel);
            return channel;
        }

        // Не создаёт объект тика: GetChannel, RemoveChannel и сеттеры каналов вызываются и из
        // OnDisable/OnDestroy при выходе из Play Mode, когда новый объект остался бы висеть в сцене.
        public static TickChannel GetChannel(string id) => FindExistingChannel(id);

        public static bool HasChannel(string id) => GetChannel(id) != null;

        // Lookup that does not create the tick object: removals run from OnDisable/OnDestroy, also while a scene
        // closes or Play Mode exits, when creating it would leave a stray "GameplayTick" object behind.
        private static TickChannel FindExistingChannel(string id)
        {
            if (string.IsNullOrEmpty(id) || _instance == null) return null;
            return _instance._lookup.TryGetValue(id, out var channel) ? channel : null;
        }

        public static TickChannel GetOrCreateChannel(string id, float rate = DefaultTickRate, bool startPaused = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError($"[{nameof(GameplayTick)}] Channel id cannot be empty.");
                return null;
            }

            return Instance.EnsureChannel(id, rate, startPaused);
        }

        public static bool RemoveChannel(string id)
        {
            var channel = GetChannel(id);
            if (channel == null) return false;

            channel.IsRemoved = true;
            channel.ClearSubscribers();
            _instance._lookup.Remove(id);

            if (_instance._isTicking) _instance._hasPendingRemove = true;
            else _instance._channels.Remove(channel);

            OnChannelRemoved?.Invoke(channel);
            return true;
        }

        public static void Subscribe(string id, Action<float> handler, float rate = DefaultTickRate)
        {
            if (handler == null) return;

            var channel = GetOrCreateChannel(id, rate);
            if (channel != null) channel.OnTick += handler;
        }

        public static void Unsubscribe(string id, Action<float> handler)
        {
            if (handler == null) return;

            var channel = FindExistingChannel(id);
            if (channel != null) channel.OnTick -= handler;
        }

        public static void SetChannelRate(string id, float rate)
        {
            var channel = GetChannel(id);
            if (channel != null) channel.Rate = rate;
        }

        public static void SetChannelPaused(string id, bool paused)
        {
            var channel = GetChannel(id);
            if (channel != null) channel.IsPaused = paused;
        }

        public static void ResetChannel(string id)
        {
            GetChannel(id)?.ResetAccumulator();
        }

        public static void StartTick()
        {
            _ = Instance;
            if (IsStarted) return;

            IsPaused = false;
            _instance.ResetAllChannels();
            IsStarted = true;
            OnTickStarted?.Invoke();
        }

        public static void RestartTick()
        {
            _ = Instance;
            if (!IsStarted) return;

            IsPaused = false;
            _instance.ResetAllChannels();
            OnTickRestarted?.Invoke();
        }

        public static void Pause()
        {
            _ = Instance;
            if (!IsStarted || IsPaused) return;

            IsPaused = true;
            OnTickPaused?.Invoke();
        }

        public static void Resume()
        {
            _ = Instance;
            if (!IsStarted || !IsPaused) return;

            IsPaused = false;
            OnTickResumed?.Invoke();
        }

        private void ResetAllChannels()
        {
            for (int i = 0; i < _channels.Count; i++)
                _channels[i].ResetAccumulator();
        }

        private void Update()
        {
            if (!IsStarted || IsPaused) return;

            // Clamp in real seconds, then apply time scale manually
            float unscaledDt = Mathf.Min(Time.unscaledDeltaTime, MaxDeltaTime);
            float scaledDt = unscaledDt * Time.timeScale;

            OnFrame?.Invoke(scaledDt);
            OnUnscaledFrame?.Invoke(unscaledDt);

            _isTicking = true;
            for (int i = 0; i < _channels.Count; i++)
            {
                var channel = _channels[i];
                channel.Advance(channel.UseUnscaledTime ? unscaledDt : scaledDt);
            }
            _isTicking = false;

            FlushPendingChanges();
        }

        private void FlushPendingChanges()
        {
            if (_hasPendingRemove)
            {
                _channels.RemoveAll(channel => channel.IsRemoved);
                _hasPendingRemove = false;
            }

            if (_pendingAdd.Count > 0)
            {
                _channels.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < _channels.Count; i++)
                _channels[i]?.ValidateId($"Channel_{i}");
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            for (int i = 0; i < _channels.Count; i++)
                _channels[i].ClearSubscribers();

            _channels.Clear();
            _pendingAdd.Clear();
            _lookup.Clear();

            _instance = null;
            IsPaused = true;
            IsStarted = false;

            OnFrame = null;
            OnUnscaledFrame = null;
            OnTickPaused = null;
            OnTickResumed = null;
            OnTickStarted = null;
            OnTickRestarted = null;
            OnChannelAdded = null;
            OnChannelRemoved = null;
        }
    }
}
