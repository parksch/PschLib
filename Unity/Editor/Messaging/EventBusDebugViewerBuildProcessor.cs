using PschLib.Unity.Debugging;
using PschLib.Unity.Messaging;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace PschLib.Unity.Editor.Messaging
{
    public sealed class EventBusDebugViewerBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null)
            {
                DebugViewerBuildUtility.RemoveFromScene<EventBusDebugViewer>(scene);
            }
        }
    }
}
