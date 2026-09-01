using System.Collections.Generic;
using PschLib.Unity.Pooling;
using UnityEditor;
using UnityEngine;

namespace PschLib.Unity.Editor.Pooling
{
    [CustomEditor(typeof(PrefabPoolManager))]
    public sealed class PrefabPoolManagerEditor : UnityEditor.Editor
    {
        private readonly List<PrefabPoolManager.DebugEntry> _entries = new List<PrefabPoolManager.DebugEntry>();
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
        private PrefabPoolManager _manager;

        private void OnEnable()
        {
            _manager = (PrefabPoolManager)target;
            _manager.DebugStateChanged += Repaint;
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.DebugStateChanged -= Repaint;
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                return;
            }

            _manager.GetDebugEntries(_entries);
            DrawRuntimeStatus();
            DrawRegisteredPools();
        }

        private void DrawRuntimeStatus()
        {
            int inUseCount = 0;
            int inactiveCount = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                inUseCount += _entries[i].InUseCount;
                inactiveCount += _entries[i].InactiveCount;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Registered Pools", _entries.Count.ToString());
            EditorGUILayout.LabelField("Managed Instances", (inUseCount + inactiveCount).ToString());
            EditorGUILayout.LabelField("In Use Instances", inUseCount.ToString());
            EditorGUILayout.LabelField("Inactive Instances", inactiveCount.ToString());
            EditorGUILayout.EndVertical();
        }

        private void DrawRegisteredPools()
        {
            EditorGUILayout.LabelField("Registered Pools", EditorStyles.boldLabel);

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No pools are registered.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                PrefabPoolManager.DebugEntry entry = _entries[i];
                _foldouts.TryGetValue(entry.Key, out bool foldout);
                foldout = EditorGUILayout.Foldout(foldout, entry.Key, true);
                _foldouts[entry.Key] = foldout;

                if (!foldout)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Prefab", entry.PrefabName);
                EditorGUILayout.LabelField("In Use", entry.InUseCount.ToString());
                EditorGUILayout.LabelField("Inactive", entry.InactiveCount.ToString());
                EditorGUILayout.LabelField("Total", entry.TotalCount.ToString());
                EditorGUILayout.LabelField("Max Inactive", entry.MaxInactiveCount.ToString());
                EditorGUI.indentLevel--;
            }
        }
    }
}
