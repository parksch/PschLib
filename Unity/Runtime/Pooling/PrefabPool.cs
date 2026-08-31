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

        private readonly Stack<GameObject> _inactiveObjects = new Stack<GameObject>();
        private readonly HashSet<GameObject> _activeObjects = new HashSet<GameObject>();

        public int ActiveCount => _activeObjects.Count;
        public int InactiveCount => _inactiveObjects.Count;
        public int TotalCount => ActiveCount + InactiveCount;

        public PrefabPool(GameObject prefab, Transform storageParent, int maxInactiveCount = 30)
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

        public GameObject Spawn(Transform parent = null)
        {
            ValidateStorageParent();

            GameObject instance = null;

            while (_inactiveObjects.Count > 0)
            {
                instance = _inactiveObjects.Pop();

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

            _activeObjects.Add(instance);
            instance.SetActive(true);

            return instance;
        }

        public bool Despawn(GameObject instance)
        {
            if (instance == null)
            {
                _activeObjects.Remove(instance);
                Debug.LogWarning("Cannot return a null or destroyed object to the pool.");
                return false;
            }

            if (!_activeObjects.Contains(instance))
            {
                Debug.LogWarning("Object was already returned or does not belong to this pool.", instance);
                return false;
            }

            ValidateStorageParent();
            _activeObjects.Remove(instance);
            instance.SetActive(false);

            if (instance == null)
            {
                return true;
            }

            if (_inactiveObjects.Count >= _maxInactiveCount)
            {
                UnityEngine.Object.Destroy(instance);
                return true;
            }

            instance.transform.SetParent(_storageParent, false);

            if (instance != null)
            {
                _inactiveObjects.Push(instance);
            }

            return true;
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
