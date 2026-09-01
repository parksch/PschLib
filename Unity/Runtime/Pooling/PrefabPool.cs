using System;
using System.Collections.Generic;
using UnityEngine;

namespace PschLib.Unity.Pooling
{
    public sealed class PrefabPool
    {
        private readonly Transform _storageParent;
        private readonly GameObject _prefab;
        private readonly Vector3 _initialLocalScale;
        private readonly int _maxInactiveCount;

        private readonly Queue<GameObject> _inactiveObjects = new Queue<GameObject>();
        private readonly HashSet<GameObject> _inUseObjects = new HashSet<GameObject>();

        public int InUseCount => _inUseObjects.Count;
        public int InactiveCount => _inactiveObjects.Count;
        public int TotalCount => InUseCount + InactiveCount;
        internal string PrefabName => _prefab != null ? _prefab.name : "Missing";
        internal int MaxInactiveCount => _maxInactiveCount;

        internal void RemoveDestroyedReferences()
        {
            RemoveDestroyedObjects();
        }

        internal void RemoveDestroyedInUseReferences()
        {
            _inUseObjects.RemoveWhere(instance => instance == null);
        }

        internal void DestroyStorageParent()
        {
            if (_storageParent != null)
            {
                UnityEngine.Object.Destroy(_storageParent.gameObject);
            }
        }

        public PrefabPool(GameObject prefab, Transform storageParent, int maxInactiveCount = 50)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (storageParent == null)
            {
                throw new ArgumentNullException(nameof(storageParent));
            }

            if (storageParent.gameObject.activeSelf)
            {
                throw new ArgumentException("Storage parent must be inactive.", nameof(storageParent));
            }

            if (maxInactiveCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInactiveCount), "Must be greater than zero.");
            }

            _prefab = prefab;
            _initialLocalScale = prefab.transform.localScale;
            _storageParent = storageParent;
            _maxInactiveCount = maxInactiveCount;
        }

        public void Prewarm(int count)
        {
            if (count < 0 || count > _maxInactiveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(count), $"Must be between 0 and {_maxInactiveCount}.");
            }

            ValidateStorageParent();
            RemoveDestroyedObjects();

            int createCount = count - _inactiveObjects.Count;

            for (int i = 0; i < createCount; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(_prefab, _storageParent);
                instance.SetActive(false);
                _inactiveObjects.Enqueue(instance);
            }
        }

        public GameObject Get(Transform parent = null)
        {
            ValidateStorageParent();

            GameObject instance = null;

            while (_inactiveObjects.Count > 0)
            {
                instance = _inactiveObjects.Dequeue();

                if (instance != null)
                {
                    break;
                }
            }

            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(_prefab, _storageParent);
            }

            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(_storageParent.position, _storageParent.rotation);
            instance.transform.localScale = _initialLocalScale;

            _inUseObjects.Add(instance);

            return instance;
        }

        public bool Return(GameObject instance)
        {
            if (instance == null)
            {
                RemoveDestroyedObjects();
                Debug.LogWarning("Cannot return a null or destroyed object to the pool.");
                return false;
            }

            if (!_inUseObjects.Contains(instance))
            {
                Debug.LogWarning("Object was already returned or does not belong to this pool.", instance);
                return false;
            }

            ValidateStorageParent();
            _inUseObjects.Remove(instance);
            instance.SetActive(false);

            if (instance == null)
            {
                return true;
            }

            if (_inactiveObjects.Count >= _maxInactiveCount)
            {
                RemoveDestroyedInactiveObjects();

                if (_inactiveObjects.Count >= _maxInactiveCount)
                {
                    UnityEngine.Object.Destroy(instance);
                    return true;
                }
            }

            instance.transform.SetParent(_storageParent, false);

            if (instance != null)
            {
                _inactiveObjects.Enqueue(instance);
            }

            return true;
        }

        public void Clear()
        {
            while (_inactiveObjects.Count > 0)
            {
                GameObject instance = _inactiveObjects.Dequeue();

                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }
        }

        private void RemoveDestroyedObjects()
        {
            RemoveDestroyedInUseReferences();
            RemoveDestroyedInactiveObjects();
        }

        private void RemoveDestroyedInactiveObjects()
        {
            int inactiveCount = _inactiveObjects.Count;

            for (int i = 0; i < inactiveCount; i++)
            {
                GameObject instance = _inactiveObjects.Dequeue();

                if (instance != null)
                {
                    _inactiveObjects.Enqueue(instance);
                }
            }
        }

        private void ValidateStorageParent()
        {
            if (_storageParent == null)
            {
                throw new InvalidOperationException("Storage parent has been destroyed.");
            }

            if (_storageParent.gameObject.activeSelf)
            {
                throw new InvalidOperationException("Storage parent must remain inactive.");
            }
        }
    }
}
