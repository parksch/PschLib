using System;
using System.Collections.Generic;

namespace PschLib.GoogleSheets
{
    [Serializable]
    internal sealed class GoogleSheetProjectListResponse
    {
        public bool Success;
        public string Error;
        public string RegistrySpreadsheetId;
        public int RegistrySheetId;
        public List<GoogleSheetProjectItem> Projects = new List<GoogleSheetProjectItem>();
    }

    [Serializable]
    internal sealed class GoogleSheetProjectItem
    {
        public string Key;
        public string SpreadsheetId;
        public string Name;
    }

    [Serializable]
    internal sealed class GoogleSheetProjectRegistrationResponse
    {
        public bool Success;
        public string Error;
        public GoogleSheetProjectItem Project;
    }

    [Serializable]
    internal sealed class GoogleSheetListResponse
    {
        public bool Success;
        public string Error;
        public string SpreadsheetId;
        public string Name;
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
        public string SpreadsheetId;
        public string SpreadsheetName;
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
