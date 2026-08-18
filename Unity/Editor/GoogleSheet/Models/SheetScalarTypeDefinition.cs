using System;

namespace PschLib.GoogleSheets
{
    public sealed class SheetScalarTypeDefinition
    {
        public string SheetName { get; }
        public Type RuntimeType { get; }
        public string CSharpName { get; }

        public SheetScalarTypeDefinition(string sheetName, Type runtimeType, string cSharpName)
        {
            SheetName = sheetName;
            RuntimeType = runtimeType;
            CSharpName = cSharpName;
        }
    }
}
