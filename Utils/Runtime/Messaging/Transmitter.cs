#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils.Messaging
{
    // Broadcast receiver - fire-and-forget
    public interface IReceiver<T>
    {
        void Receive(T data);
    }

    // Request-response provider
    public interface IProvider<TRequest, TResponse>
    {
        TResponse Provide(TRequest request);
    }

    public static class Transmitter
    {
        // Runtime storage. Receivers are keyed by message type; providers by the closed
        // IProvider<TRequest, TResponse> type, so two providers with the same request type but
        // different response types live in separate slots instead of failing the cast in Request.
        private static readonly Dictionary<Type, List<object>> receivers = new();
        private static readonly Dictionary<Type, List<object>> providers = new();

        // Domain Reload can be disabled, so static state must be reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            receivers.Clear();
            providers.Clear();
#if UNITY_EDITOR
            receiverEntries.Clear();
            providerEntries.Clear();
            OnRegistryChanged = null;
#endif
        }

        #region RECEIVERS

        public static void Register<T>(IReceiver<T> receiver, bool single = false, bool replace = false, MonoBehaviour? creator = null)
        {
            if (receiver == null)
            {
                Debug.LogWarning("[Transmitter] Trying to register null receiver");
                return;
            }

            AddToSlot(receivers, typeof(T), receiver, single, replace, TransmitterEntryType.Receiver, creator);
        }

        public static void Unregister<T>(IReceiver<T> receiver)
        {
            if (receiver == null) return;

            RemoveFromSlot(receivers, typeof(T), receiver, TransmitterEntryType.Receiver);
        }

        // Broadcast fire-and-forget, zero-alloc
        public static void Send<T>(T data)
        {
            if (data == null)
            {
                Debug.LogWarning($"[Transmitter] Trying to send null data of type {typeof(T)}");
                return;
            }

            var type = typeof(T);

            if (!receivers.TryGetValue(type, out var list)) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                // A receiver may unregister others while handling the message.
                if (i >= list.Count) continue;

                var obj = list[i];

                if (!IsAlive(obj))
                {
                    list.RemoveAt(i);
                    MarkEntryDestroyed(type, obj, TransmitterEntryType.Receiver);
                    continue;
                }

                try
                {
                    ((IReceiver<T>)obj).Receive(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Transmitter] Error sending {typeof(T)} to {obj.GetType()}: {ex}");
                }
            }
        }

        #endregion

        #region PROVIDERS

        public static void Register<TRequest, TResponse>(IProvider<TRequest, TResponse> provider, bool single = false, bool replace = false, MonoBehaviour? creator = null)
        {
            if (provider == null)
            {
                Debug.LogWarning("[Transmitter] Trying to register null provider");
                return;
            }

            AddToSlot(providers, typeof(IProvider<TRequest, TResponse>), provider, single, replace, TransmitterEntryType.Provider, creator);
        }

        public static void Unregister<TRequest, TResponse>(IProvider<TRequest, TResponse> provider)
        {
            if (provider == null) return;

            RemoveFromSlot(providers, typeof(IProvider<TRequest, TResponse>), provider, TransmitterEntryType.Provider);
        }

        /// <summary>
        /// Synchronous request. Returns false when no live provider answered.
        /// <paramref name="silent"/> suppresses the "no provider" error for optional requests.
        /// </summary>
        public static bool Request<TRequest, TResponse>(TRequest request, out TResponse response, bool silent = false)
        {
            response = default!;
            var type = typeof(IProvider<TRequest, TResponse>);

            if (providers.TryGetValue(type, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var obj = list[i];

                    if (!IsAlive(obj))
                    {
                        list.RemoveAt(i);
                        MarkEntryDestroyed(type, obj, TransmitterEntryType.Provider);
                        continue;
                    }

                    try
                    {
                        response = ((IProvider<TRequest, TResponse>)obj).Provide(request);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Transmitter] Error requesting {typeof(TRequest).Name} from {obj.GetType()}: {ex}");
                    }
                }
            }

            if (!silent)
            {
                Debug.LogError(
                    $"[Transmitter] No provider for {typeof(TRequest).Name} -> {typeof(TResponse).Name}.\n" +
                    "Is the provider MonoBehaviour in the scene?");
            }

            return false;
        }

        // Request-response: with callback
        public static void Request<TRequest, TResponse>(TRequest request, Action<TResponse> onAnswer, bool silent = false)
        {
            if (Request<TRequest, TResponse>(request, out var response, silent))
                onAnswer?.Invoke(response);
        }

        // Request-response: direct return (returns default if no provider)
        public static TResponse? Ask<TRequest, TResponse>(TRequest request, bool silent = false)
        {
            if (Request<TRequest, TResponse>(request, out var response, silent))
                return response;

            return default;
        }

        #endregion

        #region REGISTRY

        private static void AddToSlot(
            Dictionary<Type, List<object>> registry,
            Type type,
            object obj,
            bool single,
            bool replace,
            TransmitterEntryType entryType,
            MonoBehaviour? creator)
        {
            if (!registry.TryGetValue(type, out var list))
            {
                list = new List<object>();
                registry[type] = list;
            }

            if (single && list.Count > 0)
            {
                if (!replace) return;

                list.Clear();
                MarkSlotDestroyed(type, entryType);
            }

            if (!list.Contains(obj))
                list.Add(obj);

            AddEntry(type, obj, entryType, creator);
        }

        private static void RemoveFromSlot(Dictionary<Type, List<object>> registry, Type type, object obj, TransmitterEntryType entryType)
        {
            if (registry.TryGetValue(type, out var list))
                list.Remove(obj);

            MarkEntryDestroyed(type, obj, entryType);
        }

        public static void CleanupDeadReferences()
        {
            CleanupDead(receivers);
            CleanupDead(providers);

#if UNITY_EDITOR
            PruneEntries(receiverEntries, receivers);
            PruneEntries(providerEntries, providers);
            OnRegistryChanged?.Invoke();
#endif
        }

        private static void CleanupDead(Dictionary<Type, List<object>> registry)
        {
            List<Type>? emptyTypes = null;

            foreach (var kvp in registry)
            {
                kvp.Value.RemoveAll(obj => !IsAlive(obj));

                if (kvp.Value.Count == 0)
                    (emptyTypes ??= new List<Type>()).Add(kvp.Key);
            }

            if (emptyTypes == null) return;

            foreach (var type in emptyTypes)
                registry.Remove(type);
        }

        public static void ClearAll()
        {
            receivers.Clear();
            providers.Clear();

#if UNITY_EDITOR
            receiverEntries.Clear();
            providerEntries.Clear();
            OnRegistryChanged?.Invoke();
#endif
        }

        private static bool IsAlive(object obj)
        {
            if (obj == null) return false;
            if (obj is UnityEngine.Object uo) return uo != null;
            return true;
        }

        #endregion

        #region MONITORING

        // The monitor window is the only reader of the entries, so in a build these calls are
        // compiled out and Register allocates nothing extra.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void AddEntry(Type type, object obj, TransmitterEntryType entryType, MonoBehaviour? creator)
        {
#if UNITY_EDITOR
            var entries = EntriesFor(entryType);

            if (!entries.TryGetValue(type, out var list))
            {
                list = new List<TransmitterEntry>();
                entries[type] = list;
            }

            // Re-registering the same object (OnEnable after OnDisable) revives its entry
            // instead of adding a new one, so the list does not grow with every toggle.
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i].weakReference.Target, obj))
                {
                    list[i].Revive();
                    OnRegistryChanged?.Invoke();
                    return;
                }
            }

            list.Add(new TransmitterEntry(type, obj, entryType, creator));
            TrimDeadEntries(list);
            OnRegistryChanged?.Invoke();
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void MarkEntryDestroyed(Type type, object obj, TransmitterEntryType entryType)
        {
#if UNITY_EDITOR
            if (EntriesFor(entryType).TryGetValue(type, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var entry = list[i];

                    if (!entry.isDestroyed && entry.Matches(obj))
                    {
                        entry.MarkAsDestroyed();
                        break;
                    }
                }
            }

            OnRegistryChanged?.Invoke();
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void MarkSlotDestroyed(Type type, TransmitterEntryType entryType)
        {
#if UNITY_EDITOR
            if (!EntriesFor(entryType).TryGetValue(type, out var list)) return;

            foreach (var entry in list)
            {
                if (!entry.isDestroyed)
                    entry.MarkAsDestroyed();
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>Dead entries kept per slot for the monitor's "Show Dead References".</summary>
        private const int MaxDeadEntriesPerSlot = 32;

        private static readonly Dictionary<Type, List<TransmitterEntry>> receiverEntries = new();
        private static readonly Dictionary<Type, List<TransmitterEntry>> providerEntries = new();

        public static event Action? OnRegistryChanged;

        private static Dictionary<Type, List<TransmitterEntry>> EntriesFor(TransmitterEntryType entryType)
        {
            return entryType == TransmitterEntryType.Receiver ? receiverEntries : providerEntries;
        }

        // Objects that are spawned and destroyed all the time (enemies, bullets) would otherwise
        // leave one dead entry each, forever.
        private static void TrimDeadEntries(List<TransmitterEntry> list)
        {
            int dead = 0;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].IsAlive() && ++dead > MaxDeadEntriesPerSlot)
                    list.RemoveAt(i);
            }
        }

        private static void PruneEntries(Dictionary<Type, List<TransmitterEntry>> entries, Dictionary<Type, List<object>> registry)
        {
            List<Type>? emptyTypes = null;

            foreach (var kvp in entries)
            {
                registry.TryGetValue(kvp.Key, out var current);
                kvp.Value.RemoveAll(entry => current == null || !current.Exists(entry.Matches));

                if (kvp.Value.Count == 0)
                    (emptyTypes ??= new List<Type>()).Add(kvp.Key);
            }

            if (emptyTypes == null) return;

            foreach (var type in emptyTypes)
                entries.Remove(type);
        }

        /// <summary>Key: message type.</summary>
        public static Dictionary<Type, List<TransmitterEntry>> GetReceiverEntries() => Snapshot(receiverEntries);

        /// <summary>Key: IProvider&lt;TRequest, TResponse&gt;.</summary>
        public static Dictionary<Type, List<TransmitterEntry>> GetProviderEntries() => Snapshot(providerEntries);

        private static Dictionary<Type, List<TransmitterEntry>> Snapshot(Dictionary<Type, List<TransmitterEntry>> entries)
        {
            foreach (var list in entries.Values)
            {
                foreach (var entry in list)
                    entry.IsAlive();
            }

            return new Dictionary<Type, List<TransmitterEntry>>(entries);
        }
