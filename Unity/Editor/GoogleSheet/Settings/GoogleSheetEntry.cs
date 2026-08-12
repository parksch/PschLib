using System;

namespace PschLib
{
    [Serializable]
    public sealed class GoogleSheetEntry
    {
        public string Name;
        public int SheetId;
        public bool Selected = true;
    }
}
