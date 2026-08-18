using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace PschLib.AssetLoading.Internal
{
    internal sealed class AssetCache
    {
        private readonly Dictionary<AssetCacheKey, AssetCacheEntry> _entries = new Dictionary<AssetCacheKey, AssetCacheEntry>();

        public int Count => _entries.Count;

        public bool TryAcquire<TAsset>(string address, out TAsset asset) where TAsset : Object
        {
            var key = new AssetCacheKey(address, typeof(TAsset));

            if (_entries.TryGetValue(key, out var entry))
            {
                entry.ReferenceCount++;
                asset = (TAsset)entry.Asset;
                return true;
            }

            asset = null;
            return false;
        }

        public void Add<TAsset>(string address, TAsset asset) where TAsset : Object
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Asset address cannot be empty.", nameof(address));
            }

            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var key = new AssetCacheKey(address, typeof(TAsset));

            if (_entries.ContainsKey(key))
            {
                throw new InvalidOperationException($"Asset is already cached: {address} ({typeof(TAsset).Name})");
            }

            _entries.Add(key, new AssetCacheEntry(asset));
        }

        public Object Release<TAsset>(string address) where TAsset : Object
        {
            var key = new AssetCacheKey(address, typeof(TAsset));

            if (!_entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            entry.ReferenceCount--;

            if (entry.ReferenceCount > 0)
            {
                return null;
            }

            _entries.Remove(key);
            return entry.Asset;
        }

        public void Clear(Action<Object> release)
        {
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            foreach (var entry in _entries.Values)
            {
                release(entry.Asset);
            }

            _entries.Clear();
        }

        private readonly struct AssetCacheKey : IEquatable<AssetCacheKey>
        {
            private readonly string _address;
            private readonly Type _assetType;

            public AssetCacheKey(string address, Type assetType)
            {
                _address = address;
                _assetType = assetType;
            }

            public bool Equals(AssetCacheKey other)
            {
                return string.Equals(_address, other._address, StringComparison.Ordinal) && _assetType == other._assetType;
            }

            public override bool Equals(object obj)
            {
                return obj is AssetCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_address != null ? StringComparer.Ordinal.GetHashCode(_address) : 0) * 397) ^ (_assetType != null ? _assetType.GetHashCode() : 0);
                }
            }
        }

        private sealed class AssetCacheEntry
        {
            public readonly Object Asset;
            public int ReferenceCount;

            public AssetCacheEntry(Object asset)
            {
                Asset = asset;
                ReferenceCount = 1;
            }
        }
    }
}
