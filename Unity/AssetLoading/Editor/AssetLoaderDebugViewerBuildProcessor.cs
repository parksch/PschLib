using PschLib.AssetLoading.Debugging;
using PschLib.Unity.Debugging;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace PschLib.AssetLoading.Editor
{
    public sealed class AssetLoaderDebugViewerBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null)
            {
                DebugViewerBuildUtility.RemoveFromScene<AssetLoaderDebugViewer>(scene);
            }
        }
    }
}
