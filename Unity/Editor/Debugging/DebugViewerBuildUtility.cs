using UnityEngine;
using UnityEngine.SceneManagement;

namespace PschLib.Unity.Debugging
{
    public static class DebugViewerBuildUtility
    {
        public static void RemoveFromScene<TViewer>(Scene scene) where TViewer : Component
        {
            var rootObjects = scene.GetRootGameObjects();

            for (var i = 0; i < rootObjects.Length; i++)
            {
                var viewers = rootObjects[i].GetComponentsInChildren<TViewer>(true);

                for (var j = 0; j < viewers.Length; j++)
                {
                    Object.DestroyImmediate(viewers[j]);
                }
            }
        }
    }
}
