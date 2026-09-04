using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using PschLib.AssetLoading.Debugging;
#endif
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace PschLib.AssetLoading.Addressables.Internal
{
    internal sealed class AddressableAssetCache
    {
        private readonly Dictionary<AddressableAssetKey, Entry> entries =
            new Dictionary<AddressableAssetKey, Entry>();

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
            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (!entries.TryGetValue(key, out var entry))
            {
                asset = null;
                return false;
            }

            if (entry.Asset == null || !entry.Handle.IsValid())
            {
                entries.Remove(key);

                if (entry.Handle.IsValid())
                {
                    UnityAddressables.Release(entry.Handle);
                }

                asset = null;
                return false;
            }

            entry.ReferenceCount++;
            asset = (TAsset)entry.Asset;
            return true;
        }

        public bool TryGet<TAsset>(string address, out TAsset asset) where TAsset : Object
        {
            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (!entries.TryGetValue(key, out var entry))
            {
                asset = null;
                return false;
            }

            if (entry.Asset == null || !entry.Handle.IsValid())
            {
                entries.Remove(key);
                ReleaseHandle(entry.Handle);
                asset = null;
                return false;
            }

            asset = (TAsset)entry.Asset;
            return true;
        }

        public void AddUnused<TAsset>(string address, TAsset asset,
            AsyncOperationHandle<TAsset> handle) where TAsset : Object
        {
            Add(address, asset, handle, 0);
        }

        private void Add<TAsset>(string address, TAsset asset,
            AsyncOperationHandle<TAsset> handle, int referenceCount) where TAsset : Object
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (entries.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Addressable asset is already cached: {address} ({typeof(TAsset).Name})");
            }

            entries.Add(key, new Entry(asset, handle, referenceCount));
        }

        public AddressableReleaseResult Release<TAsset>(string address) where TAsset : Object
        {
            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (!entries.TryGetValue(key, out var entry))
            {
                return AddressableReleaseResult.NotFound;
            }

            if (entry.ReferenceCount == 0)
            {
                return AddressableReleaseResult.AlreadyUnused;
            }

            entry.ReferenceCount--;
            return entry.ReferenceCount > 0
                ? AddressableReleaseResult.Retained
                : AddressableReleaseResult.Unused;
        }

        public AddressableUnloadResult Unload<TAsset>(string address,
            out AsyncOperationHandle handle) where TAsset : Object
        {
            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (!entries.TryGetValue(key, out var entry))
            {
                handle = default;
                return AddressableUnloadResult.NotFound;
            }

            if (entry.ReferenceCount > 0)
            {
                handle = default;
                return AddressableUnloadResult.InUse;
            }

            entries.Remove(key);
            handle = entry.Handle;
            return AddressableUnloadResult.Unloaded;
        }

        public int ClearUnused()
        {
            var unusedKeys = new List<AddressableAssetKey>();

            foreach (var pair in entries)
            {
                if (pair.Value.ReferenceCount == 0)
                {
                    unusedKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < unusedKeys.Count; i++)
            {
                var key = unusedKeys[i];
                var handle = entries[key].Handle;
                entries.Remove(key);
                ReleaseHandle(handle);
            }

            return unusedKeys.Count;
        }

        public void Clear()
        {
            foreach (var entry in entries.Values)
            {
                ReleaseHandle(entry.Handle);
            }

            entries.Clear();
        }

#if UNITY_EDITOR
        public void GetDebugEntries(List<AssetLoaderDebugEntry> entries)
        {
            entries.Clear();

            foreach (var pair in this.entries)
            {
                entries.Add(new AssetLoaderDebugEntry(pair.Key.Address,
                    pair.Key.AssetType.Name, pair.Value.ReferenceCount));
            }
        }
#endif

        private static void ReleaseHandle(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                UnityAddressables.Release(handle);
            }
        }

        private sealed class Entry
        {
            public readonly Object Asset;
            public readonly AsyncOperationHandle Handle;
            public int ReferenceCount;

            public Entry(Object asset, AsyncOperationHandle handle, int referenceCount)
            {
                Asset = asset;
                Handle = handle;
                ReferenceCount = referenceCount;
            }
        }
    }
}
