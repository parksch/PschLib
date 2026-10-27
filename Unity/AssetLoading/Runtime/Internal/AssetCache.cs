using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
#if UNITY_EDITOR
using PschLib.AssetLoading.Debugging;
#endif
using Object = UnityEngine.Object;

namespace PschLib.AssetLoading.Internal
{
    internal sealed class AssetCache
    {
        private readonly Dictionary<AssetKey, AssetCacheEntry> entries = new Dictionary<AssetKey, AssetCacheEntry>();

        public int Count => entries.Count;
        public int ActiveCount
        {
            get
            {
                var count = 0;

                foreach (var entry in entries.Values)
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

            if (entries.TryGetValue(key, out var entry))
            {
                if (entry.Asset == null)
                {
                    entries.Remove(key);
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

            if (entries.TryGetValue(key, out var entry))
            {
                if (entry.Asset == null)
                {
                    entries.Remove(key);
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

            if (entries.ContainsKey(key))
            {
                throw new InvalidOperationException($"Asset is already cached: {address} ({typeof(TAsset).Name})");
            }

            entries.Add(key, new AssetCacheEntry(asset, referenceCount));
        }

        public AssetReleaseResult Release<TAsset>(string address) where TAsset : Object
        {
            var key = new AssetKey(address, typeof(TAsset));

            if (!entries.TryGetValue(key, out var entry))
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

        public AssetUnloadResult Unload<TAsset>(string address, out Object asset, Func<AssetKey, bool> canUnload = null) where TAsset : Object
        {
            var key = new AssetKey(address, typeof(TAsset));

            if (!entries.TryGetValue(key, out var entry))
            {
                asset = null;
                return AssetUnloadResult.NotFound;
            }

            // Different paths or requested types can resolve to the same Unity asset.
            if (!CanUnloadAsset(entry.Asset, canUnload))
            {
                asset = null;
                return AssetUnloadResult.InUse;
            }

            asset = entry.Asset;
            RemoveAssetEntries(asset);
            return AssetUnloadResult.Unloaded;
        }

        public IDisposable Pin<TAsset>(string address) where TAsset : Object
        {
            if (!entries.TryGetValue(new AssetKey(address, typeof(TAsset)), out var entry) || entry.Asset == null)
            {
                throw new InvalidOperationException($"Cannot pin an asset that is not cached: {address} ({typeof(TAsset).Name})");
            }

            entry.PinCount++;
            return new CachePin(entry);
        }

        private bool CanUnloadAsset(Object asset, Func<AssetKey, bool> canUnload)
        {
            foreach (var pair in entries)
            {
                if (pair.Value.Asset == asset &&
                    (pair.Value.ReferenceCount > 0 || pair.Value.PinCount > 0 || (canUnload != null && !canUnload(pair.Key))))
                {
                    return false;
                }
            }

            return true;
        }

        private void RemoveAssetEntries(Object asset)
        {
            var keys = new List<AssetKey>();
            foreach (var pair in entries)
            {
                if (pair.Value.Asset == asset)
                {
                    keys.Add(pair.Key);
                }
            }

            foreach (var key in keys)
            {
                entries.Remove(key);
            }
        }

        public int ClearUnused(Func<AssetKey, bool> canUnload, Action<Object> unload)
        {
            if (canUnload == null)
            {
                throw new ArgumentNullException(nameof(canUnload));
            }

            if (unload == null)
            {
                throw new ArgumentNullException(nameof(unload));
            }

            var unusedAssets = new HashSet<Object>();
            var retainedAssets = new HashSet<Object>();

            foreach (var pair in entries)
            {
                if (pair.Value.ReferenceCount > 0 || pair.Value.PinCount > 0 || !canUnload(pair.Key))
                {
                    retainedAssets.Add(pair.Value.Asset);
                }
                else
                {
                    unusedAssets.Add(pair.Value.Asset);
                }
            }

            unusedAssets.ExceptWith(retainedAssets);
            var unusedKeys = new List<AssetKey>();
            foreach (var pair in entries)
            {
                if (unusedAssets.Contains(pair.Value.Asset))
                {
                    unusedKeys.Add(pair.Key);
                }
            }

            foreach (var key in unusedKeys)
            {
                entries.Remove(key);
            }

            List<Exception> exceptions = null;

            foreach (var asset in unusedAssets)
            {
                try
                {
                    unload(asset);
                }
                catch (Exception exception)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(exception);
                }
            }

            ThrowClearExceptions(exceptions);
            return unusedAssets.Count;
        }

        public void Clear(Action<Object> unload)
        {
            if (unload == null)
            {
                throw new ArgumentNullException(nameof(unload));
            }

            if (entries.Count == 0)
            {
                return;
            }

            var assets = new HashSet<Object>();

            foreach (var entry in entries.Values)
            {
                assets.Add(entry.Asset);
            }

            entries.Clear();

            List<Exception> exceptions = null;

            foreach (var asset in assets)
            {
                try
                {
                    unload(asset);
                }
                catch (Exception exception)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(exception);
                }
            }

            ThrowClearExceptions(exceptions);
        }

        private static void ThrowClearExceptions(List<Exception> exceptions)
        {
            if (exceptions == null)
            {
                return;
            }

            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
                return;
            }

            throw new AggregateException("Multiple resource assets failed to unload while clearing the cache.", exceptions);
        }

#if UNITY_EDITOR
        public void GetDebugEntries(List<AssetLoaderDebugEntry> entries)
        {
            entries.Clear();

            foreach (var pair in this.entries)
            {
                entries.Add(new AssetLoaderDebugEntry(
                    pair.Key.Address,
                    pair.Key.AssetType.Name,
                    pair.Value.ReferenceCount));
            }
        }
#endif

        private sealed class AssetCacheEntry
        {
            public readonly Object Asset;
            public int ReferenceCount;
            public int PinCount;

            public AssetCacheEntry(Object asset, int referenceCount)
            {
                Asset = asset;
                ReferenceCount = referenceCount;
            }
        }

        private sealed class CachePin : IDisposable
        {
            private AssetCacheEntry entry;

            public CachePin(AssetCacheEntry entry)
            {
                this.entry = entry;
            }

            public void Dispose()
            {
                if (entry == null)
                {
                    return;
                }

                entry.PinCount--;
                entry = null;
            }
        }
    }
}
