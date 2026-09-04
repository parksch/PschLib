using System;

namespace PschLib.AssetLoading.Internal
{
    internal readonly struct AssetKey : IEquatable<AssetKey>
    {
        private readonly string address;
        private readonly Type assetType;

        public string Address => address;
        public Type AssetType => assetType;

        public AssetKey(string address, Type assetType)
        {
            this.address = address;
            this.assetType = assetType;
        }

        public bool Equals(AssetKey other)
        {
            return string.Equals(address, other.address, StringComparison.Ordinal) && assetType == other.assetType;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((address != null ? StringComparer.Ordinal.GetHashCode(address) : 0) * 397) ^ (assetType != null ? assetType.GetHashCode() : 0);
            }
        }
    }
}
