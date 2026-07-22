using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    public sealed class EventBusDebugWindow : EditorWindow
    {
        private readonly List<EventBus.DebugInfo> _debugInfo = new List<EventBus.DebugInfo>();
        private readonly Dictionary<Type, bool> _foldoutStates = new Dictionary<Type, bool>();
        private Vector2 _scrollPosition;

        [MenuItem("Window/PschLib/Event Bus Debugger")]
        private static void Open()
        {
            GetWindow<EventBusDebugWindow>("Event Bus Debugger");
        }

        private void Update()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            EventBus.GetDebugInfo(_debugInfo);

            EditorGUILayout.LabelField("Active Listeners", _debugInfo.Count.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_debugInfo.Count == 0)
            {
                EditorGUILayout.HelpBox("There are no active EventBus listeners.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var index = 0;
            while (index < _debugInfo.Count)
            {
                var eventType = _debugInfo[index].EventType;
                var endIndex = index + 1;

                while (endIndex < _debugInfo.Count && _debugInfo[endIndex].EventType == eventType)
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
            if (!_foldoutStates.TryGetValue(eventType, out isExpanded))
            {
                isExpanded = true;
            }

            var eventTypeName = eventType.FullName ?? eventType.Name;
            isExpanded = EditorGUILayout.Foldout(
                isExpanded,
                $"{eventTypeName} ({endIndex - startIndex})",
                true);
            _foldoutStates[eventType] = isExpanded;

            if (!isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;

            for (var i = startIndex; i < endIndex; i++)
            {
                DrawListener(_debugInfo[i]);
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
