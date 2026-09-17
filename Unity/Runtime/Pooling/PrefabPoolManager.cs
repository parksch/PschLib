using System;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PschLib.Unity.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PrefabPoolManager : MonoBehaviour
    {
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private List<PrefabPoolSetting> settings = new List<PrefabPoolSetting>();

        private readonly Dictionary<string, PrefabPool> pools = new Dictionary<string, PrefabPool>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, PrefabPool> inUseInstancePools = new Dictionary<GameObject, PrefabPool>();
        private readonly List<GameObject> destroyedInstances = new List<GameObject>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly Dictionary<Scene, int> unexpectedDestroyCounts = new Dictionary<Scene, int>();
#endif
        private bool isInitialized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool isDestroying;
        private bool isDestroyWarningScheduled;
#endif

        public int Count => pools.Count;

#if UNITY_EDITOR
        private bool isNotifyingDebugStateChanged;
        private Action debugStateChanged;
        private Delegate[] debugListenerSnapshot = Array.Empty<Delegate>();

        public event Action DebugStateChanged
        {
            add
            {
                debugStateChanged += value;
                debugListenerSnapshot = debugStateChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            }
            remove
            {
                debugStateChanged -= value;
                debugListenerSnapshot = debugStateChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            }
        }

        public readonly struct DebugEntry
        {
            public readonly string Key;
            public readonly string PrefabName;
            public readonly int InUseCount;
            public readonly int InactiveCount;
            public readonly int MaxInactiveCount;
            public int TotalCount => InUseCount + InactiveCount;

            public DebugEntry(string key, string prefabName, int inUseCount, int inactiveCount, int maxInactiveCount)
            {
                Key = key;
                PrefabName = prefabName;
                InUseCount = inUseCount;
                InactiveCount = inactiveCount;
                MaxInactiveCount = maxInactiveCount;
            }
        }

        public void GetDebugEntries(List<DebugEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            entries.Clear();
            RemoveDestroyedInstanceReferences();

            foreach (var pair in pools)
            {
                PrefabPool pool = pair.Value;
                pool.RemoveDestroyedReferences();
                entries.Add(new DebugEntry(pair.Key, pool.PrefabName, pool.InUseCount, pool.InactiveCount, pool.MaxInactiveCount));
            }
        }
#endif

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            isDestroying = true;
#endif
            RemoveDestroyedInstanceReferences();

            if (inUseInstancePools.Count > 0)
            {
                Debug.LogWarning($"PrefabPoolManager was destroyed with {inUseInstancePools.Count} object(s) still in use.", this);
            }
        }

        public bool Initialize()
        {
            if (isInitialized)
            {
                Debug.LogWarning("PrefabPoolManager is already initialized.", this);
                return false;
            }

            if (!ValidateSettings())
            {
                return false;
            }

            var registeredKeys = new List<string>(settings.Count);

            try
            {
                for (int i = 0; i < settings.Count; i++)
                {
                    PrefabPoolSetting setting = settings[i];

                    if (!Register(setting.Key, setting.Prefab, setting.InitialCapacity, setting.MaxInactiveCount))
                    {
                        RollbackRegisteredPools(registeredKeys);
                        return false;
                    }

                    registeredKeys.Add(setting.Key);
                }
            }
            catch
            {
                RollbackRegisteredPools(registeredKeys);
                throw;
            }

            isInitialized = true;
            NotifyDebugStateChanged();
            return true;
        }

        public bool Register(string key, GameObject prefab, int initialCapacity = 1, int maxInactiveCount = 50)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Pool key cannot be empty.", nameof(key));
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (maxInactiveCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInactiveCount), "Must be greater than zero.");
            }

            if (initialCapacity < 0 || initialCapacity > maxInactiveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Must be between 0 and {maxInactiveCount}.");
            }

            if (pools.ContainsKey(key))
            {
                Debug.LogWarning($"Pool is already registered: {key}", this);
                return false;
            }

            GameObject storageObject = null;

            try
            {
                storageObject = new GameObject($"{key} Pool");
                storageObject.transform.SetParent(transform, false);
                storageObject.SetActive(false);

                var pool = new PrefabPool(prefab, storageObject.transform, this, maxInactiveCount);
                pool.Prewarm(initialCapacity);
                pools.Add(key, pool);
            }
            catch
            {
                if (storageObject != null)
                {
                    Destroy(storageObject);
                }

                throw;
            }

            NotifyDebugStateChanged();
            return true;
        }

        public GameObject Get(string key, Transform parent = null, bool activate = false)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("Pool key cannot be empty.", this);
                return null;
            }

            if (!pools.TryGetValue(key, out var pool))
            {
                Debug.LogWarning($"Pool is not registered: {key}", this);
                return null;
            }

            GameObject instance = pool.GetForManager(parent);

            if (inUseInstancePools.ContainsKey(instance))
            {
                throw new InvalidOperationException($"Pooled object is already tracked as in use: {instance.name}");
            }

            inUseInstancePools.Add(instance, pool);

            if (activate)
            {
                instance.SetActive(true);
            }

            NotifyDebugStateChanged();
            return instance;
        }

        public bool Return(GameObject instance)
        {
            if (ReferenceEquals(instance, null))
            {
                Debug.LogWarning("Cannot return a null object to the pool manager.", this);
                return false;
            }

            if (!inUseInstancePools.TryGetValue(instance, out var pool))
            {
                Debug.LogWarning("Object was not spawned by this pool manager.", instance);
                return false;
            }

            bool result = pool.Return(instance);

            bool removed = result || instance == null;

            if (removed)
            {
                inUseInstancePools.Remove(instance);
            }

            if (removed)
            {
                NotifyDebugStateChanged();
            }

            return result;
        }

        internal void NotifyTrackedObjectDestroyed(GameObject instance, PrefabPool pool, PooledObjectState state,
            bool expectedDestroy, Scene scene)
        {
            inUseInstancePools.Remove(instance);
            pool.RemoveTrackedObject(instance);
            NotifyDebugStateChanged();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (expectedDestroy || isDestroying)
            {
                return;
            }

            if (!unexpectedDestroyCounts.TryGetValue(scene, out var count))
            {
                count = 0;
            }

            unexpectedDestroyCounts[scene] = count + 1;

            if (isDestroyWarningScheduled)
            {
                return;
            }

            isDestroyWarningScheduled = true;
            _ = ReportUnexpectedDestroysAsync(destroyCancellationToken);
#endif
        }

        public bool Clear(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("Pool key cannot be empty.", this);
                return false;
            }

            if (!pools.TryGetValue(key, out var pool))
            {
                Debug.LogWarning($"Pool is not registered: {key}", this);
                return false;
            }

            RemoveDestroyedInstanceReferences();
            pool.Clear();
            NotifyDebugStateChanged();
            return true;
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("Pool key cannot be empty.", this);
                return false;
            }

            if (!pools.TryGetValue(key, out var pool))
            {
                Debug.LogWarning($"Pool is not registered: {key}", this);
                return false;
            }

            RemoveDestroyedInstanceReferences();
            pool.RemoveDestroyedReferences();

            if (pool.InUseCount > 0)
            {
                Debug.LogWarning($"Pool cannot be removed while {pool.InUseCount} object(s) are still in use: {key}", this);
                return false;
            }

            pool.Clear();
            pool.DestroyStorageParent();
            pools.Remove(key);
            NotifyDebugStateChanged();
            return true;
        }

        private bool ValidateSettings()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < settings.Count; i++)
            {
                PrefabPoolSetting setting = settings[i];

                if (setting == null || string.IsNullOrWhiteSpace(setting.Key) || setting.Prefab == null ||
                    setting.MaxInactiveCount < 1 || setting.InitialCapacity < 0 || setting.InitialCapacity > setting.MaxInactiveCount)
                {
                    Debug.LogError($"Pool setting is invalid at index {i}.", this);
                    return false;
                }

                if (!keys.Add(setting.Key) || pools.ContainsKey(setting.Key))
                {
                    Debug.LogError($"Pool key is duplicated: {setting.Key}", this);
                    return false;
                }
            }

            return true;
        }

        private void RollbackRegisteredPools(List<string> registeredKeys)
        {
            if (registeredKeys.Count == 0)
            {
                return;
            }

            for (var i = registeredKeys.Count - 1; i >= 0; i--)
            {
                var key = registeredKeys[i];

                if (!pools.TryGetValue(key, out var pool))
                {
                    continue;
                }

                pool.Clear();
                pool.DestroyStorageParent();
                pools.Remove(key);
            }

            NotifyDebugStateChanged();
        }

        private void RemoveDestroyedInstanceReferences()
        {
            destroyedInstances.Clear();

            foreach (var pair in inUseInstancePools)
            {
                if (pair.Key == null)
                {
                    destroyedInstances.Add(pair.Key);
                }
            }

            for (int i = 0; i < destroyedInstances.Count; i++)
            {
                inUseInstancePools.Remove(destroyedInstances[i]);
            }

            if (destroyedInstances.Count == 0)
            {
                return;
            }

            foreach (PrefabPool pool in pools.Values)
            {
                pool.RemoveDestroyedInUseReferences();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async Awaitable ReportUnexpectedDestroysAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Awaitable.NextFrameAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            isDestroyWarningScheduled = false;
            var unexpectedCount = 0;

            foreach (var pair in unexpectedDestroyCounts)
            {
                if (pair.Key.IsValid() && pair.Key.isLoaded)
                {
                    unexpectedCount += pair.Value;
                }
            }

            unexpectedDestroyCounts.Clear();

            if (unexpectedCount > 0)
            {
                LogUnexpectedDestroy(unexpectedCount, null);
            }
        }

        private void LogUnexpectedDestroy(int count, PooledObjectState? state)
        {
            string stateText = state.HasValue ? $" while {state.Value}" : string.Empty;
            Debug.LogWarning($"PrefabPoolManager removed {count} pooled object reference(s) destroyed{stateText}. Return pooled objects with PrefabPoolManager.Return instead of destroying them.", this);
        }
#endif

        private void NotifyDebugStateChanged()
        {
#if UNITY_EDITOR
            var listeners = debugListenerSnapshot;
            if (listeners.Length == 0 || isNotifyingDebugStateChanged)
            {
                return;
            }

            isNotifyingDebugStateChanged = true;

            try
            {
                for (var i = 0; i < listeners.Length; i++)
                {
                    try
                    {
                        ((Action)listeners[i])();
                    }
                    catch (Exception exception)
                    {
                        PschLib.Debugging.DebugObserverExceptionReporter.Report(
                            nameof(PrefabPoolManager), exception, this);
                    }
                }
            }
            finally
            {
                isNotifyingDebugStateChanged = false;
            }
#endif
        }

    }
}
