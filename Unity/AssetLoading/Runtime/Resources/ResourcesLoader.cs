using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using PschLib.AssetLoading.Debugging;
#endif
using PschLib.AssetLoading.Internal;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityResources = UnityEngine.Resources;

namespace PschLib.AssetLoading.Resources
{
    public sealed class ResourcesLoader
#if UNITY_EDITOR
        : IAssetLoaderDebugInfo
#endif
    {
        private readonly AssetCache _cache = new AssetCache();
        private readonly Dictionary<AssetKey, PendingLoad> _pendingLoads = new Dictionary<AssetKey, PendingLoad>();
        private int _generation;

#if UNITY_EDITOR
        public event Action DebugStateChanged;
        public string LoaderName => nameof(ResourcesLoader);
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
                entries.Add(new AssetLoaderDebugEntry(pair.Key.Address, pair.Key.AssetType.Name, 0));
            }
        }
#endif

        public TAsset Load<TAsset>(string path) where TAsset : Object
        {
            ValidateMainThread();
            ValidatePath(path);

            if (_cache.TryAcquire<TAsset>(path, out var cachedAsset))
            {
                NotifyDebugStateChanged();
                return cachedAsset;
            }

            var asset = UnityResources.Load<TAsset>(path);

            if (asset == null)
            {
                Debug.LogError($"Resource asset was not found: {path} ({typeof(TAsset).Name})");
                return null;
            }

            _cache.Add(path, asset);
            NotifyDebugStateChanged();
            return asset;
        }

        public async UniTask<TAsset> LoadAsync<TAsset>(string path, CancellationToken cancellationToken = default) where TAsset : Object
        {
            ValidatePath(path);
            await UniTask.SwitchToMainThread(cancellationToken);

            if (_cache.TryAcquire<TAsset>(path, out var cachedAsset))
            {
                NotifyDebugStateChanged();
                return cachedAsset;
            }

            var key = new AssetKey(path, typeof(TAsset));

            if (!_pendingLoads.TryGetValue(key, out var pendingLoad))
            {
                pendingLoad = new PendingLoad();
                _pendingLoads.Add(key, pendingLoad);
                NotifyDebugStateChanged();
                LoadPendingAsync<TAsset>(path, key, pendingLoad, _generation).Forget();
            }

            var loadedAsset = cancellationToken.CanBeCanceled
                ? await pendingLoad.Task.AttachExternalCancellation(cancellationToken)
                : await pendingLoad.Task;

            if (loadedAsset == null)
            {
                return null;
            }

            if (!_cache.TryAcquire<TAsset>(path, out var asset))
            {
                return null;
            }

            NotifyDebugStateChanged();
            return asset;
        }

        public void Release<TAsset>(string path) where TAsset : Object
        {
            ValidateMainThread();
            ValidatePath(path);

            var result = _cache.Release<TAsset>(path);

            switch (result)
            {
                case AssetReleaseResult.NotFound:
                    Debug.LogWarning($"Resource asset was not found in cache: {path} ({typeof(TAsset).Name})");
                    return;

                case AssetReleaseResult.AlreadyUnused:
                    Debug.LogWarning($"Resource asset was already released: {path} ({typeof(TAsset).Name})");
                    return;

                case AssetReleaseResult.Retained:
                    NotifyDebugStateChanged();
                    return;

                case AssetReleaseResult.Unused:
                    NotifyDebugStateChanged();
                    return;
            }
        }

        public void Unload<TAsset>(string path) where TAsset : Object
        {
            ValidateMainThread();
            ValidatePath(path);

            var key = new AssetKey(path, typeof(TAsset));

            if (_pendingLoads.ContainsKey(key))
            {
                Debug.LogWarning($"Resource asset unload was ignored because it is still loading: {path} ({typeof(TAsset).Name})");
                return;
            }

            var result = _cache.Unload<TAsset>(path, out var asset);

            switch (result)
            {
                case AssetUnloadResult.NotFound:
                    Debug.LogWarning($"Resource asset was not found in cache: {path} ({typeof(TAsset).Name})");
                    return;

                case AssetUnloadResult.InUse:
                    Debug.LogWarning($"Resource asset is still in use: {path} ({typeof(TAsset).Name})");
                    return;

                case AssetUnloadResult.Unloaded:
                    UnloadAsset(asset);
                    NotifyDebugStateChanged();
                    return;
            }
        }

        public void ClearUnused()
        {
            ValidateMainThread();
            var unloadedCount = _cache.ClearUnused(key => !_pendingLoads.ContainsKey(key), UnloadAsset);

            if (unloadedCount > 0)
            {
                NotifyDebugStateChanged();
            }
        }

        public void Clear()
        {
            ValidateMainThread();
            var pendingCount = _pendingLoads.Count;
            var activeCount = _cache.ActiveCount;

            if (pendingCount > 0 || activeCount > 0)
            {
                Debug.LogWarning($"ResourcesLoader was cleared with {pendingCount} pending load(s) and {activeCount} active cached asset(s).");
            }

            _generation++;
            _pendingLoads.Clear();
            _cache.Clear(UnloadAsset);
            NotifyDebugStateChanged();
        }

        private async UniTask LoadPendingAsync<TAsset>(string path, AssetKey key, PendingLoad pendingLoad, int generation) where TAsset : Object
        {
            try
            {
                var request = UnityResources.LoadAsync<TAsset>(path);
                var loadedAsset = await request.ToUniTask();
                var asset = loadedAsset as TAsset;

                if (asset == null)
                {
                    Debug.LogError($"Resource asset was not found: {path} ({typeof(TAsset).Name})");
                    RemovePending(key, pendingLoad);
                    pendingLoad.TrySetResult(null);
                    return;
                }

                if (generation != _generation)
                {
                    RemovePending(key, pendingLoad);
                    pendingLoad.TrySetResult(null);
                    return;
                }

                if (!_cache.TryGet<TAsset>(path, out var cachedAsset))
                {
                    _cache.AddUnused(path, asset);
                    cachedAsset = asset;
                }

                RemovePending(key, pendingLoad);
                pendingLoad.TrySetResult(cachedAsset);
            }
            catch (Exception exception)
            {
                RemovePending(key, pendingLoad);
                pendingLoad.TrySetException(exception);
            }
            finally
            {
                RemovePending(key, pendingLoad);
            }
        }

        private void RemovePending(AssetKey key, PendingLoad pendingLoad)
        {
            if (!_pendingLoads.TryGetValue(key, out var current) || !ReferenceEquals(current, pendingLoad))
            {
                return;
            }

            _pendingLoads.Remove(key);
            NotifyDebugStateChanged();
        }

        private static void UnloadAsset(Object asset)
        {
            if (asset == null || asset is GameObject || asset is Component)
            {
                return;
            }

            UnityResources.UnloadAsset(asset);
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Resource path cannot be empty.", nameof(path));
            }
        }

        private static void ValidateMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new InvalidOperationException("ResourcesLoader must be used on the Unity main thread.");
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
            private readonly UniTaskCompletionSource<Object> _completionSource = new UniTaskCompletionSource<Object>();

            public UniTask<Object> Task => _completionSource.Task;

            public bool TrySetResult(Object asset)
            {
                return _completionSource.TrySetResult(asset);
            }

            public bool TrySetException(Exception exception)
            {
                return _completionSource.TrySetException(exception);
            }
        }
    }
}
