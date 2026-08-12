using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    [CustomEditor(typeof(StateMachineDebugViewer))]
    public sealed class StateMachineDebugViewerEditor : UnityEditor.Editor
    {
        private readonly List<string> _stateNames = new List<string>();

        public override void OnInspectorGUI()
        {
            var viewer = (StateMachineDebugViewer)target;
            var foundCount = DrawStateMachinesOnGameObject(viewer.gameObject);

            if (foundCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No StateMachine was found on this GameObject.",
                    MessageType.Info);
            }

        }

        public override bool RequiresConstantRepaint()
        {
            return EditorApplication.isPlaying;
        }

        private int DrawStateMachinesOnGameObject(GameObject gameObject)
        {
            var foundCount = 0;
            var components = gameObject.GetComponents<MonoBehaviour>();

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];

                if (component == null || component is StateMachineDebugViewer)
                {
                    continue;
                }

                foundCount += DrawStateMachines(component);
            }

            return foundCount;
        }

        private int DrawStateMachines(MonoBehaviour component)
        {
            var foundCount = 0;
            var type = component.GetType();

            while (type != null && type != typeof(MonoBehaviour))
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (var i = 0; i < fields.Length; i++)
                {
                    var debugInfo = fields[i].GetValue(component) as IStateMachineDebugInfo;

                    if (debugInfo == null)
                    {
                        continue;
                    }

                    DrawStateMachine(component, fields[i].Name, debugInfo);
                    foundCount++;
                }

                type = type.BaseType;
            }

            return foundCount;
        }

        private void DrawStateMachine(MonoBehaviour component, string fieldName, IStateMachineDebugInfo debugInfo)
        {
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
            EditorGUILayout.Space();
        }
    }
}
