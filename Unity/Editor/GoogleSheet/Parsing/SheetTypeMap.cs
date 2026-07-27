using System;
using System.Collections.Generic;

namespace PschLib
{
    public static class SheetTypeMap
    {
        private static readonly Dictionary<string, Type> ScalarTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "string", typeof(string) },
            { "int", typeof(int) },
            { "long", typeof(long) },
            { "float", typeof(float) },
            { "double", typeof(double) },
            { "bool", typeof(bool) }
        };

        public static bool TryParse(string rawType, out SheetTypeInfo typeInfo)
        {
            typeInfo = null;

            if(string.IsNullOrWhiteSpace(rawType))
            {
                return false;
            }

            var typeName = rawType.Trim();

            if (typeName.Equals("enum",StringComparison.OrdinalIgnoreCase))
            {
                typeInfo = new SheetTypeInfo(SheetTypeKind.Enum, null);
                return true;
            }

            if (typeName.StartsWith("List<",StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">",StringComparison.OrdinalIgnoreCase))
            {
                var elementName = typeName.Substring(5, typeName.Length - 6).Trim();

                if (elementName.Equals("enum", StringComparison.OrdinalIgnoreCase))
                {
                    typeInfo = new SheetTypeInfo(SheetTypeKind.EnumList, null);
                    return true;
                }

                if (!ScalarTypes.TryGetValue(elementName, out var elementType))
                {
                    return false;
                }

                typeInfo = new SheetTypeInfo(SheetTypeKind.List, elementType);
                return true;
            }

            if (!ScalarTypes.TryGetValue(typeName, out var scalarType))
            {
                return false;
            }

            typeInfo = new SheetTypeInfo(SheetTypeKind.Scalar, scalarType);
            return true;
        }
    }
}
