using System;
using UnityEngine;

namespace PschLib.Unity.Pooling
{
    [Serializable]
    public sealed class PrefabPoolSetting
    {
        [SerializeField] private string key;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int initialCapacity = 1;
        [SerializeField, Min(1)] private int maxInactiveCount = 50;

        public string Key => key;
        public GameObject Prefab => prefab;
        public int InitialCapacity => initialCapacity;
        public int MaxInactiveCount => maxInactiveCount;
    }
}
