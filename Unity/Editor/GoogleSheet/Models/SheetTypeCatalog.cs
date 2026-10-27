using System;
using System.Collections.Generic;

namespace PschLib.GoogleSheets
{
    public static class SheetTypeCatalog
    {
        private static readonly SheetScalarTypeDefinition[] definitions =
        {
            new SheetScalarTypeDefinition("string", typeof(string), "string"),
            new SheetScalarTypeDefinition("int", typeof(int), "int"),
            new SheetScalarTypeDefinition("long", typeof(long), "long"),
            new SheetScalarTypeDefinition("float", typeof(float), "float"),
            new SheetScalarTypeDefinition("double", typeof(double), "double"),
            new SheetScalarTypeDefinition("bool", typeof(bool), "bool")
        };

        private static readonly Dictionary<string, SheetScalarTypeDefinition> bySheetName = CreateSheetNameMap();
        private static readonly Dictionary<Type, SheetScalarTypeDefinition> byRuntimeType = CreateRuntimeTypeMap();

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

            return bySheetName.TryGetValue(sheetName.Trim(), out definition);
        }

        public static bool TryGetByRuntimeType(Type runtimeType, out SheetScalarTypeDefinition definition)
        {
            if (runtimeType == null)
            {
                definition = null;
                return false;
            }

            return byRuntimeType.TryGetValue(runtimeType, out definition);
        }

        private static bool TryParseEnum(string typeName, SheetTypeKind kind, out SheetTypeInfo typeInfo)
        {
            typeInfo = null;

            if (typeName.Equals("enum", StringComparison.OrdinalIgnoreCase))
            {
                typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Local, null, null);
                return true;
            }

            if (kind == SheetTypeKind.Enum &&
                (typeName.Equals("senum", StringComparison.OrdinalIgnoreCase) ||
                 typeName.Equals("shared-enum", StringComparison.OrdinalIgnoreCase)))
            {
                typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Shared, null, null);
                return true;
            }

            string sharedEnumName;

            if (TryGetGenericArgument(typeName, "senum", out sharedEnumName) ||
                TryGetGenericArgument(typeName, "shared-enum", out sharedEnumName))
            {
                typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Shared, sharedEnumName, null);
                return true;
            }

            if (!TryGetGenericArgument(typeName, "enum", out var existingEnumName))
            {
                return false;
            }

            var enumType = FindType(existingEnumName);

            if (!TryGetExistingEnumTypeName(enumType, out var csharpName, out _))
            {
                return false;
            }

            typeInfo = new SheetTypeInfo(kind, SheetEnumMode.Existing, csharpName, enumType);
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
            if (fullName.StartsWith("global::", StringComparison.Ordinal))
            {
                fullName = fullName.Substring("global::".Length);
            }
            fullName = fullName.Replace("@", string.Empty);

            // Prefer an exact CLR name, then try each namespace/type boundary.
            // Namespace.Outer.Inner.Kind -> Namespace.Outer+Inner+Kind.
            var candidate = fullName;
            while (true)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(candidate, false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                var boundary = candidate.LastIndexOf('.');
                if (boundary < 0)
                {
                    return null;
                }
                candidate = candidate.Substring(0, boundary) + "+" + candidate.Substring(boundary + 1);
            }
        }

        internal static bool TryGetExistingEnumTypeName(Type enumType, out string typeName, out string error)
        {
            typeName = null;
            error = null;
            if (enumType == null || !enumType.IsEnum)
            {
                error = "The referenced type must be an enum.";
                return false;
            }

            if (!enumType.IsVisible)
            {
                error = $"Enum '{enumType.FullName}' and all its containing types must be public.";
                return false;
            }

            if (enumType.IsGenericType || enumType.ContainsGenericParameters)
            {
                error = $"Enum '{enumType.FullName}' inside a generic type is not supported.";
                return false;
            }

            var sections = enumType.FullName.Replace('+', '.').Split('.');
            for (var i = 0; i < sections.Length; i++)
            {
                if (SheetDataCodeGenerator.IsValidIdentifier(sections[i]))
                {
                    continue;
                }

                // Reflection omits the @ escape used for C# keyword identifiers.
                if (string.IsNullOrEmpty(sections[i]) || !SheetDataCodeGenerator.IsValidIdentifier(sections[i] + "Value"))
                {
                    error = $"Enum '{enumType.FullName}' cannot be represented as a C# type name.";
                    return false;
                }
                sections[i] = "@" + sections[i];
            }

            typeName = string.Join(".", sections);
            return true;
        }

        private static Dictionary<string, SheetScalarTypeDefinition> CreateSheetNameMap()
        {
            var result = new Dictionary<string, SheetScalarTypeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in definitions)
            {
                result.Add(definition.SheetName, definition);
            }

            return result;
        }

        private static Dictionary<Type, SheetScalarTypeDefinition> CreateRuntimeTypeMap()
        {
            var result = new Dictionary<Type, SheetScalarTypeDefinition>();

            foreach (var definition in definitions)
            {
                result.Add(definition.RuntimeType, definition);
            }

            return result;
        }
    }
}
