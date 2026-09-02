using System;
using System.Collections.Generic;
using UnityEngine;

namespace PschLib.Unity.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PrefabPoolManager : MonoBehaviour
    {
        [SerializeField] private bool _initializeOnAwake = true;
        [SerializeField] private List<PrefabPoolSetting> _settings = new List<PrefabPoolSetting>();

        private readonly Dictionary<string, PrefabPool> _pools = new Dictionary<string, PrefabPool>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, PrefabPool> _inUseInstancePools = new Dictionary<GameObject, PrefabPool>();
        private readonly List<GameObject> _destroyedInstances = new List<GameObject>();
        private bool _isInitialized;

        public int Count => _pools.Count;

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

            foreach (var pair in _pools)
            {
                PrefabPool pool = pair.Value;
                pool.RemoveDestroyedReferences();
                entries.Add(new DebugEntry(pair.Key, pool.PrefabName, pool.InUseCount, pool.InactiveCount, pool.MaxInactiveCount));
            }
        }
#endif

        private void Awake()
        {
            if (_initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            RemoveDestroyedInstanceReferences();

            if (_inUseInstancePools.Count > 0)
            {
                Debug.LogWarning($"PrefabPoolManager was destroyed with {_inUseInstancePools.Count} object(s) still in use.", this);
            }
        }

        public bool Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("PrefabPoolManager is already initialized.", this);
                return false;
            }

            if (!ValidateSettings())
            {
                return false;
            }

            for (int i = 0; i < _settings.Count; i++)
            {
                PrefabPoolSetting setting = _settings[i];
                Register(setting.Key, setting.Prefab, setting.InitialCapacity, setting.MaxInactiveCount);
            }

            _isInitialized = true;
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

            if (_pools.ContainsKey(key))
            {
                Debug.LogWarning($"Pool is already registered: {key}", this);
                return false;
            }

            var storageObject = new GameObject($"{key} Pool");
            storageObject.transform.SetParent(transform, false);
            storageObject.SetActive(false);

            var pool = new PrefabPool(prefab, storageObject.transform, maxInactiveCount);
            pool.Prewarm(initialCapacity);
            _pools.Add(key, pool);
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

            if (!_pools.TryGetValue(key, out var pool))
            {
                Debug.LogWarning($"Pool is not registered: {key}", this);
                return null;
            }

            GameObject instance = pool.GetForManager(parent);

            if (_inUseInstancePools.ContainsKey(instance))
            {
                throw new InvalidOperationException($"Pooled object is already tracked as in use: {instance.name}");
            }

            _inUseInstancePools.Add(instance, pool);

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

            if (!_inUseInstancePools.TryGetValue(instance, out var pool))
            {
                Debug.LogWarning("Object was not spawned by this pool manager.", instance);
                return false;
            }

            bool result = pool.Return(instance);

            bool removed = result || instance == null;

            if (removed)
            {
                _inUseInstancePools.Remove(instance);
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

            if (!_pools.TryGetValue(key, out var pool))
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

            if (!_pools.TryGetValue(key, out var pool))
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
            _pools.Remove(key);
            NotifyDebugStateChanged();
            return true;
        }

        private bool ValidateSettings()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _settings.Count; i++)
            {
                PrefabPoolSetting setting = _settings[i];

                if (setting == null || string.IsNullOrWhiteSpace(setting.Key) || setting.Prefab == null ||
                    setting.MaxInactiveCount < 1 || setting.InitialCapacity < 0 || setting.InitialCapacity > setting.MaxInactiveCount)
                {
                    Debug.LogError($"Pool setting is invalid at index {i}.", this);
                    return false;
                }

                if (!keys.Add(setting.Key) || _pools.ContainsKey(setting.Key))
                {
                    Debug.LogError($"Pool key is duplicated: {setting.Key}", this);
                    return false;
                }
            }

            return true;
        }

        private void RemoveDestroyedInstanceReferences()
        {
            _destroyedInstances.Clear();

            foreach (var pair in _inUseInstancePools)
            {
                if (pair.Key == null)
                {
                    _destroyedInstances.Add(pair.Key);
                }
            }

            for (int i = 0; i < _destroyedInstances.Count; i++)
            {
                _inUseInstancePools.Remove(_destroyedInstances[i]);
            }

            if (_destroyedInstances.Count == 0)
            {
                return;
            }

            foreach (PrefabPool pool in _pools.Values)
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
