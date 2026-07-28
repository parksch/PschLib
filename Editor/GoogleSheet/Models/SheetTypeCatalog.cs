using System;
using System.Collections.Generic;

namespace PschLib
{
    public static class SheetTypeCatalog
    {
        private static readonly SheetScalarTypeDefinition[] Definitions =
        {
            new SheetScalarTypeDefinition("string", typeof(string), "string"),
            new SheetScalarTypeDefinition("int", typeof(int), "int"),
            new SheetScalarTypeDefinition("long", typeof(long), "long"),
            new SheetScalarTypeDefinition("float", typeof(float), "float"),
            new SheetScalarTypeDefinition("double", typeof(double), "double"),
            new SheetScalarTypeDefinition("bool", typeof(bool), "bool")
        };

        private static readonly Dictionary<string, SheetScalarTypeDefinition> BySheetName = CreateSheetNameMap();
        private static readonly Dictionary<Type, SheetScalarTypeDefinition> ByRuntimeType = CreateRuntimeTypeMap();

        public static bool TryParse(string rawType, out SheetTypeInfo typeInfo)
        {
            typeInfo = null;

            if (string.IsNullOrWhiteSpace(rawType))
            {
                return false;
            }

            var typeName = rawType.Trim();

            if (typeName.Equals("enum", StringComparison.OrdinalIgnoreCase))
            {
                typeInfo = new SheetTypeInfo(SheetTypeKind.Enum, null);
                return true;
            }

            if (typeName.StartsWith("List<", StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">", StringComparison.OrdinalIgnoreCase))
            {
                var elementName = typeName.Substring(5, typeName.Length - 6).Trim();

                if (elementName.Equals("enum", StringComparison.OrdinalIgnoreCase))
                {
                    typeInfo = new SheetTypeInfo(SheetTypeKind.EnumList, null);
                    return true;
                }

                if (!TryGetBySheetName(elementName, out var listElementType))
                {
                    return false;
                }

                typeInfo = new SheetTypeInfo(SheetTypeKind.List, listElementType);
                return true;
            }

            if (!TryGetBySheetName(typeName, out var scalarType))
            {
                return false;
            }

            typeInfo = new SheetTypeInfo(SheetTypeKind.Scalar, scalarType);
            return true;
        }

        public static bool TryGetBySheetName(string sheetName, out SheetScalarTypeDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                definition = null;
                return false;
            }

            return BySheetName.TryGetValue(sheetName.Trim(), out definition);
        }

        public static bool TryGetByRuntimeType(Type runtimeType, out SheetScalarTypeDefinition definition)
        {
            if (runtimeType == null)
            {
                definition = null;
                return false;
            }

            return ByRuntimeType.TryGetValue(runtimeType, out definition);
        }

        private static Dictionary<string, SheetScalarTypeDefinition> CreateSheetNameMap()
        {
            var result = new Dictionary<string, SheetScalarTypeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in Definitions)
            {
                result.Add(definition.SheetName, definition);
            }

            return result;
        }

        private static Dictionary<Type, SheetScalarTypeDefinition> CreateRuntimeTypeMap()
        {
            var result = new Dictionary<Type, SheetScalarTypeDefinition>();

            foreach (var definition in Definitions)
            {
                result.Add(definition.RuntimeType, definition);
            }

            return result;
        }
    }
}
