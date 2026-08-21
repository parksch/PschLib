using System;

namespace PschLib.AssetLoading.Internal
{
    internal readonly struct AssetKey : IEquatable<AssetKey>
    {
        private readonly string _address;
        private readonly Type _assetType;

        public AssetKey(string address, Type assetType)
        {
            _address = address;
            _assetType = assetType;
        }

        public bool Equals(AssetKey other)
        {
            return string.Equals(_address, other._address, StringComparison.Ordinal) && _assetType == other._assetType;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_address != null ? StringComparer.Ordinal.GetHashCode(_address) : 0) * 397) ^ (_assetType != null ? _assetType.GetHashCode() : 0);
            }
        }
    }
}
