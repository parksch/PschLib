using System;
using System.Collections.Generic;
using PschLib.Scheduling;
using PschLib.Unity.Debugging;
using PschLib.Unity.Scheduling;
using UnityEditor;
using UnityEngine;

namespace PschLib.Unity.Editor.Scheduling
{
    [CustomEditor(typeof(TimerSchedulerDebugViewer))]
    public sealed class TimerSchedulerDebugViewerEditor
        : DebugViewerEditorBase<TimerSchedulerDebugViewer, ITimerSchedulerDebugInfo>
    {
        private readonly List<TimerDebugEntry> entries = new List<TimerDebugEntry>();

        protected override string EmptyMessage => "No TimerScheduler was found on this GameObject.";

        protected override void Subscribe(ITimerSchedulerDebugInfo debugInfo, Action callback)
        {
            debugInfo.DebugStateChanged += callback;
        }

        protected override void Unsubscribe(ITimerSchedulerDebugInfo debugInfo, Action callback)
        {
            debugInfo.DebugStateChanged -= callback;
        }

        protected override void DrawDebugInfo(
            MonoBehaviour component,
            string fieldName,
            ITimerSchedulerDebugInfo debugInfo)
        {
            debugInfo.GetTimerEntries(entries);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"{component.GetType().Name}.{fieldName}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Timers", debugInfo.TimerCount.ToString());
            EditorGUILayout.LabelField("Pending Timers", debugInfo.PendingTimerCount.ToString());
            EditorGUILayout.LabelField("Ticking", debugInfo.IsTicking.ToString());
            DrawEntries();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawEntries()
        {
            EditorGUILayout.LabelField("Scheduled Timers", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("None");
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var label = entry.IsPending ? $"Timer {i + 1} (Pending)" : $"Timer {i + 1}";

                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("State", entry.State.ToString());
                    EditorGUILayout.LabelField("Time Mode", entry.TimeMode.ToString());
                    EditorGUILayout.LabelField("Duration", FormatSeconds(entry.Duration));
                    EditorGUILayout.LabelField("Elapsed", FormatSeconds(entry.ElapsedTime));
                    EditorGUILayout.LabelField("Remaining", FormatSeconds(entry.RemainingTime));
                    EditorGUILayout.LabelField("Progress", $"{entry.Progress:P1}");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }

        private static string FormatSeconds(float value)
        {
            return $"{value:0.###} s";
        }
    }
}
