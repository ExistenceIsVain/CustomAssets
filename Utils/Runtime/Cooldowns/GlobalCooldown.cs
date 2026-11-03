#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Utils.Cooldowns
{
    #region Public Types

    /// <summary>
    /// Clock used by <see cref="GlobalCooldown"/>. Implement to feed authoritative server time.
    /// </summary>
    public interface ICooldownTimeSource
    {
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// Immutable snapshot of a single cooldown entry.
    /// </summary>
    public readonly struct CooldownInfo
    {
        public readonly string Key;
        public readonly DateTime StartUtc;
        public readonly DateTime EndUtc;
        public readonly TimeSpan Duration;
        public readonly TimeSpan Remaining;
        public readonly float Progress;

        public CooldownInfo(string key, DateTime startUtc, DateTime endUtc, TimeSpan duration, TimeSpan remaining, float progress)
        {
            Key = key;
            StartUtc = startUtc;
            EndUtc = endUtc;
            Duration = duration;
            Remaining = remaining;
            Progress = progress;
        }

        public bool IsReady => Remaining <= TimeSpan.Zero;

        public DateTime StartLocal => StartUtc.ToLocalTime();
        public DateTime EndLocal => EndUtc.ToLocalTime();

        public override string ToString()
        {
            return $"{Key}: {GlobalCooldown.Format(Remaining)} left ({Progress:P0})";
        }
    }

    /// <summary>
    /// Rendering style for remaining time. See <see cref="GlobalCooldown.Format"/>.
    /// </summary>
    public enum CooldownFormat
    {
        /// <summary>Largest three meaningful units: "2d 5h 30m", "5h 30m 15s", "30m 15s", "15s".</summary>
        Auto,

        /// <summary>Largest unit only: "2d", "5h", "30m", "15s".</summary>
        Short,

        /// <summary>Digital clock, days folded into hours: "53:30:15".</summary>
        Clock,

        /// <summary>Days split out, rest as a clock: "2d 05:30:15".</summary>
        Full
    }

    #endregion

    /// <summary>
    /// Persistent wall-clock cooldowns for boosters, daily/weekly rewards and energy refills.
    /// Survives application restarts; stored as a single JSON blob in PlayerPrefs.
    /// <para>
    /// For short in-session gameplay cooldowns use <c>FastCooldown</c> instead — it runs on
    /// <c>Time.time</c> and never touches disk.
    /// </para>
    /// Main thread only. PlayerPrefs is not thread safe.
    /// </summary>
    public static class GlobalCooldown
    {
        #region State

        private const string StorePrefsKey = "global_cooldown_store";
        private const string CorruptPrefsKey = "global_cooldown_store_corrupt";
        private const int StoreVersion = 1;

        /// <summary>Backwards clock drift below this is treated as NTP jitter, not tampering.</summary>
        private const long ClockToleranceSeconds = 5;

        /// <summary>How often a pure clock advance (no data change) is allowed to hit the disk.</summary>
        private const long HighWaterSaveIntervalSeconds = 60;

        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
        private static readonly List<string> ScratchKeys = new();

        private static ICooldownTimeSource? _timeSource;
        private static bool _loaded;
        private static bool _dirty;
        private static bool _clockRolledBack;

        /// <summary>Monotonic high-water mark of observed UTC, in unix seconds.</summary>
        private static long _lastSeenUtc;

        /// <summary>Value of <see cref="_lastSeenUtc"/> at the last flush, to throttle disk writes.</summary>
        private static long _persistedLastSeenUtc;

        /// <summary>
        /// When true (default) every mutation is written to PlayerPrefs immediately.
        /// Set to false around bulk edits and call <see cref="Save"/> once when done.
        /// </summary>
        public static bool AutoSave { get; set; } = true;

        /// <summary>
        /// True once the system clock has been observed moving backwards past the tolerance.
        /// Diagnostics only — the cooldowns themselves are already protected.
        /// </summary>
        public static bool ClockRolledBack => _clockRolledBack;

        [Serializable]
        private sealed class Entry
        {
            public string key = string.Empty;
            public long startUtc;
            public long endUtc;
        }

        [Serializable]
        private sealed class Store
        {
            public int version = StoreVersion;
            public long lastSeenUtc;
            public List<Entry> entries = new();
        }

        #endregion

        #region Initialization

        // Domain Reload can be disabled, so static state must be reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
            ScratchKeys.Clear();

            _timeSource = null;
            _loaded = false;
            _dirty = false;
            _clockRolledBack = false;
            _lastSeenUtc = 0;
            _persistedLastSeenUtc = 0;
            AutoSave = true;

            // Unsubscribe first: with Domain Reload off these handlers would stack up.
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;

            Application.focusChanged -= OnApplicationFocusChanged;
            Application.focusChanged += OnApplicationFocusChanged;
        }

        private static void OnApplicationQuitting() => Flush();

        private static void OnApplicationFocusChanged(bool hasFocus)
        {
            // Losing focus on mobile can be the last moment before the process is killed.
            if (!hasFocus)
                Flush();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            Load();
        }

        #endregion

        #region Set API

        /// <summary>
        /// Starts a cooldown. A duration of zero or less removes the entry instead (ready immediately).
        /// </summary>
        public static void Set(string key, TimeSpan duration)
        {
            if (!ValidateKey(key))
                return;

            EnsureLoaded();

            if (duration <= TimeSpan.Zero)
            {
                if (duration < TimeSpan.Zero)
                    Debug.LogWarning($"[GlobalCooldown] Negative duration ({duration}) for '{key}'. Treated as ready.");

                Remove(key);
                return;
            }

            long now = NowUnix();
            Write(key, now, now + (long)Math.Ceiling(duration.TotalSeconds));
        }

        public static void SetSeconds(string key, double seconds) => Set(key, TimeSpan.FromSeconds(seconds));

        public static void SetMinutes(string key, double minutes) => Set(key, TimeSpan.FromMinutes(minutes));

        public static void SetHours(string key, double hours) => Set(key, TimeSpan.FromHours(hours));

        public static void SetDays(string key, double days) => Set(key, TimeSpan.FromDays(days));

        /// <summary>
        /// Runs the cooldown until an absolute UTC moment. A moment in the past clears the entry.
        /// </summary>
        public static void SetUntilUtc(string key, DateTime utc)
        {
            if (!ValidateKey(key))
                return;

            EnsureLoaded();

            long now = NowUnix();
            long end = ToUnix(utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime());

            if (end <= now)
            {
                Remove(key);
                return;
            }

            Write(key, now, end);
        }

        /// <summary>
        /// Runs the cooldown until an absolute moment in the device's local time zone.
        /// </summary>
        public static void SetUntilLocal(string key, DateTime local) => SetUntilUtc(key, LocalToUtc(local));

        /// <summary>
        /// Pushes an active cooldown further out, measured from its current end.
        /// Returns false if the key has no entry — there is nothing to extend.
        /// </summary>
        public static bool Extend(string key, TimeSpan amount)
        {
            if (!ValidateKey(key))
                return false;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return false;

            long delta = (long)Math.Round(amount.TotalSeconds);
            if (delta == 0)
                return true;

            long now = NowUnix();

            // An already expired entry extends from now, not from its stale end.
            long baseline = Math.Max(entry.endUtc, now);
            entry.endUtc = Math.Max(entry.startUtc, baseline + delta);

            MarkDirty();
            return true;
        }

        /// <summary>
        /// Pulls an active cooldown's end closer, clamped so it never lands before now.
        /// The entry is kept even when it becomes ready — remove it explicitly if you need it gone.
        /// </summary>
        public static bool Reduce(string key, TimeSpan amount)
        {
            if (!ValidateKey(key))
                return false;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return false;

            long delta = (long)Math.Round(amount.TotalSeconds);
            if (delta == 0)
                return true;

            long now = NowUnix();
            entry.endUtc = Math.Max(now, entry.endUtc - delta);

            MarkDirty();
            return true;
        }

        #endregion

        #region Reset Schedules

        /// <summary>
        /// Runs until the next occurrence of the given wall-clock time in the device's local zone.
        /// The default (00:00) is midnight tonight.
        /// </summary>
        public static void SetDailyReset(string key, int hour = 0, int minute = 0, int second = 0)
        {
            DateTime nowLocal = EffectiveUtcNow.ToLocalTime();
            DateTime next = new(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
            next = next.AddHours(hour).AddMinutes(minute).AddSeconds(second);

            if (next <= nowLocal)
                next = next.AddDays(1);

            SetUntilUtc(key, LocalToUtc(next));
        }

        /// <summary>
        /// Same as <see cref="SetDailyReset"/> but anchored to UTC — use this when the reset
        /// must line up across time zones (server-driven dailies).
        /// </summary>
        public static void SetDailyResetUtc(string key, int hour = 0, int minute = 0, int second = 0)
        {
            DateTime nowUtc = EffectiveUtcNow;
            DateTime next = new(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
            next = next.AddHours(hour).AddMinutes(minute).AddSeconds(second);

            if (next <= nowUtc)
                next = next.AddDays(1);

            SetUntilUtc(key, next);
        }

        /// <summary>
        /// Runs until the next occurrence of a weekday at a local wall-clock time.
        /// </summary>
        public static void SetWeeklyReset(string key, DayOfWeek day, int hour = 0, int minute = 0, int second = 0)
        {
            DateTime nowLocal = EffectiveUtcNow.ToLocalTime();
            DateTime next = new(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
            next = next.AddHours(hour).AddMinutes(minute).AddSeconds(second);

            int daysAhead = ((int)day - (int)nowLocal.DayOfWeek + 7) % 7;
            next = next.AddDays(daysAhead);

            if (next <= nowLocal)
                next = next.AddDays(7);

            SetUntilUtc(key, LocalToUtc(next));
        }

        /// <summary>
        /// Runs until the next occurrence of a day of the month at a local wall-clock time.
        /// Days past the end of a short month are clamped (31 → 28/29 in February).
        /// </summary>
        public static void SetMonthlyReset(string key, int dayOfMonth = 1, int hour = 0, int minute = 0, int second = 0)
        {
            DateTime nowLocal = EffectiveUtcNow.ToLocalTime();

            DateTime next = BuildMonthly(nowLocal.Year, nowLocal.Month, dayOfMonth, hour, minute, second);
            if (next <= nowLocal)
            {
                DateTime nextMonth = new DateTime(nowLocal.Year, nowLocal.Month, 1).AddMonths(1);
                next = BuildMonthly(nextMonth.Year, nextMonth.Month, dayOfMonth, hour, minute, second);
            }

            SetUntilUtc(key, LocalToUtc(next));
        }

        private static DateTime BuildMonthly(int year, int month, int dayOfMonth, int hour, int minute, int second)
        {
            int day = Mathf.Clamp(dayOfMonth, 1, DateTime.DaysInMonth(year, month));
            DateTime date = new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
            return date.AddHours(hour).AddMinutes(minute).AddSeconds(second);
        }

        #endregion

        #region Query API

        /// <summary>
        /// True when the key has no entry or its entry has expired. Never mutates state.
        /// </summary>
        public static bool IsReady(string key)
        {
            if (string.IsNullOrEmpty(key))
                return true;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return true;

            return NowUnix() >= entry.endUtc;
        }

        /// <summary>True while the cooldown is still running.</summary>
        public static bool IsActive(string key) => !IsReady(key);

        /// <summary>
        /// True when a record exists at all, expired or not. Distinct from <see cref="IsActive"/>:
        /// finished cooldowns stay on disk until <see cref="Remove"/> or <see cref="Prune"/>.
        /// </summary>
        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            EnsureLoaded();
            return Entries.ContainsKey(key);
        }

        /// <summary>
        /// Atomic test-and-set: when ready, starts the cooldown and returns true.
        /// A key driven by <c>Check</c> must not also be driven by a separate <c>Set</c>.
        /// </summary>
        public static bool Check(string key, TimeSpan duration)
        {
            if (!IsReady(key))
                return false;

            Set(key, duration);
            return true;
        }

        /// <summary>
        /// <see cref="Check(string, TimeSpan)"/> that also reports how long is left when it refuses.
        /// </summary>
        public static bool Check(string key, TimeSpan duration, out TimeSpan remaining)
        {
            remaining = GetTimeRemaining(key);
            if (remaining > TimeSpan.Zero)
                return false;

            Set(key, duration);
            return true;
        }

        /// <summary>Reads the full snapshot of an entry. False when the key is unknown.</summary>
        public static bool TryGet(string key, out CooldownInfo info)
        {
            info = default;

            if (string.IsNullOrEmpty(key))
                return false;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return false;

            long now = NowUnix();
            info = new CooldownInfo(
                entry.key,
                FromUnix(entry.startUtc),
                FromUnix(entry.endUtc),
                TimeSpan.FromSeconds(entry.endUtc - entry.startUtc),
                TimeSpan.FromSeconds(Math.Max(0, entry.endUtc - now)),
                CalculateProgress(entry, now));

            return true;
        }

        /// <summary>Time left, or <see cref="TimeSpan.Zero"/> when ready or unknown.</summary>
        public static TimeSpan GetTimeRemaining(string key)
        {
            if (string.IsNullOrEmpty(key))
                return TimeSpan.Zero;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(Math.Max(0, entry.endUtc - NowUnix()));
        }

        /// <summary>
        /// Elapsed fraction in the 0..1 range: 0 the moment it starts, 1 when it finishes.
        /// Unknown keys report 1 (nothing to wait for).
        /// </summary>
        public static float GetProgress(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 1f;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return 1f;

            return CalculateProgress(entry, NowUnix());
        }

        /// <summary>Full length the cooldown was started with, or zero when unknown.</summary>
        public static TimeSpan GetDuration(string key)
        {
            if (string.IsNullOrEmpty(key))
                return TimeSpan.Zero;

            EnsureLoaded();

            if (!Entries.TryGetValue(key, out Entry entry))
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(Math.Max(0, entry.endUtc - entry.startUtc));
        }

        /// <summary>Absolute end moment in UTC, or <see cref="DateTime.MinValue"/> when unknown.</summary>
        public static DateTime GetEndTimeUtc(string key)
        {
            if (string.IsNullOrEmpty(key))
                return DateTime.MinValue;

            EnsureLoaded();

            return Entries.TryGetValue(key, out Entry entry) ? FromUnix(entry.endUtc) : DateTime.MinValue;
        }

        /// <summary>Absolute end moment in local time — for labels like "next reward at 14:00".</summary>
        public static DateTime GetEndTimeLocal(string key)
        {
            DateTime utc = GetEndTimeUtc(key);
            return utc == DateTime.MinValue ? DateTime.MinValue : utc.ToLocalTime();
        }

        #endregion

        #region Batch Operations

        /// <summary>True when every key is ready. An empty set counts as ready.</summary>
        public static bool AllReady(params string[]? keys)
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

        /// <summary>True when at least one key is ready. An empty set counts as not ready.</summary>
        public static bool AnyReady(params string[]? keys)
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

        /// <summary>Starts the same cooldown on several keys, writing to disk once.</summary>
        public static void SetMultiple(TimeSpan duration, params string[]? keys)
        {
            if (keys == null || keys.Length == 0)
                return;

            bool autoSave = AutoSave;
            AutoSave = false;

            try
            {
                for (int i = 0; i < keys.Length; i++)
                    Set(keys[i], duration);
            }
            finally
            {
                // A leaked AutoSave = false would silently stop every later write.
                AutoSave = autoSave;
            }

            if (AutoSave)
                Flush();
        }

        /// <summary>Removes several keys, writing to disk once.</summary>
        public static void RemoveMultiple(params string[]? keys)
        {
            if (keys == null || keys.Length == 0)
                return;

            EnsureLoaded();

            bool removed = false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!string.IsNullOrEmpty(keys[i]) && Entries.Remove(keys[i]))
                    removed = true;
            }

            if (removed)
                MarkDirty();
        }

        #endregion

        #region Formatting

        /// <summary>
        /// Renders a time span. Zero and negative spans render as the format's own zero
        /// ("0s", "00:00:00") — substitute a "ready" caption at the call site if you need one.
        /// </summary>
        public static string Format(TimeSpan time, CooldownFormat format = CooldownFormat.Auto)
        {
            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;

            int days = (int)time.TotalDays;
            int hours = time.Hours;
            int minutes = time.Minutes;
            int seconds = time.Seconds;

            switch (format)
            {
                case CooldownFormat.Short:
                    if (days >= 1)
                        return string.Format(CultureInfo.InvariantCulture, "{0}d", days);
                    if (time.TotalHours >= 1)
                        return string.Format(CultureInfo.InvariantCulture, "{0}h", hours);
                    if (time.TotalMinutes >= 1)
                        return string.Format(CultureInfo.InvariantCulture, "{0}m", minutes);
                    return string.Format(CultureInfo.InvariantCulture, "{0}s", seconds);

                case CooldownFormat.Clock:
                    return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                        (int)time.TotalHours, minutes, seconds);

                case CooldownFormat.Full:
                    return days >= 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0}d {1:00}:{2:00}:{3:00}", days, hours, minutes, seconds)
                        : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", hours, minutes, seconds);

                default:
                    if (days >= 1)
                        return string.Format(CultureInfo.InvariantCulture, "{0}d {1}h {2}m", days, hours, minutes);
                    if (time.TotalHours >= 1)
                        return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m {2}s", hours, minutes, seconds);
                    if (time.TotalMinutes >= 1)
                        return string.Format(CultureInfo.InvariantCulture, "{0}m {1}s", minutes, seconds);
                    return string.Format(CultureInfo.InvariantCulture, "{0}s", seconds);
            }
        }

        /// <summary>
        /// Renders the time left on a key, or <paramref name="readyLabel"/> when it is ready.
        /// Pass a localized caption for <paramref name="readyLabel"/>.
        /// </summary>
        public static string GetFormattedTime(string key, CooldownFormat format = CooldownFormat.Auto, string readyLabel = "Ready")
        {
            TimeSpan remaining = GetTimeRemaining(key);
            return remaining <= TimeSpan.Zero ? readyLabel : Format(remaining, format);
        }

        #endregion

        #region Cleanup

        /// <summary>Deletes one entry. Returns false when there was nothing to delete.</summary>
        public static bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            EnsureLoaded();

            if (!Entries.Remove(key))
                return false;

            MarkDirty();
            return true;
        }

        /// <summary>Deletes every expired entry and returns how many went away.</summary>
        public static int Prune()
        {
            EnsureLoaded();

            if (Entries.Count == 0)
                return 0;

            long now = NowUnix();

            ScratchKeys.Clear();

            foreach (KeyValuePair<string, Entry> pair in Entries)
            {
                if (now >= pair.Value.endUtc)
                    ScratchKeys.Add(pair.Key);
            }

            for (int i = 0; i < ScratchKeys.Count; i++)
                Entries.Remove(ScratchKeys[i]);

            int removed = ScratchKeys.Count;
            ScratchKeys.Clear();

            if (removed > 0)
                MarkDirty();

            return removed;
        }

        /// <summary>
        /// Wipes every cooldown from memory and from PlayerPrefs. The clock high-water mark
        /// is kept — clearing it would hand back the rollback exploit.
        /// </summary>
        public static void ClearAll()
        {
            EnsureLoaded();

            if (Entries.Count == 0)
                return;

            Entries.Clear();
            MarkDirty();
        }

        #endregion

        #region Persistence

        /// <summary>Writes pending changes to PlayerPrefs. No-op when nothing changed.</summary>
        public static void Save() => Flush();

        /// <summary>Drops in-memory state and re-reads PlayerPrefs, discarding unsaved changes.</summary>
        public static void Reload()
        {
            _dirty = false;
            _loaded = true;
            Load();
        }

        private static void Load()
        {
            Entries.Clear();
            _lastSeenUtc = 0;
            _persistedLastSeenUtc = 0;

            string raw = PlayerPrefs.GetString(StorePrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return;

            Store? store = null;

            try
            {
                store = JsonUtility.FromJson<Store>(raw);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GlobalCooldown] Store is corrupt and was reset: {e.Message}");
            }

            if (store?.entries == null)
            {
                // Keep the bad payload around so it can be inspected instead of vanishing.
                PlayerPrefs.SetString(CorruptPrefsKey, raw);
                PlayerPrefs.DeleteKey(StorePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            if (store.version != StoreVersion)
                Debug.LogWarning($"[GlobalCooldown] Store version {store.version} differs from {StoreVersion}. Reading as-is.");

            _lastSeenUtc = Math.Max(0, store.lastSeenUtc);
            _persistedLastSeenUtc = _lastSeenUtc;

            int dropped = 0;

            for (int i = 0; i < store.entries.Count; i++)
            {
                Entry? entry = store.entries[i];

                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.endUtc < entry.startUtc)
                {
                    dropped++;
                    continue;
                }

                // Later duplicates lose: the first record for a key wins.
                if (!Entries.ContainsKey(entry.key))
                    Entries.Add(entry.key, entry);
                else
                    dropped++;
            }

            if (dropped > 0)
                Debug.LogWarning($"[GlobalCooldown] Dropped {dropped} invalid entr{(dropped == 1 ? "y" : "ies")} while loading.");
        }

        private static void MarkDirty()
        {
            _dirty = true;

            if (AutoSave)
                Flush();
        }

        private static void Flush()
        {
            if (!_loaded)
                return;

            // A clock advance alone is worth a write only every so often.
            if (!_dirty && _lastSeenUtc - _persistedLastSeenUtc < HighWaterSaveIntervalSeconds)
                return;

            Store store = new()
            {
                version = StoreVersion,
                lastSeenUtc = _lastSeenUtc,
                entries = new List<Entry>(Entries.Count)
            };

            foreach (KeyValuePair<string, Entry> pair in Entries)
                store.entries.Add(pair.Value);

            PlayerPrefs.SetString(StorePrefsKey, JsonUtility.ToJson(store));
            PlayerPrefs.Save();

            _dirty = false;
            _persistedLastSeenUtc = _lastSeenUtc;
        }

        #endregion

        #region Clock

        /// <summary>
        /// The clock every cooldown is measured against: the later of the system clock and the
        /// highest UTC ever recorded. Winding the device clock back therefore buys nothing.
        /// </summary>
        public static DateTime EffectiveUtcNow
        {
            get
            {
                EnsureLoaded();
                return FromUnix(NowUnix());
            }
        }

        /// <summary>
        /// Replaces the underlying clock — pass a server-time implementation to close the
        /// remaining hole (winding the device clock <em>forward</em> while offline).
        /// Pass null to go back to <see cref="DateTime.UtcNow"/>.
        /// </summary>
        public static void SetTimeSource(ICooldownTimeSource? source) => _timeSource = source;

        /// <summary>
        /// Drops the high-water mark back to the current clock. Only call this once the current
        /// time is trusted (a server sync), otherwise it re-opens the rollback exploit.
        /// Needed when a device that was set years into the future gets corrected.
        /// </summary>
        public static void ResetClockGuard()
        {
            EnsureLoaded();

            _lastSeenUtc = ToUnix(RawUtcNow());
            _clockRolledBack = false;

            MarkDirty();
        }

        private static DateTime RawUtcNow() => _timeSource?.UtcNow ?? DateTime.UtcNow;

        /// <summary>
        /// Monotonic "now" in unix seconds. Never returns a value lower than one already seen.
        /// </summary>
        private static long NowUnix()
        {
            long raw = ToUnix(RawUtcNow());

            if (raw > _lastSeenUtc)
            {
                _lastSeenUtc = raw;

                // Persist the advance occasionally so the mark survives a crash.
                if (_lastSeenUtc - _persistedLastSeenUtc >= HighWaterSaveIntervalSeconds && AutoSave)
                    Flush();
            }
            else if (raw < _lastSeenUtc - ClockToleranceSeconds)
            {
                _clockRolledBack = true;
            }

            return _lastSeenUtc;
        }

        #endregion

        #region Diagnostics

        /// <summary>Number of stored entries, expired ones included.</summary>
        public static int Count
        {
            get
            {
                EnsureLoaded();
                return Entries.Count;
            }
        }

        /// <summary>Number of entries still running.</summary>
        public static int ActiveCount
        {
            get
            {
                EnsureLoaded();

                if (Entries.Count == 0)
                    return 0;

                long now = NowUnix();
                int count = 0;

                foreach (KeyValuePair<string, Entry> pair in Entries)
                {
                    if (now < pair.Value.endUtc)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Fills the buffer with every stored key. The buffer is cleared first.</summary>
        public static void GetKeys(List<string>? buffer)
        {
            if (buffer == null)
                return;

            EnsureLoaded();
            buffer.Clear();

            foreach (KeyValuePair<string, Entry> pair in Entries)
                buffer.Add(pair.Key);
        }

        /// <summary>Fills the buffer with the keys still running. The buffer is cleared first.</summary>
        public static void GetActiveKeys(List<string>? buffer)
        {
            if (buffer == null)
                return;

            EnsureLoaded();
            buffer.Clear();

            long now = NowUnix();

            foreach (KeyValuePair<string, Entry> pair in Entries)
            {
                if (now < pair.Value.endUtc)
                    buffer.Add(pair.Key);
            }
        }

        #endregion

        #region Internal Helpers

        private static bool ValidateKey(string key)
        {
            if (!string.IsNullOrEmpty(key))
                return true;

            Debug.LogWarning("[GlobalCooldown] Key must not be null or empty.");
            return false;
        }

        private static void Write(string key, long startUtc, long endUtc)
        {
            if (Entries.TryGetValue(key, out Entry entry))
            {
                entry.startUtc = startUtc;
                entry.endUtc = endUtc;
            }
            else
            {
                Entries.Add(key, new Entry { key = key, startUtc = startUtc, endUtc = endUtc });
            }

            MarkDirty();
        }

        private static float CalculateProgress(Entry entry, long now)
        {
            long duration = entry.endUtc - entry.startUtc;
            if (duration <= 0)
                return 1f;

            long remaining = entry.endUtc - now;
            return Mathf.Clamp01(1f - (float)remaining / duration);
        }

        /// <summary>
        /// Converts a local wall-clock time to UTC, resolving the two daylight-saving edge cases:
        /// times inside the spring-forward gap do not exist, ambiguous autumn times resolve to
        /// standard time.
        /// </summary>
        private static DateTime LocalToUtc(DateTime local)
        {
            DateTime unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            TimeZoneInfo tz = TimeZoneInfo.Local;

            if (tz.IsInvalidTime(unspecified))
                unspecified = unspecified.AddHours(1);

            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
            }
            catch (ArgumentException)
            {
                // Exotic transition the adjustment above did not cover — fall back rather than throw.
                return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            }
        }

        private static long ToUnix(DateTime utc) => (long)(utc - UnixEpoch).TotalSeconds;

        private static DateTime FromUnix(long seconds) => UnixEpoch.AddSeconds(seconds);

        #endregion
    }
}
