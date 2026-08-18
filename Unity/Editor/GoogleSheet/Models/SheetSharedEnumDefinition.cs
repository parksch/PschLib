using System;
using System.Collections.Generic;

namespace PschLib.GoogleSheets
{
    [Serializable]
    public sealed class SheetSharedEnumDefinition
    {
        public string Name;
        public List<string> Values = new List<string>();
    }
}
