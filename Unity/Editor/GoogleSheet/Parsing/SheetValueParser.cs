using System;
using System.Collections;
using System.Globalization;

namespace PschLib
{
    public static class SheetValueParser
    {
        public static bool TryParse(string rawValue, SheetTypeInfo typeInfo, out object value, out string error)
        {
            value = null;
            error = null;
            rawValue = rawValue?.Trim();

            if (typeInfo.Kind == SheetTypeKind.Enum)
            {
                if (string.IsNullOrEmpty(rawValue))
                {
                    error = "Enum 값은 비워둘 수 없습니다.";
                    return false;
                }

                value = rawValue;
                return true;
            }

            if (typeInfo.Kind == SheetTypeKind.EnumList)
            {
                value = ParseEnumList(rawValue);
                return true;
            }

            if (typeInfo.Kind == SheetTypeKind.List)
            {
                return TryParseList(rawValue, typeInfo.ElementType, out value, out error);
            }

            return TryParseScalar(rawValue, typeInfo.ElementType, out value, out error);
        }

        private static bool TryParseScalar(string rawValue, Type type, out object value, out string error)
        {
            value = null;
            error = null;

            if (type == typeof(string))
            {
                value = string.IsNullOrEmpty(rawValue) ? string.Empty : rawValue;
                return true;
            }

            if (string.IsNullOrEmpty(rawValue))
            {
                value = Activator.CreateInstance(type);
                return true;
            }

            if (type == typeof(int) && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                value = intValue;
                return true;
            }

            if (type == typeof(long) && long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                value = longValue;
                return true;
            }

            if (type == typeof(float) && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                value = floatValue;
                return true;
            }

            if (type == typeof(double) && double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                value = doubleValue;
                return true;
            }

            if (type == typeof(bool) && bool.TryParse(rawValue, out var boolValue))
            {
                value = boolValue;
                return true;
            }

            error = $"'{rawValue}' 값을 {type.Name} 타입으로 변환할 수 없습니다.";
            return false;
        }

        private static bool TryParseList(string rawValue, Type elementType, out object value, out string error)
        {
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);

            value = list;
            error = null;

            if (string.IsNullOrEmpty(rawValue))
            {
                return true;
            }

            foreach (var element in rawValue.Split(','))
            {
                if (!TryParseScalar(element.Trim(), elementType, out var parsedElement, out error))
                {
                    return false;
                }

                list.Add(parsedElement);
            }

            return true;
        }

        private static string[] ParseEnumList(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue))
            {
                return Array.Empty<string>();
            }

            var values = rawValue.Split(',');

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = values[i].Trim();
            }

            return values;
        }
    }
}
