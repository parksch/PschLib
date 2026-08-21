using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace PschLib.AssetLoading.Internal
{
    internal sealed class AssetCache
    {
        private readonly Dictionary<AssetKey, AssetCacheEntry> _entries = new Dictionary<AssetKey, AssetCacheEntry>();

        public int Count => _entries.Count;
        public int ActiveCount
        {
            get
            {
                var count = 0;

                foreach (var entry in _entries.Values)
                {
                    if (entry.ReferenceCount > 0)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryAcquire<TAsset>(string address, out TAsset asset) where TAsset : Object
        {
            var key = new AssetKey(address, typeof(TAsset));

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.Asset == null)
                {
                    _entries.Remove(key);
                    asset = null;
                    return false;
                }

                entry.ReferenceCount++;
                asset = (TAsset)entry.Asset;
                return true;
            }

            asset = null;
            return false;
        }

        public bool TryGet<TAsset>(string address, out TAsset asset) where TAsset : Object
        {
            var key = new AssetKey(address, typeof(TAsset));

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.Asset == null)
                {
                    _entries.Remove(key);
                    asset = null;
                    return false;
                }

                asset = (TAsset)entry.Asset;
                return true;
            }

            asset = null;
            return false;
        }

        public void Add<TAsset>(string address, TAsset asset) where TAsset : Object
        {
            Add(address, asset, 1);
        }

        public void AddUnused<TAsset>(string address, TAsset asset) where TAsset : Object
        {
            Add(address, asset, 0);
        }

        private void Add<TAsset>(string address, TAsset asset, int referenceCount) where TAsset : Object
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Asset address cannot be empty.", nameof(address));
            }

            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var key = new AssetKey(address, typeof(TAsset));

            if (_entries.ContainsKey(key))
            {
                throw new InvalidOperationException($"Asset is already cached: {address} ({typeof(TAsset).Name})");
            }

            _entries.Add(key, new AssetCacheEntry(asset, referenceCount));
        }

        public AssetReleaseResult Release<TAsset>(string address) where TAsset : Object
        {
            var key = new AssetKey(address, typeof(TAsset));

            if (!_entries.TryGetValue(key, out var entry))
            {
                return AssetReleaseResult.NotFound;
            }

            if (entry.ReferenceCount == 0)
            {
                return AssetReleaseResult.AlreadyUnused;
            }

            entry.ReferenceCount--;

            if (entry.ReferenceCount > 0)
            {
                return AssetReleaseResult.Retained;
            }

            return AssetReleaseResult.Unused;
        }

        public AssetUnloadResult Unload<TAsset>(string address, out Object asset) where TAsset : Object
        {
            var key = new AssetKey(address, typeof(TAsset));

            if (!_entries.TryGetValue(key, out var entry))
            {
                asset = null;
                return AssetUnloadResult.NotFound;
            }

            if (entry.ReferenceCount > 0)
            {
                asset = null;
                return AssetUnloadResult.InUse;
            }

            _entries.Remove(key);
            asset = entry.Asset;
            return AssetUnloadResult.Unloaded;
        }

        public void ClearUnused(Func<AssetKey, bool> canUnload, Action<Object> unload)
        {
            if (canUnload == null)
            {
                throw new ArgumentNullException(nameof(canUnload));
            }

            if (unload == null)
            {
                throw new ArgumentNullException(nameof(unload));
            }

            var unusedKeys = new List<AssetKey>();

            foreach (var pair in _entries)
            {
                if (pair.Value.ReferenceCount == 0 && canUnload(pair.Key))
                {
                    unusedKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < unusedKeys.Count; i++)
            {
                var key = unusedKeys[i];
                var asset = _entries[key].Asset;
                _entries.Remove(key);
                unload(asset);
            }
        }

        public void Clear(Action<Object> unload)
        {
            if (unload == null)
            {
                throw new ArgumentNullException(nameof(unload));
            }

            foreach (var entry in _entries.Values)
            {
                unload(entry.Asset);
            }

            _entries.Clear();
        }

        private sealed class AssetCacheEntry
        {
            public readonly Object Asset;
            public int ReferenceCount;

            public AssetCacheEntry(Object asset, int referenceCount)
            {
                Asset = asset;
                ReferenceCount = referenceCount;
            }
        }
    }
}
