using System;

namespace PschLib
{
    public sealed class SheetTypeInfo
    {
        public SheetTypeKind Kind { get; }
        public Type ElementType { get; }

        public SheetTypeInfo(SheetTypeKind kind, Type elementType)
        {
            Kind = kind;
            ElementType = elementType;
        }
    }
}
