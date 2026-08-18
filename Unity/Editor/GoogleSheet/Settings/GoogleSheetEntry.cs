using System;
using UnityEngine.Scripting.APIUpdating;

namespace PschLib.GoogleSheets
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "PschLib", sourceAssembly: "PschLib.Unity.Editor")]
    public sealed class GoogleSheetEntry
    {
        public string Name;
        public int SheetId;
        public bool Selected = true;
    }
}
