using System;
using System.Collections.Generic;

namespace PschLib
{
    public static class SheetHeaderParser
    {
        public static bool TryParse(SheetDocument document, out List<SheetField> fields, out string error)
        {
            fields = new List<SheetField>();
            error = null;

            if (document.Rows.Count < 2)
            {
                error = $"[{document.Name}] 이름 행과 타입 행이 필요합니다.";
                return false;
            }

            var nameRow = document.Rows[0];
            var typeRow = document.Rows[1];

            for (var column = 0; column < nameRow.Count; column++)
            {
                var name = nameRow[column].Trim();

                if (string.IsNullOrEmpty(name) || name.StartsWith("&"))
                {
                    continue;
                }

                var rawType = column < typeRow.Count ? typeRow[column] : string.Empty;

                if (!SheetTypeMap.TryParse(rawType, out var typeInfo))
                {
                    error = $"[{document.Name}] {column + 1}번째 열 '{name}'의 타입 '{rawType}'을 해석할 수 없습니다.";
                    return false;
                }

                var isKey = name.Equals("id", StringComparison.OrdinalIgnoreCase);
                fields.Add(new SheetField(column, name, typeInfo, isKey));
            }

            return true;
        }
    }
}
