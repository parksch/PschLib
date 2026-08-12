using System;

namespace PschLib
{
    public sealed class SheetTypeInfo
    {
        public SheetTypeKind Kind { get; }
        public SheetScalarTypeDefinition ElementType { get; }
        public SheetEnumMode EnumMode { get; }
        public string EnumTypeName { get; }
        public Type EnumRuntimeType { get; }

        public SheetTypeInfo(SheetTypeKind kind, SheetScalarTypeDefinition elementType)
        {
            Kind = kind;
            ElementType = elementType;
            EnumMode = SheetEnumMode.None;
        }

        public SheetTypeInfo(SheetTypeKind kind, SheetEnumMode enumMode, string enumTypeName, Type enumRuntimeType)
        {
            Kind = kind;
            EnumMode = enumMode;
            EnumTypeName = enumTypeName;
            EnumRuntimeType = enumRuntimeType;
        }
    }
}
