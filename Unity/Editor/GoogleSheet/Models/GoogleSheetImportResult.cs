using System.Collections.Generic;

namespace PschLib.GoogleSheets
{
    internal sealed class GoogleSheetImportResult
    {
        public SheetDocument Document { get; }
        public List<SheetField> Fields { get; }
        public List<SheetDataRow> Rows { get; }
        public string GeneratedCode { get; }

        public GoogleSheetImportResult(SheetDocument document, List<SheetField> fields, List<SheetDataRow> rows, string generatedCode)
        {
            Document = document;
            Fields = fields;
            Rows = rows;
            GeneratedCode = generatedCode;
        }
    }
}
