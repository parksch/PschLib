using System;

namespace PschLib.AssetLoading.Addressables.Internal
{
    internal readonly struct AddressableAssetKey : IEquatable<AddressableAssetKey>
    {
        private readonly string address;
        private readonly Type assetType;

        public string Address => address;
        public Type AssetType => assetType;

        public AddressableAssetKey(string address, Type assetType)
        {
            this.address = address;
            this.assetType = assetType;
        }

        public bool Equals(AddressableAssetKey other)
        {
            return string.Equals(address, other.address, StringComparison.Ordinal) &&
                assetType == other.assetType;
        }

        public override bool Equals(object obj)
        {
            return obj is AddressableAssetKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var addressHash = address != null ? StringComparer.Ordinal.GetHashCode(address) : 0;
                return (addressHash * 397) ^ (assetType != null ? assetType.GetHashCode() : 0);
            }
        }
    }
}
