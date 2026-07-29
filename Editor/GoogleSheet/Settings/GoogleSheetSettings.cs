using UnityEditor;

namespace PschLib
{
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
