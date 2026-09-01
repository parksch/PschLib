using System;
using UnityEngine;

namespace PschLib.Unity.Pooling
{
    [Serializable]
    public sealed class PrefabPoolSetting
    {
        [SerializeField] private string _key;
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0)] private int _initialCapacity = 1;
        [SerializeField, Min(1)] private int _maxInactiveCount = 50;

        public string Key => _key;
        public GameObject Prefab => _prefab;
        public int InitialCapacity => _initialCapacity;
        public int MaxInactiveCount => _maxInactiveCount;
    }
}
