using UnityEngine;

namespace PschLib
{
    [CreateAssetMenu(fileName = "GoogleSheetServer", menuName = "PschLib/Google Sheet Server")]
    public sealed class GoogleSheetServer : ScriptableObject
    {
        public string WebAppUrl;
        [HideInInspector] public string RegistrySpreadsheetId;
        [HideInInspector] public int RegistrySheetId;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(WebAppUrl);
        public bool HasRegistrySpreadsheet => !string.IsNullOrWhiteSpace(RegistrySpreadsheetId);
    }
}
