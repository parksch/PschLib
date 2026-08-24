#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace PschLib.AssetLoading.Debugging
{
    public interface IAssetLoaderDebugInfo
    {
        event Action DebugStateChanged;
        string LoaderName { get; }
        int CachedAssetCount { get; }
        int ActiveAssetCount { get; }
        int PendingLoadCount { get; }
        void GetCachedAssetEntries(List<AssetLoaderDebugEntry> entries);
        void GetPendingLoadEntries(List<AssetLoaderDebugEntry> entries);
    }
}
#endif
