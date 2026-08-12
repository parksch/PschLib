using System.Collections.Generic;

namespace PschLib
{
    public sealed class SheetDataRow
    {
        public int RowNumber { get; }
        public string Id { get; }
        public Dictionary<SheetField, object> Values { get; }

        public SheetDataRow(int rowNumber, string id, Dictionary<SheetField, object> values)
        {
            RowNumber = rowNumber;
            Id = id;
            Values = values;
        }
    }
}