#endif

        #endregion
    }

    public enum TransmitterEntryType
    {
        Receiver,
        Provider
    }

#if UNITY_EDITOR
    /// <summary>Registry record for the Transmitter Monitor window. Editor only.</summary>
    public class TransmitterEntry
    {
        public string typeName;
        public string className;
        public string objectName;
        public TransmitterEntryType entryType;
        public bool isDestroyed;
        public DateTime registrationTime;
        public DateTime? destroyTime;
        public WeakReference weakReference;
        public int? instanceId;
        public int? gameObjectInstanceId;
        public Type classType;

        public TransmitterEntry(Type type, object obj, TransmitterEntryType entryType, MonoBehaviour? creator = null)
        {
            typeName = type.Name;
            classType = obj.GetType();
            className = classType.Name;
            this.entryType = entryType;

            if (obj is MonoBehaviour mb)
            {
                objectName = mb.gameObject.name;
                gameObjectInstanceId = mb.gameObject.GetInstanceID();
            }
            else if (creator != null)
            {
                objectName = creator.gameObject.name;
                gameObjectInstanceId = creator.gameObject.GetInstanceID();
            }
            else
            {
                objectName = obj.ToString() ?? "Unknown";
            }

            isDestroyed = false;
            registrationTime = DateTime.Now;
            weakReference = new WeakReference(obj);

            if (obj is UnityEngine.Object unityObj)
                instanceId = unityObj.GetInstanceID();
        }

        public void MarkAsDestroyed()
        {
            isDestroyed = true;
            destroyTime = DateTime.Now;
        }

        public void Revive()
        {
            isDestroyed = false;
            destroyTime = null;
            registrationTime = DateTime.Now;
        }

        /// <summary>
        /// Same object as this entry. A destroyed Unity object still matches by instance id,
        /// because its C# wrapper compares equal to null.
        /// </summary>
        public bool Matches(object obj)
        {
            if (instanceId.HasValue && obj is UnityEngine.Object uo && !ReferenceEquals(uo, null))
                return uo.GetInstanceID() == instanceId.Value;

            return ReferenceEquals(weakReference.Target, obj);
        }

        public bool IsAlive()
        {
            var target = weakReference.Target;

            bool alive = !isDestroyed
                         && target != null
                         && !(target is UnityEngine.Object unityObj && unityObj == null);

            if (!alive && !isDestroyed)
                MarkAsDestroyed();

            return alive;
        }
    }
#endif
}
