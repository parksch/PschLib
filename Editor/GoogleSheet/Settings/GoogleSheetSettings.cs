using System.Collections.Generic;
using UnityEditor;

namespace PschLib
{
    [FilePath("ProjectSettings/PschGoogleSheetSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class GoogleSheetSettings : ScriptableSingleton<GoogleSheetSettings>
    {
        public string WebAppUrl;
        public string TargetNamespace = "GoogleSheetData";
        public string ScriptOutputPath = "Assets/Scripts/GoogleSheetData";
        public string AssetOutputPath = "Assets/Data/GoogleSheetData";
        public List<GoogleSheetEntry> Sheets = new List<GoogleSheetEntry>();

        public bool IsConnected => !string.IsNullOrWhiteSpace(WebAppUrl);

        public void SaveSettings()
        {
            Save(true);
        }
    }
}
