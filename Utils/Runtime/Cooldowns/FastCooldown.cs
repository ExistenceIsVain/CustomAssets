#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Utils.Cooldowns
{
    public readonly struct CooldownKey : IEquatable<CooldownKey>
    {
        private readonly object? _owner;
        private readonly string _name;

        public CooldownKey(string name)
        {
            _owner = null;
            _name = name ?? string.Empty;
        }

        public CooldownKey(object? owner, string name)
        {
            _owner = owner;
            _name = name ?? string.Empty;
        }

        public object? Owner => _owner;
        public string Name => _name;

        public bool Equals(CooldownKey other)
        {
            return ReferenceEquals(_owner, other._owner)
                && string.Equals(_name, other._name, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is CooldownKey other && Equals(other);

        public override int GetHashCode()
        {
            // Reference hash: Unity objects override Equals/GetHashCode, we want identity.
            int ownerHash = _owner == null ? 0 : RuntimeHelpers.GetHashCode(_owner);
            return unchecked(ownerHash * 397 ^ _name.GetHashCode());
        }

        public override string ToString()
        {
            return _owner == null
                ? _name
                : $"{_owner.GetType().Name}#{RuntimeHelpers.GetHashCode(_owner)}:{_name}";
        }

        public static implicit operator CooldownKey(string name) => new CooldownKey(name);
    }

    // Main thread only. Time.time / Time.unscaledTime are not thread safe.
    public static class FastCooldown
    {
        private readonly struct CooldownData
        {
            public readonly float EndTime;
            public readonly float Duration;
            public readonly bool Unscaled;

            public CooldownData(float endTime, float duration, bool unscaled)
            {
                EndTime = endTime;
                Duration = duration;
                Unscaled = unscaled;
            }
        }

        private static readonly Dictionary<CooldownKey, CooldownData> Cooldowns = new();
        private static readonly List<CooldownKey> ScratchKeys = new();

        // Domain Reload can be disabled, so static state must be reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cooldowns.Clear();
            ScratchKeys.Clear();
        }

        private static float CurrentTime(bool unscaled) => unscaled ? Time.unscaledTime : Time.time;

        #region Core API

        public static void Set(CooldownKey key, float duration, bool unscaled = false)
        {
            if (duration <= 0f)
            {
                if (duration < 0f)
                    Debug.LogWarning($"[FastCooldown] Negative duration ({duration}) for '{key}'. Treated as ready.");

                Cooldowns.Remove(key);
                return;
            }

            float now = CurrentTime(unscaled);
            Cooldowns[key] = new CooldownData(now + duration, duration, unscaled);
        }

        public static bool IsReady(CooldownKey key)
        {
            if (!Cooldowns.TryGetValue(key, out var data))
                return true;

            return CurrentTime(data.Unscaled) >= data.EndTime;
        }

        public static bool IsActive(CooldownKey key) => !IsReady(key);

        public static bool Check(CooldownKey key, float duration, bool unscaled = false)
        {
            if (!IsReady(key))
                return false;

            Set(key, duration, unscaled);
            return true;
        }

        public static bool Check(CooldownKey key, float duration, out float remaining, bool unscaled = false)
        {
            remaining = GetTimeRemaining(key);
            if (remaining > 0f)
                return false;

            Set(key, duration, unscaled);
            return true;
        }
    
        public static bool CheckDelayed(CooldownKey key, float duration, bool unscaled = false)
        {
            if (!Cooldowns.ContainsKey(key))
            {
                Set(key, duration, unscaled);
                return false;
            }

            if (!IsReady(key))
                return false;

            Set(key, duration, unscaled);
            return true;
        }
        #endregion

        #region Time Info

        public static float GetTimeRemaining(CooldownKey key)
        {
            if (!Cooldowns.TryGetValue(key, out var data))
                return 0f;

            return Mathf.Max(0f, data.EndTime - CurrentTime(data.Unscaled));
        }

        public static float GetProgress(CooldownKey key)
        {
            if (!Cooldowns.TryGetValue(key, out var data))
                return 1f;

            if (data.Duration <= 0f)
                return 1f;

            float remaining = data.EndTime - CurrentTime(data.Unscaled);
            return Mathf.Clamp01(1f - remaining / data.Duration);
        }

        public static float GetDuration(CooldownKey key)
        {
            return Cooldowns.TryGetValue(key, out var data) ? data.Duration : 0f;
        }

        public static bool IsUnscaled(CooldownKey key)
        {
            return Cooldowns.TryGetValue(key, out var data) && data.Unscaled;
        }

        #endregion

        #region Batch Operations

        public static void SetMultiple(float duration, bool unscaled, params CooldownKey[] keys)
        {
            if (keys == null)
                return;

            for (int i = 0; i < keys.Length; i++)
                Set(keys[i], duration, unscaled);
        }

        public static void SetMultiple(float duration, params CooldownKey[] keys)
        {
            SetMultiple(duration, false, keys);
        }

        public static void RemoveMultiple(params CooldownKey[] keys)
        {
            if (keys == null)
                return;

            for (int i = 0; i < keys.Length; i++)
                Cooldowns.Remove(keys[i]);
        }

        public static bool AllReady(params CooldownKey[] keys)
        {
            if (keys == null || keys.Length == 0)
                return true;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!IsReady(keys[i]))
                    return false;
            }

            return true;
        }

        public static bool AnyReady(params CooldownKey[] keys)
        {
            if (keys == null || keys.Length == 0)
                return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (IsReady(keys[i]))
                    return true;
            }

            return false;
        }

        #endregion

        #region Cleanup

        public static void Remove(CooldownKey key) => Cooldowns.Remove(key);

        public static void RemoveAllFor(object owner)
        {
            if (owner == null || Cooldowns.Count == 0)
                return;

            ScratchKeys.Clear();

            foreach (var pair in Cooldowns)
            {
                if (ReferenceEquals(pair.Key.Owner, owner))
                    ScratchKeys.Add(pair.Key);
            }

            for (int i = 0; i < ScratchKeys.Count; i++)
                Cooldowns.Remove(ScratchKeys[i]);

            ScratchKeys.Clear();
        }

        public static void Clear() => Cooldowns.Clear();

        public static void Prune()
        {
            if (Cooldowns.Count == 0)
            {
                return;
            }

            ScratchKeys.Clear();

            float scaledNow = Time.time;
            float unscaledNow = Time.unscaledTime;

            foreach (var pair in Cooldowns)
            {
                float now = pair.Value.Unscaled ? unscaledNow : scaledNow;
                if (now >= pair.Value.EndTime)
                    ScratchKeys.Add(pair.Key);
            }

            for (int i = 0; i < ScratchKeys.Count; i++)
                Cooldowns.Remove(ScratchKeys[i]);

            ScratchKeys.Clear();
        }


        #endregion

        #region Diagnostics

        public static int Count => Cooldowns.Count;

        public static void GetActiveKeys(List<CooldownKey> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();

            foreach (var pair in Cooldowns)
            {
                if (CurrentTime(pair.Value.Unscaled) < pair.Value.EndTime)
                    buffer.Add(pair.Key);
            }
        }

        #endregion
    }
}
