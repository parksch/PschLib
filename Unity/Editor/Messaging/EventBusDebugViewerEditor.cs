using System;
using System.Collections.Generic;
using PschLib.Messaging;
using PschLib.Unity.Debugging;
using PschLib.Unity.Messaging;
using UnityEditor;
using UnityEngine;

namespace PschLib.Unity.Editor.Messaging
{
    [CustomEditor(typeof(EventBusDebugViewer))]
    public sealed class EventBusDebugViewerEditor
        : DebugViewerEditorBase<EventBusDebugViewer, EventBusDebugViewer>
    {
        private readonly List<EventBus.DebugInfo> debugInfo = new List<EventBus.DebugInfo>();
        private readonly Dictionary<Type, bool> foldoutStates = new Dictionary<Type, bool>();

        protected override string EmptyMessage => "The EventBus debug source is unavailable.";

        protected override void Subscribe(EventBusDebugViewer viewer, Action callback)
        {
            viewer.DebugStateChanged += callback;
        }

        protected override void Unsubscribe(EventBusDebugViewer viewer, Action callback)
        {
            viewer.DebugStateChanged -= callback;
        }

        protected override void DrawDebugInfo(
            MonoBehaviour component,
            string fieldName,
            EventBusDebugViewer viewer)
        {
            viewer.GetDebugInfo(debugInfo);

            EditorGUILayout.LabelField("Active Listeners", debugInfo.Count.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (debugInfo.Count == 0)
            {
                EditorGUILayout.HelpBox("There are no active EventBus listeners.", MessageType.Info);
                return;
            }

            var index = 0;
            while (index < debugInfo.Count)
            {
                var eventType = debugInfo[index].EventType;
                var endIndex = index + 1;

                while (endIndex < debugInfo.Count && debugInfo[endIndex].EventType == eventType)
                {
                    endIndex++;
                }

                DrawEventGroup(eventType, index, endIndex);
                index = endIndex;
            }
        }

        private void DrawEventGroup(Type eventType, int startIndex, int endIndex)
        {
            bool isExpanded;
            if (!foldoutStates.TryGetValue(eventType, out isExpanded))
            {
                isExpanded = true;
            }

            var eventTypeName = eventType.FullName ?? eventType.Name;
            isExpanded = EditorGUILayout.Foldout(isExpanded, $"{eventTypeName} ({endIndex - startIndex})", true);
            foldoutStates[eventType] = isExpanded;

            if (!isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;

            for (var i = startIndex; i < endIndex; i++)
            {
                DrawListener(debugInfo[i]);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private static void DrawListener(EventBus.DebugInfo info)
        {
            var declaringTypeName = info.DeclaringType != null
                ? info.DeclaringType.FullName ?? info.DeclaringType.Name
                : "Unknown";
            var methodName = $"#{info.ListenerId}  {declaringTypeName}.{info.MethodName}";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(methodName);

            var unityTarget = info.Target as UnityEngine.Object;
            if (unityTarget != null)
            {
                EditorGUILayout.ObjectField(unityTarget, typeof(UnityEngine.Object), true);
            }
            else
            {
                EditorGUILayout.LabelField(info.Target == null ? "Static" : info.Target.GetType().Name);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
