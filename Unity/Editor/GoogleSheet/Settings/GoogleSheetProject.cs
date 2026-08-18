using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PschLib.GoogleSheets
{
    [MovedFrom(true, sourceNamespace: "PschLib", sourceAssembly: "PschLib.Unity.Editor")]
    [CreateAssetMenu(fileName = "GoogleSheetProject", menuName = "PschLib/Google Sheet Project")]
    public sealed class GoogleSheetProject : ScriptableObject
    {
        [Header("Connection")]
        public GoogleSheetServer Server;
        public string ProjectKey;
        public string SpreadsheetId;
        public string SpreadsheetName;

        [Header("Generation")]
        public string RootNamespace = "GoogleSheetData";
        public string ScriptOutputPath = "Assets/Scripts/GoogleSheetData";
        public bool GenerateScriptableObject = true;
        public string AssetOutputPath = "Assets/Data/GoogleSheetData";

        [Header("Sheets")]
        public List<GoogleSheetEntry> Sheets = new List<GoogleSheetEntry>();

        [HideInInspector]
        public List<SheetSharedEnumDefinition> SharedEnums = new List<SheetSharedEnumDefinition>();

        public bool IsConnected => Server != null && Server.IsConfigured && !string.IsNullOrWhiteSpace(SpreadsheetId);
    }
}
