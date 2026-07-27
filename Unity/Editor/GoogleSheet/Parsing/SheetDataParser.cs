
using System;
using System.Collections.Generic;

namespace PschLib
{
    public static class SheetDataParser
    {
        public static bool TryParse(SheetDocument document, List<SheetField> fields, out List<SheetDataRow> rows, out string error)
        {
            rows = new List<SheetDataRow>();
            error = null;

            var keyField = FindKeyField(fields);

            if (keyField == null)
            {
                error = $"[{document.Name}] id 열이 없습니다.";
                return false;
            }

            if (keyField.Type.Kind != SheetTypeKind.Scalar || keyField.Type.ElementType != typeof(string))
            {
                error = $"[{document.Name}] id 열의 타입은 string이어야 합니다.";
                return false;
            }

            var idRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var rowIndex = 2; rowIndex < document.Rows.Count; rowIndex++)
            {
                var sourceRow = document.Rows[rowIndex];

                if (IsEmptyRow(sourceRow, fields))
                {
                    continue;
                }

                if (!TryParseRow(document.Name, rowIndex, sourceRow, fields, out var dataRow, out error))
                {
                    return false;
                }

                if (idRows.TryGetValue(dataRow.Id, out var firstRow))
                {
                    error = $"[{document.Name}] ID '{dataRow.Id}'가 {firstRow}행과 {dataRow.RowNumber}행에서 중복됐습니다.";
                    return false;
                }

                idRows.Add(dataRow.Id, dataRow.RowNumber);
                rows.Add(dataRow);
            }

            return true;
        }

        private static bool TryParseRow(string sheetName, int rowIndex, List<string> sourceRow, List<SheetField> fields, out SheetDataRow dataRow, out string error)
        {
            dataRow = null;
            error = null;

            var rowNumber = rowIndex + 1;
            var values = new Dictionary<SheetField, object>();
            string id = null;

            foreach (var field in fields)
            {
                var rawValue = field.ColumnIndex < sourceRow.Count ? sourceRow[field.ColumnIndex] : string.Empty;

                if (!SheetValueParser.TryParse(rawValue, field.Type, out var value, out var valueError))
                {
                    error = $"[{sheetName}] {rowNumber}행 '{field.Name}' 열: {valueError}";
                    return false;
                }

                if (field.IsKey)
                {
                    id = ((string)value).Trim().ToLowerInvariant();

                    if (string.IsNullOrEmpty(id))
                    {
                        error = $"[{sheetName}] {rowNumber}행의 id가 비어 있습니다.";
                        return false;
                    }

                    value = id;
                }

                values.Add(field, value);
            }

            dataRow = new SheetDataRow(rowNumber, id, values);
            return true;
        }

        private static SheetField FindKeyField(List<SheetField> fields)
        {
            foreach (var field in fields)
            {
                if (field.IsKey)
                {
                    return field;
                }
            }

            return null;
        }

        private static bool IsEmptyRow(List<string> sourceRow, List<SheetField> fields)
        {
            foreach (var field in fields)
            {
                if (field.ColumnIndex < sourceRow.Count && !string.IsNullOrWhiteSpace(sourceRow[field.ColumnIndex]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

