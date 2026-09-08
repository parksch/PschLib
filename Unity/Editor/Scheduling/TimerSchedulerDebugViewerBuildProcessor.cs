using PschLib.Unity.Debugging;
using PschLib.Unity.Scheduling;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace PschLib.Unity.Editor.Scheduling
{
    public sealed class TimerSchedulerDebugViewerBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null)
            {
                DebugViewerBuildUtility.RemoveFromScene<TimerSchedulerDebugViewer>(scene);
            }
        }
    }
}
