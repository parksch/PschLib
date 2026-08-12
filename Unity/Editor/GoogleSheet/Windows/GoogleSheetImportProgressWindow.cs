using UnityEditor;
using UnityEngine;

namespace PschLib
{
    internal sealed class GoogleSheetImportProgressWindow : EditorWindow
    {
        public static void Open()
        {
            var window = GetWindow<GoogleSheetImportProgressWindow>(true, "Google Sheet Import", true);
            window.minSize = new Vector2(420f, 145f);
            window.maxSize = new Vector2(420f, 145f);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Google Sheet Import", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            var state = GoogleSheetPendingImportProcessor.State;
            var messageType = state == GoogleSheetImportState.Failed ? MessageType.Error : state == GoogleSheetImportState.Completed ? MessageType.Info : MessageType.None;
            EditorGUILayout.HelpBox(GoogleSheetPendingImportProcessor.StatusMessage, messageType);
            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(!GoogleSheetPendingImportProcessor.IsFinished))
            {
                if (GUILayout.Button("Close", GUILayout.Height(26f)))
                {
                    Close();
                }
            }
        }
    }
}
