using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib.Messaging
{
    public sealed class EventBusDebugWindow : EditorWindow
    {
        private readonly List<EventBus.DebugInfo> debugInfo = new List<EventBus.DebugInfo>();
        private readonly Dictionary<Type, bool> foldoutStates = new Dictionary<Type, bool>();
        private Vector2 scrollPosition;

        [MenuItem("Window/PschLib/Event Bus Debugger")]
        private static void Open()
        {
            GetWindow<EventBusDebugWindow>("Event Bus Debugger");
        }

        private void OnEnable()
        {
            EventBus.DebugListenersChanged += Repaint;
        }

        private void OnDisable()
        {
            EventBus.DebugListenersChanged -= Repaint;
        }

        private void OnGUI()
        {
            EventBus.GetDebugInfo(debugInfo);

            EditorGUILayout.LabelField("Active Listeners", debugInfo.Count.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (debugInfo.Count == 0)
            {
                EditorGUILayout.HelpBox("There are no active EventBus listeners.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

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

            EditorGUILayout.EndScrollView();
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
