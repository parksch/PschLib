using UnityEditor;
using UnityEngine.Scripting.APIUpdating;

namespace PschLib.GoogleSheets
{
    [MovedFrom(true, sourceNamespace: "PschLib", sourceAssembly: "PschLib.Unity.Editor")]
    [FilePath("ProjectSettings/PschGoogleSheetSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class GoogleSheetSettings : ScriptableSingleton<GoogleSheetSettings>
    {
        public GoogleSheetServer Server;
        public GoogleSheetProject Project;

        public void SaveSettings()
        {
            Save(true);
        }
    }
}
