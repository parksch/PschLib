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
                error = $"[{document.Name}] Name and type rows are required.";
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

                if (!SheetTypeCatalog.TryParse(rawType, out var typeInfo))
                {
                    error = $"[{document.Name}] Column {column + 1} ('{name}') has an unsupported type: '{rawType}'.";
                    return false;
                }

                var isKey = name.Equals("id", StringComparison.OrdinalIgnoreCase);
                fields.Add(new SheetField(column, name, typeInfo, isKey));
            }

            return true;
        }
    }
}
