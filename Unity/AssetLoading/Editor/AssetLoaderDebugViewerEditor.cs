using System;
using System.Collections.Generic;
using PschLib.AssetLoading.Debugging;
using PschLib.Unity.Debugging;
using UnityEditor;
using UnityEngine;

namespace PschLib.AssetLoading.Editor
{
    [CustomEditor(typeof(AssetLoaderDebugViewer))]
    public sealed class AssetLoaderDebugViewerEditor
        : DebugViewerEditorBase<AssetLoaderDebugViewer, IAssetLoaderDebugInfo>
    {
        private readonly List<AssetLoaderDebugEntry> cachedEntries = new List<AssetLoaderDebugEntry>();
        private readonly List<AssetLoaderDebugEntry> pendingEntries = new List<AssetLoaderDebugEntry>();

        protected override string EmptyMessage => "No asset loader was found on this GameObject.";

        protected override void Subscribe(IAssetLoaderDebugInfo debugInfo, Action callback)
        {
            debugInfo.DebugStateChanged += callback;
        }

        protected override void Unsubscribe(IAssetLoaderDebugInfo debugInfo, Action callback)
        {
            debugInfo.DebugStateChanged -= callback;
        }

        protected override void DrawDebugInfo(MonoBehaviour component, string fieldName, IAssetLoaderDebugInfo debugInfo)
        {
            debugInfo.GetCachedAssetEntries(cachedEntries);
            debugInfo.GetPendingLoadEntries(pendingEntries);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{component.GetType().Name}.{fieldName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Loader", debugInfo.LoaderName);
            EditorGUILayout.LabelField("Cached Assets", debugInfo.CachedAssetCount.ToString());
            EditorGUILayout.LabelField("Active Assets", debugInfo.ActiveAssetCount.ToString());
            EditorGUILayout.LabelField("Unused Assets", (debugInfo.CachedAssetCount - debugInfo.ActiveAssetCount).ToString());
            EditorGUILayout.LabelField("Pending Loads", debugInfo.PendingLoadCount.ToString());
            DrawCachedEntries();
            DrawPendingEntries();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawCachedEntries()
        {
            EditorGUILayout.LabelField("Cached Assets", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (cachedEntries.Count == 0)
            {
                EditorGUILayout.LabelField("None");
            }
            else
            {
                for (var i = 0; i < cachedEntries.Count; i++)
                {
                    var entry = cachedEntries[i];
                    var state = entry.ReferenceCount > 0 ? "Active" : "Unused";
                    EditorGUILayout.LabelField(entry.Address, EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Type", entry.AssetTypeName);
                    EditorGUILayout.LabelField("Reference Count", entry.ReferenceCount.ToString());
                    EditorGUILayout.LabelField("State", state);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawPendingEntries()
        {
            EditorGUILayout.LabelField("Pending Loads", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (pendingEntries.Count == 0)
            {
                EditorGUILayout.LabelField("None");
            }
            else
            {
                for (var i = 0; i < pendingEntries.Count; i++)
                {
                    var entry = pendingEntries[i];
                    EditorGUILayout.LabelField(entry.Address, EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Type", entry.AssetTypeName);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }
    }
}
