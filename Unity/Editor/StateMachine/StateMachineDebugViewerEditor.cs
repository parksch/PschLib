using System;
using System.Collections.Generic;
using PschLib.StateMachines;
using UnityEditor;
using UnityEngine;

namespace PschLib.Unity.Debugging
{
    [CustomEditor(typeof(StateMachineDebugViewer))]
    public sealed class StateMachineDebugViewerEditor
        : DebugViewerEditorBase<StateMachineDebugViewer, IStateMachineDebugInfo>
    {
        private readonly List<string> _stateNames = new List<string>();

        protected override string EmptyMessage => "No StateMachine was found on this GameObject.";

        protected override void Subscribe(IStateMachineDebugInfo debugInfo, Action callback)
        {
            debugInfo.DebugStateChanged += callback;
        }

        protected override void Unsubscribe(IStateMachineDebugInfo debugInfo, Action callback)
        {
            debugInfo.DebugStateChanged -= callback;
        }

        protected override void DrawDebugInfo(MonoBehaviour component, string fieldName, IStateMachineDebugInfo debugInfo)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{component.GetType().Name}.{fieldName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State Type", debugInfo.StateTypeName);
            EditorGUILayout.LabelField("Started", debugInfo.IsStarted.ToString());
            EditorGUILayout.LabelField("Current State", debugInfo.CurrentStateName);

            debugInfo.GetRegisteredStateNames(_stateNames);
            EditorGUILayout.LabelField("Registered States", _stateNames.Count.ToString());
            EditorGUI.indentLevel++;

            for (var i = 0; i < _stateNames.Count; i++)
            {
                EditorGUILayout.LabelField(_stateNames[i]);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
    }
}
