using System;
using PschLib.Debugging;
using UnityEditor;
using UnityEngine;

namespace PschLib.Unity.Editor.Debugging
{
    [InitializeOnLoad]
    internal static class DebugObserverExceptionReporterInitializer
    {
        static DebugObserverExceptionReporterInitializer()
        {
            DebugObserverExceptionReporter.SetReporter(ReportToUnityConsole);
        }

        private static void ReportToUnityConsole(string source, Exception exception, object context)
        {
            var reportedException = new Exception(
                $"{source} debug listener failed: {exception.Message}",
                exception);
            var unityContext = context as UnityEngine.Object;

            if (unityContext != null)
            {
                Debug.LogException(reportedException, unityContext);
                return;
            }

            Debug.LogException(reportedException);
        }
    }
}
