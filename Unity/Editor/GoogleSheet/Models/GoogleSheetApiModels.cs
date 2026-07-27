using System;
using System.Collections.Generic;

namespace PschLib
{
    [Serializable]
    internal sealed class GoogleSheetListResponse
    {
        public bool Success;
        public string Error;
        public List<GoogleSheetListItem> Sheets = new List<GoogleSheetListItem>();
    }

    [Serializable]
    internal sealed class GoogleSheetListItem
    {
        public string Name;
        public int SheetId;
    }

    [Serializable]
    internal sealed class GoogleSheetDataResponse
    {
        public bool Success;
        public string Error;
        public GoogleSheetDataPayload Sheet;
    }

    [Serializable]
    internal sealed class GoogleSheetDataPayload
    {
        public string Name;
        public int SheetId;
        public List<GoogleSheetRowPayload> Rows = new List<GoogleSheetRowPayload>();
    }

    [Serializable]
    internal sealed class GoogleSheetRowPayload
    {
        public List<string> Cells = new List<string>();
    }
}
