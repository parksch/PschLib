using System.Collections.Generic;

namespace PschLib.GoogleSheets
{
    public sealed class SheetDocument
    {
        public string Name { get; }
        public List<List<string>> Rows { get; }

        public SheetDocument(string name, List<List<string>> rows)
        {
            Name = name;
            Rows = rows;
        }
    }
}
