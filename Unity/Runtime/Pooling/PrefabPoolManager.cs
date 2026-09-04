using System;
using System.Collections.Generic;
using UnityEngine;

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
        private bool isInitialized;

        public int Count => pools.Count;

#if UNITY_EDITOR
        public event Action DebugStateChanged;

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

            for (int i = 0; i < settings.Count; i++)
            {
                PrefabPoolSetting setting = settings[i];
                Register(setting.Key, setting.Prefab, setting.InitialCapacity, setting.MaxInactiveCount);
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

            var storageObject = new GameObject($"{key} Pool");
            storageObject.transform.SetParent(transform, false);
            storageObject.SetActive(false);

            var pool = new PrefabPool(prefab, storageObject.transform, maxInactiveCount);
            pool.Prewarm(initialCapacity);
            pools.Add(key, pool);
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

        private void NotifyDebugStateChanged()
        {
#if UNITY_EDITOR
            DebugStateChanged?.Invoke();
#endif
        }
    }
}
