using UnityEngine;

namespace PschLib
{
    [CreateAssetMenu(fileName = "GoogleSheetServer", menuName = "PschLib/Google Sheet Server")]
    public sealed class GoogleSheetServer : ScriptableObject
    {
        public string WebAppUrl;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(WebAppUrl);
    }
}
