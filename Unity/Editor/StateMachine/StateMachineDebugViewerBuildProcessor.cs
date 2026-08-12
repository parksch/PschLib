using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PschLib
{
    public sealed class StateMachineDebugViewerBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null)
            {
                return;
            }

            var rootObjects = scene.GetRootGameObjects();

            for (var i = 0; i < rootObjects.Length; i++)
            {
                var viewers = rootObjects[i].GetComponentsInChildren<StateMachineDebugViewer>(true);

                for (var j = 0; j < viewers.Length; j++)
                {
                    Object.DestroyImmediate(viewers[j]);
                }
            }
        }
    }
}
