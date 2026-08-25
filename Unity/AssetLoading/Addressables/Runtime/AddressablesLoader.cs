using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using PschLib.AssetLoading.Debugging;
#endif
using PschLib.AssetLoading.Addressables.Internal;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace PschLib.AssetLoading.Addressables
{
    public sealed class AddressablesLoader
#if UNITY_EDITOR
        : IAssetLoaderDebugInfo
#endif
    {
        private readonly AddressableAssetCache _cache = new AddressableAssetCache();
        private readonly Dictionary<AddressableAssetKey, PendingLoad> _pendingLoads =
            new Dictionary<AddressableAssetKey, PendingLoad>();
        private int _generation;

#if UNITY_EDITOR
        public event Action DebugStateChanged;
        public string LoaderName => nameof(AddressablesLoader);
        public int CachedAssetCount => _cache.Count;
        public int ActiveAssetCount => _cache.ActiveCount;
        public int PendingLoadCount => _pendingLoads.Count;

        public void GetCachedAssetEntries(List<AssetLoaderDebugEntry> entries)
        {
            _cache.GetDebugEntries(entries);
        }

        public void GetPendingLoadEntries(List<AssetLoaderDebugEntry> entries)
        {
            entries.Clear();

            foreach (var pair in _pendingLoads)
            {
                entries.Add(new AssetLoaderDebugEntry(pair.Key.Address,
                    pair.Key.AssetType.Name, 0));
            }
        }
#endif

        public async UniTask<TAsset> LoadAsync<TAsset>(string address,
            CancellationToken cancellationToken = default) where TAsset : Object
        {
            ValidateAddress(address);
            await UniTask.SwitchToMainThread(cancellationToken);

            if (_cache.TryAcquire<TAsset>(address, out var cachedAsset))
            {
                NotifyDebugStateChanged();
                return cachedAsset;
            }

            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (!_pendingLoads.TryGetValue(key, out var pendingLoad))
            {
                pendingLoad = new PendingLoad();
                _pendingLoads.Add(key, pendingLoad);
                NotifyDebugStateChanged();
                LoadPendingAsync<TAsset>(address, key, pendingLoad, _generation).Forget();
            }

            var loadedAsset = cancellationToken.CanBeCanceled
                ? await pendingLoad.Task.AttachExternalCancellation(cancellationToken)
                : await pendingLoad.Task;

            if (loadedAsset == null)
            {
                return null;
            }

            if (!_cache.TryAcquire<TAsset>(address, out var asset))
            {
                return null;
            }

            NotifyDebugStateChanged();
            return asset;
        }

        private async UniTask LoadPendingAsync<TAsset>(string address, AddressableAssetKey key,
            PendingLoad pendingLoad, int generation) where TAsset : Object
        {
            var handle = UnityAddressables.LoadAssetAsync<TAsset>(address);

            try
            {
                var asset = await handle.ToUniTask();

                if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                {
                    Debug.LogError($"Addressable asset load failed: {address} ({typeof(TAsset).Name})");
                    ReleaseHandle(handle);
                    RemovePending(key, pendingLoad);
                    pendingLoad.TrySetResult(null);
                    return;
                }

                if (generation != _generation)
                {
                    ReleaseHandle(handle);
                    RemovePending(key, pendingLoad);
                    pendingLoad.TrySetResult(null);
                    return;
                }

                if (!_cache.TryGet<TAsset>(address, out var cachedAsset))
                {
                    _cache.AddUnused(address, asset, handle);
                    cachedAsset = asset;
                }
                else
                {
                    ReleaseHandle(handle);
                }

                RemovePending(key, pendingLoad);
                pendingLoad.TrySetResult(cachedAsset);
            }
            catch (Exception exception)
            {
                ReleaseHandle(handle);
                RemovePending(key, pendingLoad);
                Debug.LogError($"Addressable asset load failed: {address} ({typeof(TAsset).Name})\n{exception.Message}");
                pendingLoad.TrySetResult(null);
            }
            finally
            {
                RemovePending(key, pendingLoad);
            }
        }

        public void Release<TAsset>(string address) where TAsset : Object
        {
            ValidateMainThread();
            ValidateAddress(address);

            switch (_cache.Release<TAsset>(address))
            {
                case AddressableReleaseResult.NotFound:
                    Debug.LogWarning($"Addressable asset was not found in cache: {address} ({typeof(TAsset).Name})");
                    return;

                case AddressableReleaseResult.AlreadyUnused:
                    Debug.LogWarning($"Addressable asset was already released: {address} ({typeof(TAsset).Name})");
                    return;

                case AddressableReleaseResult.Retained:
                case AddressableReleaseResult.Unused:
                    NotifyDebugStateChanged();
                    return;
            }
        }

        public void Unload<TAsset>(string address) where TAsset : Object
        {
            ValidateMainThread();
            ValidateAddress(address);
            var key = new AddressableAssetKey(address, typeof(TAsset));

            if (_pendingLoads.ContainsKey(key))
            {
                Debug.LogWarning($"Addressable asset unload was ignored because it is still loading: {address} ({typeof(TAsset).Name})");
                return;
            }

            var result = _cache.Unload<TAsset>(address, out var handle);

            switch (result)
            {
                case AddressableUnloadResult.NotFound:
                    Debug.LogWarning($"Addressable asset was not found in cache: {address} ({typeof(TAsset).Name})");
                    return;

                case AddressableUnloadResult.InUse:
                    Debug.LogWarning($"Addressable asset is still in use: {address} ({typeof(TAsset).Name})");
                    return;

                case AddressableUnloadResult.Unloaded:
                    if (handle.IsValid())
                    {
                        UnityAddressables.Release(handle);
                    }

                    NotifyDebugStateChanged();
                    return;
            }
        }

        public void ClearUnused()
        {
            ValidateMainThread();
            var unloadedCount = _cache.ClearUnused();

            if (unloadedCount > 0)
            {
                NotifyDebugStateChanged();
            }
        }

        public void Clear()
        {
            ValidateMainThread();

            if (_pendingLoads.Count > 0 || _cache.ActiveCount > 0)
            {
                Debug.LogWarning($"AddressablesLoader was cleared with {_pendingLoads.Count} pending load(s) and {_cache.ActiveCount} active cached asset(s).");
            }

            _generation++;
            _pendingLoads.Clear();
            _cache.Clear();
            NotifyDebugStateChanged();
        }

        private void RemovePending(AddressableAssetKey key, PendingLoad pendingLoad)
        {
            if (!_pendingLoads.TryGetValue(key, out var current) || !ReferenceEquals(current, pendingLoad))
            {
                return;
            }

            _pendingLoads.Remove(key);
            NotifyDebugStateChanged();
        }

        private static void ReleaseHandle(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                UnityAddressables.Release(handle);
            }
        }

        private static void ValidateAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Addressable address cannot be empty.", nameof(address));
            }
        }

        private static void ValidateMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new InvalidOperationException("AddressablesLoader must be used on the Unity main thread.");
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void NotifyDebugStateChanged()
        {
#if UNITY_EDITOR
            DebugStateChanged?.Invoke();
#endif
        }

        private sealed class PendingLoad
        {
            private readonly UniTaskCompletionSource<Object> _completionSource =
                new UniTaskCompletionSource<Object>();

            public UniTask<Object> Task => _completionSource.Task;

            public bool TrySetResult(Object asset)
            {
                return _completionSource.TrySetResult(asset);
            }
        }
    }
}
