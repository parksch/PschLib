using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PschLib.AssetLoading.Internal;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityResources = UnityEngine.Resources;

namespace PschLib.AssetLoading.Resources
{
    public sealed class ResourcesLoader
    {
        private readonly AssetCache _cache = new AssetCache();
        private readonly Dictionary<AssetKey, PendingLoad> _pendingLoads = new Dictionary<AssetKey, PendingLoad>();
        private int _generation;

        public TAsset Load<TAsset>(string path) where TAsset : Object
        {
            ValidateMainThread();
            ValidatePath(path);

            if (_cache.TryAcquire<TAsset>(path, out var cachedAsset))
            {
                return cachedAsset;
            }

            var asset = UnityResources.Load<TAsset>(path);

            if (asset == null)
            {
                Debug.LogError($"Resource asset was not found: {path} ({typeof(TAsset).Name})");
                return null;
            }

            _cache.Add(path, asset);
            return asset;
        }

        public async UniTask<TAsset> LoadAsync<TAsset>(string path, CancellationToken cancellationToken = default) where TAsset : Object
        {
            ValidatePath(path);
            await UniTask.SwitchToMainThread(cancellationToken);

            if (_cache.TryAcquire<TAsset>(path, out var cachedAsset))
            {
                return cachedAsset;
            }

            var key = new AssetKey(path, typeof(TAsset));

            if (!_pendingLoads.TryGetValue(key, out var pendingLoad))
            {
                pendingLoad = new PendingLoad();
                _pendingLoads.Add(key, pendingLoad);
                LoadPendingAsync<TAsset>(path, key, pendingLoad, _generation).Forget();
            }

            var loadedAsset = cancellationToken.CanBeCanceled
                ? await pendingLoad.Task.AttachExternalCancellation(cancellationToken)
                : await pendingLoad.Task;

            if (loadedAsset == null)
            {
                return null;
            }

            return _cache.TryAcquire<TAsset>(path, out var asset) ? asset : null;
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
                    return;

                case AssetReleaseResult.Unused:
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
                    return;
            }
        }

        public void ClearUnused()
        {
            ValidateMainThread();
            _cache.ClearUnused(key => !_pendingLoads.ContainsKey(key), UnloadAsset);
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
                    pendingLoad.TrySetResult(null);
                    return;
                }

                if (generation != _generation)
                {
                    pendingLoad.TrySetResult(null);
                    return;
                }

                if (!_cache.TryGet<TAsset>(path, out var cachedAsset))
                {
                    _cache.AddUnused(path, asset);
                    cachedAsset = asset;
                }

                pendingLoad.TrySetResult(cachedAsset);
            }
            catch (Exception exception)
            {
                pendingLoad.TrySetException(exception);
            }
            finally
            {
                if (_pendingLoads.TryGetValue(key, out var current) && ReferenceEquals(current, pendingLoad))
                {
                    _pendingLoads.Remove(key);
                }
            }
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
