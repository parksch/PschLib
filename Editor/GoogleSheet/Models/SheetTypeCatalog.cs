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

            if (TryParseEnum(typeName, SheetTypeKind.Enum, out typeInfo))
            {
                return true;
            }

            if (typeName.StartsWith("List<", StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">", StringComparison.OrdinalIgnoreCase))
            {
                var elementName = typeName.Substring(5, typeName.Length - 6).Trim();

                if (TryParseEnum(elementName, SheetTypeKind.EnumList, out typeInfo))
                {
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

        private static bool TryParseEnum(string typeName, SheetTypeKind kind, out SheetTypeInfo typeInfo)
        {
            typeInfo = null;

            if (typeName.Equals("enum", StringComparison.OrdinalIgnoreCase))
            {
                typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Local, null, null);
                return true;
            }

            if (kind == SheetTypeKind.Enum && typeName.Equals("shared-enum", StringComparison.OrdinalIgnoreCase))
            {
                typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Shared, null, null);
                return true;
            }

            if (TryGetGenericArgument(typeName, "shared-enum", out var sharedEnumName))
            {
                typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Shared, sharedEnumName, null);
                return true;
            }

            if (!TryGetGenericArgument(typeName, "enum", out var existingEnumName))
            {
                return false;
            }

            var enumType = FindType(existingEnumName);

            if (enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Existing, existingEnumName, enumType);
            return true;
        }

        private static bool TryGetGenericArgument(string value, string prefix, out string argument)
        {
            argument = null;
            var opening = $"{prefix}<";

            if (!value.StartsWith(opening, StringComparison.OrdinalIgnoreCase) || !value.EndsWith(">", StringComparison.Ordinal))
            {
                return false;
            }

            argument = value.Substring(opening.Length, value.Length - opening.Length - 1).Trim();
            return !string.IsNullOrWhiteSpace(argument);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);

                if (type != null)
                {
                    return type;
                }
            }

            return null;
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
