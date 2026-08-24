#if UNITY_EDITOR
namespace PschLib.AssetLoading.Debugging
{
    public readonly struct AssetLoaderDebugEntry
    {
        public readonly string Address;
        public readonly string AssetTypeName;
        public readonly int ReferenceCount;

        public AssetLoaderDebugEntry(string address, string assetTypeName, int referenceCount)
        {
            Address = address;
            AssetTypeName = assetTypeName;
            ReferenceCount = referenceCount;
        }
    }
}
#endif
