using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    [CustomEditor(typeof(SheetTableBase), true)]
    public sealed class SheetTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var property = target.GetType().GetProperty("ById", BindingFlags.Instance | BindingFlags.Public);
            var entries = property?.GetValue(target) as IDictionary;

            if (entries == null)
            {
                EditorGUILayout.HelpBox("Dictionary data is unavailable.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Entries ({entries.Count})", EditorStyles.boldLabel);

            foreach (DictionaryEntry entry in entries)
            {
                DrawEntry(entry.Key?.ToString() ?? string.Empty, entry.Value);
            }
        }

        private static void DrawEntry(string id, object value)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(id, EditorStyles.boldLabel);

            if (value != null)
            {
                foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    EditorGUILayout.LabelField(field.Name, FormatValue(field.GetValue(value)));
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string text)
            {
                return text;
            }

            if (value is IEnumerable values)
            {
                var result = string.Empty;

                foreach (var element in values)
                {
                    result += string.IsNullOrEmpty(result) ? element?.ToString() : $", {element}";
                }

                return result;
            }

            return Convert.ToString(value);
        }
    }
}
