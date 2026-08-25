using System;

namespace PschLib.AssetLoading.Addressables.Internal
{
    internal readonly struct AddressableAssetKey : IEquatable<AddressableAssetKey>
    {
        private readonly string _address;
        private readonly Type _assetType;

        public string Address => _address;
        public Type AssetType => _assetType;

        public AddressableAssetKey(string address, Type assetType)
        {
            _address = address;
            _assetType = assetType;
        }

        public bool Equals(AddressableAssetKey other)
        {
            return string.Equals(_address, other._address, StringComparison.Ordinal) &&
                _assetType == other._assetType;
        }

        public override bool Equals(object obj)
        {
            return obj is AddressableAssetKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var addressHash = _address != null ? StringComparer.Ordinal.GetHashCode(_address) : 0;
                return (addressHash * 397) ^ (_assetType != null ? _assetType.GetHashCode() : 0);
            }
        }
    }
}
