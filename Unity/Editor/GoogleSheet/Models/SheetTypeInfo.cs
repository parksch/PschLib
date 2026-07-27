namespace PschLib
{
    public sealed class SheetTypeInfo
    {
        public SheetTypeKind Kind { get; }
        public SheetScalarTypeDefinition ElementType { get; }

        public SheetTypeInfo(SheetTypeKind kind, SheetScalarTypeDefinition elementType)
        {
            Kind = kind;
            ElementType = elementType;
        }
    }
}
