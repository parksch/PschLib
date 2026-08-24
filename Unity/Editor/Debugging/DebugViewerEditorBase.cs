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
        private readonly List<DebugTarget> _targets = new List<DebugTarget>();
        private readonly List<TDebugInfo> _foundDebugInfos = new List<TDebugInfo>();
        private readonly List<TDebugInfo> _subscribedDebugInfos = new List<TDebugInfo>();

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

            for (var i = 0; i < _targets.Count; i++)
            {
                var debugTarget = _targets[i];
                DrawDebugInfo(debugTarget.Component, debugTarget.FieldName, debugTarget.DebugInfo);
            }

            if (_targets.Count == 0)
            {
                EditorGUILayout.HelpBox(EmptyMessage, MessageType.Info);
            }
        }

        public sealed override bool RequiresConstantRepaint()
        {
            return EditorApplication.isPlaying && _subscribedDebugInfos.Count == 0;
        }

        protected abstract void DrawDebugInfo(MonoBehaviour component, string fieldName, TDebugInfo debugInfo);
        protected abstract void Subscribe(TDebugInfo debugInfo, Action callback);
        protected abstract void Unsubscribe(TDebugInfo debugInfo, Action callback);

        private void RefreshTargets()
        {
            _targets.Clear();
            _foundDebugInfos.Clear();

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

                    _targets.Add(new DebugTarget(component, fields[i].Name, debugInfo));

                    if (!_foundDebugInfos.Contains(debugInfo))
                    {
                        _foundDebugInfos.Add(debugInfo);
                    }
                }

                type = type.BaseType;
            }
        }

        private void RefreshSubscriptions()
        {
            if (_targets.Count == 0)
            {
                RefreshTargets();
            }

            for (var i = _subscribedDebugInfos.Count - 1; i >= 0; i--)
            {
                var debugInfo = _subscribedDebugInfos[i];

                if (_foundDebugInfos.Contains(debugInfo))
                {
                    continue;
                }

                Unsubscribe(debugInfo, Repaint);
                _subscribedDebugInfos.RemoveAt(i);
            }

            for (var i = 0; i < _foundDebugInfos.Count; i++)
            {
                var debugInfo = _foundDebugInfos[i];

                if (_subscribedDebugInfos.Contains(debugInfo))
                {
                    continue;
                }

                Subscribe(debugInfo, Repaint);
                _subscribedDebugInfos.Add(debugInfo);
            }
        }

        private void UnsubscribeAll()
        {
            for (var i = 0; i < _subscribedDebugInfos.Count; i++)
            {
                Unsubscribe(_subscribedDebugInfos[i], Repaint);
            }

            _subscribedDebugInfos.Clear();
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
