using System;
using System.Collections.Generic;
using UnityEngine;

namespace PschLib.Unity.Pooling
{
    public sealed class PrefabPool
    {
        private readonly Transform storageParent;
        private readonly GameObject prefab;
        private readonly Vector3 initialLocalScale;
        private readonly int maxInactiveCount;

        private readonly Queue<GameObject> inactiveObjects = new Queue<GameObject>();
        private readonly HashSet<GameObject> inUseObjects = new HashSet<GameObject>();

        public int InUseCount => inUseObjects.Count;
        public int InactiveCount => inactiveObjects.Count;
        public int TotalCount => InUseCount + InactiveCount;
        internal string PrefabName => prefab != null ? prefab.name : "Missing";
        internal int MaxInactiveCount => maxInactiveCount;

        internal void RemoveDestroyedReferences()
        {
            RemoveDestroyedObjects();
        }

        internal void RemoveDestroyedInUseReferences()
        {
            inUseObjects.RemoveWhere(instance => instance == null);
        }

        internal void DestroyStorageParent()
        {
            if (storageParent != null)
            {
                UnityEngine.Object.Destroy(storageParent.gameObject);
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

            this.prefab = prefab;
            initialLocalScale = prefab.transform.localScale;
            this.storageParent = storageParent;
            this.maxInactiveCount = maxInactiveCount;
        }

        public void Prewarm(int count)
        {
            if (count < 0 || count > maxInactiveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(count), $"Must be between 0 and {maxInactiveCount}.");
            }

            ValidateStorageParent();
            RemoveDestroyedObjects();

            int createCount = count - inactiveObjects.Count;

            for (int i = 0; i < createCount; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, storageParent);
                instance.SetActive(false);
                inactiveObjects.Enqueue(instance);
            }
        }

        public GameObject Get(Transform parent = null, bool activate = false)
        {
            return GetInternal(parent, activate);
        }

        internal GameObject GetForManager(Transform parent = null)
        {
            return GetInternal(parent, false);
        }

        private GameObject GetInternal(Transform parent, bool activate)
        {
            ValidateStorageParent();

            GameObject instance = null;

            while (inactiveObjects.Count > 0)
            {
                instance = inactiveObjects.Dequeue();

                if (instance != null)
                {
                    break;
                }
            }

            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab, storageParent);
            }

            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(storageParent.position, storageParent.rotation);
            instance.transform.localScale = initialLocalScale;

            inUseObjects.Add(instance);
            instance.SetActive(activate);

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

            if (!inUseObjects.Contains(instance))
            {
                Debug.LogWarning("Object was already returned or does not belong to this pool.", instance);
                return false;
            }

            ValidateStorageParent();
            inUseObjects.Remove(instance);
            instance.SetActive(false);

            if (instance == null)
            {
                return true;
            }

            if (inactiveObjects.Count >= maxInactiveCount)
            {
                RemoveDestroyedInactiveObjects();

                if (inactiveObjects.Count >= maxInactiveCount)
                {
                    UnityEngine.Object.Destroy(instance);
                    return true;
                }
            }

            instance.transform.SetParent(storageParent, false);

            if (instance != null)
            {
                inactiveObjects.Enqueue(instance);
            }

            return true;
        }

        public void Clear()
        {
            while (inactiveObjects.Count > 0)
            {
                GameObject instance = inactiveObjects.Dequeue();

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
            int inactiveCount = inactiveObjects.Count;

            for (int i = 0; i < inactiveCount; i++)
            {
                GameObject instance = inactiveObjects.Dequeue();

                if (instance != null)
                {
                    inactiveObjects.Enqueue(instance);
                }
            }
        }

        private void ValidateStorageParent()
        {
            if (storageParent == null)
            {
                throw new InvalidOperationException("Storage parent has been destroyed.");
            }

            if (storageParent.gameObject.activeSelf)
            {
                throw new InvalidOperationException("Storage parent must remain inactive.");
            }
        }
    }
}
