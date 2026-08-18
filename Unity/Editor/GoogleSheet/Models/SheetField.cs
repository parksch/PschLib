namespace PschLib.GoogleSheets
{
    public sealed class SheetField
    {
        public int ColumnIndex { get; }
        public string Name { get; }
        public SheetTypeInfo Type { get; }
        public bool IsKey { get; }

        public SheetField(int columnIndex, string name, SheetTypeInfo type, bool isKey)
        {
            ColumnIndex = columnIndex;
            Name = name;
            Type = type;
            IsKey = isKey;
        }
    }
}

