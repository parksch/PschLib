using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PschLib.Unity.Debugging
{
    public abstract class DebugViewerEditorBase<TViewer, TDebugInfo> : UnityEditor.Editor
        where TViewer : MonoBehaviour
        where TDebugInfo : class
    {
        private readonly List<DebugTarget> targets = new List<DebugTarget>();
        private readonly List<TDebugInfo> foundDebugInfos = new List<TDebugInfo>();
        private readonly List<TDebugInfo> subscribedDebugInfos = new List<TDebugInfo>();

        protected abstract string EmptyMessage { get; }

        protected virtual void OnEnable()
        {
            RefreshSubscriptions();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeAll();
        }

        public sealed override void OnInspectorGUI()
        {
            RefreshTargets();
            RefreshSubscriptions();

            for (var i = 0; i < targets.Count; i++)
            {
                var debugTarget = targets[i];
                DrawDebugInfo(debugTarget.Component, debugTarget.FieldName, debugTarget.DebugInfo);
            }

            if (targets.Count == 0)
            {
                EditorGUILayout.HelpBox(EmptyMessage, MessageType.Info);
            }
        }

        public sealed override bool RequiresConstantRepaint()
        {
            return EditorApplication.isPlaying && subscribedDebugInfos.Count == 0;
        }

        protected abstract void DrawDebugInfo(MonoBehaviour component, string fieldName, TDebugInfo debugInfo);
        protected abstract void Subscribe(TDebugInfo debugInfo, Action callback);
        protected abstract void Unsubscribe(TDebugInfo debugInfo, Action callback);

        private void RefreshTargets()
        {
            targets.Clear();
            foundDebugInfos.Clear();

            if (!(target is TViewer viewer))
            {
                return;
            }

            var components = viewer.GetComponents<MonoBehaviour>();

            for (var i = 0; i < components.Length; i++)
            {
                FindDebugTargets(components[i]);
            }
        }

        private void FindDebugTargets(MonoBehaviour component)
        {
            if (component == null || component is TViewer)
            {
                return;
            }

            var type = component.GetType();

            while (type != null && type != typeof(MonoBehaviour))
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (var i = 0; i < fields.Length; i++)
                {
                    if (!(fields[i].GetValue(component) is TDebugInfo debugInfo))
                    {
                        continue;
                    }

                    targets.Add(new DebugTarget(component, fields[i].Name, debugInfo));

                    if (!foundDebugInfos.Contains(debugInfo))
                    {
                        foundDebugInfos.Add(debugInfo);
                    }
                }

                type = type.BaseType;
            }
        }

        private void RefreshSubscriptions()
        {
            if (targets.Count == 0)
            {
                RefreshTargets();
            }

            for (var i = subscribedDebugInfos.Count - 1; i >= 0; i--)
            {
                var debugInfo = subscribedDebugInfos[i];

                if (foundDebugInfos.Contains(debugInfo))
                {
                    continue;
                }

                Unsubscribe(debugInfo, Repaint);
                subscribedDebugInfos.RemoveAt(i);
            }

            for (var i = 0; i < foundDebugInfos.Count; i++)
            {
                var debugInfo = foundDebugInfos[i];

                if (subscribedDebugInfos.Contains(debugInfo))
                {
                    continue;
                }

                Subscribe(debugInfo, Repaint);
                subscribedDebugInfos.Add(debugInfo);
            }
        }

        private void UnsubscribeAll()
        {
            for (var i = 0; i < subscribedDebugInfos.Count; i++)
            {
                Unsubscribe(subscribedDebugInfos[i], Repaint);
            }

            subscribedDebugInfos.Clear();
        }

        private readonly struct DebugTarget
        {
            public readonly MonoBehaviour Component;
            public readonly string FieldName;
            public readonly TDebugInfo DebugInfo;

            public DebugTarget(MonoBehaviour component, string fieldName, TDebugInfo debugInfo)
            {
                Component = component;
                FieldName = fieldName;
                DebugInfo = debugInfo;
            }
        }
    }
}
